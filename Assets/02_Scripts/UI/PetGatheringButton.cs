using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.AI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

public class PetGatheringButton : MonoBehaviour
{
    [Header("Gathering Settings")]
    public Button gatherButton;    // 인스펙터에서 할당
    public float gatherRadius = 3f;  // 터치한 지점을 중심으로 펫들을 모을 영역 반경
    public TMP_Text feedbackText;    // UI 피드백용 텍스트 (인스펙터에서 할당)

    // 펫 이동 속도 변경용 변수들
    [Header("Agent Parameter Multipliers")]
    [SerializeField] private float speedMultiplier = 4f;
    [SerializeField] private float angularSpeedMultiplier = 4f;
    [SerializeField] private float accelerationMultiplier = 4f;
    [SerializeField] private float stoppingDistanceMultiplier = 3f;

    [Header("Arrival Settings")]
    [SerializeField] private int maxReassignAttempts = 1;
    [SerializeField] private float stuckTimeout = 5f;
    [SerializeField] private float gracePeriod = 7f;
    [SerializeField] private float velocityThreshold = 0.2f;
    [SerializeField] private float arrivalDelay = 0.5f;
    [SerializeField] private float cameraRotationSpeed = 180f;
    [SerializeField] private float angleThreshold = 0.1f;

    // 두 가지 상태 플래그:
    // waitingForTerrainInput: 버튼을 눌러 모으기 모드가 활성화되어 지형 터치를 기다리는 상태
    // isGatheringActive: 펫들이 모여있는 상태 (즉, 모으기가 완료된 상태)
    private bool waitingForTerrainInput = false;
    private bool isGatheringActive = false;

    void Start()
    {
        if (gatherButton != null)
        {
            // 버튼 클릭 시 모드 토글 (활성화 또는 해제)
            gatherButton.onClick.AddListener(ToggleGatheringMode);
        }
        else
        {
            Debug.LogWarning("Gather Button이 할당되지 않았습니다.");
        }
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
        // 초기 버튼 텍스트 설정
        UpdateButtonText("펫 모이기");
    }

    // 버튼 클릭 시 현재 상태에 따라 모으기 모드를 활성화하거나 해제합니다.
    public void ToggleGatheringMode()
    {
        if (waitingForTerrainInput || isGatheringActive)
        {
            // 이미 모으기 모드(대기중 또는 활성 상태)라면 취소합니다.
            CancelGathering();
        }
        else
        {
            // 모으기 모드 활성화: 지형 터치를 기다립니다.
            waitingForTerrainInput = true;
            if (feedbackText != null)
            {
                feedbackText.text = "지형을 터치하여 펫들을 모으세요.";
                feedbackText.gameObject.SetActive(true);
            }
            UpdateButtonText("모이기 해제");
            Debug.Log("펫 모으기 모드 활성화됨.");
        }
    }

    // PetGatheringButton.cs의 Update 메서드 내부 수정 부분

