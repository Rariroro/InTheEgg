using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 펫 종류를 구분하는 열거형 - 총 60종류의 펫 타입 정의
public enum PetType
{
    Turtle, Flamingo, Chick, Chicken, Pig, Cow, Cat, Dog, Duck, Elk, Boar, Wolf,
    Rabbit, Skunk, Deer, Raccoon, Owl, Fox, Squirrel, Mole, Porcupine, Camel, Goat,
    Anteater, Iguana, Pangolin, Alpaca, Kangaroo, Meerkat, Mule, Bison, Ostrich,
    Horse, Zebra, Bull, Lioness, Giraffe, Lion, Rhino, Elephant, Sheep, Gorilla,
    Possum, Leopard, Bear, Peacock, Tiger, Panda, Monkey, Sloth, RedPanda, Koala,
    Malayan, Chameleon, Buffalo, Hippo, Armadillo, Crocodile, Platypus, Otter
}

// 펫 간 상호작용 종류를 정의하는 열거형
public enum InteractionType
{
    Fight,        // 싸우기
    WalkTogether, // 같이 걷기
    RestTogether, // 같이 쉬기
    Race,         // 달리기 시합
    ChaseAndRun,  // 쫓고 쫓기기
    SleepTogether, // 같이 자기
    RideAndWalk,   // 타고 걷기
    SlothKoalaRace, // 나무늘보-코알라 달리기
    ChameleonCamouflage // 카멜레온 위장
}

/// <summary>
/// 간소화된 펫 상호작용 매니저
/// 콜라이더 기반 감지 시스템과 함께 작동
/// </summary>
public class PetInteractionManager : MonoBehaviour
{
    // 싱글톤 패턴
    public static PetInteractionManager Instance { get; private set; }

    [Header("상호작용 설정")]
    [Tooltip("레거시 쿨타임 값 - CooldownManager 사용 시 무시됨")]
    [System.Obsolete("CooldownManager를 사용하세요")]
    public float interactionCooldown = 30f;

    [Header("동시 상호작용 제한")]
    [Tooltip("동시에 진행할 수 있는 최대 상호작용 수")]
    [Range(1, 10)]
    public int maxConcurrentInteractions = 5;

    [Tooltip("카메라와의 거리 기반 우선순위 사용")]
    public bool useDistancePriority = true;

    [Tooltip("우선순위 재평가 주기 (초)")]
    public float priorityCheckInterval = 2.0f;

    [Header("시작 지연")]
    public float startDelay = 3.0f;
    private bool canStartInteractions = false;

    [Header("쿨타임 관리")]
    [Tooltip("CooldownManager 사용 여부")]
    public bool useCooldownManager = true;

    // 상호작용 관리
    private Dictionary<PetController, PetController> interactingPets = new Dictionary<PetController, PetController>();
    private Dictionary<PetController, float> lastInteractionTime = new Dictionary<PetController, float>(); // 레거시 호환용

    // 우선순위 관리
    private class InteractionInfo
    {
        public PetController pet1;
        public PetController pet2;
        public BasePetInteraction interaction;
        public float distance; // 카메라와의 거리
        public float startTime;

        public InteractionInfo(PetController p1, PetController p2, BasePetInteraction inter)
        {
            pet1 = p1;
            pet2 = p2;
            interaction = inter;
            startTime = Time.time;
            UpdateDistance();
        }

        public void UpdateDistance()
        {
            if (Camera.main != null && pet1 != null && pet2 != null)
            {
                Vector3 midPoint = (pet1.transform.position + pet2.transform.position) / 2f;
                distance = Vector3.Distance(Camera.main.transform.position, midPoint);
            }
            else
            {
                distance = float.MaxValue;
            }
        }
    }

    private List<InteractionInfo> activeInteractions = new List<InteractionInfo>();
    private Queue<InteractionInfo> pendingInteractions = new Queue<InteractionInfo>();
    private float lastPriorityCheck = 0f;
    
    // 등록된 상호작용 컴포넌트
    private List<BasePetInteraction> registeredInteractions = new List<BasePetInteraction>();
    
    // 펫 목록 (선택적 - 디버그용)
    private List<PetController> allPets = new List<PetController>();

    private void Awake()
    {
        // 싱글톤 패턴 구현
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 상호작용 컴포넌트 등록
        RegisterInteractions();
    }

