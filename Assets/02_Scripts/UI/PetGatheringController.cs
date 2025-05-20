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
            // 동적으로 현재 존재하는 모든 펫을 가져옵니다.
            PetController[] pets = Object.FindObjectsByType<PetController>(FindObjectsSortMode.None);
            int petCount = pets.Length;
            Vector3 centerPoint = hit.point;
            List<Vector3> targetPositions = GenerateGridPositions(centerPoint, gatherRadius, petCount);

            // 각 펫에 대해 모으기 명령을 수행합니다.
            for (int i = 0; i < petCount; i++)
            {
                PetController pet = pets[i];
                if (pet.agent != null)
                {
                    // 모으기 명령 버전을 증가시키고 모으기 상태 해제 (이후 도착 시 isGathered를 true로 설정)
                    pet.gatherCommandVersion++;
                    pet.isGathered = false;

                    Vector3 targetPoint = targetPositions[i];
                    // NavMesh상에서 유효한 위치로 보정
                    NavMeshHit navHit;
                    if (NavMesh.SamplePosition(targetPoint, out navHit, gatherRadius, NavMesh.AllAreas))
                    {
                        targetPoint = navHit.position;
                    }

                    // 기본 속도 등의 값을 보관한 후, 배수 적용
                    float originalSpeed = pet.baseSpeed;
                    float originalAngularSpeed = pet.baseAngularSpeed;
                    float originalAcceleration = pet.baseAcceleration;
                    float originalStoppingDistance = pet.baseStoppingDistance;

                    pet.agent.speed = originalSpeed * speedMultiplier;
                    pet.agent.angularSpeed = originalAngularSpeed * angularSpeedMultiplier;
                    pet.agent.acceleration = originalAcceleration * accelerationMultiplier;
                    pet.agent.stoppingDistance = originalStoppingDistance * stoppingDistanceMultiplier;

                    pet.agent.SetDestination(targetPoint);
                    // 도착 후 카메라를 바라보도록 하는 코루틴 실행
                    StartCoroutine(StopAndLookAtCameraWhenArrived(pet, originalSpeed, originalAngularSpeed, originalAcceleration, originalStoppingDistance));
                }
            }

            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
            Debug.Log("펫들이 지정된 영역으로 이동합니다: " + centerPoint);
            waitingForTerrainInput = false;
            isGatheringActive = true;
        }
        else
        {
            if (feedbackText != null)
            {
                feedbackText.text = "유효한 지형을 터치하세요.";
                StartCoroutine(HideFeedbackAfterDelay(2f));
            }
            Debug.Log("터치한 위치가 유효한 지형이 아닙니다.");
        }
    }
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
        Debug.Log("펫 모으기 모드 취소됨.");
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
private IEnumerator StopAndLookAtCameraWhenArrived(PetController pet, float originalSpeed, float originalAngularSpeed, float originalAcceleration, float originalStoppingDistance)
{
    int currentGatherVersion = pet.gatherCommandVersion;
    NavMeshAgent agent = pet.agent;
    int reassignAttempts = 0;
    float stuckTimer = 0f;
    float startTime = Time.time;

    while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f)
    {
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
                Debug.Log($"{pet.petName}이(가) 막힌 상태입니다. 새로운 목적지를 한 번 재할당합니다.");
                Vector3 randomOffset = Random.insideUnitSphere * 2f;
                randomOffset.y = 0;
                Vector3 newDestination = pet.transform.position + randomOffset;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(newDestination, out hit, 2f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                startTime = Time.time;
                stuckTimer = 0f;
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

    if (pet.gatherCommandVersion != currentGatherVersion)
        yield break;

    // 원래 속도로 복원
    agent.speed = originalSpeed;
    agent.angularSpeed = originalAngularSpeed;
    agent.acceleration = originalAcceleration;
    agent.stoppingDistance = originalStoppingDistance;
    agent.SetDestination(pet.transform.position);
    pet.isGathered = true;

    if (Camera.main != null)
    {
        Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
        directionToCamera.y = 0;
        if (directionToCamera.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
            while (Quaternion.Angle(pet.transform.rotation, targetRotation) > angleThreshold)
            {
                pet.transform.rotation = Quaternion.RotateTowards(pet.transform.rotation, targetRotation, cameraRotationSpeed * Time.deltaTime);
                yield return null;
            }
        }
    }
    Debug.Log($"{pet.petName}이(가) 도착 후 카메라를 부드럽게 바라보며 멈췄습니다.");
}


}
