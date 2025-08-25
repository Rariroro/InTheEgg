using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 성격 조합별 가벼운 반응 패턴을 구현하는 상호작용
/// 짧고 빈번하게 발생하며 감정 표현 없이 동작만으로 표현
/// </summary>
public class PersonalityReactionInteraction : BasePetInteraction
{
    public override string InteractionName => "PersonalityReaction";

    [Header("반응 설정")]
    [Tooltip("반응 지속 시간")]
    public float reactionDuration = 8f;
    
    [Tooltip("접근 거리")]
    public float approachDistance = 3f;
    
    [Tooltip("도망 거리")]
    public float fleeDistance = 10f;
    
    [Tooltip("움직임 타임아웃")]
    public float moveTimeout = 5f;

    [Header("타이밍 설정")]
    [Tooltip("정적 대기 시간")]
    public float pauseDuration = 1.5f;
    
    [Tooltip("쳐다보기 시간")]
    public float lookDuration = 1f;
    
    [Tooltip("점프 간격")]
    public float jumpInterval = 0.8f;
    
    [Tooltip("추격전 지속 시간")]
    public float chaseDuration = 3f;

    protected override InteractionType DetermineInteractionType()
    {
        // 기본 타입 반환 (새로운 타입을 추가하거나 기존 타입 재사용)
        return InteractionType.WalkTogether;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 모든 성격 조합에서 상호작용 가능
        // 단, 다른 조건들은 체크 (이미 상호작용 중, 홀딩 중 등은 상위에서 체크됨)
        return true;
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"<color=green>[PersonalityReaction] ========== 상호작용 시작 ==========</color>");
        Debug.Log($"[PersonalityReaction] 참여 펫: {pet1.petName}({pet1.personality}) & {pet2.petName}({pet2.personality})");
        
        // NavMeshAgent 준비 확인
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

