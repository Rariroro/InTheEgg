using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using InTheEgg.Constants;

/// <summary>
/// 성격 조합별 가벼운 반응 패턴을 구현하는 상호작용
/// 짧고 빈번하게 발생하며 감정 표현 없이 동작만으로 표현
/// </summary>
public class PersonalityReactionInteraction : BasePetInteraction
{
    // 상호작용 이름을 "PersonalityReaction"으로 고정 반환
    public override string InteractionName => "PersonalityReaction";

    /// <summary>
    /// 우선순위 상호작용으로 설정 (5개 제한 무시)
    /// 토스트 알림도 없고, 빈번하게 발생하는 가벼운 반응이므로 제한하지 않음
    /// </summary>
    public override bool IsPriorityInteraction => true;

    // 우선순위: 40 (13순위 - 가장 낮음)
    public override int Priority => 40;

    /// <summary>
    /// 기본 위치 이동 비활성화 - 각 패턴에서 자체적으로 위치 조정
    /// </summary>
    public override bool ShouldPerformInitialMovement => false;

    // 유저가 펫을 터치하거나 홀드해서 상호작용이 중단되었는지 추적하는 플래그
    private bool wasInterrupted = false;

    // 현재 진행 중인 패턴 코루틴 추적 (강제 정리용)
    private Coroutine currentPatternCoroutine = null;

    [Header("반응 설정")]
    [Tooltip("반응 지속 시간")]
    public float reactionDuration = 8f;  // 전체 상호작용이 지속되는 시간

    [Header("거리 설정")]
    [Tooltip("펫들이 서로 유지할 기본 거리 (펫 크기에 따라 자동 조정됨)")]
    public float approachDistance = 3f;  // 펫들이 서로 접근할 기본 거리

    [Tooltip("정밀한 도착 판정 거리 (이동 완료 오차 범위)")]
    public float preciseThreshold = 0.6f;  // 목표 위치 도착 판정 거리

    [Tooltip("도망 거리")]
    public float fleeDistance = 10f;  // 수줍은 펫이 도망갈 거리

    [Tooltip("움직임 타임아웃")]
    public float moveTimeout = 5f;  // 이동 명령이 완료되기를 기다리는 최대 시간

    [Header("타이밍 설정")]
    [Tooltip("정적 대기 시간")]
    public float pauseDuration = 1.5f;  // 동작 사이의 일시정지 시간
    
    [Tooltip("쳐다보기 시간")]
    public float lookDuration = 1f;  // 서로를 쳐다보는 지속 시간
    
    [Tooltip("점프 간격")]
    public float jumpInterval = 0.8f;  // 점프 애니메이션 사이 간격
    
    [Tooltip("추격전 지속 시간")]
    public float chaseDuration = 3f;  // 추격전이 지속되는 시간

    // 현재 상호작용 중인 펫 참조 (강제 정리용)
    private PetController activePet1;
    private PetController activePet2;

    /// <summary>
    /// 강제 종료 시 PersonalityReaction 고유 리소스를 정리합니다.
    /// BasePetInteraction.ForceCleanup()에서 호출됩니다.
    /// </summary>
    protected override void OnForceCleanup()
    {
        Debug.Log("[PersonalityReaction] OnForceCleanup 호출됨 - 고유 리소스 정리 시작");

        // 진행 중인 패턴 코루틴 중단
        if (currentPatternCoroutine != null)
        {
            StopCoroutine(currentPatternCoroutine);
            currentPatternCoroutine = null;
        }

        // 플래그 초기화
        wasInterrupted = true;

        // 펫 상태 정리
        CleanupPet(activePet1);
        CleanupPet(activePet2);

        activePet1 = null;
        activePet2 = null;

        Debug.Log("[PersonalityReaction] OnForceCleanup 완료");
    }

    /// <summary>
    /// 개별 펫 정리 헬퍼
    /// </summary>
    private void CleanupPet(PetController pet)
    {
        if (pet == null) return;

        // 애니메이션 정리
        if (pet.animationController != null)
        {
            pet.animationController.StopContinuousAnimation();
        }

        // NavMeshAgent 정리
        if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
        {
            pet.agent.isStopped = false;
            pet.agent.ResetPath();
            pet.agent.speed = pet.baseSpeed;
            pet.agent.updateRotation = true;
        }
    }

    /// <summary>
    /// 템플릿에서 이 인스턴스로 설정값을 복사합니다 (동시 실행 격리)
    /// </summary>
    public void CopySettingsFrom(PersonalityReactionInteraction template)
    {
        if (template == null) return;

        // 반응 설정
        this.reactionDuration = template.reactionDuration;

        // 거리 설정
        this.approachDistance = template.approachDistance;
        this.preciseThreshold = template.preciseThreshold;
        this.fleeDistance = template.fleeDistance;
        this.moveTimeout = template.moveTimeout;

        // 타이밍 설정
        this.pauseDuration = template.pauseDuration;
        this.lookDuration = template.lookDuration;
        this.jumpInterval = template.jumpInterval;
        this.chaseDuration = template.chaseDuration;
    }

    protected override InteractionType DetermineInteractionType()
    {
        // 상호작용 타입을 WalkTogether로 설정 (UI 표시용)
        // 실제로는 성격 조합에 따라 다양한 패턴으로 동작함
        return InteractionType.WalkTogether;
    }

    /// <summary>
    /// PersonalityReaction은 토스트 알림을 표시하지 않음
    /// </summary>
    protected override bool ShouldShowToastNotification()
    {
        return false;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // PersonalityReactionInteraction은 모든 펫 조합에서 가능
        // 이미 상호작용 중이거나 홀딩 중인지는 BasePetInteraction에서 체크함
        // 따라서 여기서는 항상 true를 반환하여 성격 상호작용을 허용
        return true;
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        // 상호작용 시작 로그 출력
        Debug.Log($"<color=green>[PersonalityReaction] ========== 상호작용 시작 ==========</color>");
        Debug.Log($"[PersonalityReaction] 참여 펫: {pet1.petName}({pet1.personality}) & {pet2.petName}({pet2.personality})");

        // 강제 정리용 참조 저장
        activePet1 = pet1;
        activePet2 = pet2;

        // 상호작용이 중단되었는지 추적하는 플래그를 false로 초기화
        wasInterrupted = false;

        // 펫들의 NavMeshAgent가 준비되었는지 확인 (최대 3초 대기)
        // NavMeshAgent가 없거나 비활성화되어 있으면 상호작용 불가능
        Debug.Log($"[PersonalityReaction] NavMeshAgent 준비 확인 중...");
        yield return StartCoroutine(WaitUntilAgentIsReady(pet1, 3f));
        yield return StartCoroutine(WaitUntilAgentIsReady(pet2, 3f));

        if (!pet1.agent.enabled || !pet2.agent.enabled)
        {
            Debug.LogWarning($"[PersonalityReaction] NavMeshAgent 준비 실패! 상호작용 중단");
            EndInteraction(pet1, pet2);
            yield break;
        }
        Debug.Log($"[PersonalityReaction] NavMeshAgent 준비 완료");

        // 상호작용 전 펫들의 원래 상태(속도, 회전속도 등)를 저장
        // 나중에 상호작용이 끝나면 이 상태로 복원함
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);

