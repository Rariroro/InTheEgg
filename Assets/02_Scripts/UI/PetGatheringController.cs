using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.AI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;

public class PetGatheringController : MonoBehaviour
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

                // ★ 1단계: 모든 펫을 우선 모이기 상태로 설정 (실패하더라도 일반 움직임 차단)
                foreach (PetController pet in pets)
                {
                    if (pet != null)
                    {
                        pet.gatherCommandVersion++;
                        pet.isGathered = false;
                        pet.isGathering = true; // ← 우선 모든 펫을 모이기 상태로 설정

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

                // ★ 2단계: 개별 펫에 대해 모이기 명령 처리
                int successfulAssignments = 0;
                for (int i = 0; i < petCount; i++)
                {
                    PetController pet = pets[i];

                    // Agent 상태 체크
                    if (pet == null || pet.agent == null || !pet.agent.enabled || !pet.agent.isOnNavMesh)
                    {
                        Debug.LogWarning($"[Gathering] {pet?.petName}: Agent 상태 불량 - 현재 위치에서 대기");
                        // ★ isGathering = true 유지하여 일반 움직임 차단
                        continue;
                    }

                    Vector3 targetPoint = targetPositions[i];

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
                        // ★ isGathering = true 유지하고 현재 위치에서 카메라 바라보기
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

                        // ★ 모이기 중 방향 제어 활성화
                        pet.isGatheringAnimationOverride = true;
                        pet.isGatheringRotationOverride = true; // 방향 오버라이드 활성화

                        if (pet.animator != null)
                        {
                            pet.animator.SetInteger("animation", 2); // 뛰기 애니메이션
                        }

                        Debug.Log($"[Gathering] {pet.petName}: 애니메이션/방향 오버라이드 활성화");

                        if (pet.agent.SetDestination(targetPoint))
                        {
                            StartCoroutine(StopAndLookAtCameraWhenArrived(pet, originalSpeed, originalAngularSpeed, originalAcceleration, originalStoppingDistance));
                            successfulAssignments++;
                            Debug.Log($"[Gathering] {pet.petName}: 모이기 명령 성공");
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

                Debug.Log($"모이기 명령 완료: 총 {petCount}마리 중 {successfulAssignments}마리 이동, 나머지는 제자리 대기");

                if (feedbackText != null)
                    feedbackText.gameObject.SetActive(false);

                waitingForTerrainInput = false;
                isGatheringActive = true;
            }
        }

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
                pet.isGatheringRotationOverride = false; // ★ 방향 오버라이드 해제
                yield break;
            }

            // Agent가 비활성화되었는지 체크
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                Debug.LogWarning($"{pet.petName}의 NavMeshAgent가 비활성화되어 모이기를 중단합니다.");
                pet.isGatheringAnimationOverride = false;
                pet.isGatheringRotationOverride = false;
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
            pet.isGatheringRotationOverride = false;
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
                            pet.isGatheringRotationOverride = false;
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
                            pet.isGatheringRotationOverride = false;
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
        pet.isGatheringRotationOverride = false;

        Debug.Log($"{pet.petName}: 카메라 바라보기 완료. 애니메이션/방향 제어 해제.");
    }

    // 5. StayAndLookAtCamera 코루틴도 방향 제어 추가
    private IEnumerator StayAndLookAtCamera(PetController pet)
    {
        int currentGatherVersion = pet.gatherCommandVersion;

        // 제자리 대기 설정
        pet.isGatheringAnimationOverride = true;
        pet.isGatheringRotationOverride = true; // ★ 방향 오버라이드 활성화

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
            pet.isGatheringRotationOverride = false;
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
                            pet.isGatheringRotationOverride = false;
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
                            pet.isGatheringRotationOverride = false;
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
        pet.isGatheringRotationOverride = false;

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
                pet.isGatheringRotationOverride = false; // ★ 방향 오버라이드 해제

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