    void Update()
    {
        // UI 위에서의 터치를 무시하기 위한 처리
        bool isOverUI = false;
        if (Input.touchCount > 0)
        {
            isOverUI = EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }
        else
        {
            isOverUI = EventSystem.current.IsPointerOverGameObject();
        }
        if (isOverUI)
            return;

        // 지형 터치 입력 처리 (모으기 대기 상태일 때)
        if (waitingForTerrainInput && Input.GetMouseButtonDown(0))
        {
            LayerMask terrainMask = LayerMask.GetMask("Terrain");
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainMask))
            {
                PetController[] pets = Object.FindObjectsByType<PetController>(FindObjectsSortMode.None);
                int petCount = pets.Length;
                Vector3 centerPoint = hit.point;
                List<Vector3> targetPositions = GenerateGridPositions(centerPoint, gatherRadius, petCount);

                // ★ 새로운 스마트 위치 배정 시스템 적용
                Dictionary<PetController, Vector3> smartAssignments = OptimizePositionAssignment(pets, targetPositions);

                // ★ 1단계: 모든 펫을 우선 모이기 상태로 설정
                foreach (PetController pet in pets)
                {
                    if (pet != null)
                    {
                        pet.gatherCommandVersion++;
                        pet.isGathered = false;
                        pet.isGathering = true;

                        // ▼▼▼▼▼ [이 부분을 수정합니다] ▼▼▼▼▼
                        // ★ 나무에 올라가 있는 펫 강제로 내려오게 하기
                        if (pet.isClimbingTree)
                        {
                            // 기존의 StartCoroutine(ForceClimbDownForGathering(pet)) 대신
                            // PetMovementController의 ForceCancelClimbing()을 직접 호출합니다.
                            var movementController = pet.GetComponent<PetMovementController>();
                            if (movementController != null)
                            {
                                movementController.ForceCancelClimbing();
                            }
                            // 'continue'를 제거하여, 나무에서 내려온 펫이 바로 다음 모이기 로직을 타도록 합니다.
                        }
                        // ▲▲▲▲▲ [여기까지 수정] ▲▲▲▲▲




                        // 현재 움직임 중단
                        if (pet.agent != null && pet.agent.enabled)
                        {
                            try
                            {
                                pet.agent.isStopped = true;
                            }
                            catch { }
                        }
                    }
                }

                // ★ 2단계: 최적화된 배정에 따라 개별 펫 처리
                int successfulAssignments = 0;
                foreach (var assignment in smartAssignments)
                {
                    PetController pet = assignment.Key;
                    Vector3 targetPoint = assignment.Value;

                    // Agent 상태 체크
                    if (pet == null || pet.agent == null || !pet.agent.enabled || !pet.agent.isOnNavMesh)
                    {
                        Debug.LogWarning($"[Gathering] {pet?.petName}: Agent 상태 불량 - 현재 위치에서 대기");
                        continue;
                    }

                    // NavMesh 위치 검증
                    NavMeshHit navHit;
                    bool foundValidPosition = false;
                    float searchRadius = gatherRadius;

                    for (int attempt = 0; attempt < 3 && !foundValidPosition; attempt++)
                    {
                        if (NavMesh.SamplePosition(targetPoint, out navHit, searchRadius, NavMesh.AllAreas))
                        {
                            NavMeshPath testPath = new NavMeshPath();
                            if (pet.agent.CalculatePath(navHit.position, testPath) && testPath.status == NavMeshPathStatus.PathComplete)
                            {
                                targetPoint = navHit.position;
                                foundValidPosition = true;
                            }
                        }
                        searchRadius *= 1.5f;
                    }

                    if (!foundValidPosition)
                    {
                        Debug.LogWarning($"[Gathering] {pet.petName}: 유효한 경로 없음 - 현재 위치에서 대기");
                        StartCoroutine(StayAndLookAtCamera(pet));
                        continue;
                    }

                    // 속도 설정 및 이동 시작
                    try
                    {
                        float originalSpeed = pet.baseSpeed;
                        float originalAngularSpeed = pet.baseAngularSpeed;
                        float originalAcceleration = pet.baseAcceleration;
                        float originalStoppingDistance = pet.baseStoppingDistance;

                        pet.agent.speed = originalSpeed * speedMultiplier;
                        pet.agent.angularSpeed = originalAngularSpeed * angularSpeedMultiplier;
                        pet.agent.acceleration = originalAcceleration * accelerationMultiplier;
                        pet.agent.stoppingDistance = originalStoppingDistance * stoppingDistanceMultiplier;
                        pet.agent.isStopped = false;

                        pet.isGatheringAnimationOverride = true;
                        // pet.isGatheringRotationOverride = true;

                        if (pet.animator != null)
                        {
                            pet.animator.SetInteger("animation", 2);
                        }

                        Debug.Log($"[Smart Gathering] {pet.petName}: 최적 거리 {Vector3.Distance(pet.transform.position, targetPoint):F1}m");

                        if (pet.agent.SetDestination(targetPoint))
                        {
                            StartCoroutine(StopAndLookAtCameraWhenArrived(pet, originalSpeed, originalAngularSpeed, originalAcceleration, originalStoppingDistance));
                            successfulAssignments++;
                        }
                        else
                        {
                            Debug.LogWarning($"[Gathering] {pet.petName}: SetDestination 실패 - 현재 위치에서 대기");
                            StartCoroutine(StayAndLookAtCamera(pet));
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[Gathering] {pet.petName}: 오류 발생 - {e.Message}");
                        StartCoroutine(StayAndLookAtCamera(pet));
                    }
                }

                Debug.Log($"스마트 모이기 명령 완료: 총 {petCount}마리 중 {successfulAssignments}마리 이동");

                if (feedbackText != null)
                    feedbackText.gameObject.SetActive(false);

                waitingForTerrainInput = false;
                isGatheringActive = true;
            }
        }
    }
    // 나무에 올라가 있는 펫을 강제로 내려오게 하는 코루틴
    private IEnumerator ForceClimbDownForGathering(PetController pet)
    {
        Debug.Log($"{pet.petName}: 모이기 명령으로 나무에서 내려옵니다.");

        // 나무 올라가기 상태 해제
        pet.isClimbingTree = false;

        // 현재 나무 올라가기 코루틴들 중단
        pet.StopAllCoroutines();

        // NavMeshAgent 재활성화 및 지면으로 이동
        if (pet.agent != null)
        {
            pet.agent.enabled = true;

            // 현재 위치에서 지면으로 NavMesh 위치 찾기
            NavMeshHit navHit;
            Vector3 groundPosition = pet.transform.position;
            groundPosition.y = 0; // 지면 높이로 설정

            if (NavMesh.SamplePosition(groundPosition, out navHit, 10f, NavMesh.AllAreas))
            {
                pet.transform.position = navHit.position;
                pet.agent.Warp(navHit.position);
            }

            // 모이기 설정 적용
            float originalSpeed = pet.baseSpeed;
            float originalAngularSpeed = pet.baseAngularSpeed;
            float originalAcceleration = pet.baseAcceleration;
            float originalStoppingDistance = pet.baseStoppingDistance;

            pet.agent.speed = originalSpeed * speedMultiplier;
            pet.agent.angularSpeed = originalAngularSpeed * angularSpeedMultiplier;
            pet.agent.acceleration = originalAcceleration * accelerationMultiplier;
            pet.agent.stoppingDistance = originalStoppingDistance * stoppingDistanceMultiplier;

            pet.isGatheringAnimationOverride = true;

            if (pet.animator != null)
            {
                pet.animator.SetInteger("animation", 2); // 뛰기 애니메이션
            }

            // 모이기 목표 위치로 이동 (기존 로직과 연결)
            yield return new WaitForSeconds(0.5f); // 잠시 대기 후 모이기 진행
        }

        pet.currentTree = null;
    }
    // ★ 새로 추가되는 스마트 위치 배정 메서드들

    /// <summary>
    /// 거리 기반 최적 위치 배정 시스템
    /// 각 펫을 가장 가까운 목표 위치에 배정하여 전체 이동 거리를 최소화
    /// </summary>
    private Dictionary<PetController, Vector3> OptimizePositionAssignment(PetController[] pets, List<Vector3> positions)
    {
        Dictionary<PetController, Vector3> assignments = new Dictionary<PetController, Vector3>();
        List<PetController> remainingPets = new List<PetController>(pets);
        List<Vector3> remainingPositions = new List<Vector3>(positions);

        Debug.Log($"[Smart Assignment] 시작: {pets.Length}마리 펫, {positions.Count}개 위치");

        // 그리디 방식으로 가장 가까운 펫-위치 쌍을 순차적으로 매칭
        while (remainingPets.Count > 0 && remainingPositions.Count > 0)
        {
            float minDistance = float.MaxValue;
            PetController closestPet = null;
            Vector3 closestPosition = Vector3.zero;
            int closestPetIndex = -1;
            int closestPositionIndex = -1;

            // 모든 펫-위치 조합에서 최단 거리 찾기
            for (int petIndex = 0; petIndex < remainingPets.Count; petIndex++)
            {
                var pet = remainingPets[petIndex];
                if (pet == null) continue;

                for (int posIndex = 0; posIndex < remainingPositions.Count; posIndex++)
                {
                    var position = remainingPositions[posIndex];
                    float distance = Vector3.Distance(pet.transform.position, position);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPet = pet;
                        closestPosition = position;
                        closestPetIndex = petIndex;
                        closestPositionIndex = posIndex;
                    }
                }
            }

            // 최적 쌍을 찾았다면 배정하고 리스트에서 제거
            if (closestPet != null)
            {
                assignments[closestPet] = closestPosition;
                remainingPets.RemoveAt(closestPetIndex);
                remainingPositions.RemoveAt(closestPositionIndex);

                Debug.Log($"[Smart Assignment] {closestPet.petName} → 거리 {minDistance:F1}m");
            }
            else
            {
                Debug.LogWarning("[Smart Assignment] 유효한 펫-위치 쌍을 찾을 수 없음");
                break;
            }
        }

        // 배정되지 않은 펫들은 가장 가까운 할당된 위치 근처에 배치
        foreach (var unassignedPet in remainingPets)
        {
            if (unassignedPet != null && assignments.Count > 0)
            {
                Vector3 nearestAssignedPosition = FindNearestAssignedPosition(unassignedPet.transform.position, assignments.Values);
                Vector3 fallbackPosition = GetFallbackPosition(nearestAssignedPosition, assignments.Values);
                assignments[unassignedPet] = fallbackPosition;

                Debug.Log($"[Smart Assignment] {unassignedPet.petName} → 대체 위치 배정");
            }
        }

        // 배정 결과 통계 출력
        float totalDistance = 0f;
        foreach (var assignment in assignments)
        {
            totalDistance += Vector3.Distance(assignment.Key.transform.position, assignment.Value);
        }
        Debug.Log($"[Smart Assignment] 완료: 평균 이동 거리 {totalDistance / assignments.Count:F1}m");

        return assignments;
    }

    /// <summary>
    /// 배정된 위치들 중에서 가장 가까운 위치 찾기
    /// </summary>
    private Vector3 FindNearestAssignedPosition(Vector3 petPosition, IEnumerable<Vector3> assignedPositions)
    {
        Vector3 nearest = Vector3.zero;
        float minDistance = float.MaxValue;

        foreach (var pos in assignedPositions)
        {
            float distance = Vector3.Distance(petPosition, pos);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = pos;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 이미 배정된 위치들과 겹치지 않는 대체 위치 생성
    /// </summary>
    private Vector3 GetFallbackPosition(Vector3 basePosition, IEnumerable<Vector3> occupiedPositions)
    {
        float searchRadius = 2f;
        int maxAttempts = 8;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 기준 위치 주변에 원형으로 대체 위치 생성
            float angle = (360f / maxAttempts) * attempt;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * searchRadius,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * searchRadius
            );

            Vector3 candidatePosition = basePosition + offset;

            // 다른 배정된 위치들과 최소 거리 유지 확인
            bool tooClose = false;
            foreach (var occupiedPos in occupiedPositions)
            {
                if (Vector3.Distance(candidatePosition, occupiedPos) < 1.5f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                return candidatePosition;
            }
        }

        // 모든 시도가 실패하면 기준 위치에서 약간 떨어진 곳 반환
        return basePosition + Vector3.right * searchRadius;
    }



    // 버튼의 텍스트를 변경하는 헬퍼 함수
    private void UpdateButtonText(string newText)
    {
        TMP_Text btnText = gatherButton.GetComponentInChildren<TMP_Text>();
        if (btnText != null)
        {
            btnText.text = newText;
        }
    }

    // 피드백 메시지를 일정 시간 후 숨김
    private IEnumerator HideFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }

    // 터치한 지점을 중심으로 격자 형태의 위치 리스트 생성 (모으기 대상 펫 수에 따라)
    private List<Vector3> GenerateGridPositions(Vector3 center, float halfWidth, int count)
    {
        List<Vector3> positions = new List<Vector3>();
        int gridCount = Mathf.CeilToInt(Mathf.Sqrt(count));
        float spacing = (gridCount > 1) ? (halfWidth * 2f) / (gridCount - 1) : 0f;
        float offset = (gridCount - 1) / 2f;
        float randomness = spacing * 0.3f;
        for (int i = 0; i < gridCount; i++)
        {
            for (int j = 0; j < gridCount; j++)
            {
                Vector3 pos = center + new Vector3((i - offset) * spacing, 0, (j - offset) * spacing);
                pos.x += Random.Range(-randomness, randomness);
                pos.z += Random.Range(-randomness, randomness);
                positions.Add(pos);
                if (positions.Count >= count)
                    break;
            }
            if (positions.Count >= count)
                break;
        }
        return positions;
    }

    // 펫이 지정된 위치에 도착한 후 카메라를 바라보며 멈추도록 하는 코루틴
    // StopAndLookAtCameraWhenArrived 코루틴 수정
    private IEnumerator StopAndLookAtCameraWhenArrived(PetController pet, float originalSpeed, float originalAngularSpeed, float originalAcceleration, float originalStoppingDistance)
    {
        int currentGatherVersion = pet.gatherCommandVersion;
        NavMeshAgent agent = pet.agent;
        int reassignAttempts = 0;
        float stuckTimer = 0f;
        float startTime = Time.time;
        float lastSpeedCheck = Time.time;
        float speedCheckInterval = 0.5f;

        // ★ 이동 중에는 NavMeshAgent의 자동 회전 허용 (목적지 방향으로)
        // petModelTransform은 계속 동기화
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f)
        {
            // 모으기 명령이 취소되었는지 체크
            if (pet.gatherCommandVersion != currentGatherVersion)
            {
                Debug.Log($"{pet.petName}의 모이기 명령이 취소되었습니다.");
                pet.isGatheringAnimationOverride = false;
                // pet.isGatheringRotationOverride = false; // ★ 방향 오버라이드 해제
                yield break;
            }

            // Agent가 비활성화되었는지 체크
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                Debug.LogWarning($"{pet.petName}의 NavMeshAgent가 비활성화되어 모이기를 중단합니다.");
                pet.isGatheringAnimationOverride = false;
                // pet.isGatheringRotationOverride = false;
                yield break;
            }

            // ★ 이동 중 모델 위치/회전 동기화 (NavMeshAgent 따라가기)
            if (pet.petModelTransform != null)
            {
                pet.petModelTransform.position = pet.transform.position;
                pet.petModelTransform.rotation = pet.transform.rotation; // Agent 회전 따라가기
            }

            // 뛰기 애니메이션 유지
            if (pet.animator != null && pet.animator.GetInteger("animation") != 2)
            {
                pet.animator.SetInteger("animation", 2);
            }

            // 속도 체크 및 복원 로직...
            if (Time.time - lastSpeedCheck > speedCheckInterval)
            {
                float expectedSpeed = originalSpeed * speedMultiplier;
                if (Mathf.Abs(agent.speed - expectedSpeed) > 0.1f)
                {
                    Debug.Log($"{pet.petName}의 속도가 변경되어 복원합니다.");
                    try
                    {
                        agent.speed = expectedSpeed;
                        agent.angularSpeed = originalAngularSpeed * angularSpeedMultiplier;
                        agent.acceleration = originalAcceleration * accelerationMultiplier;
                        agent.stoppingDistance = originalStoppingDistance * stoppingDistanceMultiplier;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"{pet.petName}: 속도 복원 실패 - {e.Message}");
                    }
                }
                lastSpeedCheck = Time.time;
            }

            // 막힘 처리 로직 (기존과 동일)...
            if (Time.time - startTime > gracePeriod)
            {
                if (agent.velocity.magnitude < velocityThreshold)
                {
                    stuckTimer += Time.deltaTime;
                }
                else
                {
                    stuckTimer = 0f;
                }
            }

            if (stuckTimer > stuckTimeout)
            {
                if (reassignAttempts < maxReassignAttempts)
                {
                    reassignAttempts++;
                    Debug.Log($"{pet.petName}이(가) 막힌 상태입니다. 재할당합니다.");

                    Vector3 currentPos = pet.transform.position;
                    Vector3 randomOffset = Random.insideUnitSphere * 3f;
                    randomOffset.y = 0;
                    Vector3 newDestination = currentPos + randomOffset;

                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(newDestination, out hit, 5f, NavMesh.AllAreas))
                    {
                        NavMeshPath testPath = new NavMeshPath();
                        if (agent.CalculatePath(hit.position, testPath) && testPath.status == NavMeshPathStatus.PathComplete)
                        {
                            agent.SetDestination(hit.position);
                            startTime = Time.time;
                            stuckTimer = 0f;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    Debug.Log($"{pet.petName}: 재할당 후에도 도착하지 못해 현재 위치에서 멈춥니다.");
                    break;
                }
            }
            yield return null;
        }

        yield return new WaitForSeconds(arrivalDelay);

        // 모으기 명령이 취소되었는지 다시 체크
        if (pet.gatherCommandVersion != currentGatherVersion)
        {
            Debug.Log($"{pet.petName}의 모이기 명령이 취소되었습니다.");
            pet.isGatheringAnimationOverride = false;
            // pet.isGatheringRotationOverride = false;
            yield break;
        }

        // ★ 도착 후 처리 - Agent 멈추고 애니메이션 변경
        try
        {
            agent.speed = originalSpeed;
            agent.angularSpeed = originalAngularSpeed;
            agent.acceleration = originalAcceleration;
            agent.stoppingDistance = originalStoppingDistance;
            agent.SetDestination(pet.transform.position); // 현재 위치로 설정해서 멈춤

            if (pet.animator != null)
            {
                pet.animator.SetInteger("animation", 0); // Idle 애니메이션
            }

            Debug.Log($"[Gathering] {pet.petName}: 도착 - Idle 애니메이션 설정");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"{pet.petName}: 도착 후 설정 실패 - {e.Message}");
        }

        pet.isGathered = true;

        // ★ 카메라 방향으로 회전 - petModelTransform 직접 제어
        if (Camera.main != null)
        {
            Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
            directionToCamera.y = 0;

            if (directionToCamera.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);

                // ★ petModelTransform을 직접 회전 (Agent 회전 무시)
                if (pet.petModelTransform != null)
                {
                    while (Quaternion.Angle(pet.petModelTransform.rotation, targetRotation) > angleThreshold)
                    {
                        if (pet.gatherCommandVersion != currentGatherVersion)
                        {
                            Debug.Log($"{pet.petName}의 모이기 명령이 취소되었습니다.");
                            pet.isGatheringAnimationOverride = false;
                            // pet.isGatheringRotationOverride = false;
                            yield break;
                        }

                        // ★ 모델 위치 동기화하면서 회전만 독립적으로 제어
                        pet.petModelTransform.position = pet.transform.position;
                        pet.petModelTransform.rotation = Quaternion.RotateTowards(pet.petModelTransform.rotation, targetRotation, cameraRotationSpeed * Time.deltaTime);
                        yield return null;
                    }
                }
                else
                {
                    // petModelTransform이 없다면 부모 오브젝트 회전
                    while (Quaternion.Angle(pet.transform.rotation, targetRotation) > angleThreshold)
                    {
                        if (pet.gatherCommandVersion != currentGatherVersion)
                        {
                            pet.isGatheringAnimationOverride = false;
                            // pet.isGatheringRotationOverride = false;
                            yield break;
                        }

                        pet.transform.rotation = Quaternion.RotateTowards(pet.transform.rotation, targetRotation, cameraRotationSpeed * Time.deltaTime);
                        yield return null;
                    }
                }
            }
        }

        // ★ 모든 처리 완료 후 오버라이드 해제
        pet.isGatheringAnimationOverride = false;
        // pet.isGatheringRotationOverride = false;

        Debug.Log($"{pet.petName}: 카메라 바라보기 완료. 애니메이션/방향 제어 해제.");
    }

    // 5. StayAndLookAtCamera 코루틴도 방향 제어 추가
    private IEnumerator StayAndLookAtCamera(PetController pet)
    {
        int currentGatherVersion = pet.gatherCommandVersion;

        // 제자리 대기 설정
        pet.isGatheringAnimationOverride = true;
        // pet.isGatheringRotationOverride = true; // ★ 방향 오버라이드 활성화

        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", 0); // Idle 애니메이션
        }

        // 제자리에서 멈추기
        if (pet.agent != null && pet.agent.enabled)
        {
            try
            {
                pet.agent.isStopped = true;
                pet.agent.SetDestination(pet.transform.position);
            }
            catch { }
        }

        yield return new WaitForSeconds(arrivalDelay);

        if (pet.gatherCommandVersion != currentGatherVersion)
        {
            pet.isGatheringAnimationOverride = false;
            // pet.isGatheringRotationOverride = false;
            yield break;
        }

        pet.isGathered = true;

        // ★ 카메라 바라보기 - petModelTransform 직접 제어
        if (Camera.main != null)
        {
            Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
            directionToCamera.y = 0;

            if (directionToCamera.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);

                if (pet.petModelTransform != null)
                {
                    while (Quaternion.Angle(pet.petModelTransform.rotation, targetRotation) > angleThreshold)
                    {
                        if (pet.gatherCommandVersion != currentGatherVersion)
                        {
                            pet.isGatheringAnimationOverride = false;
                            // pet.isGatheringRotationOverride = false;
                            yield break;
                        }

                        pet.petModelTransform.position = pet.transform.position;
                        pet.petModelTransform.rotation = Quaternion.RotateTowards(pet.petModelTransform.rotation, targetRotation, cameraRotationSpeed * Time.deltaTime);
                        yield return null;
                    }
                }
                else
                {
                    while (Quaternion.Angle(pet.transform.rotation, targetRotation) > angleThreshold)
                    {
                        if (pet.gatherCommandVersion != currentGatherVersion)
                        {
                            pet.isGatheringAnimationOverride = false;
                            // pet.isGatheringRotationOverride = false;
                            yield break;
                        }

                        pet.transform.rotation = Quaternion.RotateTowards(pet.transform.rotation, targetRotation, cameraRotationSpeed * Time.deltaTime);
                        yield return null;
                    }
                }
            }
        }

        // ★ 오버라이드 해제
        pet.isGatheringAnimationOverride = false;
        // pet.isGatheringRotationOverride = false;

        Debug.Log($"{pet.petName}: 제자리 모이기 완료. 애니메이션/방향 제어 해제.");
    }

    // 6. CancelGathering에도 방향 오버라이드 해제 추가
    private void CancelGathering()
    {
        if (waitingForTerrainInput)
        {
            waitingForTerrainInput = false;
            if (feedbackText != null)
                feedbackText.gameObject.SetActive(false);
            UpdateButtonText("펫 모이기");
            Debug.Log("펫 모이기 모드 취소됨.");
        }
        if (isGatheringActive)
        {
            PetController[] pets = Object.FindObjectsByType<PetController>(FindObjectsSortMode.None);
            foreach (PetController pet in pets)
            {
                pet.gatherCommandVersion++;
                pet.isGathered = false;
                pet.isGathering = false;
                pet.isGatheringAnimationOverride = false;
                // pet.isGatheringRotationOverride = false; // ★ 방향 오버라이드 해제

                if (pet.agent != null)
                {
                    pet.agent.speed = pet.baseSpeed;
                    pet.agent.angularSpeed = pet.baseAngularSpeed;
                    pet.agent.acceleration = pet.baseAcceleration;
                    pet.agent.stoppingDistance = pet.baseStoppingDistance;
                    pet.agent.isStopped = false;

                    if (pet.animator != null)
                    {
                        pet.animator.SetInteger("animation", 0);
                    }
                    pet.GetComponent<PetMovementController>().SetRandomDestination();
                }
            }
            isGatheringActive = false;
            if (feedbackText != null)
                feedbackText.gameObject.SetActive(false);
            UpdateButtonText("펫 모이기");
            Debug.Log("펫 모이기 해제됨. 펫들이 정상적으로 움직입니다.");
        }
    }
}