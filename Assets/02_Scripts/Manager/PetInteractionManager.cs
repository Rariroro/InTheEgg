using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using InTheEgg.Constants;

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
    Fight,                  // 싸우기
    Headbutt,               // 박치기
    CamelAlpacaSpitFight,   // 침 뱉기 대결
    WalkTogether,           // 같이 걷기
    RestTogether,           // 같이 쉬기
    SleepTogether,          // 같이 자기
    TurtleRabbitRace,       // 토끼와 거북이 경주
    ChaseAndRun,            // 쫓고 쫓기기
    RideAndWalk,            // 타고 걷기
    SlowRace,               // 느린 펫 경주 (나무늘보, 코알라, 거북이)
    PredatorMoleHunt,       // 두더지 사냥
    PredatorPossumPrank,    // 주머니쥐 장난
    ChameleonCamouflage,    // 카멜레온 위장
    PersonalityReaction,    // 성격 반응
    SkunkDefense            // 스컹크 방어
}

/// <summary>
/// 간소화된 펫 상호작용 매니저
/// 콜라이더 기반 감지 시스템과 함께 작동
/// </summary>
public class PetInteractionManager : MonoBehaviour
{
    // 싱글톤 패턴
    public static PetInteractionManager Instance { get; private set; }

    [Header("동시 상호작용 제한")]
    [Tooltip("동시에 진행할 수 있는 최대 상호작용 수")]
    [Range(1, 10)]
    public int maxConcurrentInteractions = 5;

    [Header("시작 지연")]
    public float startDelay = 3.0f;
    private bool canStartInteractions = false;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLogs = false;

    // 상호작용 관리
    private Dictionary<PetController, PetController> interactingPets = new Dictionary<PetController, PetController>();

    // 상호작용 정보
    private class InteractionInfo
    {
        public PetController pet1;
        public PetController pet2;
        public BasePetInteraction interaction;
        public float startTime;

        public InteractionInfo(PetController p1, PetController p2, BasePetInteraction inter)
        {
            pet1 = p1;
            pet2 = p2;
            interaction = inter;
            startTime = Time.time;
        }
    }

    private List<InteractionInfo> activeInteractions = new List<InteractionInfo>();
    private Queue<InteractionInfo> pendingInteractions = new Queue<InteractionInfo>();

    // 우선순위 상호작용 (PersonalityReaction 등 - 5개 제한 없음)
    private List<InteractionInfo> priorityInteractions = new List<InteractionInfo>();

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
        if (enableDebugLogs)
            Debug.Log($"[Manager] 상호작용 요청 받음: {pet1?.petName ?? "null"} ↔ {pet2?.petName ?? "null"}");

        if (!canStartInteractions)
        {
            if (enableDebugLogs)
                Debug.Log($"[Manager] ❌ 시작 지연 중 (아직 {startDelay}초 대기 필요)");
            return;
        }

        // 빠른 거부 조건들
        if (pet1 == null || pet2 == null)
        {
            if (enableDebugLogs)
                Debug.Log($"[Manager] ❌ null 펫: pet1={pet1 != null}, pet2={pet2 != null}");
            return;
        }

        if (IsInteracting(pet1) || IsInteracting(pet2))
        {
            if (enableDebugLogs)
                Debug.Log($"[Manager] ❌ 이미 상호작용 중: {pet1.petName}={IsInteracting(pet1)}, {pet2.petName}={IsInteracting(pet2)}");
            return;
        }

        // 개별 펫 쿨타임 체크
        if (IsOnCooldown(pet1) || IsOnCooldown(pet2))
        {
            if (enableDebugLogs)
                Debug.Log($"[Manager] ❌ 쿨타임 중: {pet1.petName}={IsOnCooldown(pet1)}, {pet2.petName}={IsOnCooldown(pet2)}");
            return;
        }

        // 일반 펫 상호작용 욕구 체크 (50/50 임계값)
        // PersonalityReaction은 FindSuitableInteraction 이후에 별도 체크
        if (IsNeedsTooHighForInteraction(pet1) || IsNeedsTooHighForInteraction(pet2))
        {
            if (enableDebugLogs)
                Debug.Log($"[Manager] ❌ 욕구 과다: {pet1.petName}(H:{pet1.Needs?.Hunger:F0},S:{pet1.Needs?.Sleepiness:F0}), {pet2.petName}(H:{pet2.Needs?.Hunger:F0},S:{pet2.Needs?.Sleepiness:F0})");
            return;
        }