    private void Start()
    {
        // 초기 펫 리스트 구축 (선택적)
        RefreshPetList();

        // 지정된 시간 후에 상호작용 활성화
        StartCoroutine(EnableInteractionsAfterDelay());

        // 우선순위 재평가 코루틴 시작
        if (useDistancePriority)
        {
            StartCoroutine(UpdateInteractionPriorities());
        }
    }

    private IEnumerator EnableInteractionsAfterDelay()
    {
        yield return new WaitForSeconds(startDelay);
        canStartInteractions = true;
        // Debug.Log("[PetInteractionManager] 상호작용 시스템 활성화!");
    }

    private void RegisterInteractions()
    {
        registeredInteractions.AddRange(GetComponents<BasePetInteraction>());
        // Debug.Log($"[PetInteractionManager] {registeredInteractions.Count}개의 상호작용 컴포넌트 등록됨");
    }

    /// <summary>
    /// 콜라이더 감지기로부터 상호작용 요청 처리
    /// </summary>
    public void RequestInteraction(PetController pet1, PetController pet2)
    {
        if (!canStartInteractions)
            return;

        // 빠른 거부 조건들
        if (pet1 == null || pet2 == null)
            return;

        if (IsInteracting(pet1) || IsInteracting(pet2))
            return;

        // 개별 펫 쿨타임 체크
        if (IsOnCooldown(pet1) || IsOnCooldown(pet2))
            return;

        // 적합한 상호작용 찾기
        BasePetInteraction suitableInteraction = FindSuitableInteraction(pet1, pet2);
        if (suitableInteraction == null)
            return;

        // 새 상호작용 정보 생성
        InteractionInfo newInteraction = new InteractionInfo(pet1, pet2, suitableInteraction);

        // 동시 상호작용 수 체크
        if (activeInteractions.Count >= maxConcurrentInteractions)
        {
            // 우선순위 기반으로 처리
            if (useDistancePriority)
            {
                TryReplaceOrQueueInteraction(newInteraction);
            }
            else
            {
                // 대기열에 추가
                pendingInteractions.Enqueue(newInteraction);
                // Debug.Log($"[PetInteractionManager] 최대 상호작용 수 도달. 대기열에 추가: {pet1.petName} & {pet2.petName}");
            }
            return;
        }

        // 상호작용 시작
        StartInteraction(newInteraction);
    }

    private void TryReplaceOrQueueInteraction(InteractionInfo newInteraction)
    {
        // 가장 멀리 있는 상호작용 찾기
        InteractionInfo furthestInteraction = null;
        float maxDistance = newInteraction.distance;

        foreach (var interaction in activeInteractions)
        {
            if (interaction.distance > maxDistance)
            {
                maxDistance = interaction.distance;
                furthestInteraction = interaction;
            }
        }

        if (furthestInteraction != null)
        {
            // 먼 상호작용 종료하고 새 상호작용 시작
            // Debug.Log($"[PetInteractionManager] 거리 우선순위: {furthestInteraction.pet1.petName} & {furthestInteraction.pet2.petName} 종료, {newInteraction.pet1.petName} & {newInteraction.pet2.petName} 시작");

            EndInteraction(furthestInteraction);
            StartInteraction(newInteraction);
        }
        else
        {
            // 대기열에 추가
            pendingInteractions.Enqueue(newInteraction);
        }
    }

    private void StartInteraction(InteractionInfo interactionInfo)
    {
        // Debug.Log($"[PetInteractionManager] {interactionInfo.pet1.petName}와 {interactionInfo.pet2.petName} 사이에 {interactionInfo.interaction.InteractionName} 시작!");

        // 활성 상호작용 목록에 추가
        activeInteractions.Add(interactionInfo);

        // 상호작용 실행
        interactionInfo.interaction.StartInteraction(interactionInfo.pet1, interactionInfo.pet2);

        // 상호작용 중인 펫 쌍 기록
        interactingPets[interactionInfo.pet1] = interactionInfo.pet2;
        interactingPets[interactionInfo.pet2] = interactionInfo.pet1;

        // 토스트 알림에 활성 상호작용 수 전달
        if (InteractionNotificationHandler.Instance != null)
        {
            var handler = InteractionNotificationHandler.Instance;
            handler.SetActiveInteractionCount(activeInteractions.Count);
        }
    }

