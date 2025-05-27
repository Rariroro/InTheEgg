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
// ★ 이동하지 못하는 펫들을 위한 제자리 대기 코루틴
private IEnumerator StayAndLookAtCamera(PetController pet)
{
    int currentGatherVersion = pet.gatherCommandVersion;
    
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
    
    // 명령이 취소되었는지 확인
    if (pet.gatherCommandVersion != currentGatherVersion)
        yield break;
    
    // 모이기 완료 상태로 설정
    pet.isGathered = true;
    
    // 카메라 바라보기
    if (Camera.main != null)
    {
        Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
        directionToCamera.y = 0;
        if (directionToCamera.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
            while (Quaternion.Angle(pet.transform.rotation, targetRotation) > angleThreshold)
            {
                if (pet.gatherCommandVersion != currentGatherVersion)
                    yield break;
                    
                pet.transform.rotation = Quaternion.RotateTowards(pet.transform.rotation, targetRotation, cameraRotationSpeed * Time.deltaTime);
                yield return null;
            }
        }
    }
    
    Debug.Log($"{pet.petName}: 제자리에서 모이기 완료");
}
    // 모으기 모드가 활성 또는 대기 상태인 경우 취소하여 펫들을 전 정상 이동 상태로 복귀시킵니다.
    private void CancelGathering()
    {
        if (waitingForTerrainInput)
        {
            waitingForTerrainInput = false;
            if (feedbackText != null)
                feedbackText.gameObject.SetActive(false);
            UpdateButtonText("펫 모이기");
            Debug.Log("펫 모우기 모드 취소됨.");
        }
        if (isGatheringActive)
        {
            // 현재 모으기 중인 모든 펫에 대해 모으기 해제 처리
            PetController[] pets = Object.FindObjectsByType<PetController>(FindObjectsSortMode.None);
            foreach (PetController pet in pets)
            {
                // 진행 중인 모으기 코루틴이 있다면 종료시키기 위해 버전을 증가시킵니다.
                pet.gatherCommandVersion++;

                pet.isGathered = false;
                pet.isGathering = false;  // 모이기 중 상태 해제 (중요!)

                if (pet.agent != null)
                {
                    // 기본 속성으로 복원
                    pet.agent.speed = pet.baseSpeed;
                    pet.agent.angularSpeed = pet.baseAngularSpeed;
                    pet.agent.acceleration = pet.baseAcceleration;
                    pet.agent.stoppingDistance = pet.baseStoppingDistance;

                    pet.agent.isStopped = false;
                    // 애니메이션 상태도 idle(0)로 복원
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
            Debug.Log("펫 모으기 해제됨. 펫들이 정상적으로 움직입니다.");
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

        // ★ 모이기 중 속도 유지를 위한 주기적 체크
        float lastSpeedCheck = Time.time;
        float speedCheckInterval = 0.5f;

        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f)
        {
            // 모으기 명령이 취소되었는지 체크
            if (pet.gatherCommandVersion != currentGatherVersion)
            {
                Debug.Log($"{pet.petName}의 모이기 명령이 취소되었습니다.");
                yield break;
            }

            // ★ Agent가 비활성화되었는지 체크
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                Debug.LogWarning($"{pet.petName}의 NavMeshAgent가 비활성화되어 모이기를 중단합니다.");
                yield break;
            }

            // ★ 주기적으로 속도가 변경되었는지 체크하고 복원
            if (Time.time - lastSpeedCheck > speedCheckInterval)
            {
                float expectedSpeed = originalSpeed * speedMultiplier;
                if (Mathf.Abs(agent.speed - expectedSpeed) > 0.1f)
                {
                    Debug.Log($"{pet.petName}의 속도가 변경되어 복원합니다. 현재: {agent.speed}, 예상: {expectedSpeed}");
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
                    Debug.Log($"{pet.petName}이(가) 막힌 상태입니다. 새로운 목적지를 재할당합니다.");

                    // ★ 더 안전한 재할당 로직
                    Vector3 currentPos = pet.transform.position;
                    Vector3 randomOffset = Random.insideUnitSphere * 3f;
                    randomOffset.y = 0;
                    Vector3 newDestination = currentPos + randomOffset;

                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(newDestination, out hit, 5f, NavMesh.AllAreas))
                    {
                        // 경로 계산 가능한지 확인
                        NavMeshPath testPath = new NavMeshPath();
                        if (agent.CalculatePath(hit.position, testPath) && testPath.status == NavMeshPathStatus.PathComplete)
                        {
                            agent.SetDestination(hit.position);
                            startTime = Time.time;
                            stuckTimer = 0f;
                            Debug.Log($"{pet.petName}: 새 목적지로 재할당 완료 - {hit.position}");
                        }
                        else
                        {
                            Debug.LogWarning($"{pet.petName}: 재할당할 경로를 찾을 수 없습니다.");
                            break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"{pet.petName}: 재할당할 위치를 찾을 수 없습니다.");
                        break;
                    }
                }
                else
                {
                    Debug.Log($"{pet.petName}이(가) 재할당 후에도 도착하지 못했습니다. 현재 위치에서 멈춥니다.");
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
            yield break;
        }

        // ★ 안전하게 원래 속도로 복원
        try
        {
            agent.speed = originalSpeed;
            agent.angularSpeed = originalAngularSpeed;
            agent.acceleration = originalAcceleration;
            agent.stoppingDistance = originalStoppingDistance;
            agent.SetDestination(pet.transform.position);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"{pet.petName}: 속도 복원 실패 - {e.Message}");
        }

        // 모이기 완료 상태로 설정
        pet.isGathered = true;
        // isGathering은 여전히 true로 유지 (모이기가 활성 상태이므로)

        if (Camera.main != null)
        {
            Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
            directionToCamera.y = 0;
            if (directionToCamera.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                while (Quaternion.Angle(pet.transform.rotation, targetRotation) > angleThreshold)
                {
                    // 모으기 명령이 취소되었는지 체크
                    if (pet.gatherCommandVersion != currentGatherVersion)
                    {
                        Debug.Log($"{pet.petName}의 모이기 명령이 취소되었습니다.");
                        yield break;
                    }

                    pet.transform.rotation = Quaternion.RotateTowards(pet.transform.rotation, targetRotation, cameraRotationSpeed * Time.deltaTime);
                    yield return null;
                }
            }
        }
        Debug.Log($"{pet.petName}이(가) 도착 후 카메라를 부드럽게 바라보며 멈췄습니다.");
    }
}