        // 적합한 상호작용 찾기
        BasePetInteraction suitableInteraction = FindSuitableInteraction(pet1, pet2);
        if (suitableInteraction == null)
        {
            if (enableDebugLogs)
                Debug.Log($"[Manager] ❌ 적합한 상호작용 없음: {pet1.petName}({pet1.PetType}) ↔ {pet2.petName}({pet2.PetType})");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[Manager] ✅ 적합한 상호작용 발견: {suitableInteraction.InteractionName}");

        // 새 상호작용 정보 생성
        InteractionInfo newInteraction = new InteractionInfo(pet1, pet2, suitableInteraction);

        // 우선순위 상호작용(PersonalityReaction 등)은 5개 제한 무시하고 즉시 시작
        if (suitableInteraction.IsPriorityInteraction)
        {
            if (enableDebugLogs)
                Debug.Log($"[Manager] ⭐ 우선순위 상호작용 시작: {pet1.petName} & {pet2.petName} ({suitableInteraction.InteractionName})");
            StartPriorityInteraction(newInteraction);
            return;
        }

        // 일반 상호작용은 동시 상호작용 수 체크
        if (activeInteractions.Count >= maxConcurrentInteractions)
        {
            if (enableDebugLogs)
                Debug.Log($"[Manager] ⚠️ 최대 상호작용 수 도달 ({activeInteractions.Count}/{maxConcurrentInteractions})");

            // 대기열에 추가
            pendingInteractions.Enqueue(newInteraction);
            if (enableDebugLogs)
                Debug.Log($"[Manager] 대기열에 추가: {pet1.petName} & {pet2.petName}");
            return;
        }

        // 상호작용 시작
        StartInteraction(newInteraction);
    }

    /// <summary>
    /// 우선순위 상호작용 시작 (5개 제한 무시) - 새 인스턴스 생성
    /// </summary>
    private void StartPriorityInteraction(InteractionInfo interactionInfo)
    {
        // Debug.Log($"[PetInteractionManager] 우선순위 상호작용 시작: {interactionInfo.pet1.petName} & {interactionInfo.pet2.petName} ({interactionInfo.interaction.InteractionName})");

        // ★★★ 수정: 템플릿에서 새로운 인스턴스 생성 (동시 실행 격리) ★★★
        var templateInteraction = interactionInfo.interaction;
        var newInteraction = CreateInteractionInstance(templateInteraction);
        interactionInfo.interaction = newInteraction;

        // 우선순위 상호작용 목록에 추가
        priorityInteractions.Add(interactionInfo);

        // 상호작용 실행
        newInteraction.StartInteraction(interactionInfo.pet1, interactionInfo.pet2);

        // 상호작용 중인 펫 쌍 기록
        interactingPets[interactionInfo.pet1] = interactionInfo.pet2;
        interactingPets[interactionInfo.pet2] = interactionInfo.pet1;

        // 토스트 알림은 필요 없음 (PersonalityReaction은 ShouldShowToastNotification = false)
    }

    /// <summary>
    /// 일반 상호작용 시작 (5개 제한 적용) - 새 인스턴스 생성
    /// </summary>
    private void StartInteraction(InteractionInfo interactionInfo)
    {
        // Debug.Log($"[PetInteractionManager] {interactionInfo.pet1.petName}와 {interactionInfo.pet2.petName} 사이에 {interactionInfo.interaction.InteractionName} 시작!");

        // 템플릿에서 새로운 인스턴스 생성 (격리를 위해)
        var templateInteraction = interactionInfo.interaction;
        var newInteraction = CreateInteractionInstance(templateInteraction);
        interactionInfo.interaction = newInteraction;

        // 활성 상호작용 목록에 추가
        activeInteractions.Add(interactionInfo);

        // 상호작용 실행
        newInteraction.StartInteraction(interactionInfo.pet1, interactionInfo.pet2);

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
        BasePetInteraction bestInteraction = null;
        int highestPriority = -1;

        // 모든 가능한 상호작용을 확인하고 가장 높은 우선순위를 가진 것을 선택
        foreach (var interaction in registeredInteractions)
        {
            if (interaction.CanInteract(pet1, pet2))
            {
                if (interaction.Priority > highestPriority)
                {
                    highestPriority = interaction.Priority;
                    bestInteraction = interaction;
                }
            }
        }

        if (enableDebugLogs && bestInteraction != null)
        {
            Debug.Log($"[Manager] 선택된 상호작용: {bestInteraction.InteractionName} (우선순위: {highestPriority})");
        }

        return bestInteraction;
    }

