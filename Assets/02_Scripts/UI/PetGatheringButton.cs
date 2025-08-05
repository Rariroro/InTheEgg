// PetGatheringButton.cs

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
    public float gatherRadius = 15f;  // 터치한 지점을 중심으로 펫들을 모을 영역 반경
    public TMP_Text feedbackText;    // UI 피드백용 텍스트 (인스펙터에서 할당)

    // ▼▼▼ 이 변수들은 이제 GatherAction에서 직접 사용하므로 여기서는 제거해도 되지만,
    // 다른 곳에서 참조할 가능성을 위해 남겨두거나, 중앙 설정 파일로 옮기는 것을 권장합니다.
    // 이 예제에서는 시각적 확인을 위해 남겨두겠습니다.
    [Header("Agent Parameter Multipliers (참고용)")]
    [SerializeField] private float speedMultiplier = 4f;
    [SerializeField] private float angularSpeedMultiplier = 4f;
    [SerializeField] private float accelerationMultiplier = 4f;
    [SerializeField] private float stoppingDistanceMultiplier = 3f;

    private bool waitingForTerrainInput = false;
    private bool isGatheringActive = false;

    void Start()
    {
        if (gatherButton != null)
        {
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
        UpdateButtonText("펫 모이기");
    }

    public void ToggleGatheringMode()
    {
        if (waitingForTerrainInput || isGatheringActive)
        {
            CancelGathering();
        }
        else
        {
            waitingForTerrainInput = true;
            if (feedbackText != null)
            {
                feedbackText.text = "지형을 터치하여 펫들을 모으세요.";
                feedbackText.gameObject.SetActive(true);
            }
            UpdateButtonText("모이기 해제");
            // Debug.Log("펫 모으기 모드 활성화됨.");
        }
    }

    void Update()
    {
        bool isOverUI = EventSystem.current.IsPointerOverGameObject();
        if (Input.touchCount > 0)
        {
            isOverUI = EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }
        if (isOverUI) return;

        if (waitingForTerrainInput && Input.GetMouseButtonDown(0))
        {
            LayerMask terrainMask = LayerMask.GetMask("Terrain");
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainMask))
            {
                Vector3 centerPoint = hit.point;
                PetController[] pets = FindObjectsByType<PetController>(FindObjectsSortMode.None);
                List<Vector3> targetPositions = GenerateGridPositions(centerPoint, gatherRadius, pets.Length);
                Dictionary<PetController, Vector3> smartAssignments = OptimizePositionAssignment(pets, targetPositions);

                foreach (var assignment in smartAssignments)
                {
                    PetController pet = assignment.Key;
                    Vector3 targetPoint = assignment.Value;

                    if (pet == null) continue;

                    // 상호작용 중이라면 즉시 중단
                    if (pet.State.IsInteracting && pet.State.InteractionLogic != null)
                    {
                        var interactionLogic = pet.State.InteractionLogic;
                        interactionLogic.StopAllCoroutines();
                        
                        // 상호작용 파트너가 있다면 그 쪽도 중단
                        if (pet.State.InteractionPartner != null)
                        {
                            var partner = pet.State.InteractionPartner;
                            
                            // 파트너의 상호작용 로직도 중단
                            if (partner.State.InteractionLogic != null)
                            {
                                partner.State.InteractionLogic.StopAllCoroutines();
                            }
                            
                            // 매니저에 종료 알림
                            if (PetInteractionManager.Instance != null)
                            {
                                PetInteractionManager.Instance.NotifyInteractionEnded(pet, partner);
                            }
                        }
                    }

                    // ★★★ 역할 축소: 이제 버튼은 상태와 목표만 설정합니다. ★★★
                    // 실제 이동 로직(속도 변경, 애니메이션)은 GatherActivity가 책임집니다.
                    pet.State.SetGatheringState(targetPoint);
                    
                    // AI를 즉시 업데이트하여 GatherActivity를 시작하도록 함
                    if (pet.AI != null)
                    {
                        pet.AI.InterruptAndResetAI();
                    }
                }

                // Debug.Log($"모이기 명령 완료: {pets.Length}마리에게 명령 전달.");

                if (feedbackText != null)
                    feedbackText.gameObject.SetActive(false);

                waitingForTerrainInput = false;
                isGatheringActive = true;
            }
        }
    }

    // 모으기 취소
    private void CancelGathering()
    {
        waitingForTerrainInput = false;
        
        if (isGatheringActive)
        {
            PetController[] pets = FindObjectsByType<PetController>(FindObjectsSortMode.None);
            foreach (PetController pet in pets)
            {
                // 모이기 상태를 해제하여 Idle 상태로 전환
                if (pet != null)
                {
                    pet.State.TrySetStatus(PetStatus.Idle); // GatheringInProgress/GatheredWaiting 상태를 해제
                }
            }
            isGatheringActive = false;
            // Debug.Log("펫 모이기 해제됨. 펫들이 정상적으로 움직입니다.");
        }
        
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);
            
        UpdateButtonText("펫 모이기");
    }

    // (이하 나머지 헬퍼 메서드들은 변경 없음)
    // UpdateButtonText, HideFeedbackAfterDelay, GenerateGridPositions, OptimizePositionAssignment 등...
    private void UpdateButtonText(string newText)
    {
        TMP_Text btnText = gatherButton.GetComponentInChildren<TMP_Text>();
        if (btnText != null)
        {
            btnText.text = newText;
        }
    }

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
                if (positions.Count >= count) break;
            }
            if (positions.Count >= count) break;
        }
        return positions;
    }
    
    private Dictionary<PetController, Vector3> OptimizePositionAssignment(PetController[] pets, List<Vector3> positions)
    {
        Dictionary<PetController, Vector3> assignments = new Dictionary<PetController, Vector3>();
        List<PetController> remainingPets = new List<PetController>(pets);
        List<Vector3> remainingPositions = new List<Vector3>(positions);

        while (remainingPets.Count > 0 && remainingPositions.Count > 0)
        {
            float minDistance = float.MaxValue;
            PetController closestPet = null;
            Vector3 closestPosition = Vector3.zero;
            int closestPetIndex = -1;
            int closestPositionIndex = -1;

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

            if (closestPet != null)
            {
                assignments[closestPet] = closestPosition;
                remainingPets.RemoveAt(closestPetIndex);
                remainingPositions.RemoveAt(closestPositionIndex);
            }
            else
            {
                break;
            }
        }
        return assignments;
    }
}