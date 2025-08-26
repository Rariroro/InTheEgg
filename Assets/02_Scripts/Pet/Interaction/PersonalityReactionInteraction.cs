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
    
    // 중단 상태 추적
    private bool wasInterrupted = false;

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
        
        // 중단 플래그 초기화
        wasInterrupted = false;
        
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
            // 중단 여부 체크
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
                // 정상 종료 시에도 상태 복원 및 정리
                if (pet1State != null) pet1State.Restore(pet1);
                if (pet2State != null) pet2State.Restore(pet2);
                
                // 상호작용 종료
                EndInteraction(pet1, pet2);
                
                // AI 재시작
                if (pet1?.AI != null)
                {
                    pet1.AI.InterruptAndResetAI();
                }
                if (pet2?.AI != null)
                {
                    pet2.AI.InterruptAndResetAI();
                }
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
        Debug.Log($"[LazyLazy] Lazy: {pet1.petName}, Lazy: {pet2.petName}");
        
        // 거리 설정
        float approachDistance = 3f;
        float restDuration = 3f;
        
        // 0단계: 서로 마주보기
        Debug.Log($"[LazyLazy] 단계0: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 1.5f));
        yield return new WaitForSeconds(1f);
        
        // 1단계: 매우 천천히 서로 접근
        Debug.Log($"[LazyLazy] 단계1: 서로 매우 천천히 접근");
        
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        meetPoint = FindValidPositionOnNavMesh(meetPoint, 10f);
        
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.speed = pet1.baseSpeed * 0.3f; // 매우 천천히
        pet2.agent.speed = pet2.baseSpeed * 0.3f;
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        // 접근 대기
        float waitTime = 0f;
        float maxWaitTime = 4f;
        while (waitTime < maxWaitTime)
        {
            if (Vector3.Distance(pet1.transform.position, pet2.transform.position) < approachDistance)
            {
                Debug.Log($"[LazyLazy] 충분히 가까워짐");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        // 2단계: 멈춰서 서로 쳐다보기
        Debug.Log($"[LazyLazy] 단계2: 멈춰서 서로 쳐다보기");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 1f));
        yield return new WaitForSeconds(0.5f);
        
        // 3단계: Pet1이 먼저 누움
        Debug.Log($"[LazyLazy] 단계3: {pet1.petName}이 먼저 누움");
        yield return StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, restDuration, false, false));
        
        // 4단계: Pet2도 누움
        Debug.Log($"[LazyLazy] 단계4: {pet2.petName}도 따라서 누움");
        yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, restDuration, false, false));
        
        // 5단계: 동시에 일어남
        Debug.Log($"[LazyLazy] 단계5: 둘 다 일어나서 천천히 헤어짐");
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"<color=blue>[LazyLazy] 반응 완료</color>");
    }

    // ===== 2. Lazy + Shy =====
    private IEnumerator LazyShyReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        Debug.Log($"[LazyShy] Lazy: {lazyPet.petName}, Shy: {shyPet.petName}");
        
        // 거리 설정
        float approachDistance = 4f;
        float retreatDistance = 5f;
        
        // 0단계: 서로 마주보기
        Debug.Log($"[LazyShy] 단계0: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(lazyPet, shyPet, 1f));
        yield return new WaitForSeconds(0.5f);
        
        // 1단계: Lazy가 천천히 접근
        Debug.Log($"[LazyShy] 단계1: Lazy가 천천히 접근");
        
        Vector3 direction = (shyPet.transform.position - lazyPet.transform.position).normalized;
        Vector3 targetPosition = shyPet.transform.position - direction * approachDistance;
        targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);
        
        lazyPet.agent.isStopped = false;
        lazyPet.agent.speed = lazyPet.baseSpeed * 0.4f;
        lazyPet.agent.SetDestination(targetPosition);
        lazyPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        // 접근 대기
        float waitTime = 0f;
        float maxWaitTime = 3f;
        while (waitTime < maxWaitTime)
        {
            if (!lazyPet.agent.pathPending && lazyPet.agent.remainingDistance < 0.5f)
            {
                Debug.Log($"[LazyShy] Lazy가 목표 위치 도달");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        // 2단계: Lazy가 누움
        Debug.Log($"[LazyShy] 단계2: {lazyPet.petName}이 피곤해서 누움");
        lazyPet.agent.isStopped = true;
        lazyPet.animationController.StopContinuousAnimation();
        yield return StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 3f, false, false));
        
        // 3단계: Shy가 놀라서 도망
        Debug.Log($"[LazyShy] 단계3: {shyPet.petName}이 놀라서 도망");
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        shyPet.agent.isStopped = false;
        yield return StartCoroutine(QuickRetreat(shyPet, lazyPet.transform.position, retreatDistance, 1f));
        
        // 4단계: Shy가 멈춰서 돌아봄
        Debug.Log($"[LazyShy] 단계4: {shyPet.petName}이 멈춰서 돌아봄");
        shyPet.agent.isStopped = true;
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, lazyPet, 0.5f));
        yield return new WaitForSeconds(1f);
        
        // 5단계: Shy가 조심스럽게 다시 접근
        Debug.Log($"[LazyShy] 단계5: {shyPet.petName}이 조심스럽게 다시 접근");
        shyPet.agent.isStopped = false;
        shyPet.agent.speed = shyPet.baseSpeed * 0.3f;
        Vector3 sniffPoint = lazyPet.transform.position + lazyPet.transform.forward * 2f;
        sniffPoint = FindValidPositionOnNavMesh(sniffPoint, 5f);
        shyPet.agent.SetDestination(sniffPoint);
        shyPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        yield return new WaitForSeconds(2f);
        shyPet.agent.isStopped = true;
        shyPet.animationController.StopContinuousAnimation();
        
        // 6단계: 냄새 맡기 동작
        Debug.Log($"[LazyShy] 단계6: {shyPet.petName}이 냄새를 맡음");
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1f, false, false));
        
        Debug.Log($"<color=blue>[LazyShy] 반응 완료</color>");
    }

    // ===== 3. Lazy + Brave =====
    private IEnumerator LazyBraveReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        Debug.Log($"[LazyBrave] Lazy: {lazyPet.petName}, Brave: {bravePet.petName}");
        
        // 거리 설정
        float approachDistance = 3f;
        float circleRadius = 4f;
        
        // 0단계: 서로 마주보기
        Debug.Log($"[LazyBrave] 단계0: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(lazyPet, bravePet, 0.5f));
        yield return new WaitForSeconds(0.5f);
        
        // 1단계: Brave가 빠르게 접근
        Debug.Log($"[LazyBrave] 단계1: Brave가 당당하게 빠르게 접근");
        
        Vector3 direction = (lazyPet.transform.position - bravePet.transform.position).normalized;
        Vector3 targetPosition = lazyPet.transform.position - direction * approachDistance;
        targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);
        
        bravePet.agent.isStopped = false;
        bravePet.agent.speed = bravePet.baseSpeed * 1.5f;
        bravePet.agent.SetDestination(targetPosition);
        bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        // 2단계: Lazy는 무반응으로 누움
        Debug.Log($"[LazyBrave] 단계2: {lazyPet.petName}은 귀찮아서 누움");
        lazyPet.agent.isStopped = true;
        yield return StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 4f, false, false));
        
        // Brave 도착 대기
        float waitTime = 0f;
        float maxWaitTime = 2f;
        while (waitTime < maxWaitTime)
        {
            if (!bravePet.agent.pathPending && bravePet.agent.remainingDistance < 0.5f)
            {
                Debug.Log($"[LazyBrave] Brave가 도착");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        bravePet.agent.isStopped = true;
        bravePet.animationController.StopContinuousAnimation();
        
        // 3단계: Brave가 Lazy 주위를 돔
        Debug.Log($"[LazyBrave] 단계3: {bravePet.petName}이 주위를 돌며 살펴봄");
        yield return StartCoroutine(CircleAroundTarget(bravePet, lazyPet, circleRadius, 3f));
        
        // 4단계: Brave가 점프하며 자랑
        Debug.Log($"[LazyBrave] 단계4: {bravePet.petName}이 점프하며 자랑");
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        
        // 5단계: Lazy는 계속 무시
        Debug.Log($"[LazyBrave] 단계5: {lazyPet.petName}은 계속 무시");
        yield return new WaitForSeconds(1f);
        
        // 6단계: Brave가 흥미를 잃고 떠남
        Debug.Log($"[LazyBrave] 단계6: {bravePet.petName}이 흥미를 잃고 떠남");
        bravePet.agent.isStopped = false;
        bravePet.agent.speed = bravePet.baseSpeed;
        
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
        float approachDistance = 3f;
        
        // 0단계: 서로 마주보기
        Debug.Log($"[LazyPlayful] 단계0: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(lazyPet, playfulPet, 0.5f));
        yield return new WaitForSeconds(0.5f);
        
        // 1단계: Playful이 신나게 접근
        Debug.Log($"[LazyPlayful] 단계1: Playful이 신나게 접근");
        
        Vector3 direction = (lazyPet.transform.position - playfulPet.transform.position).normalized;
        Vector3 targetPosition = lazyPet.transform.position - direction * approachDistance;
        targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);
        
        playfulPet.agent.isStopped = false;
        playfulPet.agent.speed = playfulPet.baseSpeed * 2f;
        playfulPet.agent.SetDestination(targetPosition);
        playfulPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        // 2단계: Lazy는 귀찮아서 누움
        Debug.Log($"[LazyPlayful] 단계2: {lazyPet.petName}은 귀찮아서 누움");
        lazyPet.agent.isStopped = true;
        yield return StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 3f, false, false));
        
        // Playful 도착 대기
        float waitTime = 0f;
        float maxWaitTime = 2f;
        while (waitTime < maxWaitTime)
        {
            if (!playfulPet.agent.pathPending && playfulPet.agent.remainingDistance < 0.5f)
            {
                Debug.Log($"[LazyPlayful] Playful이 도착");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        playfulPet.agent.isStopped = true;
        playfulPet.animationController.StopContinuousAnimation();
        
        // 3단계: Playful이 연속 점프하며 놀자고 함
        Debug.Log($"[LazyPlayful] 단계3: {playfulPet.petName}이 점프하며 놀자고 함 (3회)");
        for (int i = 0; i < 3; i++)
        {
            yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(0.3f);
        }
        
        // 4단계: Playful이 Lazy 주위를 빙빙 돔
        Debug.Log($"[LazyPlayful] 단계4: {playfulPet.petName}이 주위를 돌며 놀자고 함");
        yield return StartCoroutine(CircleAroundTarget(playfulPet, lazyPet, 3f, 2f));
        
        // 5단계: Lazy는 계속 무시
        Debug.Log($"[LazyPlayful] 단계5: {lazyPet.petName}은 계속 무시");
        yield return new WaitForSeconds(1f);
        
        // 6단계: Playful이 포기하고 실망하며 떠남
        Debug.Log($"[LazyPlayful] 단계6: {playfulPet.petName}이 실망하며 떠남");
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1f, false, false));
        
        playfulPet.agent.isStopped = false;
        playfulPet.agent.speed = playfulPet.baseSpeed;
        
        Debug.Log($"<color=blue>[LazyPlayful] 반응 완료</color>");
    }

    // ===== 5. Shy + Shy =====
    private IEnumerator ShyShyReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[ShyShy] Shy: {pet1.petName}, Shy: {pet2.petName}");
        
        // 거리 설정
        float retreatDistance = 5f;
        float finalFleeDistance = 8f;
        
        // 0단계: 서로 발견하고 멈춤
        Debug.Log($"[ShyShy] 단계0: 서로 발견하고 멈춤");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        
        // 1단계: 조심스럽게 서로 쳐다보기
        Debug.Log($"[ShyShy] 단계1: 조심스럽게 서로 쳐다보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 1f));
        
        // 2단계: 긴 정적
        Debug.Log($"[ShyShy] 단계2: 긴 정적... (불안한 기다림)");
        yield return new WaitForSeconds(pauseDuration * 2f);
        
        // 3단계: 둘 다 놀라서 점프
        Debug.Log($"[ShyShy] 단계3: 둘 다 놀라서 점프");
        StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        
        // 4단계: 동시에 뒤로 물러남
        Debug.Log($"[ShyShy] 단계4: 동시에 뒤로 물러남");
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        
        // 동시에 도망 시작
        StartCoroutine(QuickRetreat(pet1, pet2.transform.position, retreatDistance, 1f));
        yield return StartCoroutine(QuickRetreat(pet2, pet1.transform.position, retreatDistance, 1f));
        
        // 5단계: 다시 돌아봄
        Debug.Log($"[ShyShy] 단계5: 멈춰서 다시 돌아봄");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));
        yield return new WaitForSeconds(0.5f);
        
        // 6단계: 완전히 반대 방향으로 도망
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
        
        pet1.agent.speed = pet1.baseSpeed * 1.8f;
        pet2.agent.speed = pet2.baseSpeed * 1.8f;
        
        pet1.agent.SetDestination(pet1Run);
        pet2.agent.SetDestination(pet2Run);
        
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        yield return new WaitForSeconds(2f);
        
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
        
        // 거리 설정
        float approachDistance = 4f;
        float firstRetreatDistance = 5f;
        float finalFleeDistance = 10f;
        
        // 0단계: 서로 마주보기
        Debug.Log($"[ShyBrave] 단계0: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, bravePet, 1f));
        yield return new WaitForSeconds(0.5f);
        
        // 1단계: Brave가 당당히 접근
        Debug.Log($"[ShyBrave] 단계1: Brave가 당당히 접근");
        
        Vector3 direction = (shyPet.transform.position - bravePet.transform.position).normalized;
        Vector3 targetPosition = shyPet.transform.position - direction * approachDistance;
        targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);
        
        bravePet.agent.isStopped = false;
        bravePet.agent.speed = bravePet.baseSpeed * 1.3f;
        bravePet.agent.SetDestination(targetPosition);
        bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        // Shy가 불안해함
        yield return new WaitForSeconds(0.5f);
        
        // 2단계: Shy가 놀라서 첫 번째 도망
        Debug.Log($"[ShyBrave] 단계2: {shyPet.petName}이 놀라서 도망");
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        
        shyPet.agent.isStopped = false;
        yield return StartCoroutine(QuickRetreat(shyPet, bravePet.transform.position, firstRetreatDistance, 1f));
        
        // 3단계: Brave가 천천히 따라감
        Debug.Log($"[ShyBrave] 단계3: {bravePet.petName}이 천천히 따라감");
        bravePet.agent.isStopped = false;
        bravePet.agent.speed = bravePet.baseSpeed * 0.7f;
        bravePet.agent.SetDestination(shyPet.transform.position);
        bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        yield return new WaitForSeconds(1.5f);
        
        // 4단계: Shy가 멈춰서 돌아봄
        Debug.Log($"[ShyBrave] 단계4: {shyPet.petName}이 멈춰서 돌아봄");
        shyPet.agent.isStopped = true;
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, bravePet, 0.5f));
        
        // 5단계: Brave가 점프하며 인사
        Debug.Log($"[ShyBrave] 단계5: {bravePet.petName}이 점프하며 인사");
        bravePet.agent.isStopped = true;
        bravePet.animationController.StopContinuousAnimation();
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        
        // 6단계: Shy가 완전히 도망
        Debug.Log($"[ShyBrave] 단계6: {shyPet.petName}이 완전히 도망");
        Vector3 fleeDirection = (shyPet.transform.position - bravePet.transform.position).normalized;
        Vector3 fleePos = shyPet.transform.position + fleeDirection * finalFleeDistance;
        fleePos = FindValidPositionOnNavMesh(fleePos, 10f);
        
        shyPet.agent.isStopped = false;
        shyPet.agent.speed = shyPet.baseSpeed * 2f;
        shyPet.agent.SetDestination(fleePos);
        shyPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        // 7단계: Brave가 잠시 쫓다가 포기
        Debug.Log($"[ShyBrave] 단계7: {bravePet.petName}이 잠시 쫓다가 포기");
        bravePet.agent.isStopped = false;
        bravePet.agent.speed = bravePet.baseSpeed * 1.5f;
        bravePet.agent.SetDestination(shyPet.transform.position);
        bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        yield return new WaitForSeconds(1f);
        
        bravePet.agent.isStopped = true;
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
                yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, playfulPet, 1f));

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
        Debug.Log($"[BraveBrave] Brave: {pet1.petName}, Brave: {pet2.petName}");
        
        // 거리 설정
        float meetDistance = 4f;
        float circleRadius = 5f;
        float raceDistance = 8f;
        
        // 0단계: 서로 마주보기
        Debug.Log($"[BraveBrave] 단계0: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));
        yield return new WaitForSeconds(0.3f);
        
        // 1단계: 빠르게 서로 접근
        Debug.Log($"[BraveBrave] 단계1: 당당하게 빠르게 접근");
        
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        meetPoint = FindValidPositionOnNavMesh(meetPoint, 10f);
        
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.speed = pet1.baseSpeed * 1.5f;
        pet2.agent.speed = pet2.baseSpeed * 1.5f;
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        // 접근 대기
        float waitTime = 0f;
        float maxWaitTime = 2f;
        while (waitTime < maxWaitTime)
        {
            if (Vector3.Distance(pet1.transform.position, pet2.transform.position) < meetDistance)
            {
                Debug.Log($"[BraveBrave] 충분히 가까워짐");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        // 2단계: 정면 대치
        Debug.Log($"[BraveBrave] 단계2: 정면 대치");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 1f));
        yield return new WaitForSeconds(0.5f);
        
        // 3단계: 서로 주위를 돔 (위엄 과시)
        Debug.Log($"[BraveBrave] 단계3: 서로 주위를 돌며 위엄 과시");
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, circleRadius, 2.5f));
        
        // 4단계: 동시에 점프하며 자랑
        Debug.Log($"[BraveBrave] 단계4: 동시에 점프하며 자랑");
        StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        
        // 5단계: 짧은 달리기 시합
        Debug.Log($"[BraveBrave] 단계5: 짧은 달리기 시합");
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
        
        // 6단계: 서로 인정하고 헤어짐
        Debug.Log($"[BraveBrave] 단계6: 서로 인정하고 헤어짐");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));
        
        Debug.Log($"<color=blue>[BraveBrave] 반응 완료</color>");
    }

    // ===== 9. Brave + Playful =====
    private IEnumerator BravePlayfulReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        Debug.Log($"[BravePlayful] Brave: {bravePet.petName}, Playful: {playfulPet.petName}");
        
        // 거리 설정
        float meetDistance = 3f;
        float circleRadius = 4f;
        float chaseDistance = 6f;
        
        // 0단계: 서로 마주보기
        Debug.Log($"[BravePlayful] 단계0: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(bravePet, playfulPet, 0.5f));
        yield return new WaitForSeconds(0.3f);
        
        // 1단계: 둘 다 빠르게 접근
        Debug.Log($"[BravePlayful] 단계1: 둘 다 신나게 빠르게 접근");
        
        Vector3 meetPoint = (bravePet.transform.position + playfulPet.transform.position) / 2f;
        meetPoint = FindValidPositionOnNavMesh(meetPoint, 10f);
        
        bravePet.agent.isStopped = false;
        playfulPet.agent.isStopped = false;
        bravePet.agent.speed = bravePet.baseSpeed * 1.8f;
        playfulPet.agent.speed = playfulPet.baseSpeed * 1.8f;
        bravePet.agent.SetDestination(meetPoint);
        playfulPet.agent.SetDestination(meetPoint);
        bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        playfulPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        // 접근 대기
        float waitTime = 0f;
        float maxWaitTime = 1.5f;
        while (waitTime < maxWaitTime)
        {
            if (Vector3.Distance(bravePet.transform.position, playfulPet.transform.position) < meetDistance)
            {
                Debug.Log($"[BravePlayful] 충분히 가까워짐");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        bravePet.agent.isStopped = true;
        playfulPet.agent.isStopped = true;
        bravePet.animationController.StopContinuousAnimation();
        playfulPet.animationController.StopContinuousAnimation();
        
        // 2단계: 서로 주위를 빙빙 돔
        Debug.Log($"[BravePlayful] 단계2: 서로 주위를 빙빙 돔");
        yield return StartCoroutine(CircleAroundEachOther(bravePet, playfulPet, circleRadius, 2f));
        
        // 3단계: Playful이 먼저 점프
        Debug.Log($"[BravePlayful] 단계3: {playfulPet.petName}이 신나서 점프");
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.6f, true, false));
        
        // 4단계: Brave도 따라서 점프
        Debug.Log($"[BravePlayful] 단계4: {bravePet.petName}도 따라서 점프");
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.6f, true, false));
        
        // 5단계: Playful이 도망가며 추격전 시작
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
        bravePet.agent.isStopped = true;
        playfulPet.agent.isStopped = true;
        bravePet.animationController.StopContinuousAnimation();
        playfulPet.animationController.StopContinuousAnimation();
        
        Debug.Log($"<color=blue>[BravePlayful] 반응 완료</color>");
    }

    // ===== 10. Playful + Playful =====
    private IEnumerator PlayfulPlayfulReaction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[PlayfulPlayful] Playful: {pet1.petName}, Playful: {pet2.petName}");
        
        // 거리 설정
        float meetDistance = 3f;
        float circleRadius = 4f;
        float chaseDistance = 7f;
        
        // 0단계: 서로 마주보기
        Debug.Log($"[PlayfulPlayful] 단계0: 서로 마주보고 흥분");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.3f));
        
        // 1단계: 신나게 달려옴
        Debug.Log($"[PlayfulPlayful] 단계1: 둘 다 신나게 달려옴");
        
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        meetPoint = FindValidPositionOnNavMesh(meetPoint, 10f);
        
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.speed = pet1.baseSpeed * 2f;
        pet2.agent.speed = pet2.baseSpeed * 2f;
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        // 접근 대기
        float waitTime = 0f;
        float maxWaitTime = 1f;
        while (waitTime < maxWaitTime)
        {
            if (Vector3.Distance(pet1.transform.position, pet2.transform.position) < meetDistance)
            {
                Debug.Log($"[PlayfulPlayful] 충분히 가까워짐");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        
        // 2단계: 서로 주위를 빠르게 돔
        Debug.Log($"[PlayfulPlayful] 단계2: 서로 주위를 빠르게 돔");
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, circleRadius, 1.5f));
        
        // 3단계: 동시 점프 파티 (3회)
        Debug.Log($"[PlayfulPlayful] 단계3: 신나게 점프 파티 (3회)");
        for (int i = 0; i < 3; i++)
        {
            StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(0.2f);
        }
        
        // 4단계: Pet1이 도망가며 추격전 시작
        Debug.Log($"[PlayfulPlayful] 단계4: {pet1.petName}이 도망가며 추격전 시작");
        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        Vector3 chaseTarget1 = pet1.transform.position + randomDir * chaseDistance;
        chaseTarget1 = FindValidPositionOnNavMesh(chaseTarget1, 10f);
        
        pet1.agent.isStopped = false;
        pet1.agent.speed = pet1.baseSpeed * 2f;
        pet1.agent.SetDestination(chaseTarget1);
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        yield return new WaitForSeconds(0.2f);
        
        pet2.agent.isStopped = false;
        pet2.agent.speed = pet2.baseSpeed * 2f;
        pet2.agent.SetDestination(pet1.transform.position);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        yield return new WaitForSeconds(1.5f);
        
        // 5단계: 역할 바꿔서 추격
        Debug.Log($"[PlayfulPlayful] 단계5: 역할 바꿔서 {pet2.petName}이 도망");
        Vector3 chaseTarget2 = pet2.transform.position - randomDir * chaseDistance;
        chaseTarget2 = FindValidPositionOnNavMesh(chaseTarget2, 10f);
        
        pet2.agent.SetDestination(chaseTarget2);
        yield return new WaitForSeconds(0.2f);
        pet1.agent.SetDestination(pet2.transform.position);
        
        yield return new WaitForSeconds(1.5f);
        
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        
        // 6단계: 다시 모여서 점프 파티 (2회)
        Debug.Log($"[PlayfulPlayful] 단계6: 다시 모여서 점프 파티 (2회)");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.3f));
        
        for (int i = 0; i < 2; i++)
        {
            StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(0.2f);
        }
        
        // 7단계: 마지막으로 서로 주위를 돌다가 헤어짐
        Debug.Log($"[PlayfulPlayful] 단계7: 마지막으로 한 바퀴 돌고 헤어짐");
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, circleRadius, 1f));
        
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
    /// 상호작용이 중단되었는지 체크하는 헬퍼 메서드
    /// </summary>
    private bool CheckIfInterrupted(PetController pet1, PetController pet2)
    {
        return (pet1 != null && (pet1.State.IsHolding || pet1.State.IsSelected)) ||
               (pet2 != null && (pet2.State.IsHolding || pet2.State.IsSelected));
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