    /// <summary>
    /// 템플릿에서 새로운 상호작용 인스턴스를 생성합니다 (동시 실행 격리)
    /// </summary>
    private BasePetInteraction CreateInteractionInstance(BasePetInteraction template)
    {
        if (template == null) return null;

        // 새로운 GameObject 생성
        GameObject interactionObj = new GameObject($"{template.InteractionName}_{Time.time}");
        interactionObj.transform.SetParent(this.transform);

        BasePetInteraction newInstance = null;

        if (template is RideAndWalkInteraction)
        {
            var original = template as RideAndWalkInteraction;
            var newComp = interactionObj.AddComponent<RideAndWalkInteraction>();
            newComp.CopySettingsFrom(original);
            newInstance = newComp;
        }
        else if (template is TurtleRabbitRace)
        {
            var original = template as TurtleRabbitRace;
            var newComp = interactionObj.AddComponent<TurtleRabbitRace>();
            newComp.CopySettingsFrom(original);
            newInstance = newComp;
        }
        else if (template is ChaseAndRunInteraction)
        {
            var original = template as ChaseAndRunInteraction;
            var newComp = interactionObj.AddComponent<ChaseAndRunInteraction>();
            newComp.CopySettingsFrom(original);
            newInstance = newComp;
        }
        else if (template is ChameleonCamouflageInteraction)
        {
            var original = template as ChameleonCamouflageInteraction;
            var newComp = interactionObj.AddComponent<ChameleonCamouflageInteraction>();
            newComp.CopySettingsFrom(original);
            newInstance = newComp;
        }
        else if (template is HeadbuttInteraction)
        {
            var original = template as HeadbuttInteraction;
            var newComp = interactionObj.AddComponent<HeadbuttInteraction>();
            newComp.CopySettingsFrom(original);
            newInstance = newComp;
        }
        else if (template is FightInteraction)
        {
            var original = template as FightInteraction;
            var newComp = interactionObj.AddComponent<FightInteraction>();
            newComp.CopySettingsFrom(original);
            newInstance = newComp;
        }
        else if (template is SlowRaceInteraction)
        {
            var original = template as SlowRaceInteraction;
            var newComp = interactionObj.AddComponent<SlowRaceInteraction>();
            newComp.CopySettingsFrom(original);
            newInstance = newComp;
        }
        else if (template is WalkTogetherInteraction)
        {
            var original = template as WalkTogetherInteraction;
            var newComp = interactionObj.AddComponent<WalkTogetherInteraction>();
            newComp.CopySettingsFrom(original);
            newInstance = newComp;
        }
        else if (template is PersonalityReactionInteraction)
        {
            var original = template as PersonalityReactionInteraction;
            var newComp = interactionObj.AddComponent<PersonalityReactionInteraction>();
            newComp.CopySettingsFrom(original);
            newInstance = newComp;
        }
        else
        {
            // 다른 상호작용은 기존 방식 사용 (템플릿 그대로)
            return template;
        }

        return newInstance;
    }