        // 원래 상태 저장
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);

        try
        {
            // 성격 조합 확인
            string combination = GetPersonalityCombination(pet1.personality, pet2.personality);
            Debug.Log($"<color=yellow>[PersonalityReaction] 성격 조합: {combination}</color>");

            // 성격 조합에 따른 반응 실행
            Debug.Log($"[PersonalityReaction] {combination} 패턴 실행 시작");
            yield return StartCoroutine(ExecuteReactionPattern(combination, pet1, pet2));
            Debug.Log($"[PersonalityReaction] {combination} 패턴 실행 완료");
        }
        finally
        {
            // 원래 상태 복원
            pet1State.Restore(pet1);
            pet2State.Restore(pet2);
            
            // 상호작용 종료
            EndInteraction(pet1, pet2);
            
            // 추가 보장: 각 펫의 AI를 명시적으로 재시작
            if (pet1.AI != null)
            {
                Debug.Log($"[PersonalityReaction] {pet1.petName}: AI 강제 재시작");
                pet1.AI.InterruptAndResetAI();
            }
            if (pet2.AI != null)
            {
                Debug.Log($"[PersonalityReaction] {pet2.petName}: AI 강제 재시작");
                pet2.AI.InterruptAndResetAI();
            }
            
            Debug.Log($"<color=blue>[PersonalityReaction] ========== 상호작용 종료 ==========</color>");
        }
    }

    /// <summary>
    /// 성격 조합 문자열 생성
    /// </summary>
    private string GetPersonalityCombination(PetTraits.Personality p1, PetTraits.Personality p2)
    {
        // 알파벳 순서로 정렬하여 일관된 조합 생성
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

    // ===== 1. Lazy + Lazy =====
    private IEnumerator LazyLazyReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[LazyLazy] 단계1: 서로 멈춤");
        // 서로 멈춤
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        
        Debug.Log($"[LazyLazy] 단계2: 서로 쳐다보기 ({lookDuration}초)");
        // 서로 쳐다보기
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, lookDuration));
        
        Debug.Log($"[LazyLazy] 단계3: 천천히 서로 접근");
        // 천천히 서로 다가감
        float distance = CalculateDistanceBySize(pet1, pet2);
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.speed = pet1.baseSpeed * 0.5f; // 천천히
        pet2.agent.speed = pet2.baseSpeed * 0.5f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        Debug.Log($"[LazyLazy] 이동 속도: {pet1.agent.speed}, 목표 거리: {distance}");
        
        yield return new WaitForSeconds(2f);
        
        Debug.Log($"[LazyLazy] 단계4: {pet1.petName} 누움");
        // 한 펫이 누움
        yield return StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 3f, false, false));
        
        Debug.Log($"[LazyLazy] 단계5: {pet2.petName}도 누움");
        // 다른 펫도 누움 (동시에)
        StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 2f, false, false));
        
        yield return new WaitForSeconds(2f);
        
        Debug.Log($"[LazyLazy] 단계6: 일어나서 각자 길 감");
        // 일어나서 각자 길 감 (자동으로 Idle로 복귀)
        Debug.Log($"<color=blue>[LazyLazy] 반응 완료</color>");
    }

    // ===== 2. Lazy + Shy =====
    private IEnumerator LazyShyReaction(PetController pet1, PetController pet2)
    {
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        Debug.Log($"[LazyShy] Lazy: {lazyPet.petName}, Shy: {shyPet.petName}");
        
        Debug.Log($"[LazyShy] 단계1: {lazyPet.petName} 천천히 접근");
        // 천천히 접근
        float distance = CalculateDistanceBySize(pet1, pet2);
        Vector3 approachPoint = shyPet.transform.position + (lazyPet.transform.position - shyPet.transform.position).normalized * distance;
        
        lazyPet.agent.speed = lazyPet.baseSpeed * 0.5f;
        lazyPet.agent.SetDestination(approachPoint);
        
        yield return new WaitForSeconds(1.5f);
        
        Debug.Log($"[LazyShy] 단계2: {lazyPet.petName} 누움");
        // Lazy는 누움
        lazyPet.agent.isStopped = true;
        StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 3f, false, false));
        
        Debug.Log($"[LazyShy] 단계3: {shyPet.petName} 돌아서 도망");
        // Shy는 돌아서 도망
        yield return StartCoroutine(QuickRetreat(shyPet, lazyPet.transform.position, 2f, 1f));
        
        Debug.Log($"[LazyShy] 단계4: {shyPet.petName} 조심스럽게 다시 접근");
        // Shy가 조심스럽게 다시 접근
        shyPet.agent.speed = shyPet.baseSpeed * 0.3f;
        Vector3 sniffPoint = lazyPet.transform.position + lazyPet.transform.forward * 1.5f;
        shyPet.agent.SetDestination(sniffPoint);
        
        yield return new WaitForSeconds(2f);
        
        Debug.Log($"[LazyShy] 단계5: 냄새 맡기");
        // 잠시 멈춤 (냄새 맡기 연출)
        shyPet.agent.isStopped = true;
        yield return new WaitForSeconds(1f);
        
        Debug.Log($"[LazyShy] 단계6: 헤어짐");
        // 헤어짐
        // Lazy는 자동으로 Idle로 복귀
        Debug.Log($"<color=blue>[LazyShy] 반응 완료</color>");
    }

    // ===== 3. Lazy + Brave =====
    private IEnumerator LazyBraveReaction(PetController pet1, PetController pet2)
    {
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        Debug.Log($"[LazyBrave] Lazy: {lazyPet.petName}, Brave: {bravePet.petName}");
        
        Debug.Log($"[LazyBrave] 단계1: Brave가 빠르게 접근");
        // Brave가 빠르게 접근
        float distance = CalculateDistanceBySize(pet1, pet2);
        bravePet.agent.speed = bravePet.baseSpeed * 1.5f;
        bravePet.agent.SetDestination(lazyPet.transform.position);
        
        Debug.Log($"[LazyBrave] 단계2: Lazy는 무반응으로 누움");
        // Lazy는 무반응으로 누움
        lazyPet.agent.isStopped = true;
        StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 4f, false, false));
        
        yield return new WaitForSeconds(2f);
        
        Debug.Log($"[LazyBrave] 단계3: Brave가 주위를 돔");
        // Brave가 주위를 돔
        yield return StartCoroutine(CircleAroundTarget(bravePet, lazyPet, distance, 3f));
        
        Debug.Log($"[LazyBrave] 단계4: Lazy 계속 무시");
        // Lazy 계속 무시
        yield return new WaitForSeconds(1f);
        
        Debug.Log($"[LazyBrave] 단계5: Brave 흥미 잃고 떠남");
        // Brave 흥미 잃고 떠남
        bravePet.agent.speed = bravePet.baseSpeed;
        // Lazy는 자동으로 Idle로 복귀
        Debug.Log($"<color=blue>[LazyBrave] 반응 완료</color>");
    }

    // ===== 4. Lazy + Playful =====
    private IEnumerator LazyPlayfulReaction(PetController pet1, PetController pet2)
    {
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        Debug.Log($"[LazyPlayful] Lazy: {lazyPet.petName}, Playful: {playfulPet.petName}");
        
        Debug.Log($"[LazyPlayful] 단계1: Playful이 신나게 접근");
        // Playful이 신나게 접근
        playfulPet.agent.speed = playfulPet.baseSpeed * 2f;
        playfulPet.agent.SetDestination(lazyPet.transform.position);
        
        yield return new WaitForSeconds(1f);
        
        Debug.Log($"[LazyPlayful] 단계2: Lazy는 누움");
        // Lazy는 누움
        lazyPet.agent.isStopped = true;
        StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 3f, false, false));
        
        Debug.Log($"[LazyPlayful] 단계3: Playful이 점프하며 놀자고 함 (3회)");
        // Playful이 점프하며 놀자고 함
        for (int i = 0; i < 3; i++)
        {
            yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(0.3f);
        }
        
        Debug.Log($"[LazyPlayful] 단계4: Lazy 무시");
        // Lazy 무시
        yield return new WaitForSeconds(1f);
        
        Debug.Log($"[LazyPlayful] 단계5: Playful 포기하고 떠남");
        // Playful 포기하고 떠남
        playfulPet.agent.speed = playfulPet.baseSpeed;
        // Lazy는 자동으로 Idle로 복귀
        Debug.Log($"<color=blue>[LazyPlayful] 반응 완료</color>");
    }

    // ===== 5. Shy + Shy =====
    private IEnumerator ShyShyReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[ShyShy] 단계1: 서로 발견하고 멈춤");
        // 서로 발견하고 멈춤
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        
        Debug.Log($"[ShyShy] 단계2: 서로 쳐다보기");
        // 서로 쳐다보기
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, lookDuration));
        
        Debug.Log($"[ShyShy] 단계3: 긴 정적 ({pauseDuration * 1.5f}초)");
        // 긴 정적
        yield return new WaitForSeconds(pauseDuration * 1.5f);
        
        Debug.Log($"[ShyShy] 단계4: 동시에 돌아서 도망");
        // 동시에 돌아서 도망
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        
        // 두 펫이 동시에 서로 반대 방향으로 도망
        StartCoroutine(QuickRetreat(pet1, pet2.transform.position, 3f, 1f));
        yield return StartCoroutine(QuickRetreat(pet2, pet1.transform.position, 3f, 1f));
        
        Debug.Log($"[ShyShy] 단계5: 서로 다른 방향으로 도망");
        // 서로 다른 방향으로 도망
        Vector3 pet1Run = pet1.transform.position + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * fleeDistance;
        Vector3 pet2Run = pet2.transform.position + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * fleeDistance;
        
        pet1.agent.speed = pet1.baseSpeed * 1.5f;
        pet2.agent.speed = pet2.baseSpeed * 1.5f;
        
        pet1.agent.SetDestination(pet1Run);
        pet2.agent.SetDestination(pet2Run);
        
        yield return new WaitForSeconds(2f);
        Debug.Log($"<color=blue>[ShyShy] 반응 완료</color>");
    }

    // ===== 6. Shy + Brave =====
    private IEnumerator ShyBraveReaction(PetController pet1, PetController pet2)
    {
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        Debug.Log($"[ShyBrave] Shy: {shyPet.petName}, Brave: {bravePet.petName}");
        
        Debug.Log($"[ShyBrave] 단계1: Brave가 당당히 접근");
        // Brave가 당당히 접근
        bravePet.agent.speed = bravePet.baseSpeed * 1.2f;
        bravePet.agent.SetDestination(shyPet.transform.position);
        
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"[ShyBrave] 단계2: Shy는 돌아서 도망");
        // Shy는 돌아서 도망
        yield return StartCoroutine(QuickRetreat(shyPet, bravePet.transform.position, 3f, 1f));
        
        Debug.Log($"[ShyBrave] 단계3: Brave가 천천히 따라감");
        // Brave가 천천히 따라감
        bravePet.agent.speed = bravePet.baseSpeed * 0.7f;
        bravePet.agent.SetDestination(shyPet.transform.position);
        
        yield return new WaitForSeconds(1.5f);
        
        Debug.Log($"[ShyBrave] 단계4: Shy 도망");
        // Shy 도망
        Vector3 fleePos = shyPet.transform.position + (shyPet.transform.position - bravePet.transform.position).normalized * fleeDistance;
        shyPet.agent.speed = shyPet.baseSpeed * 2f;
        shyPet.agent.SetDestination(fleePos);
        
        Debug.Log($"[ShyBrave] 단계5: Brave는 잠시 쫓다가 포기");
        // Brave는 잠시 쫓다가 포기
        yield return new WaitForSeconds(1f);
        bravePet.agent.isStopped = true;
        
        yield return new WaitForSeconds(1f);
        bravePet.agent.isStopped = false;
        Debug.Log($"<color=blue>[ShyBrave] 반응 완료</color>");
    }

    // ===== 7. Shy + Playful =====
    private IEnumerator ShyPlayfulReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        Debug.Log($"[ShyPlayful] Shy: {shyPet.petName}, Playful: {playfulPet.petName}");
        
        // 거리 설정
        float approachDistance = 5f;  // Shy가 놀라는 거리 (더 멀리서 반응)
        float retreatDistance = 7f;   // 첫 번째 도망 거리
        float secondRetreatDistance = 10f; // 두 번째 도망 거리
        
        // 0단계: 서로 마주보기
        Debug.Log($"[ShyPlayful] 단계0: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, playfulPet, 1f));
        yield return new WaitForSeconds(1f);
        
        // 1단계: Playful이 정확한 거리까지 접근
        Debug.Log($"[ShyPlayful] 단계1: Playful이 Shy에게 접근 시작");
        
        // Shy의 위치 기준으로 접근 목표 설정 (정확한 거리)
        Vector3 direction = (shyPet.transform.position - playfulPet.transform.position).normalized;
        Vector3 targetPosition = shyPet.transform.position - direction * approachDistance;
        targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);
        
        playfulPet.agent.isStopped = false;  // 명시적으로 설정
        playfulPet.agent.speed = playfulPet.baseSpeed * 1.5f;  // 천천히 접근
        playfulPet.agent.SetDestination(targetPosition);
        playfulPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk); // 걷기 애니메이션
        
        // 목표 위치 도달 대기
        float waitTime = 0f;
        float maxWaitTime = 5f;
        while (waitTime < maxWaitTime)
        {
            // agent가 경로 계산 완료하고 남은 거리 체크
            if (!playfulPet.agent.pathPending && playfulPet.agent.remainingDistance < 0.5f)
            {
                Debug.Log($"[ShyPlayful] Playful이 목표 위치에 도달!");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
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
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, playfulPet, 0.5f));
        yield return new WaitForSeconds(0.5f);
        
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
        Debug.Log($"[BraveBrave] 단계1: 빠르게 접근");
        // 빠르게 접근
        float distance = CalculateDistanceBySize(pet1, pet2) * 1.5f;
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.speed = pet1.baseSpeed * 1.5f;
        pet2.agent.speed = pet2.baseSpeed * 1.5f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        
        yield return new WaitForSeconds(1.5f);
        
        Debug.Log($"[BraveBrave] 단계2: 서로 정면 대치");
        // 서로 정면 대치
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, lookDuration));
        
        Debug.Log($"[BraveBrave] 단계3: 서로 주위를 돔 (위엄 과시)");
        // 서로 주위를 돔 (위엄 과시)
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        
        // 동시에 상대 주위를 도는 효과
        float circleTime = 0f;
        while (circleTime < 2f)
        {
            Vector3 offset1 = Quaternion.Euler(0, 90, 0) * (pet2.transform.position - pet1.transform.position).normalized * distance;
            Vector3 offset2 = Quaternion.Euler(0, -90, 0) * (pet1.transform.position - pet2.transform.position).normalized * distance;
            
            pet1.agent.SetDestination(pet2.transform.position + offset1);
            pet2.agent.SetDestination(pet1.transform.position + offset2);
            
            circleTime += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log($"[BraveBrave] 단계4: 짧은 달리기 시합");
        // 짧은 달리기 시합
        Vector3 raceDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        Vector3 raceTarget = meetPoint + raceDirection * 10f;
        
        pet1.agent.speed = pet1.baseSpeed * 2f;
        pet2.agent.speed = pet2.baseSpeed * 2f;
        
        pet1.agent.SetDestination(raceTarget);
        pet2.agent.SetDestination(raceTarget);
        
        yield return new WaitForSeconds(2f);
        
        Debug.Log($"[BraveBrave] 단계5: 서로 인정하고 헤어짐");
        // 서로 인정하고 헤어짐
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));
        Debug.Log($"<color=blue>[BraveBrave] 반응 완료</color>");
    }

    // ===== 9. Brave + Playful =====
    private IEnumerator BravePlayfulReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[BravePlayful] 단계1: 둘 다 빠르게 접근");
        // 둘 다 빠르게 접근
        float distance = CalculateDistanceBySize(pet1, pet2);
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.speed = pet1.baseSpeed * 1.8f;
        pet2.agent.speed = pet2.baseSpeed * 1.8f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        
        yield return new WaitForSeconds(1f);
        
        Debug.Log($"[BravePlayful] 단계2: 서로 주위를 빙빙 돔");
        // 서로 주위를 빙빙 돔
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, distance, 2f));
        
        // Playful이 점프
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        
        Debug.Log($"[BravePlayful] 단계3: Playful이 점프");
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.5f, true, false));
        
        Debug.Log($"[BravePlayful] 단계4: Brave도 점프");
        // Brave도 점프
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.5f, true, false));
        
        Debug.Log($"[BravePlayful] 단계5: 짧은 추격전");
        // 짧은 추격전
        Vector3 chaseDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        
        playfulPet.agent.SetDestination(playfulPet.transform.position + chaseDirection * 5f);
        yield return new WaitForSeconds(0.3f);
        bravePet.agent.SetDestination(playfulPet.transform.position);
        
        yield return new WaitForSeconds(chaseDuration);
        
        Debug.Log($"[BravePlayful] 단계6: 만족하고 헤어짐");
        // 만족하고 헤어짐
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        Debug.Log($"<color=blue>[BravePlayful] 반응 완료</color>");
    }

    // ===== 10. Playful + Playful =====
    private IEnumerator PlayfulPlayfulReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[PlayfulPlayful] 단계1: 신나게 달려옴");
        // 신나게 달려옴
        float distance = CalculateDistanceBySize(pet1, pet2);
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.speed = pet1.baseSpeed * 2f;
        pet2.agent.speed = pet2.baseSpeed * 2f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        
        yield return new WaitForSeconds(1f);
        
        Debug.Log($"[PlayfulPlayful] 단계2: 서로 주위를 돔");
        // 서로 주위를 돔
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, distance, 1.5f));
        
        Debug.Log($"[PlayfulPlayful] 단계3: 연속 점프 (3회)");
        // 연속 점프
        for (int i = 0; i < 3; i++)
        {
            StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
        }
        
        Debug.Log($"[PlayfulPlayful] 단계4: 짧은 추격전 시작");
        // 짧은 추격전
        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        
        pet1.agent.SetDestination(pet1.transform.position + randomDir * 5f);
        pet2.agent.SetDestination(pet1.transform.position);
        
        yield return new WaitForSeconds(1.5f);
        
        Debug.Log($"[PlayfulPlayful] 단계5: 역할 바꿔서 추격");
        // 역할 바꿔서 추격
        pet2.agent.SetDestination(pet2.transform.position - randomDir * 5f);
        pet1.agent.SetDestination(pet2.transform.position);
        
        yield return new WaitForSeconds(1.5f);
        
        Debug.Log($"[PlayfulPlayful] 단계6: 다시 점프 파티 (2회)");
        // 다시 점프 파티
        for (int i = 0; i < 2; i++)
        {
            StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
        }
        
        Debug.Log($"[PlayfulPlayful] 단계7: 신나게 놀다 헤어짐");
        // 신나게 놀다 헤어짐
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        Debug.Log($"<color=blue>[PlayfulPlayful] 반응 완료</color>");
    }

    // ===== 헬퍼 메서드들 =====

    /// <summary>
    /// 기본 반응 (조합이 없을 때)
    /// </summary>
    private IEnumerator DefaultReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[DefaultReaction] 기본 반응 실행");
        
        Debug.Log($"[DefaultReaction] 단계1: 간단히 접근");
        // 간단히 접근 후 헤어짐
        float distance = CalculateDistanceBySize(pet1, pet2);
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
    private IEnumerator CircleAroundTarget(PetController circler, PetController target, float radius, float duration)
    {
        float elapsed = 0f;
        float angleStep = 360f / duration;
        
        while (elapsed < duration)
        {
            float angle = angleStep * elapsed;
            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
            Vector3 targetPos = target.transform.position + offset;
            
            circler.agent.SetDestination(targetPos);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 서로 주위를 도는 동작
    /// </summary>
    private IEnumerator CircleAroundEachOther(PetController pet1, PetController pet2, float radius, float duration)
    {
        float elapsed = 0f;
        Vector3 center = (pet1.transform.position + pet2.transform.position) / 2f;
        
        while (elapsed < duration)
        {
            float angle = (elapsed / duration) * 360f;
            
            Vector3 offset1 = Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
            Vector3 offset2 = Quaternion.Euler(0, angle + 180f, 0) * Vector3.forward * radius;
            
            pet1.agent.SetDestination(center + offset1);
            pet2.agent.SetDestination(center + offset2);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
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
    /// 빠르게 돌아서 도망가기 (뒷걸음질 대체)
    /// </summary>
    private IEnumerator QuickRetreat(PetController pet, Vector3 awayFrom, float distance, float duration = 1f)
    {
        Debug.Log($"[QuickRetreat] {pet.petName}이(가) 돌아서 도망 (거리: {distance}, 시간: {duration})");
        
        // agent 활성화 확인
        pet.agent.isStopped = false;  // 명시적으로 설정
        pet.agent.updateRotation = true;
        
        // 도망갈 방향 계산 (반대 방향)
        Vector3 runDirection = (pet.transform.position - awayFrom).normalized;
        if (runDirection == Vector3.zero) runDirection = -pet.transform.forward;
        
        // 도망갈 목표 위치
        Vector3 retreatTarget = pet.transform.position + runDirection * distance;
        retreatTarget = FindValidPositionOnNavMesh(retreatTarget, 10f);
        
        Debug.Log($"[QuickRetreat] 목표 위치: {retreatTarget}, 현재 위치: {pet.transform.position}");
        
        // 부드럽게 돌아서면서 도망 시작
        float originalSpeed = pet.agent.speed;
        float originalAngularSpeed = pet.agent.angularSpeed;
        
        pet.agent.angularSpeed = 720f;  // 빠른 회전 속도 (720도/초)
        pet.agent.speed = pet.baseSpeed * 1.5f; // 도망 속도
        pet.agent.SetDestination(retreatTarget);
        
        // Run 애니메이션 (SetContinuousAnimation 사용)
        pet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        // 회전이 완료될 시간 포함하여 대기
        yield return new WaitForSeconds(duration);
        
        // 애니메이션 정리
        pet.animationController.StopContinuousAnimation();
        
        // 속도 및 회전 속도 복원
        pet.agent.speed = originalSpeed;
        pet.agent.angularSpeed = originalAngularSpeed;
    }
}