    private void EndInteraction(InteractionInfo interactionInfo)
    {
        if (interactionInfo == null) return;

        // 활성 목록에서 제거
        activeInteractions.Remove(interactionInfo);

        // 상호작용 종료 (이미 진행 중이면)
        if (interactionInfo.pet1 != null && interactionInfo.pet1.State.IsInteracting)
        {
            interactionInfo.interaction.StopAllCoroutines();
        }

        // 딕셔너리 정리
        if (interactingPets.ContainsKey(interactionInfo.pet1))
            interactingPets.Remove(interactionInfo.pet1);
        if (interactingPets.ContainsKey(interactionInfo.pet2))
            interactingPets.Remove(interactionInfo.pet2);

        // 상태 정리
        if (interactionInfo.pet1 != null)
            interactionInfo.pet1.State.EndInteraction();
        if (interactionInfo.pet2 != null)
            interactionInfo.pet2.State.EndInteraction();
    }

    private BasePetInteraction FindSuitableInteraction(PetController pet1, PetController pet2)
    {
        foreach (var interaction in registeredInteractions)
        {
            if (interaction.CanInteract(pet1, pet2))
            {
                return interaction;
            }
        }
        return null;
    }

    /// <summary>
    /// 상호작용 종료 시 호출
    /// </summary>
    public void NotifyInteractionEnded(PetController pet1, PetController pet2)
    {
        bool pet1Valid = pet1 != null;
        bool pet2Valid = pet2 != null;

        // 해당 상호작용 정보 찾기
        InteractionInfo endedInteraction = null;
        foreach (var info in activeInteractions)
        {
            if ((info.pet1 == pet1 && info.pet2 == pet2) ||
                (info.pet1 == pet2 && info.pet2 == pet1))
            {
                endedInteraction = info;
                break;
            }
        }

        if (endedInteraction != null)
        {
            activeInteractions.Remove(endedInteraction);

            // 토스트 알림에 활성 상호작용 수 업데이트
            if (InteractionNotificationHandler.Instance != null)
            {
                InteractionNotificationHandler.Instance.SetActiveInteractionCount(activeInteractions.Count);
            }
        }

        if (pet1Valid)
        {
            if (interactingPets.ContainsKey(pet1))
            {
                interactingPets.Remove(pet1);
            }
            pet1.State.EndInteraction();
        }

        if (pet2Valid)
        {
            if (interactingPets.ContainsKey(pet2))
            {
                interactingPets.Remove(pet2);
            }
            pet2.State.EndInteraction();
        }

        // Debug.Log($"[PetInteractionManager] 상호작용 종료: {(pet1Valid ? pet1.petName : "null")} - {(pet2Valid ? pet2.petName : "null")}");

        // 대기 중인 상호작용 처리
        ProcessPendingInteractions();
    }

    private void ProcessPendingInteractions()
    {
        // 활성 상호작용 수가 최대치보다 적고 대기 중인 것이 있으면
        while (activeInteractions.Count < maxConcurrentInteractions && pendingInteractions.Count > 0)
        {
            InteractionInfo pending = pendingInteractions.Dequeue();

            // 펫이 여전히 유효하고 상호작용 가능한 상태인지 체크
            if (pending.pet1 != null && pending.pet2 != null &&
                !IsInteracting(pending.pet1) && !IsInteracting(pending.pet2) &&
                !IsOnCooldown(pending.pet1) && !IsOnCooldown(pending.pet2))
            {
                StartInteraction(pending);
                break; // 한 번에 하나씩만 처리
            }
        }
    }