    /// <summary>
    /// 상호작용 종료 시 호출
    /// </summary>
    public void NotifyInteractionEnded(PetController pet1, PetController pet2)
    {
        bool pet1Valid = pet1 != null;
        bool pet2Valid = pet2 != null;

        // 일반 상호작용에서 찾기
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

        // 일반 상호작용이면 제거
        if (endedInteraction != null)
        {
            activeInteractions.Remove(endedInteraction);

            // 생성된 인스턴스 파괴 (메모리 정리)
            if (endedInteraction.interaction != null && endedInteraction.interaction.gameObject != this.gameObject)
            {
                Destroy(endedInteraction.interaction.gameObject);
            }

            // 토스트 알림에 활성 상호작용 수 업데이트
            if (InteractionNotificationHandler.Instance != null)
            {
                InteractionNotificationHandler.Instance.SetActiveInteractionCount(activeInteractions.Count);
            }
        }
        else
        {
            // 우선순위 상호작용에서 찾기
            foreach (var info in priorityInteractions)
            {
                if ((info.pet1 == pet1 && info.pet2 == pet2) ||
                    (info.pet1 == pet2 && info.pet2 == pet1))
                {
                    endedInteraction = info;
                    break;
                }
            }

            // 우선순위 상호작용이면 제거
            if (endedInteraction != null)
            {
                priorityInteractions.Remove(endedInteraction);

                // ★★★ 추가: 생성된 인스턴스 파괴 (메모리 정리) ★★★
                if (endedInteraction.interaction != null && endedInteraction.interaction.gameObject != this.gameObject)
                {
                    Destroy(endedInteraction.interaction.gameObject);
                }
                // Debug.Log($"[PetInteractionManager] 우선순위 상호작용 종료: {endedInteraction.interaction.InteractionName}");
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
        int processedCount = 0;
        const int maxProcessAtOnce = 3; // 한 번에 최대 3개까지 시도 (무한 루프 방지)

        while (activeInteractions.Count < maxConcurrentInteractions &&
               pendingInteractions.Count > 0 &&
               processedCount < maxProcessAtOnce)
        {
            InteractionInfo pending = pendingInteractions.Dequeue();
            processedCount++;

            // 유효성 체크
            if (IsValidPendingInteraction(pending))
            {
                StartInteraction(pending);
                // 하나 시작했으면 다음 루프로 (한 번에 하나씩)
                break;
            }
            // 유효하지 않으면 그냥 버림 (다음 것 시도)
        }
    }

    /// <summary>
    /// 대기 중인 상호작용의 유효성 검사
    /// </summary>
    private bool IsValidPendingInteraction(InteractionInfo info)
    {
        // null 체크
        if (info == null || info.pet1 == null || info.pet2 == null)
            return false;

        // 이미 상호작용 중인지 체크
        if (IsInteracting(info.pet1) || IsInteracting(info.pet2))
            return false;

        // 쿨타임 체크
        if (IsOnCooldown(info.pet1) || IsOnCooldown(info.pet2))
            return false;

        // 홀딩/선택 상태 체크
        if (info.pet1.State.IsHolding || info.pet1.State.IsSelected ||
            info.pet2.State.IsHolding || info.pet2.State.IsSelected)
            return false;

        // 모이기 상태 체크
        if (info.pet1.State.CurrentStatus == PetStatus.GatheringInProgress ||
            info.pet1.State.CurrentStatus == PetStatus.GatheredWaiting ||
            info.pet2.State.CurrentStatus == PetStatus.GatheringInProgress ||
            info.pet2.State.CurrentStatus == PetStatus.GatheredWaiting)
            return false;

        return true;
    }

    private bool IsInteracting(PetController pet)
    {
        return interactingPets.ContainsKey(pet);
    }

    private bool IsOnCooldown(PetController pet)
    {
        if (CooldownManager.Instance != null)
        {
            return CooldownManager.Instance.IsOnCooldown(
                CooldownManager.CooldownType.PetInteraction,
                pet.petName);
        }
        return false;
    }

    /// <summary>
    /// 펫의 욕구가 일반 펫 상호작용 임계값(50/50)을 초과하는지 확인
    /// </summary>
    private bool IsNeedsTooHighForInteraction(PetController pet)
    {
        if (pet.Needs == null) return false;
        return pet.Needs.Hunger >= EmotionConstants.PET_INTERACTION_HUNGER_THRESHOLD ||
               pet.Needs.Sleepiness >= EmotionConstants.PET_INTERACTION_SLEEPINESS_THRESHOLD;
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
        Debug.Log($"  - 시스템 활성화: {canStartInteractions}");
        Debug.Log($"  - 총 펫 수: {allPets.Count}");
        Debug.Log($"  - 활성 상호작용: {activeInteractions.Count} / {maxConcurrentInteractions}");
        Debug.Log($"  - 우선순위 상호작용: {priorityInteractions.Count} (제한 없음)");
        Debug.Log($"  - 대기 중인 상호작용: {pendingInteractions.Count}");
        Debug.Log($"  - 상호작용 중인 펫 쌍: {interactingPets.Count / 2}");
        Debug.Log($"  - 등록된 상호작용 타입: {registeredInteractions.Count}개");

        if (activeInteractions.Count > 0)
        {
            Debug.Log("  활성 상호작용 목록 (5개 제한 적용):");
            foreach (var interaction in activeInteractions)
            {
                Debug.Log($"    - {interaction.pet1.petName} ↔ {interaction.pet2.petName} " +
                    $"({interaction.interaction.InteractionName}, 시간: {Time.time - interaction.startTime:F1}초)");
            }
        }

        if (priorityInteractions.Count > 0)
        {
            Debug.Log("  우선순위 상호작용 목록 (제한 없음):");
            foreach (var interaction in priorityInteractions)
            {
                Debug.Log($"    ⭐ {interaction.pet1.petName} ↔ {interaction.pet2.petName} " +
                    $"({interaction.interaction.InteractionName}, 시간: {Time.time - interaction.startTime:F1}초)");
            }
        }

        if (pendingInteractions.Count > 0)
        {
            Debug.Log($"  대기 중인 상호작용: {pendingInteractions.Count}개");
        }
    }

    [ContextMenu("쿨타임 상태 출력")]
    public void PrintCooldownStatus()
    {
        Debug.Log($"[PetInteractionManager] 쿨타임 상태:");

        if (CooldownManager.Instance != null)
        {
            Debug.Log($"  - CooldownManager 활성화됨");
            int cooldownCount = 0;
            foreach (var pet in allPets)
            {
                if (pet != null && IsOnCooldown(pet))
                {
                    cooldownCount++;
                    Debug.Log($"    ⏱️ {pet.petName}: 쿨타임 중");
                }
            }
            Debug.Log($"  - 쿨타임 중인 펫: {cooldownCount}마리");
        }
        else
        {
            Debug.Log($"  - CooldownManager가 없습니다!");
        }
    }

    [ContextMenu("등록된 상호작용 타입 출력")]
    public void PrintRegisteredInteractions()
    {
        Debug.Log($"[PetInteractionManager] 등록된 상호작용 타입 ({registeredInteractions.Count}개):");
        for (int i = 0; i < registeredInteractions.Count; i++)
        {
            var interaction = registeredInteractions[i];
            Debug.Log($"  {i + 1}. {interaction.InteractionName} ({interaction.GetType().Name})");
        }
    }

    [ContextMenu("테스트 - 랜덤 2마리 상호작용 강제 시작")]
    public void ForceRandomInteraction()
    {
        if (allPets.Count < 2)
        {
            Debug.LogWarning("[PetInteractionManager] 펫이 2마리 미만입니다!");
            return;
        }

        // 유효한 펫들만 필터링
        List<PetController> validPets = allPets.Where(p => p != null &&
            !p.State.IsInteracting &&
            !p.State.IsHolding).ToList();

        if (validPets.Count < 2)
        {
            Debug.LogWarning("[PetInteractionManager] 상호작용 가능한 펫이 2마리 미만입니다!");
            return;
        }

        // 랜덤 2마리 선택
        PetController pet1 = validPets[Random.Range(0, validPets.Count)];
        validPets.Remove(pet1);
        PetController pet2 = validPets[Random.Range(0, validPets.Count)];

        Debug.Log($"[PetInteractionManager] 강제 상호작용 테스트: {pet1.petName} ↔ {pet2.petName}");

        // 적합한 상호작용 찾기
        BasePetInteraction suitableInteraction = FindSuitableInteraction(pet1, pet2);
        if (suitableInteraction != null)
        {
            Debug.Log($"[PetInteractionManager] ✅ {suitableInteraction.InteractionName} 시작!");
            InteractionInfo testInteraction = new InteractionInfo(pet1, pet2, suitableInteraction);
            StartInteraction(testInteraction);
        }
        else
        {
            Debug.LogWarning($"[PetInteractionManager] ❌ {pet1.petName}({pet1.PetType})와 {pet2.petName}({pet2.PetType}) 사이에 적합한 상호작용이 없습니다!");

            // 등록된 모든 상호작용 체크
            Debug.Log($"등록된 상호작용들의 CanInteract 결과:");
            foreach (var interaction in registeredInteractions)
            {
                bool canInteract = interaction.CanInteract(pet1, pet2);
                Debug.Log($"  - {interaction.InteractionName}: {canInteract}");
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