        try
        {                     
            // 두 펫의 성격을 조합하여 어떤 패턴을 실행할지 결정
            // 예: Lazy + Lazy = "Lazy_Lazy" 패턴
            string combination = GetPersonalityCombination(pet1.personality, pet2.personality);
        Debug.Log($"<color=yellow>[PersonalityReaction] 성격 조합: {combination}</color>");

            // 결정된 성격 조합에 해당하는 반응 패턴 실행
            // 10가지 조합별로 다른 동작 패턴이 있음
        Debug.Log($"[PersonalityReaction] {combination} 패턴 실행 시작");
            yield return StartCoroutine(ExecuteReactionPattern(combination, pet1, pet2));
        Debug.Log($"[PersonalityReaction] {combination} 패턴 실행 완료");
        }
        finally
        {
            // 유저가 펫을 터치하거나 홀드하여 상호작용이 중단되었는지 확인
            // IsHolding: 펫을 잡고 있는 상태, IsSelected: 펫을 선택한 상태
            wasInterrupted = (pet1 != null && (pet1.State.IsHolding || pet1.State.IsSelected)) ||
                           (pet2 != null && (pet2.State.IsHolding || pet2.State.IsSelected));
            
            if (wasInterrupted)
            {
        Debug.Log($"<color=orange>[PersonalityReaction] 유저 입력으로 중단됨</color>");
                ForceCompleteCleanup(pet1, pet2);
            }
            else
            {
        Debug.Log($"[PersonalityReaction] 정상 종료 처리");
                // 정상 종료 시 상태 복원만 수행
                // EndInteraction은 BasePetInteraction.InteractionLifecycle의 finally에서 호출됨
                // AI 재시작도 SafeResumePet에서 처리됨
                if (pet1State != null) pet1State.Restore(pet1);
                if (pet2State != null) pet2State.Restore(pet2);
            }

            // 참조 정리
            activePet1 = null;
            activePet2 = null;
            currentPatternCoroutine = null;

        Debug.Log($"<color=blue>[PersonalityReaction] ========== 상호작용 종료 ==========</color>");
        }
    }

    /// <summary>
    /// 성격 조합 문자열 생성
    /// </summary>
    private string GetPersonalityCombination(PetTraits.Personality p1, PetTraits.Personality p2)
    {
        // 두 성격을 알파벳 순서로 정렬하여 일관된 조합명 생성
        // 예: Lazy + Brave → "Brave_Lazy" (항상 같은 순서)
        // 이렇게 하면 (pet1=Lazy, pet2=Brave)와 (pet1=Brave, pet2=Lazy)가 같은 패턴 사용
        if (p1.ToString().CompareTo(p2.ToString()) <= 0)
            return $"{p1}_{p2}";
        else
            return $"{p2}_{p1}";
    }

    /// <summary>
    /// 성격 조합에 따른 반응 패턴 실행
    /// </summary>
    private IEnumerator ExecuteReactionPattern(string combination, PetController pet1, PetController pet2)
    {
        // 10가지 성격 조합별로 다른 반응 패턴 실행
        // 각 패턴은 해당 성격들의 특성을 반영한 독특한 동작들로 구성됨
        switch (combination)
        {
            case "Lazy_Lazy":
                yield return StartCoroutine(LazyLazyReaction(pet1, pet2));
                break;
            case "Lazy_Shy":
                yield return StartCoroutine(LazyShyReaction(pet1, pet2));
                break;
            case "Brave_Lazy":
                yield return StartCoroutine(LazyBraveReaction(pet1, pet2));
                break;
            case "Lazy_Playful":
                yield return StartCoroutine(LazyPlayfulReaction(pet1, pet2));
                break;
            case "Shy_Shy":
                yield return StartCoroutine(ShyShyReaction(pet1, pet2));
                break;
            case "Brave_Shy":
                yield return StartCoroutine(ShyBraveReaction(pet1, pet2));
                break;
            case "Playful_Shy":
                yield return StartCoroutine(ShyPlayfulReaction(pet1, pet2));
                break;
            case "Brave_Brave":
                yield return StartCoroutine(BraveBraveReaction(pet1, pet2));
                break;
            case "Brave_Playful":
                yield return StartCoroutine(BravePlayfulReaction(pet1, pet2));
                break;
            case "Playful_Playful":
                yield return StartCoroutine(PlayfulPlayfulReaction(pet1, pet2));
                break;
            default:
                // 기본 반응 (간단한 접근 후 헤어짐)
                yield return StartCoroutine(DefaultReaction(pet1, pet2));
                break;
        }
    }

    // ===== 1. Lazy + Lazy: 둘 다 게으른 펫들의 반응 =====
    // 특징: 서로 천천히 접근하다가 함께 누워서 쉬는 패턴 (RestAndSleepTogether 스타일)
    private IEnumerator LazyLazyReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[LazyLazy] Lazy: {pet1.petName}, Lazy: {pet2.petName}");

        // 거리 설정
        float targetDistance = CalculateApproachDistance(pet1, pet2);
        float restDuration = Random.Range(5f, 8f);  // 함께 휴식 시간 (5-8초)
        float separateDistance = targetDistance + 3f;

        // 현재 거리 체크
        float currentDistance = Vector3.Distance(pet1.transform.position, pet2.transform.position);
        Debug.Log($"[LazyLazy] 현재 거리: {currentDistance:F2}m, 목표 거리: {targetDistance:F2}m");

        // 0단계: 목표 거리로 정밀하게 이동
        Debug.Log($"[LazyLazy] 단계0: 목표 거리 {targetDistance:F1}m로 이동");

        Vector3 midpoint = (pet1.transform.position + pet2.transform.position) / 2f;
        Vector3 direction = (pet2.transform.position - pet1.transform.position).normalized;
        if (direction == Vector3.zero) direction = pet1.transform.forward;

        Vector3 targetPosition1 = midpoint - direction * (targetDistance / 2f);
        Vector3 targetPosition2 = midpoint + direction * (targetDistance / 2f);
        targetPosition1 = FindValidPositionOnNavMesh(targetPosition1, 10f);
        targetPosition2 = FindValidPositionOnNavMesh(targetPosition2, 10f);

        // 정밀하게 이동 (게으르게 천천히, 하지만 너무 느리지 않게)
        float originalSpeed1 = pet1.agent.speed;
        float originalSpeed2 = pet2.agent.speed;
        pet1.agent.speed = pet1.baseSpeed * 0.5f;
        pet2.agent.speed = pet2.baseSpeed * 0.5f;

        yield return StartCoroutine(MoveToPositionsPrecise(pet1, pet2, targetPosition1, targetPosition2, 5f));

        pet1.agent.speed = originalSpeed1;
        pet2.agent.speed = originalSpeed2;

        // 1단계: 멈춰서 서로 마주보기
        Debug.Log($"[LazyLazy] 단계1: 멈춰서 서로 마주보기");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 1f));

        // 2단계: Sleep 감정 표시 후 누움 (시차 적용)
        Debug.Log($"[LazyLazy] 단계2: Sleep 감정 표시 및 누움");
        PetController sleepyFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController sleepySecond = sleepyFirst == pet1 ? pet2 : pet1;

        sleepyFirst.ShowEmotion(EmotionType.Sleep, EmotionConstants.DURATION_PERSISTENT);
        yield return new WaitForSeconds(Random.Range(0.5f, 1.0f));  // 시차 증가
        sleepySecond.ShowEmotion(EmotionType.Sleep, EmotionConstants.DURATION_PERSISTENT);
        yield return new WaitForSeconds(0.3f);

        // 시차를 두고 눕기 (랜덤 순서)
        PetController lieFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController lieSecond = lieFirst == pet1 ? pet2 : pet1;

        StartCoroutine(lieFirst.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, restDuration, false, false));
        yield return new WaitForSeconds(Random.Range(0.3f, 0.7f));
        yield return StartCoroutine(lieSecond.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, restDuration, false, false));

        // 3단계: 함께 휴식 (자세 변경 이벤트 포함)
        Debug.Log($"[LazyLazy] 단계3: 함께 휴식 ({restDuration:F1}초)");
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);

        float elapsedTime = 0f;
        float nextEventTime = restDuration * 0.3f;  // 30% 시점부터 시작
        int eventCount = 0;
        const int maxEvents = 2;

        while (elapsedTime < restDuration)
        {
            // 최대 2번의 자세 변경 이벤트 (50% 확률)
            if (eventCount < maxEvents && elapsedTime >= nextEventTime && Random.value < 0.5f)
            {
                eventCount++;
                PetController activePet = Random.value < 0.5f ? pet1 : pet2;
                Debug.Log($"[LazyLazy] {activePet.petName}이 자세를 바꿈 ({eventCount}회)");

                // 앉았다가 다시 눕기
                yield return StartCoroutine(activePet.animationController.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Eat, 1.5f, false, false));
                activePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);

                nextEventTime = elapsedTime + restDuration * 0.25f;  // 다음 이벤트까지 25% 간격
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 4단계: 순차적으로 일어남 (랜덤 순서)
        Debug.Log($"[LazyLazy] 단계4: 순차적으로 일어남");
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();

        PetController wakeFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController wakeSecond = wakeFirst == pet1 ? pet2 : pet1;

        // 첫 번째 펫 일어남
        yield return StartCoroutine(wakeFirst.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1f, false, false));
        wakeFirst.HideEmotion();

        yield return new WaitForSeconds(Random.Range(0.3f, 0.7f));

        // 두 번째 펫도 일어남
        yield return StartCoroutine(wakeSecond.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1f, false, false));
        wakeSecond.HideEmotion();

        // 5단계: 서로 마주보기
        Debug.Log($"[LazyLazy] 단계5: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));
        yield return new WaitForSeconds(0.5f);

        // 6단계: 천천히 각자의 길로 헤어짐
        Debug.Log($"[LazyLazy] 단계6: 천천히 각자의 길로 헤어짐");

        Vector3 pet1Direction = (pet1.transform.position - pet2.transform.position).normalized;
        Vector3 pet2Direction = (pet2.transform.position - pet1.transform.position).normalized;

        Vector3 pet1Destination = pet1.transform.position + pet1Direction * separateDistance;
        Vector3 pet2Destination = pet2.transform.position + pet2Direction * separateDistance;

        pet1Destination = FindValidPositionOnNavMesh(pet1Destination, 10f);
        pet2Destination = FindValidPositionOnNavMesh(pet2Destination, 10f);

        // 시차를 두고 출발 (랜덤 순서)
        PetController leaveFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController leaveSecond = leaveFirst == pet1 ? pet2 : pet1;
        Vector3 firstDest = leaveFirst == pet1 ? pet1Destination : pet2Destination;
        Vector3 secondDest = leaveFirst == pet1 ? pet2Destination : pet1Destination;

        leaveFirst.agent.isStopped = false;
        leaveFirst.agent.speed = leaveFirst.baseSpeed * 0.4f;
        leaveFirst.agent.SetDestination(firstDest);
        leaveFirst.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return new WaitForSeconds(Random.Range(0.3f, 0.6f));

        leaveSecond.agent.isStopped = false;
        leaveSecond.agent.speed = leaveSecond.baseSpeed * 0.4f;
        leaveSecond.agent.SetDestination(secondDest);
        leaveSecond.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return new WaitForSeconds(2f);

        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();

        Debug.Log($"<color=blue>[LazyLazy] 반응 완료</color>");
    }

    // ===== 2. Lazy + Shy =====
    private IEnumerator LazyShyReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        Debug.Log($"[LazyShy] Lazy: {lazyPet.petName}, Shy: {shyPet.petName}");

        // 거리 설정 (인스펙터 값 사용)
        float targetDistance = CalculateApproachDistance(lazyPet, shyPet);
        float retreatDistance = targetDistance + 2f;

        // 0단계: 적절한 거리로 조정 (Shy만 움직임, Lazy는 안 움직임)
        Debug.Log($"[LazyShy] 단계0: 적절한 거리로 조정");
        yield return StartCoroutine(AdjustDistanceOneSided(lazyPet, shyPet, targetDistance, 1.0f, 5f));

        // 1단계: Lazy가 피곤해서 누움
        Debug.Log($"[LazyShy] 단계1: {lazyPet.petName}이 피곤해서 누움");
        yield return StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 1f, false, false));
        
       
        
        // 3단계: Shy가 놀라서 도망
        Debug.Log($"[LazyShy] 단계3: {shyPet.petName}이 놀라서 도망");
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));

        // NavMeshAgent 체크 후 이동
        if (SafeSetNavMeshAgent(shyPet, false)) {
            yield return StartCoroutine(QuickRetreat(shyPet, lazyPet.transform.position, retreatDistance, 1f));

            // 4단계: Shy가 멈춰서 돌아봄
            Debug.Log($"[LazyShy] 단계4: {shyPet.petName}이 멈춰서 돌아봄");
            SafeSetNavMeshAgent(shyPet, true);
        }

        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, lazyPet, 0.3f));

        // 5단계: Shy가 조심스럽게 다시 접근 (Shy→Lazy 방향으로 계산)
        Debug.Log($"[LazyShy] 단계5: {shyPet.petName}이 조심스럽게 다시 접근");
        Vector3 dirToLazy = (lazyPet.transform.position - shyPet.transform.position).normalized;
        Vector3 sniffPoint = lazyPet.transform.position - dirToLazy * 0.8f;
        sniffPoint = FindValidPositionOnNavMesh(sniffPoint, 5f);

        if (SafeSetNavMeshAgent(shyPet, false, shyPet.baseSpeed * 0.3f, sniffPoint)) {
            shyPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            // 도착까지 대기 (최대 3초 타임아웃)
            float startTime = Time.time;
            while (Time.time - startTime < 3f)
            {
                if (shyPet.agent != null && shyPet.agent.enabled && shyPet.agent.isOnNavMesh)
                {
                    if (!shyPet.agent.pathPending && shyPet.agent.remainingDistance <= 0.5f)
                        break;
                }
                yield return null;
            }
            SafeSetNavMeshAgent(shyPet, true);
        }
        shyPet.animationController.StopContinuousAnimation();

        // 6단계: 냄새 맡기 동작
        Debug.Log($"[LazyShy] 단계6: {shyPet.petName}이 냄새를 맡음");
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1f, false, false));

        // 7단계: Shy만 떠남 (Lazy는 누워있음)
        Debug.Log($"[LazyShy] 단계7: {shyPet.petName}이 떠남, {lazyPet.petName}은 계속 누워있음");
        Vector3 shyDirection = (shyPet.transform.position - lazyPet.transform.position).normalized;
        if (shyDirection == Vector3.zero) shyDirection = shyPet.transform.forward;
        Vector3 shyDest = shyPet.transform.position + shyDirection * 4f;
        shyDest = FindValidPositionOnNavMesh(shyDest, 10f);

        shyPet.agent.isStopped = false;
        shyPet.agent.speed = shyPet.baseSpeed * 0.6f;
        shyPet.agent.SetDestination(shyDest);
        shyPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return new WaitForSeconds(1.5f);
        shyPet.animationController.StopContinuousAnimation();

        Debug.Log($"<color=blue>[LazyShy] 반응 완료</color>");
    }

    // ===== 3. Lazy + Brave =====
    private IEnumerator LazyBraveReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        Debug.Log($"[LazyBrave] Lazy: {lazyPet.petName}, Brave: {bravePet.petName}");

        // 거리 설정 (인스펙터 값 사용)
        float targetDistance = CalculateApproachDistance(lazyPet, bravePet);
        float circleRadius = CalculateCircleRadius(bravePet, lazyPet);

        // 0단계: 적절한 거리로 조정 (Brave만 움직임, Lazy는 안 움직임)
        Debug.Log($"[LazyBrave] 단계0: 적절한 거리로 조정");
        yield return StartCoroutine(AdjustDistanceOneSided(lazyPet, bravePet, targetDistance, 1.2f, 4f));

        // 1단계: 서로 마주보기
        Debug.Log($"[LazyBrave] 단계1: 서로 마주보기");
        bravePet.agent.isStopped = true;
        lazyPet.agent.isStopped = true;
        bravePet.animationController.StopContinuousAnimation();
        lazyPet.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(lazyPet, bravePet, 0.3f));

        // 2단계: Lazy는 무반응으로 누움
        Debug.Log($"[LazyBrave] 단계2: {lazyPet.petName}은 귀찮아서 누움");
        yield return StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 2f, false, false));

        // 공통 단계: Brave가 Lazy 주위를 돔
        Debug.Log($"[LazyBrave] 단계3: {bravePet.petName}이 주위를 돌며 살펴봄");
        float circleDuration = CalculateCircleDuration(circleRadius, bravePet.baseSpeed);
        yield return StartCoroutine(CircleAroundTarget(bravePet, lazyPet, circleRadius, circleDuration));

        // 4단계: Brave가 점프하며 자랑
        Debug.Log($"[LazyBrave] 단계4: {bravePet.petName}이 점프하며 자랑");
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));

        // 5단계: Lazy는 계속 무시
        Debug.Log($"[LazyBrave] 단계5: {lazyPet.petName}은 계속 무시");
        yield return new WaitForSeconds(1f);

        // 6단계: Brave가 흥미를 잃고 떠남
        Debug.Log($"[LazyBrave] 단계6: {bravePet.petName}이 흥미를 잃고 떠남");
        Vector3 leaveDirection = (bravePet.transform.position - lazyPet.transform.position).normalized;
        if (leaveDirection == Vector3.zero) leaveDirection = bravePet.transform.forward;
        Vector3 leaveDestination = bravePet.transform.position + leaveDirection * 5f;
        leaveDestination = FindValidPositionOnNavMesh(leaveDestination, 10f);

        bravePet.agent.isStopped = false;
        bravePet.agent.speed = bravePet.baseSpeed * 0.8f;
        bravePet.agent.SetDestination(leaveDestination);
        bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return new WaitForSeconds(1.5f);
        bravePet.animationController.StopContinuousAnimation();

        Debug.Log($"<color=blue>[LazyBrave] 반응 완료</color>");
    }

    // ===== 4. Lazy + Playful =====
    private IEnumerator LazyPlayfulReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        Debug.Log($"[LazyPlayful] Lazy: {lazyPet.petName}, Playful: {playfulPet.petName}");

        // 거리 설정
        float targetDistance = CalculateApproachDistance(lazyPet, playfulPet);

        // 0단계: 적절한 거리로 조정 (Playful만 움직임, Lazy는 안 움직임)
        Debug.Log($"[LazyPlayful] 단계0: 적절한 거리로 조정");
        yield return StartCoroutine(AdjustDistanceOneSided(lazyPet, playfulPet, targetDistance, 1.5f, 4f));

        // 1단계: 서로 마주보기
        Debug.Log($"[LazyPlayful] 단계1: 서로 마주보기");
        playfulPet.agent.isStopped = true;
        lazyPet.agent.isStopped = true;
        playfulPet.animationController.StopContinuousAnimation();
        lazyPet.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(lazyPet, playfulPet, 0.3f));

        // 2단계: Lazy는 귀찮아서 누움
        Debug.Log($"[LazyPlayful] 단계2: {lazyPet.petName}은 귀찮아서 누움");
        yield return StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 2f, false, false));

        // 3단계: Playful이 연속 점프하며 놀자고 함
        Debug.Log($"[LazyPlayful] 단계3: {playfulPet.petName}이 점프하며 놀자고 함 (3회)");
        for (int i = 0; i < 3; i++)
        {
            yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(Random.Range(0.2f, 0.45f));  // 랜덤 간격
        }

        // 4단계: Playful이 Lazy 주위를 빙빙 돔 (중간에 방향 전환)
        Debug.Log($"[LazyPlayful] 단계4: {playfulPet.petName}이 주위를 돌며 놀자고 함");
        float circleRadius = CalculateCircleRadius(playfulPet, lazyPet);
        float halfDuration = CalculateCircleDuration(circleRadius, playfulPet.baseSpeed) / 2f;

        // 반바퀴 시계방향
        yield return StartCoroutine(CircleAroundTarget(playfulPet, lazyPet, circleRadius, halfDuration, true));
        // 반바퀴 반시계방향
        yield return StartCoroutine(CircleAroundTarget(playfulPet, lazyPet, circleRadius, halfDuration, false));

        // 5단계: Lazy는 계속 무시
        Debug.Log($"[LazyPlayful] 단계5: {lazyPet.petName}은 계속 무시");
        yield return new WaitForSeconds(1f);

        // 6단계: Playful이 포기하고 실망하며 떠남
        Debug.Log($"[LazyPlayful] 단계6: {playfulPet.petName}이 실망하며 떠남");
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1f, false, false));

        Vector3 leaveDirection = (playfulPet.transform.position - lazyPet.transform.position).normalized;
        if (leaveDirection == Vector3.zero) leaveDirection = playfulPet.transform.forward;
        Vector3 leaveDestination = playfulPet.transform.position + leaveDirection * 5f;
        leaveDestination = FindValidPositionOnNavMesh(leaveDestination, 10f);

        playfulPet.agent.isStopped = false;
        playfulPet.agent.speed = playfulPet.baseSpeed * 0.6f;  // 실망해서 느리게
        playfulPet.agent.SetDestination(leaveDestination);
        playfulPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return new WaitForSeconds(1.5f);
        playfulPet.animationController.StopContinuousAnimation();

        Debug.Log($"<color=blue>[LazyPlayful] 반응 완료</color>");
    }

    // ===== 5. Shy + Shy =====
    private IEnumerator ShyShyReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[ShyShy] Shy: {pet1.petName}, Shy: {pet2.petName}");

        // 거리 설정 (펫 크기 고려)
        float baseDistance = CalculateApproachDistance(pet1, pet2);
        float retreatDistance = baseDistance + 2f;
        float finalFleeDistance = baseDistance + 5f;

        // 0단계: 적절한 거리로 조정 (조심스럽게)
        Debug.Log($"[ShyShy] 단계0: 적절한 거리로 조정");
        yield return StartCoroutine(AdjustDistanceSymmetric(pet1, pet2, baseDistance, 0.4f, 5f));

        // 1단계: 서로 발견하고 멈춤
        Debug.Log($"[ShyShy] 단계1: 서로 발견하고 멈춤");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;

        // 2단계: 조심스럽게 서로 쳐다보기
        Debug.Log($"[ShyShy] 단계2: 조심스럽게 서로 쳐다보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));

        // 3단계: 긴 정적 후 놀라는 반응 (다른 동작으로 차별화)
        Debug.Log($"[ShyShy] 단계3: 놀라는 반응");
        PetController reactFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController reactSecond = reactFirst == pet1 ? pet2 : pet1;

        // 먼저 놀란 펫: 점프
        StartCoroutine(reactFirst.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.5f, true, false));
        yield return new WaitForSeconds(Random.Range(0.2f, 0.4f));

        // 나중에 놀란 펫: 웅크렸다가 일어남
        yield return StartCoroutine(reactSecond.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 0.4f, true, false));

        // 4단계: 다른 방식으로 물러남
        Debug.Log($"[ShyShy] 단계4: 다른 방식으로 물러남");
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;

        PetController retreatFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController retreatSecond = retreatFirst == pet1 ? pet2 : pet1;

        // 먼저 물러나는 펫: 빠르게 뛰어서 (Run, 1.5배 속도)
        float originalSpeed1 = retreatFirst.agent.speed;
        retreatFirst.agent.speed = retreatFirst.baseSpeed * 1.5f;
        retreatFirst.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        StartCoroutine(QuickRetreat(retreatFirst, retreatSecond.transform.position, retreatDistance, 0.8f));

        yield return new WaitForSeconds(Random.Range(0.3f, 0.6f));  // 시차

        // 나중에 물러나는 펫: 천천히 걸어서 (Walk, 0.8배 속도)
        float originalSpeed2 = retreatSecond.agent.speed;
        retreatSecond.agent.speed = retreatSecond.baseSpeed * 0.8f;
        retreatSecond.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        yield return StartCoroutine(QuickRetreat(retreatSecond, retreatFirst.transform.position, retreatDistance, 1.2f));

        // 속도 복원 및 애니메이션 정리
        retreatFirst.agent.speed = originalSpeed1;
        retreatSecond.agent.speed = originalSpeed2;
        retreatFirst.animationController.StopContinuousAnimation();
        retreatSecond.animationController.StopContinuousAnimation();
        
        // 5단계: 시차를 두고 돌아봄
        Debug.Log($"[ShyShy] 단계5: 시차를 두고 돌아봄");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;

        // 랜덤으로 누가 먼저 돌아볼지 결정
        PetController lookFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController lookSecond = lookFirst == pet1 ? pet2 : pet1;

        // 먼저 돌아보는 펫 (빠르게)
        yield return StartCoroutine(SmoothlyRotateTowards(lookFirst, lookSecond.transform.position, 0.25f));
        yield return new WaitForSeconds(Random.Range(0.2f, 0.4f));

        // 나중에 돌아보는 펫 (천천히)
        yield return StartCoroutine(SmoothlyRotateTowards(lookSecond, lookFirst.transform.position, 0.4f));
        
        // 6단계: 완전히 반대 방향으로 도망 (랜덤 순서)
        Debug.Log($"[ShyShy] 단계6: 완전히 반대 방향으로 도망");
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;

        // 서로 정반대 방향으로 도망
        Vector3 pet1Direction = (pet1.transform.position - pet2.transform.position).normalized;
        Vector3 pet2Direction = (pet2.transform.position - pet1.transform.position).normalized;

        Vector3 pet1Run = pet1.transform.position + pet1Direction * finalFleeDistance;
        Vector3 pet2Run = pet2.transform.position + pet2Direction * finalFleeDistance;

        pet1Run = FindValidPositionOnNavMesh(pet1Run, 10f);
        pet2Run = FindValidPositionOnNavMesh(pet2Run, 10f);

        // 랜덤 순서로 도망 시작 (속도 차별화)
        PetController fleeFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController fleeSecond = fleeFirst == pet1 ? pet2 : pet1;
        Vector3 fleeFirstDest = fleeFirst == pet1 ? pet1Run : pet2Run;
        Vector3 fleeSecondDest = fleeFirst == pet1 ? pet2Run : pet1Run;

        // 먼저 도망: 빠르게 (2.0배)
        fleeFirst.agent.speed = fleeFirst.baseSpeed * 2.0f;
        fleeFirst.agent.SetDestination(fleeFirstDest);
        fleeFirst.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);

        yield return new WaitForSeconds(Random.Range(0.3f, 0.6f));  // 시차

        // 나중에 도망: 조금 느리게 (1.5배)
        fleeSecond.agent.speed = fleeSecond.baseSpeed * 1.5f;
        fleeSecond.agent.SetDestination(fleeSecondDest);
        fleeSecond.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);

        yield return new WaitForSeconds(2f);

        // 속도 복원
        pet1.agent.speed = pet1.baseSpeed;
        pet2.agent.speed = pet2.baseSpeed;

        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();

        Debug.Log($"<color=blue>[ShyShy] 반응 완료</color>");
    }

    // ===== 6. Shy + Brave =====
    private IEnumerator ShyBraveReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        Debug.Log($"[ShyBrave] Shy: {shyPet.petName}, Brave: {bravePet.petName}");

        // 거리 설정 (인스펙터 값 사용)
        float targetDistance = CalculateApproachDistance(shyPet, bravePet);
        float firstRetreatDistance = 5f;
        float finalFleeDistance = 10f;

        // 0단계: 적절한 거리로 조정 (멀면 Brave 접근, 가까우면 Shy 후퇴)
        Debug.Log($"[ShyBrave] 단계0: 적절한 거리로 조정");
        yield return StartCoroutine(AdjustDistanceAsymmetric(bravePet, shyPet, targetDistance, 1.0f, 1.5f, 4f));

        // 1단계: 서로 마주보기
        Debug.Log($"[ShyBrave] 단계1: 서로 마주보기");
        bravePet.agent.isStopped = true;
        shyPet.agent.isStopped = true;
        bravePet.animationController.StopContinuousAnimation();
        shyPet.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, bravePet, 0.5f));
        
        // 2단계: Shy가 놀라서 첫 번째 도망
        Debug.Log($"[ShyBrave] 단계2: {shyPet.petName}이 놀라서 도망");
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        
        if (SafeSetNavMeshAgent(shyPet, false)) {
            yield return StartCoroutine(QuickRetreat(shyPet, bravePet.transform.position, firstRetreatDistance, 1f));
        }

        // 3단계: Brave가 천천히 따라감
        Debug.Log($"[ShyBrave] 단계3: {bravePet.petName}이 천천히 따라감");
        if (SafeSetNavMeshAgent(bravePet, false, bravePet.baseSpeed * 0.7f, shyPet.transform.position)) {
            bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        }

        yield return new WaitForSeconds(1.5f);

        // 4단계: Shy가 멈춰서 돌아봄
        Debug.Log($"[ShyBrave] 단계4: {shyPet.petName}이 멈춰서 돌아봄");
        SafeSetNavMeshAgent(shyPet, true);
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, bravePet, 0.3f));

        // 5단계: Brave가 점프하며 인사
        Debug.Log($"[ShyBrave] 단계5: {bravePet.petName}이 점프하며 인사");
        SafeSetNavMeshAgent(bravePet, true);
        bravePet.animationController.StopContinuousAnimation();
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        
        // 6단계: Shy가 완전히 도망
        Debug.Log($"[ShyBrave] 단계6: {shyPet.petName}이 완전히 도망");
        Vector3 fleeDirection = (shyPet.transform.position - bravePet.transform.position).normalized;
        Vector3 fleePos = shyPet.transform.position + fleeDirection * finalFleeDistance;
        fleePos = FindValidPositionOnNavMesh(fleePos, 10f);
        
        if (SafeSetNavMeshAgent(shyPet, false, shyPet.baseSpeed * 2f, fleePos)) {
            shyPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        }

        // 7단계: Brave가 잠시 쫓다가 포기
        Debug.Log($"[ShyBrave] 단계7: {bravePet.petName}이 잠시 쫓다가 포기");
        if (SafeSetNavMeshAgent(bravePet, false, bravePet.baseSpeed * 1.5f, shyPet.transform.position)) {
            bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        }

        yield return new WaitForSeconds(1f);

        SafeSetNavMeshAgent(bravePet, true);
        bravePet.animationController.StopContinuousAnimation();
        shyPet.animationController.StopContinuousAnimation();
        
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"<color=blue>[ShyBrave] 반응 완료</color>");
    }

    // ===== 7. Shy + Playful =====
    private IEnumerator ShyPlayfulReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        Debug.Log($"[ShyPlayful] Shy: {shyPet.petName}, Playful: {playfulPet.petName}");

        // 거리 설정 (인스펙터 값 사용)
        float targetDistance = CalculateApproachDistance(shyPet, playfulPet);
        float retreatDistance = 7f;   // 첫 번째 도망 거리
        float secondRetreatDistance = 10f; // 두 번째 도망 거리

        // 0단계: 적절한 거리로 조정 (멀면 Playful 접근, 가까우면 Shy 후퇴)
        Debug.Log($"[ShyPlayful] 단계0: 적절한 거리로 조정");
        yield return StartCoroutine(AdjustDistanceAsymmetric(playfulPet, shyPet, targetDistance, 1.2f, 1.5f, 4f));

        // 1단계: 서로 마주보기
        Debug.Log($"[ShyPlayful] 단계1: 서로 마주보기");
        playfulPet.agent.isStopped = true;
        shyPet.agent.isStopped = true;
        playfulPet.animationController.StopContinuousAnimation();
        shyPet.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, playfulPet, 0.5f));

        // 2단계: Shy가 놀라서 도망 후 돌아보기
        Debug.Log($"[ShyPlayful] 단계2: Shy가 깜짝 놀라서 도망");
        playfulPet.agent.isStopped = true;
        playfulPet.animationController.StopContinuousAnimation();
        
        // Shy 놀람 표현
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        
        // agent가 준비될 시간 제공
        shyPet.agent.isStopped = false;  // 명시적 활성화
        yield return new WaitForSeconds(0.1f);  // 짧은 대기
        
        // Shy 도망
        yield return StartCoroutine(QuickRetreat(shyPet, playfulPet.transform.position, retreatDistance, 1f));
        
        // Shy가 멈춰서 돌아보기
        Debug.Log($"[ShyPlayful] Shy가 멈춰서 Playful을 돌아봄");
        shyPet.agent.isStopped = true;
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, playfulPet, 0.3f));
        
        // 3단계: Playful이 점프하며 놀자고 함
        Debug.Log($"[ShyPlayful] 단계3: Playful이 점프하며 놀자고 신호");
        playfulPet.agent.isStopped = false;
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 1f, true, false));
        
        // 4단계: Shy가 다시 놀라서 더 멀리 도망
        Debug.Log($"[ShyPlayful] 단계4: Shy가 다시 놀라서 더 멀리 도망");
        shyPet.agent.isStopped = false;
        yield return StartCoroutine(QuickRetreat(shyPet, playfulPet.transform.position, secondRetreatDistance, 1.5f));
        
        // 5단계: Playful이 실망하며 고개 숙임
        Debug.Log($"[ShyPlayful] 단계5: Playful이 실망하며 고개 숙임");
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 2f, false, false));
        
        Debug.Log($"<color=blue>[ShyPlayful] 반응 완료</color>");
    }

    // ===== 8. Brave + Brave =====
    private IEnumerator BraveBraveReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[BraveBrave] Brave: {pet1.petName}, Brave: {pet2.petName}");

        // 거리 설정 (펫 크기 고려)
        float meetDistance = CalculateApproachDistance(pet1, pet2);
        float circleRadius = CalculateCircleRadius(pet1, pet2);
        float raceDistance = meetDistance + 5f;

        // meetPoint를 미리 초기화
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        meetPoint = FindValidPositionOnNavMesh(meetPoint, 10f);

        // 0단계: 적절한 거리로 조정 (빠르게)
        Debug.Log($"[BraveBrave] 단계0: 적절한 거리로 조정");
        yield return StartCoroutine(AdjustDistanceSymmetric(pet1, pet2, meetDistance, 1.2f, 4f));

        // 1단계: 정면 대치 (마주보기)
        Debug.Log($"[BraveBrave] 단계1: 정면 대치");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));

        // 경계하며 점프 (랜덤 순서)
        Debug.Log($"[BraveBrave] 둘 다 경계하며 점프!");
        PetController alertFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController alertSecond = alertFirst == pet1 ? pet2 : pet1;

        StartCoroutine(alertFirst.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.6f, true, false));
        yield return new WaitForSeconds(Random.Range(0.2f, 0.4f));  // 랜덤 시차
        yield return StartCoroutine(alertSecond.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.6f, true, false));

        yield return new WaitForSeconds(0.5f);

        // 공통 단계: 서로 주위를 돔 (위엄 과시)
        Debug.Log($"[BraveBrave] 단계2: 서로 주위를 돌며 위엄 과시");
        float circleDuration = CalculateCircleDuration(circleRadius, (pet1.baseSpeed + pet2.baseSpeed) / 2f);
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, circleRadius, circleDuration));

        // 3단계: 점프하며 자랑 (랜덤 순서)
        Debug.Log($"[BraveBrave] 단계3: 점프하며 자랑");
        PetController boastFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController boastSecond = boastFirst == pet1 ? pet2 : pet1;

        StartCoroutine(boastFirst.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        yield return new WaitForSeconds(Random.Range(0.15f, 0.35f));  // 랜덤 시차
        yield return StartCoroutine(boastSecond.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));

        // 4단계: 짧은 달리기 시합
        Debug.Log($"[BraveBrave] 단계4: 짧은 달리기 시합");
        Vector3 raceDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        Vector3 raceTarget = meetPoint + raceDirection * raceDistance;
        raceTarget = FindValidPositionOnNavMesh(raceTarget, 10f);

        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.speed = pet1.baseSpeed * 2f;
        pet2.agent.speed = pet2.baseSpeed * 2f;
        pet1.agent.SetDestination(raceTarget);
        pet2.agent.SetDestination(raceTarget);
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);

        yield return new WaitForSeconds(2f);

        // 5단계: 서로 인정하고 헤어짐
        Debug.Log($"[BraveBrave] 단계5: 서로 인정하고 헤어짐");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();

        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));

        // 6단계: 각자 당당히 떠남 (랜덤 순서)
        Debug.Log($"[BraveBrave] 단계6: 각자 당당히 떠남");
        Vector3 pet1Direction = (pet1.transform.position - pet2.transform.position).normalized;
        if (pet1Direction == Vector3.zero) pet1Direction = pet1.transform.forward;
        Vector3 pet2Direction = -pet1Direction;

        Vector3 pet1Dest = pet1.transform.position + pet1Direction * 5f;
        Vector3 pet2Dest = pet2.transform.position + pet2Direction * 5f;
        pet1Dest = FindValidPositionOnNavMesh(pet1Dest, 10f);
        pet2Dest = FindValidPositionOnNavMesh(pet2Dest, 10f);

        // 랜덤 순서로 떠남
        PetController leaveFirst = Random.value < 0.5f ? pet1 : pet2;
        PetController leaveSecond = leaveFirst == pet1 ? pet2 : pet1;
        Vector3 leaveFirstDest = leaveFirst == pet1 ? pet1Dest : pet2Dest;
        Vector3 leaveSecondDest = leaveFirst == pet1 ? pet2Dest : pet1Dest;

        leaveFirst.agent.isStopped = false;
        leaveFirst.agent.speed = leaveFirst.baseSpeed;  // 당당하게 기본 속도
        leaveFirst.agent.SetDestination(leaveFirstDest);
        leaveFirst.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return new WaitForSeconds(Random.Range(0.3f, 0.6f));  // 랜덤 시차

        leaveSecond.agent.isStopped = false;
        leaveSecond.agent.speed = leaveSecond.baseSpeed;
        leaveSecond.agent.SetDestination(leaveSecondDest);
        leaveSecond.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return new WaitForSeconds(1.5f);
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();

        Debug.Log($"<color=blue>[BraveBrave] 반응 완료</color>");
    }

    // ===== 9. Brave + Playful =====
    private IEnumerator BravePlayfulReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        Debug.Log($"[BravePlayful] Brave: {bravePet.petName}, Playful: {playfulPet.petName}");

        // 거리 설정 (펫 크기 고려)
        float meetDistance = CalculateApproachDistance(bravePet, playfulPet);
        float circleRadius = CalculateCircleRadius(bravePet, playfulPet);
        float chaseDistance = meetDistance + 3f;

        // 0단계: 적절한 거리로 조정 (둘 다 빠르게)
        Debug.Log($"[BravePlayful] 단계0: 적절한 거리로 조정");
        yield return StartCoroutine(AdjustDistanceSymmetric(bravePet, playfulPet, meetDistance, 1.5f, 4f));

        // 1단계: 서로 마주보기
        Debug.Log($"[BravePlayful] 단계1: 서로 마주보기");
        bravePet.agent.isStopped = true;
        playfulPet.agent.isStopped = true;
        bravePet.animationController.StopContinuousAnimation();
        playfulPet.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(bravePet, playfulPet, 0.3f));

        // 2단계: 서로 주위를 빙빙 돔
        Debug.Log($"[BravePlayful] 단계2: 서로 주위를 빙빙 돔");
        float circleDuration = CalculateCircleDuration(circleRadius, (bravePet.baseSpeed + playfulPet.baseSpeed) / 2f);
        yield return StartCoroutine(CircleAroundEachOther(bravePet, playfulPet, circleRadius, circleDuration));

        // 3단계: Playful이 먼저 점프
        Debug.Log($"[BravePlayful] 단계3: {playfulPet.petName}이 신나서 점프");
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.6f, true, false));

        // 4단계: Brave도 따라서 점프
        Debug.Log($"[BravePlayful] 단계4: {bravePet.petName}도 따라서 점프");
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.6f, true, false));

        // 공통 단계: 추격전 시작
        Debug.Log($"[BravePlayful] 단계5: 짧은 추격전 시작");
        Vector3 chaseDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        Vector3 chaseTarget = playfulPet.transform.position + chaseDirection * chaseDistance;
        chaseTarget = FindValidPositionOnNavMesh(chaseTarget, 10f);
        
        playfulPet.agent.isStopped = false;
        playfulPet.agent.speed = playfulPet.baseSpeed * 2f;
        playfulPet.agent.SetDestination(chaseTarget);
        playfulPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        yield return new WaitForSeconds(0.3f);
        
        bravePet.agent.isStopped = false;
        bravePet.agent.speed = bravePet.baseSpeed * 2f;
        bravePet.agent.SetDestination(playfulPet.transform.position);
        bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        yield return new WaitForSeconds(chaseDuration);
        
        // 6단계: 역할 바꿔서 추격
        Debug.Log($"[BravePlayful] 단계6: 역할 바꿔서 추격");
        Vector3 reverseChaseTarget = bravePet.transform.position - chaseDirection * chaseDistance;
        reverseChaseTarget = FindValidPositionOnNavMesh(reverseChaseTarget, 10f);
        
        bravePet.agent.SetDestination(reverseChaseTarget);
        yield return new WaitForSeconds(0.3f);
        playfulPet.agent.SetDestination(bravePet.transform.position);
        
        yield return new WaitForSeconds(1.5f);
        
        // 7단계: 만족하고 헤어짐
        Debug.Log($"[BravePlayful] 단계7: 만족하고 헤어짐");
        bravePet.animationController.StopContinuousAnimation();
        playfulPet.animationController.StopContinuousAnimation();

        // 각자 방향으로 자연스럽게 헤어짐
        Vector3 braveDirection = (bravePet.transform.position - playfulPet.transform.position).normalized;
        if (braveDirection == Vector3.zero) braveDirection = bravePet.transform.forward;
        Vector3 playfulDirection = -braveDirection;

        Vector3 braveDest = bravePet.transform.position + braveDirection * 5f;
        Vector3 playfulDest = playfulPet.transform.position + playfulDirection * 5f;
        braveDest = FindValidPositionOnNavMesh(braveDest, 10f);
        playfulDest = FindValidPositionOnNavMesh(playfulDest, 10f);

        bravePet.agent.isStopped = false;
        playfulPet.agent.isStopped = false;
        bravePet.agent.speed = bravePet.baseSpeed * 0.8f;
        playfulPet.agent.speed = playfulPet.baseSpeed * 0.8f;
        bravePet.agent.SetDestination(braveDest);
        playfulPet.agent.SetDestination(playfulDest);
        bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        playfulPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return new WaitForSeconds(1.5f);
        bravePet.animationController.StopContinuousAnimation();
        playfulPet.animationController.StopContinuousAnimation();

        Debug.Log($"<color=blue>[BravePlayful] 반응 완료</color>");
    }

    // ===== 10. Playful + Playful =====
    private IEnumerator PlayfulPlayfulReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[PlayfulPlayful] Playful: {pet1.petName}, Playful: {pet2.petName}");

        // 거리 설정 (펫 크기 고려)
        float meetDistance = CalculateApproachDistance(pet1, pet2);
        float circleRadius = CalculateCircleRadius(pet1, pet2);
        float chaseDistance = meetDistance + 8f;  // 추격 거리 증가

        // 0단계: 적절한 거리로 조정 (신나게)
        Debug.Log($"[PlayfulPlayful] 단계0: 적절한 거리로 조정");
        yield return StartCoroutine(AdjustDistanceSymmetric(pet1, pet2, meetDistance, 1.5f, 4f));

        // 1단계: 서로 마주보기
        Debug.Log($"[PlayfulPlayful] 단계1: 서로 마주보고 흥분");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.3f));

        // 2단계: 서로 주위를 빠르게 돔
        Debug.Log($"[PlayfulPlayful] 단계2: 서로 주위를 빠르게 돔");
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, circleRadius, 1.5f));

        // 3단계: 점프 파티 (3회, 랜덤 순서)
        Debug.Log($"[PlayfulPlayful] 단계3: 신나게 점프 파티 (3회)");
        for (int i = 0; i < 3; i++)
        {
            PetController jumpFirst = Random.value < 0.5f ? pet1 : pet2;
            PetController jumpSecond = jumpFirst == pet1 ? pet2 : pet1;

            StartCoroutine(jumpFirst.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(Random.Range(0.1f, 0.25f));  // 랜덤 시차
            yield return StartCoroutine(jumpSecond.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(Random.Range(0.1f, 0.35f));  // 랜덤 간격
        }

        // 4단계: 추격전 시작 (역할 랜덤, 실시간 추적)
        PetController runner = Random.value < 0.5f ? pet1 : pet2;
        PetController chaser = runner == pet1 ? pet2 : pet1;
        Debug.Log($"[PlayfulPlayful] 단계4: {runner.petName}이 도망가며 추격전 시작");

        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        Vector3 chaseTarget1 = runner.transform.position + randomDir * chaseDistance;
        chaseTarget1 = FindValidPositionOnNavMesh(chaseTarget1, 10f);

        runner.agent.isStopped = false;
        runner.agent.speed = runner.baseSpeed * 2f;
        runner.agent.SetDestination(chaseTarget1);
        runner.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);

        yield return new WaitForSeconds(Random.Range(0.15f, 0.3f));  // 랜덤 시차

        chaser.agent.isStopped = false;
        chaser.agent.speed = chaser.baseSpeed * 2.2f;  // 약간 빠르게 (따라잡기 위해)
        chaser.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);

        // 실시간 추적 (3초간)
        float chaseStartTime = Time.time;
        float chaseDuration = 3f;
        while (Time.time - chaseStartTime < chaseDuration)
        {
            chaser.agent.SetDestination(runner.transform.position);
            yield return new WaitForSeconds(0.2f);  // 0.2초마다 목적지 갱신
        }

        // 5단계: 역할 바꿔서 추격 (실시간 추적)
        Debug.Log($"[PlayfulPlayful] 단계5: 역할 바꿔서 {chaser.petName}이 도망");
        Vector3 chaseTarget2 = chaser.transform.position - randomDir * chaseDistance;
        chaseTarget2 = FindValidPositionOnNavMesh(chaseTarget2, 10f);

        // 역할 교체: chaser가 runner가 되고, runner가 chaser가 됨
        chaser.agent.speed = chaser.baseSpeed * 2f;  // 도망가는 속도
        chaser.agent.SetDestination(chaseTarget2);

        yield return new WaitForSeconds(Random.Range(0.15f, 0.3f));  // 랜덤 시차

        runner.agent.speed = runner.baseSpeed * 2.2f;  // 쫓는 속도

        // 실시간 추적 (3초간)
        chaseStartTime = Time.time;
        while (Time.time - chaseStartTime < chaseDuration)
        {
            runner.agent.SetDestination(chaser.transform.position);
            yield return new WaitForSeconds(0.2f);
        }
        
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        
        // 6단계: 다시 모여서 점프 파티 (2회, 랜덤 순서)
        Debug.Log($"[PlayfulPlayful] 단계6: 다시 모여서 점프 파티 (2회)");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.3f));

        for (int i = 0; i < 2; i++)
        {
            PetController jumpFirst = Random.value < 0.5f ? pet1 : pet2;
            PetController jumpSecond = jumpFirst == pet1 ? pet2 : pet1;

            StartCoroutine(jumpFirst.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(Random.Range(0.1f, 0.25f));  // 랜덤 시차
            yield return StartCoroutine(jumpSecond.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(Random.Range(0.1f, 0.35f));  // 랜덤 간격
        }
        
        // 7단계: 마지막으로 서로 주위를 돌다가 헤어짐
        Debug.Log($"[PlayfulPlayful] 단계7: 마지막으로 한 바퀴 돌고 헤어짐");
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, circleRadius, 1f));

        // 8단계: 자연스럽게 분리
        Debug.Log($"[PlayfulPlayful] 단계8: 자연스럽게 헤어짐");
        Vector3 pet1Direction = (pet1.transform.position - pet2.transform.position).normalized;
        if (pet1Direction == Vector3.zero) pet1Direction = pet1.transform.forward;
        Vector3 pet2Direction = -pet1Direction;

        Vector3 pet1Dest = pet1.transform.position + pet1Direction * 5f;
        Vector3 pet2Dest = pet2.transform.position + pet2Direction * 5f;
        pet1Dest = FindValidPositionOnNavMesh(pet1Dest, 10f);
        pet2Dest = FindValidPositionOnNavMesh(pet2Dest, 10f);

        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.speed = pet1.baseSpeed * 0.8f;
        pet2.agent.speed = pet2.baseSpeed * 0.8f;
        pet1.agent.SetDestination(pet1Dest);
        pet2.agent.SetDestination(pet2Dest);
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return new WaitForSeconds(1.5f);
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();

        Debug.Log($"<color=blue>[PlayfulPlayful] 반응 완료</color>");
    }

    // ===== 헬퍼 메서드들 =====

    /// <summary>
    /// NavMeshAgent를 안전하게 설정하는 헬퍼 메서드
    /// </summary>
    private bool SafeSetNavMeshAgent(PetController pet, bool isStopped = false, float? speed = null, Vector3? destination = null)
    {
        if (pet == null || pet.agent == null || !pet.agent.enabled || !pet.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[SafeSetNavMeshAgent] {pet?.petName ?? "null"}의 NavMeshAgent가 유효하지 않음");
            return false;
        }

        pet.agent.isStopped = isStopped;

        if (speed.HasValue)
            pet.agent.speed = speed.Value;

        if (destination.HasValue && !isStopped)
            pet.agent.SetDestination(destination.Value);

        return true;
    }

    /// <summary>
    /// 두 펫이 서로를 부드럽게 바라보도록 회전 (Walk 애니메이션 없이)
    /// </summary>
    protected new IEnumerator SmoothlyLookAtEachOther(PetController pet1, PetController pet2, float duration = 0.5f)
    {
        if (pet1 == null || pet2 == null) yield break;

        // 목표 회전값 계산 (Y축만 회전)
        Vector3 direction1 = pet2.transform.position - pet1.transform.position;
        direction1.y = 0; // Y축 회전만
        Quaternion pet1TargetRotation = Quaternion.LookRotation(direction1);
        
        Vector3 direction2 = pet1.transform.position - pet2.transform.position;
        direction2.y = 0; // Y축 회전만
        Quaternion pet2TargetRotation = Quaternion.LookRotation(direction2);

        // 현재 회전값 저장
        Quaternion pet1StartRotation = pet1.transform.rotation;
        Quaternion pet2StartRotation = pet2.transform.rotation;
        
        // Walk 애니메이션 없이 순수 회전만 처리
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            // EaseInOut 커브 적용으로 더 부드러운 회전
            float smoothT = Mathf.SmoothStep(0, 1, t);
            
            // Slerp를 사용하여 부드럽게 회전
            pet1.transform.rotation = Quaternion.Slerp(pet1StartRotation, pet1TargetRotation, smoothT);
            pet2.transform.rotation = Quaternion.Slerp(pet2StartRotation, pet2TargetRotation, smoothT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 회전값으로 정확하게 설정
        pet1.transform.rotation = pet1TargetRotation;
        pet2.transform.rotation = pet2TargetRotation;
    }

    /// <summary>
    /// 한 펫만 특정 위치를 향해 부드럽게 회전
    /// </summary>
    private IEnumerator SmoothlyRotateTowards(PetController pet, Vector3 targetPosition, float duration = 0.3f)
    {
        if (pet == null) yield break;

        Vector3 direction = targetPosition - pet.transform.position;
        direction.y = 0;
        if (direction == Vector3.zero) yield break;

        Quaternion startRotation = pet.transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            pet.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        pet.transform.rotation = targetRotation;
    }

    /// <summary>
    /// 두 펫 모두 목적지에 도착할 때까지 대기
    /// </summary>
    private IEnumerator WaitForBothToArrive(PetController pet1, PetController pet2, float timeout)
    {
        float startTime = Time.time;
        float stoppingDistance = 0.3f;

        while (Time.time - startTime < timeout)
        {
            bool pet1Arrived = pet1.agent == null || !pet1.agent.enabled || !pet1.agent.isOnNavMesh ||
                              (!pet1.agent.pathPending && pet1.agent.remainingDistance <= stoppingDistance);
            bool pet2Arrived = pet2.agent == null || !pet2.agent.enabled || !pet2.agent.isOnNavMesh ||
                              (!pet2.agent.pathPending && pet2.agent.remainingDistance <= stoppingDistance);

            if (pet1Arrived && pet2Arrived)
                break;

            yield return null;
        }
    }

    /// <summary>
    /// 기본 반응 (조합이 없을 때)
    /// </summary>
    private IEnumerator DefaultReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[DefaultReaction] 기본 반응 실행");
        
        Debug.Log($"[DefaultReaction] 단계1: 간단히 접근");
        // 간단히 접근 후 헤어짐
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        
        yield return new WaitForSeconds(2f);
        
        Debug.Log($"[DefaultReaction] 단계2: 서로 쳐다보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, lookDuration));
        
        yield return new WaitForSeconds(1f);
        
        Debug.Log($"<color=blue>[DefaultReaction] 반응 완료</color>");
    }

    /// <summary>
    /// 타겟 주위를 도는 동작
    /// </summary>
    /// <param name="clockwise">true면 시계방향, false면 반시계방향</param>
    private IEnumerator CircleAroundTarget(PetController circler, PetController target, float radius, float duration, bool clockwise = true)
    {
        // NavMeshAgent 체크
        if (circler.agent == null || !circler.agent.enabled || !circler.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[CircleAroundTarget] {circler.petName}의 NavMeshAgent가 준비되지 않음");
            yield break;
        }

        string directionText = clockwise ? "시계방향" : "반시계방향";
        Debug.Log($"[CircleAroundTarget] {circler.petName}이(가) {target.petName} 주위를 {duration}초 동안 {directionText}으로 돌기 시작");

        // 원래 속도 저장
        float originalSpeed = circler.agent.speed;

        // 원의 둘레와 필요한 속도 계산
        float circumference = 2f * Mathf.PI * radius;
        float requiredSpeed = circumference / duration;

        // Agent 설정
        circler.agent.isStopped = false;
        circler.agent.speed = requiredSpeed;
        circler.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        float elapsed = 0f;

        // 시작 위치에서 타겟까지의 방향을 기준으로 시작 각도 계산
        Vector3 startDirection = (circler.transform.position - target.transform.position).normalized;
        startDirection.y = 0;
        if (startDirection == Vector3.zero) startDirection = Vector3.forward;

        while (elapsed < duration)
        {
            // 진행도 계산 (0.0 ~ 1.0)
            float progress = elapsed / duration;
            // 시계방향이면 양수, 반시계방향이면 음수
            float angle = clockwise ? (progress * 360f) : (-progress * 360f);

            // 원 위의 위치 계산
            Vector3 offset = Quaternion.Euler(0, angle, 0) * startDirection * radius;
            Vector3 targetPos = target.transform.position + offset;
            targetPos = FindValidPositionOnNavMesh(targetPos, radius + 2f);

            // 이동
            circler.agent.SetDestination(targetPos);

            // 타겟을 계속 바라보기
            Vector3 lookDirection = (target.transform.position - circler.transform.position).normalized;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                circler.transform.rotation = Quaternion.Slerp(
                    circler.transform.rotation,
                    targetRotation,
                    Time.deltaTime * 5f
                );
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 정리
        circler.agent.isStopped = true;
        circler.animationController.StopContinuousAnimation();
        circler.agent.speed = originalSpeed;

        Debug.Log($"[CircleAroundTarget] {circler.petName} 원 운동 완료");
    }

    /// <summary>
    /// 서로 주위를 도는 동작 (원의 반대편에서 시작)
    /// </summary>
    private IEnumerator CircleAroundEachOther(PetController pet1, PetController pet2, float radius, float duration)
    {
        // NavMeshAgent 체크
        if (pet1.agent == null || !pet1.agent.enabled || !pet1.agent.isOnNavMesh ||
            pet2.agent == null || !pet2.agent.enabled || !pet2.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[CircleAroundEachOther] NavMeshAgent가 준비되지 않음");
            yield break;
        }

        Debug.Log($"[CircleAroundEachOther] {pet1.petName}과 {pet2.petName}이 서로 돌기 시작 ({duration}초)");

        // 중심점 계산 (고정)
        Vector3 center = (pet1.transform.position + pet2.transform.position) / 2f;

        // 원래 속도 저장
        float originalSpeed1 = pet1.agent.speed;
        float originalSpeed2 = pet2.agent.speed;

        // 원의 둘레와 필요한 속도 계산
        float circumference = 2f * Mathf.PI * radius;
        float requiredSpeed = circumference / duration;

        // Agent 설정
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.speed = requiredSpeed;
        pet2.agent.speed = requiredSpeed;
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        float elapsed = 0f;

        // 시작 방향 계산 (pet1의 현재 위치 기준)
        Vector3 startDirection = (pet1.transform.position - center).normalized;
        startDirection.y = 0;
        if (startDirection == Vector3.zero) startDirection = Vector3.forward;

        while (elapsed < duration)
        {
            // 진행도 계산
            float progress = elapsed / duration;
            float angle = progress * 360f;

            // 원 위의 위치 계산 (서로 반대편)
            Vector3 offset1 = Quaternion.Euler(0, angle, 0) * startDirection * radius;
            Vector3 offset2 = Quaternion.Euler(0, angle + 180f, 0) * startDirection * radius;

            Vector3 targetPos1 = center + offset1;
            Vector3 targetPos2 = center + offset2;

            targetPos1 = FindValidPositionOnNavMesh(targetPos1, radius + 2f);
            targetPos2 = FindValidPositionOnNavMesh(targetPos2, radius + 2f);

            // 이동
            pet1.agent.SetDestination(targetPos1);
            pet2.agent.SetDestination(targetPos2);

            // 서로를 바라보기
            Vector3 lookDir1 = (pet2.transform.position - pet1.transform.position).normalized;
            lookDir1.y = 0;
            if (lookDir1 != Vector3.zero)
            {
                Quaternion rotation1 = Quaternion.LookRotation(lookDir1);
                pet1.transform.rotation = Quaternion.Slerp(pet1.transform.rotation, rotation1, Time.deltaTime * 5f);
            }

            Vector3 lookDir2 = (pet1.transform.position - pet2.transform.position).normalized;
            lookDir2.y = 0;
            if (lookDir2 != Vector3.zero)
            {
                Quaternion rotation2 = Quaternion.LookRotation(lookDir2);
                pet2.transform.rotation = Quaternion.Slerp(pet2.transform.rotation, rotation2, Time.deltaTime * 5f);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 정리
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        pet1.agent.speed = originalSpeed1;
        pet2.agent.speed = originalSpeed2;

        Debug.Log($"[CircleAroundEachOther] 서로 돌기 완료");
    }

    /// <summary>
    /// 펫 크기에 따른 거리 계산
    /// </summary>
    private float CalculateDistanceBySize(PetController pet1, PetController pet2)
    {
        float multiplier1 = pet1.Profile.GetInteractionDistanceMultiplier();
        float multiplier2 = pet2.Profile.GetInteractionDistanceMultiplier();
        float avgMultiplier = (multiplier1 + multiplier2) / 2f;
        
        return approachDistance * avgMultiplier;
    }
    
    /// <summary>
    /// 강제 중단 시 완전한 정리를 수행하는 메서드
    /// </summary>
    private void ForceCompleteCleanup(PetController pet1, PetController pet2)
    {
        Debug.Log($"[ForceCompleteCleanup] 강제 정리 시작");
        
        // 1. 애니메이션 즉시 중단
        if (pet1 != null)
        {
            var animController1 = pet1.GetComponent<PetAnimationController>();
            if (animController1 != null)
            {
                animController1.StopAllCoroutines();
                animController1.StopContinuousAnimation();
                animController1.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            }
        }
        
        if (pet2 != null)
        {
            var animController2 = pet2.GetComponent<PetAnimationController>();
            if (animController2 != null)
            {
                animController2.StopAllCoroutines();
                animController2.StopContinuousAnimation();
                animController2.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            }
        }
        
        // 2. NavMeshAgent 상태 초기화
        if (pet1?.agent != null && pet1.agent.enabled && pet1.agent.isOnNavMesh)
        {
            pet1.agent.isStopped = false;
            pet1.agent.ResetPath();
            pet1.agent.speed = pet1.baseSpeed;
            pet1.agent.acceleration = pet1.baseAcceleration;
            pet1.agent.angularSpeed = 120f;
            pet1.agent.updateRotation = true;
        }
        
        if (pet2?.agent != null && pet2.agent.enabled && pet2.agent.isOnNavMesh)
        {
            pet2.agent.isStopped = false;
            pet2.agent.ResetPath();
            pet2.agent.speed = pet2.baseSpeed;
            pet2.agent.acceleration = pet2.baseAcceleration;
            pet2.agent.angularSpeed = 120f;
            pet2.agent.updateRotation = true;
        }
        
        // 3. 상태 완전 초기화
        if (pet1 != null)
        {
            pet1.State.EndInteraction();
            pet1.State.SetInteractionLogic(null);
            pet1.HideEmotion();
            pet1.ResumeMovement();
        }
        
        if (pet2 != null)
        {
            pet2.State.EndInteraction();
            pet2.State.SetInteractionLogic(null);
            pet2.HideEmotion();
            pet2.ResumeMovement();
        }
        
        // 4. AI 강제 재시작 (약간의 지연 후)
        if (pet1 != null)
        {
            pet1.StartCoroutine(DelayedAIRestart(pet1, 0.1f));
        }
        if (pet2 != null)
        {
            pet2.StartCoroutine(DelayedAIRestart(pet2, 0.1f));
        }
        
        // 5. 상호작용 매니저에 알림
        if (PetInteractionManager.Instance != null)
        {
            PetInteractionManager.Instance.NotifyInteractionEnded(pet1, pet2);
        }
        
        Debug.Log($"[ForceCompleteCleanup] 강제 정리 완료");
    }
    
    /// <summary>
    /// AI 재시작을 지연시키는 코루틴
    /// </summary>
    private IEnumerator DelayedAIRestart(PetController pet, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (pet?.AI != null)
        {
        Debug.Log($"[DelayedAIRestart] {pet.petName}: AI 재시작");
            pet.AI.InterruptAndResetAI();
            
            // AI가 여전히 멈춰있으면 강제로 Wander 활동 시작
            yield return new WaitForSeconds(0.1f);
            if (pet.AI.GetCurrentActivity() == null)
            {
                Debug.LogWarning($"[DelayedAIRestart] {pet.petName}: 활동이 없어서 강제 배회 시작");
                var movementController = pet.GetComponent<PetMovementController>();
                if (movementController != null)
                {
                    movementController.DecideNextBehavior();
                }
            }
        }
    }
    
    /// <summary>
    /// 펫을 목표 위치로 부드럽게 회전 후 이동시키는 헬퍼 메서드
    /// </summary>
    private IEnumerator SmoothMoveToPosition(PetController pet, Vector3 targetPosition, float moveSpeed, PetAnimationController.PetAnimationType animType)
    {
        // 목표 방향 계산
        Vector3 direction = (targetPosition - pet.transform.position).normalized;
        direction.y = 0; // Y축 회전만
        
        // 거리가 너무 가까우면 회전 생략
        if (Vector3.Distance(pet.transform.position, targetPosition) < 0.5f)
        {
            pet.agent.isStopped = false;
            pet.agent.speed = moveSpeed;
            pet.agent.SetDestination(targetPosition);
            pet.animationController.SetContinuousAnimation(animType);
            yield break;
        }
        
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        Quaternion startRotation = pet.transform.rotation;
        
        // 회전 각도 계산
        float rotationAngle = Quaternion.Angle(startRotation, targetRotation);
        
        // 회전 각도가 작으면 즉시 이동
        if (rotationAngle < 10f)
        {
            pet.agent.isStopped = false;
            pet.agent.speed = moveSpeed;
            pet.agent.SetDestination(targetPosition);
            pet.animationController.SetContinuousAnimation(animType);
            yield break;
        }
        
        // 회전 시간 계산 (각도에 비례, 최대 0.5초)
        float rotationTime = Mathf.Min(0.5f, rotationAngle / 180f * 0.5f);
        float elapsed = 0f;
        
        // 1. agent의 자동 회전 비활성화
        pet.agent.updateRotation = false;
        
        // 2. 부드러운 회전
        while (elapsed < rotationTime)
        {
            float t = elapsed / rotationTime;
            float smoothT = Mathf.SmoothStep(0, 1, t); // 더 부드러운 커브
            pet.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // 정확한 회전값 설정
        pet.transform.rotation = targetRotation;
        
        // 3. 회전 완료 후 이동 시작
        pet.agent.updateRotation = true;
        pet.agent.isStopped = false;
        pet.agent.speed = moveSpeed;
        pet.agent.SetDestination(targetPosition);
        pet.animationController.SetContinuousAnimation(animType);
    }
    
    
    /// <summary>
    /// 빠르게 돌아서 도망가기 (뒷걸음질 대체)
    /// </summary>
    private IEnumerator QuickRetreat(PetController pet, Vector3 awayFrom, float distance, float duration = 1f)
    {
        Debug.Log($"[QuickRetreat] {pet.petName}이(가) 돌아서 도망 (거리: {distance}, 시간: {duration})");

        // NavMeshAgent 체크
        if (pet.agent == null || !pet.agent.enabled || !pet.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[QuickRetreat] {pet.petName}의 NavMeshAgent가 유효하지 않음");
            yield break;
        }

        // agent 활성화 확인
        pet.agent.isStopped = false;  // 명시적으로 설정
        
        // 도망갈 방향 계산 (반대 방향)
        Vector3 runDirection = (pet.transform.position - awayFrom).normalized;
        if (runDirection == Vector3.zero) runDirection = -pet.transform.forward;
        runDirection.y = 0; // Y축 회전만
        
        // 도망갈 목표 위치
        Vector3 retreatTarget = pet.transform.position + runDirection * distance;
        retreatTarget = FindValidPositionOnNavMesh(retreatTarget, 10f);
        
        Debug.Log($"[QuickRetreat] 목표 위치: {retreatTarget}, 현재 위치: {pet.transform.position}");
        
        // 원래 속도 저장
        float originalSpeed = pet.agent.speed;
        float originalAngularSpeed = pet.agent.angularSpeed;
        
        // 1. 먼저 부드럽게 회전 (0.2초 동안)
        Quaternion targetRotation = Quaternion.LookRotation(runDirection);
        Quaternion startRotation = pet.transform.rotation;
        float rotationTime = 0.2f;
        float elapsed = 0f;
        
        // 회전 중에는 agent의 자동 회전 비활성화
        pet.agent.updateRotation = false;
        
        while (elapsed < rotationTime)
        {
            float t = elapsed / rotationTime;
            pet.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // 정확한 회전값 설정
        pet.transform.rotation = targetRotation;
        
        // 2. 회전 완료 후 도망 시작
        pet.agent.updateRotation = true;
        pet.agent.angularSpeed = 720f;  // 빠른 회전 속도 (이동 중 경로 변경 시)
        pet.agent.speed = pet.baseSpeed * 1.5f; // 도망 속도
        pet.agent.SetDestination(retreatTarget);
        
        // Run 애니메이션
        pet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        // 남은 시간 동안 도망
        yield return new WaitForSeconds(duration - rotationTime);
        
        // 애니메이션 정리
        pet.animationController.StopContinuousAnimation();
        
        // 속도 및 회전 속도 복원
        pet.agent.speed = originalSpeed;
        pet.agent.angularSpeed = originalAngularSpeed;
    }

    /// <summary>
    /// 펫 크기를 고려한 적절한 접근 거리 계산
    /// CamelAlpacaSpitFightInteraction의 구현 참고
    /// </summary>
    private float CalculateApproachDistance(PetController pet1, PetController pet2)
    {
        // 두 펫의 Collider radius 합산 기반 거리 계산
        float radius1 = GetPetRadius(pet1);
        float radius2 = GetPetRadius(pet2);

        // 기본 거리 + (반지름 합 * 배율)
        // 작은 펫: 0.5, 중간 펫: 1.0, 큰 펫: 1.5
        float sizeMultiplier = 1.2f;  // 적당한 간격 유지
        float adjustedDistance = this.approachDistance + (radius1 + radius2) * sizeMultiplier;

        Debug.Log($"[CalculateDistance] {pet1.petName}({radius1:F1}) + {pet2.petName}({radius2:F1}) = {adjustedDistance:F1}m (기본: {this.approachDistance}m)");
        return adjustedDistance;
    }

    /// <summary>
    /// 펫의 Collider radius 반환
    /// </summary>
    private float GetPetRadius(PetController pet)
    {
        if (pet == null) return 1f;

        var capsuleCollider = pet.GetComponent<CapsuleCollider>();
        if (capsuleCollider != null) return capsuleCollider.radius;

        var sphereCollider = pet.GetComponent<SphereCollider>();
        if (sphereCollider != null) return sphereCollider.radius;

        return 1f; // 기본값
    }

    /// <summary>
    /// 펫 크기를 고려한 적절한 원 운동 반지름 계산
    /// </summary>
    private float CalculateCircleRadius(PetController circler, PetController target)
    {
        float circlerRadius = GetPetRadius(circler);
        float targetRadius = GetPetRadius(target);

        // 기본 간격 (펫들 사이 여유 공간)
        float baseGap = 3f;

        // 최소 반지름 = 타겟 반지름 + 기본 간격 + 회전하는 펫 반지름
        float minRadius = targetRadius + baseGap + circlerRadius;

        Debug.Log($"[CalculateCircleRadius] {circler.petName}({circlerRadius:F1}m) → {target.petName}({targetRadius:F1}m) 원 반지름: {minRadius:F1}m");
        return minRadius;
    }

    /// <summary>
    /// 원을 한 바퀴 도는데 필요한 적절한 시간 계산
    /// </summary>
    private float CalculateCircleDuration(float radius, float baseSpeed = 4f)
    {
        // 원의 둘레 계산
        float circumference = 2f * Mathf.PI * radius;

        // 펫의 평균 속도로 한 바퀴 도는데 걸리는 시간
        float duration = circumference / baseSpeed;

        // 최소 5초, 최대 10초로 제한
        duration = Mathf.Clamp(duration, 5f, 10f);

        Debug.Log($"[CalculateCircleDuration] 반지름 {radius:F1}m, 둘레 {circumference:F1}m → {duration:F1}초");
        return duration;
    }

    /// <summary>
    /// 대칭 조합용: 둘 다 중점으로 이동하여 목표 거리 맞춤
    /// 가까우면 서로 멀어지고, 멀면 서로 가까워짐
    /// </summary>
    private IEnumerator AdjustDistanceSymmetric(
        PetController pet1, PetController pet2,
        float targetDistance, float speedMultiplier, float timeout)
    {
        float currentDistance = Vector3.Distance(pet1.transform.position, pet2.transform.position);
        Debug.Log($"[AdjustDistanceSymmetric] 현재 거리: {currentDistance:F2}m, 목표 거리: {targetDistance:F2}m");

        // 이미 적절한 거리면 스킵
        if (Mathf.Abs(currentDistance - targetDistance) <= preciseThreshold)
        {
            Debug.Log($"[AdjustDistanceSymmetric] 이미 적절한 거리 - 스킵");
            yield break;
        }

        // 중점 계산
        Vector3 midpoint = (pet1.transform.position + pet2.transform.position) / 2f;
        Vector3 direction = (pet2.transform.position - pet1.transform.position).normalized;
        if (direction == Vector3.zero) direction = pet1.transform.forward;

        // 목표 위치 계산
        Vector3 targetPosition1 = midpoint - direction * (targetDistance / 2f);
        Vector3 targetPosition2 = midpoint + direction * (targetDistance / 2f);
        targetPosition1 = FindValidPositionOnNavMesh(targetPosition1, 10f);
        targetPosition2 = FindValidPositionOnNavMesh(targetPosition2, 10f);

        bool isRetreating = currentDistance < targetDistance;

        // 속도 저장
        float originalSpeed1 = pet1.agent.speed;
        float originalSpeed2 = pet2.agent.speed;

        if (isRetreating)
        {
            // 멀어질 때: 점프하면서 동시에 반대 방향으로 뛰기
            PetController first = Random.value < 0.5f ? pet1 : pet2;
            PetController second = first == pet1 ? pet2 : pet1;
            Vector3 firstTarget = first == pet1 ? targetPosition1 : targetPosition2;
            Vector3 secondTarget = first == pet1 ? targetPosition2 : targetPosition1;

            float retreatSpeed = Mathf.Max(speedMultiplier, 1.5f);

            // 첫 번째 펫: 점프하면서 이동 시작
            first.agent.speed = first.baseSpeed * retreatSpeed;
            first.agent.isStopped = false;
            first.agent.SetDestination(firstTarget);
            yield return StartCoroutine(first.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.4f, true, false));

            // 두 번째 펫: 점프하면서 이동 시작
            second.agent.speed = second.baseSpeed * retreatSpeed;
            second.agent.isStopped = false;
            second.agent.SetDestination(secondTarget);
            yield return StartCoroutine(second.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.4f, true, false));

            // 도착 대기
            yield return StartCoroutine(WaitForBothToArrive(pet1, pet2, timeout));
        }
        else
        {
            // 가까워질 때: 기존 방식
            pet1.agent.speed = pet1.baseSpeed * speedMultiplier;
            pet2.agent.speed = pet2.baseSpeed * speedMultiplier;
            yield return StartCoroutine(MoveToPositionsPrecise(pet1, pet2, targetPosition1, targetPosition2, timeout));
        }

        // 애니메이션 정리
        if (isRetreating)
        {
            pet1.animationController.StopContinuousAnimation();
            pet2.animationController.StopContinuousAnimation();
        }

        // 속도 복원
        pet1.agent.speed = originalSpeed1;
        pet2.agent.speed = originalSpeed2;
    }

    /// <summary>
    /// 비대칭 조합용: 멀면 approacher가 접근, 가까우면 retreater가 후퇴
    /// </summary>
    private IEnumerator AdjustDistanceAsymmetric(
        PetController approacher, PetController retreater,
        float targetDistance,
        float approachSpeedMult, float retreatSpeedMult,
        float timeout)
    {
        float currentDistance = Vector3.Distance(approacher.transform.position, retreater.transform.position);
        Debug.Log($"[AdjustDistanceAsymmetric] 현재 거리: {currentDistance:F2}m, 목표 거리: {targetDistance:F2}m");

        // 이미 적절한 거리면 스킵
        if (Mathf.Abs(currentDistance - targetDistance) <= preciseThreshold)
        {
            Debug.Log($"[AdjustDistanceAsymmetric] 이미 적절한 거리 - 스킵");
            yield break;
        }

        PetController mover;
        Vector3 targetPosition;
        float speedMult;
        bool isRetreating = false;

        if (currentDistance > targetDistance)
        {
            // 멀면: approacher가 접근
            mover = approacher;
            speedMult = approachSpeedMult;
            Vector3 direction = (retreater.transform.position - approacher.transform.position).normalized;
            targetPosition = retreater.transform.position - direction * targetDistance;
            Debug.Log($"[AdjustDistanceAsymmetric] {approacher.petName}이 접근");
        }
        else
        {
            // 가까우면: retreater가 깜짝 놀라서 후퇴
            mover = retreater;
            speedMult = retreatSpeedMult;
            isRetreating = true;
            Vector3 direction = (retreater.transform.position - approacher.transform.position).normalized;
            targetPosition = approacher.transform.position + direction * targetDistance;
            Debug.Log($"[AdjustDistanceAsymmetric] {retreater.petName}이 깜짝 놀라서 후퇴");
        }

        targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);

        // 후퇴 시 깜짝 놀라는 점프 애니메이션
        if (isRetreating)
        {
            yield return StartCoroutine(mover.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        }

        // 속도 설정
        float originalSpeed = mover.agent.speed;
        mover.agent.speed = mover.baseSpeed * speedMult;

        // 이동 (후퇴 시 항상 Run 애니메이션)
        mover.agent.isStopped = false;
        mover.agent.stoppingDistance = 0.1f;
        mover.agent.SetDestination(targetPosition);
        mover.animationController.SetContinuousAnimation(
            (speedMult > 1f || isRetreating) ? PetAnimationController.PetAnimationType.Run : PetAnimationController.PetAnimationType.Walk);

        float startTime = Time.time;
        while (Time.time - startTime < timeout)
        {
            if (mover.agent != null && mover.agent.enabled && mover.agent.isOnNavMesh)
            {
                if (!mover.agent.pathPending && mover.agent.remainingDistance <= preciseThreshold)
                {
                    Debug.Log($"[AdjustDistanceAsymmetric] {mover.petName} 도착");
                    break;
                }
            }
            yield return null;
        }

        // 정리
        mover.agent.isStopped = true;
        mover.agent.speed = originalSpeed;
        mover.animationController.StopContinuousAnimation();
    }

    /// <summary>
    /// 한쪽 펫만 움직여서 거리 조절 (Lazy 조합용)
    /// staticPet은 움직이지 않고, moverPet만 접근/후퇴
    /// </summary>
    private IEnumerator AdjustDistanceOneSided(
        PetController staticPet,
        PetController moverPet,
        float targetDistance,
        float speedMult,
        float timeout)
    {
        float currentDistance = Vector3.Distance(staticPet.transform.position, moverPet.transform.position);
        Debug.Log($"[AdjustDistanceOneSided] 현재 거리: {currentDistance:F2}m, 목표 거리: {targetDistance:F2}m");

        // 이미 적절한 거리면 스킵
        if (Mathf.Abs(currentDistance - targetDistance) <= preciseThreshold)
        {
            Debug.Log($"[AdjustDistanceOneSided] 이미 적절한 거리 - 스킵");
            yield break;
        }

        Vector3 direction = (moverPet.transform.position - staticPet.transform.position).normalized;
        Vector3 targetPosition;
        bool isRetreating = false;

        if (currentDistance > targetDistance)
        {
            // 멀면: moverPet이 staticPet에게 접근
            targetPosition = staticPet.transform.position + direction * targetDistance;
            Debug.Log($"[AdjustDistanceOneSided] {moverPet.petName}이 접근");
        }
        else
        {
            // 가까우면: moverPet이 깜짝 놀라서 후퇴
            isRetreating = true;
            targetPosition = staticPet.transform.position + direction * targetDistance;
            Debug.Log($"[AdjustDistanceOneSided] {moverPet.petName}이 깜짝 놀라서 후퇴");
        }

        targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);

        // 후퇴 시 깜짝 놀라는 점프 애니메이션
        if (isRetreating)
        {
            yield return StartCoroutine(moverPet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        }

        // 속도 설정
        float originalSpeed = moverPet.agent.speed;
        moverPet.agent.speed = moverPet.baseSpeed * speedMult;

        // 이동 (후퇴 시 항상 Run 애니메이션)
        moverPet.agent.isStopped = false;
        moverPet.agent.stoppingDistance = 0.1f;
        moverPet.agent.SetDestination(targetPosition);
        moverPet.animationController.SetContinuousAnimation(
            (speedMult > 1f || isRetreating) ? PetAnimationController.PetAnimationType.Run : PetAnimationController.PetAnimationType.Walk);

        float startTime = Time.time;
        while (Time.time - startTime < timeout)
        {
            if (moverPet.agent != null && moverPet.agent.enabled && moverPet.agent.isOnNavMesh)
            {
                if (!moverPet.agent.pathPending && moverPet.agent.remainingDistance <= preciseThreshold)
                {
                    Debug.Log($"[AdjustDistanceOneSided] {moverPet.petName} 도착");
                    break;
                }
            }
            yield return null;
        }

        // 정리
        moverPet.agent.isStopped = true;
        moverPet.agent.speed = originalSpeed;
        moverPet.animationController.StopContinuousAnimation();
    }

    /// <summary>
    /// 두 펫을 목표 위치로 정밀하게 이동
    /// CamelAlpacaSpitFightInteraction의 MoveToPositionsPrecise 참고
    /// </summary>
    private IEnumerator MoveToPositionsPrecise(PetController pet1, PetController pet2,
        Vector3 pos1, Vector3 pos2, float timeout = 15f)
    {
        // NavMeshAgent 준비 확인
        if (pet1.agent == null || !pet1.agent.enabled || !pet1.agent.isOnNavMesh ||
            pet2.agent == null || !pet2.agent.enabled || !pet2.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[MoveToPositionsPrecise] NavMeshAgent가 준비되지 않았습니다.");
            yield break;
        }

        // stoppingDistance를 매우 작게 설정 (정밀한 위치 제어)
        float originalStop1 = pet1.agent.stoppingDistance;
        float originalStop2 = pet2.agent.stoppingDistance;
        pet1.agent.stoppingDistance = 0.1f;
        pet2.agent.stoppingDistance = 0.1f;

        // 목적지 설정
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.SetDestination(pos1);
        pet2.agent.SetDestination(pos2);

        // 걷기 애니메이션
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        float startTime = Time.time;

        while (Time.time - startTime < timeout)
        {
            // NavMeshAgent 상태 확인
            bool pet1Arrived = false;
            bool pet2Arrived = false;

            if (pet1.agent != null && pet1.agent.enabled && pet1.agent.isOnNavMesh)
            {
                // NavMesh 경로 기반 판정 + 실제 위치 거리 판정
                bool navMeshArrived = !pet1.agent.pathPending && pet1.agent.remainingDistance <= this.preciseThreshold;
                float actualDistance = Vector3.Distance(pet1.transform.position, pos1);
                pet1Arrived = navMeshArrived && actualDistance <= this.preciseThreshold;
            }

            if (pet2.agent != null && pet2.agent.enabled && pet2.agent.isOnNavMesh)
            {
                bool navMeshArrived = !pet2.agent.pathPending && pet2.agent.remainingDistance <= this.preciseThreshold;
                float actualDistance = Vector3.Distance(pet2.transform.position, pos2);
                pet2Arrived = navMeshArrived && actualDistance <= this.preciseThreshold;
            }

            if (pet1Arrived && pet2Arrived)
            {
                Debug.Log($"[MoveToPositionsPrecise] 두 펫이 정밀하게 목적지에 도착");
                break;
            }

            // 먼저 도착한 펫은 상대를 기다림
            if (pet1Arrived && !pet2Arrived)
            {
                pet1.agent.isStopped = true;
                pet1.animationController.StopContinuousAnimation();
            }
            if (pet2Arrived && !pet1Arrived)
            {
                pet2.agent.isStopped = true;
                pet2.animationController.StopContinuousAnimation();
            }

            yield return null;
        }

        // stoppingDistance 복원
        pet1.agent.stoppingDistance = originalStop1;
        pet2.agent.stoppingDistance = originalStop2;

        // 이동 정지
        if (pet1.agent != null && pet1.agent.enabled && pet1.agent.isOnNavMesh)
            pet1.agent.isStopped = true;
        if (pet2.agent != null && pet2.agent.enabled && pet2.agent.isOnNavMesh)
            pet2.agent.isStopped = true;

        // 애니메이션 정지
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
    }

}