    private IEnumerator UpdateInteractionPriorities()
    {
        while (true)
        {
            yield return new WaitForSeconds(priorityCheckInterval);

            if (activeInteractions.Count == 0)
                continue;

            // 모든 활성 상호작용의 거리 업데이트
            foreach (var interaction in activeInteractions)
            {
                interaction.UpdateDistance();
            }

            // 대기 중인 상호작용도 거리 업데이트
            var pendingList = pendingInteractions.ToList();
            foreach (var pending in pendingList)
            {
                pending.UpdateDistance();
            }

            // 거리 기반 우선순위 재평가
            if (pendingInteractions.Count > 0 && activeInteractions.Count >= maxConcurrentInteractions)
            {
                var nearestPending = pendingList.OrderBy(p => p.distance).FirstOrDefault();
                if (nearestPending != null)
                {
                    var furthestActive = activeInteractions.OrderByDescending(a => a.distance).FirstOrDefault();

                    if (furthestActive != null && nearestPending.distance < furthestActive.distance * 0.7f) // 30% 이상 가까워야 교체
                    {
                        // Debug.Log($"[PetInteractionManager] 우선순위 재평가로 상호작용 교체");
                        EndInteraction(furthestActive);
                        StartInteraction(nearestPending);
                        pendingInteractions = new Queue<InteractionInfo>(pendingList.Where(p => p != nearestPending));
                    }
                }
            }
        }
    }

    private bool IsInteracting(PetController pet)
    {
        return interactingPets.ContainsKey(pet);
    }

    private bool IsOnCooldown(PetController pet)
    {
        if (useCooldownManager && CooldownManager.Instance != null)
        {
            // CooldownManager 사용
            return CooldownManager.Instance.IsOnCooldown(
                CooldownManager.CooldownType.PetInteraction,
                pet.petName);
        }
        else
        {
            // 레거시 방식 사용
            if (lastInteractionTime.TryGetValue(pet, out float lastTime))
            {
                #pragma warning disable CS0618 // 사용되지 않는 멤버 사용 경고 무시
                return Time.time - lastTime < interactionCooldown;
                #pragma warning restore CS0618
            }
            return false;
        }
    }


    // === 선택적 메서드들 (디버그/호환성용) ===
    
    public void RefreshPetList()
    {
        allPets.Clear();
        PetController[] foundPets = FindObjectsOfType<PetController>();
        allPets.AddRange(foundPets);
        // Debug.Log($"[PetInteractionManager] 펫 리스트 새로고침 완료. 총 {allPets.Count}마리");
    }

    public void RegisterPet(PetController pet)
    {
        if (pet != null && !allPets.Contains(pet))
        {
            allPets.Add(pet);
        // Debug.Log($"[PetInteractionManager] 펫 등록: {pet.petName}");
        }
    }

    public void UnregisterPet(PetController pet)
    {
        if (pet != null && allPets.Contains(pet))
        {
            allPets.Remove(pet);
            
            // 상호작용 정리
            if (interactingPets.ContainsKey(pet))
            {
                PetController partner = interactingPets[pet];
                interactingPets.Remove(pet);
                if (partner != null && interactingPets.ContainsKey(partner))
                {
                    interactingPets.Remove(partner);
                }
            }
            
            lastInteractionTime.Remove(pet);
        // Debug.Log($"[PetInteractionManager] 펫 제거: {pet.petName}");
        }
    }

    // === 디버그 메서드들 ===
    
    [ContextMenu("펫 리스트 새로고침")]
    public void ForceRefreshPetList()
    {
        RefreshPetList();
    }

    [ContextMenu("현재 상태 출력")]
    public void PrintCurrentStatus()
    {
        Debug.Log($"[PetInteractionManager] 현재 상태:");
        Debug.Log($"  - 총 펫 수: {allPets.Count}");
        Debug.Log($"  - 활성 상호작용: {activeInteractions.Count} / {maxConcurrentInteractions}");
        Debug.Log($"  - 대기 중인 상호작용: {pendingInteractions.Count}");
        Debug.Log($"  - 상호작용 중인 펫 쌍: {interactingPets.Count / 2}");
        Debug.Log($"  - 등록된 상호작용 타입: {registeredInteractions.Count}개");

        if (activeInteractions.Count > 0)
        {
            Debug.Log("  활성 상호작용 목록:");
            foreach (var interaction in activeInteractions)
            {
                Debug.Log($"    - {interaction.pet1.petName} ↔ {interaction.pet2.petName} (거리: {interaction.distance:F1}m)");
            }
        }
    }

    /// <summary>
    /// 현재 활성 상호작용 수 가져오기
    /// </summary>
    public int GetActiveInteractionCount()
    {
        return activeInteractions.Count;
    }

    /// <summary>
    /// 대기 중인 상호작용 수 가져오기
    /// </summary>
    public int GetPendingInteractionCount()
    {
        return pendingInteractions.Count;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
}