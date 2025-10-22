using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 성격 조합별 가벼운 반응 패턴을 구현하는 상호작용
/// 짧고 빈번하게 발생하며 감정 표현 없이 동작만으로 표현
/// </summary>
public class PersonalityReactionInteraction : BasePetInteraction
{
    // 상호작용 이름을 "PersonalityReaction"으로 고정 반환
    public override string InteractionName => "PersonalityReaction";
    
    // 유저가 펫을 터치하거나 홀드해서 상호작용이 중단되었는지 추적하는 플래그
    private bool wasInterrupted = false;

    [Header("반응 설정")]
    [Tooltip("반응 지속 시간")]
    public float reactionDuration = 8f;  // 전체 상호작용이 지속되는 시간
    
    [Tooltip("접근 거리")]
    public float approachDistance = 3f;  // 펫들이 서로 접근할 기본 거리
    
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

    protected override InteractionType DetermineInteractionType()
    {
        // 상호작용 타입을 WalkTogether로 설정 (UI 표시용)
        // 실제로는 성격 조합에 따라 다양한 패턴으로 동작함
        return InteractionType.WalkTogether;
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
        // Debug.Log($"<color=green>[PersonalityReaction] ========== 상호작용 시작 ==========</color>");
        // Debug.Log($"[PersonalityReaction] 참여 펫: {pet1.petName}({pet1.personality}) & {pet2.petName}({pet2.personality})");
        
        // 상호작용이 중단되었는지 추적하는 플래그를 false로 초기화
        wasInterrupted = false;

        // 펫들의 NavMeshAgent가 준비되었는지 확인 (최대 3초 대기)
        // NavMeshAgent가 없거나 비활성화되어 있으면 상호작용 불가능
        // Debug.Log($"[PersonalityReaction] NavMeshAgent 준비 확인 중...");
        yield return StartCoroutine(WaitUntilAgentIsReady(pet1, 3f));
        yield return StartCoroutine(WaitUntilAgentIsReady(pet2, 3f));

        if (!pet1.agent.enabled || !pet2.agent.enabled)
        {
            Debug.LogWarning($"[PersonalityReaction] NavMeshAgent 준비 실패! 상호작용 중단");
            EndInteraction(pet1, pet2);
            yield break;
        }
        // Debug.Log($"[PersonalityReaction] NavMeshAgent 준비 완료");

        // 상호작용 전 펫들의 원래 상태(속도, 회전속도 등)를 저장
        // 나중에 상호작용이 끝나면 이 상태로 복원함
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);

        try
        {                     
            // 두 펫의 성격을 조합하여 어떤 패턴을 실행할지 결정
            // 예: Lazy + Lazy = "Lazy_Lazy" 패턴
            string combination = GetPersonalityCombination(pet1.personality, pet2.personality);
        // Debug.Log($"<color=yellow>[PersonalityReaction] 성격 조합: {combination}</color>");

            // 결정된 성격 조합에 해당하는 반응 패턴 실행
            // 10가지 조합별로 다른 동작 패턴이 있음
        // Debug.Log($"[PersonalityReaction] {combination} 패턴 실행 시작");
            yield return StartCoroutine(ExecuteReactionPattern(combination, pet1, pet2));
        // Debug.Log($"[PersonalityReaction] {combination} 패턴 실행 완료");
        }
        finally
        {
            // 유저가 펫을 터치하거나 홀드하여 상호작용이 중단되었는지 확인
            // IsHolding: 펫을 잡고 있는 상태, IsSelected: 펫을 선택한 상태
            wasInterrupted = (pet1 != null && (pet1.State.IsHolding || pet1.State.IsSelected)) ||
                           (pet2 != null && (pet2.State.IsHolding || pet2.State.IsSelected));
            
            if (wasInterrupted)
            {
        // Debug.Log($"<color=orange>[PersonalityReaction] 유저 입력으로 중단됨</color>");
                ForceCompleteCleanup(pet1, pet2);
            }
            else
            {
        // Debug.Log($"[PersonalityReaction] 정상 종료 처리");
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
            
        // Debug.Log($"<color=blue>[PersonalityReaction] ========== 상호작용 종료 ==========</color>");
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
    // 특징: 서로 천천히 접근하다가 함께 누워서 쉬는 패턴
    private IEnumerator LazyLazyReaction(PetController pet1, PetController pet2)
    {
        // Debug.Log($"[LazyLazy] Lazy: {pet1.petName}, Lazy: {pet2.petName}");
        
        // 거리 설정
        float approachDistance = 4f;  // 서로 가까이 가지 않을 거리
        float restDuration = 3f;
        float separateDistance = 6f;  // 헤어질 때 거리
        float skipApproachThreshold = 5f;  // 5미터 이내면 접근 스킵
        
        // 현재 거리 체크
        float currentDistance = Vector3.Distance(pet1.transform.position, pet2.transform.position);
        // Debug.Log($"[LazyLazy] 현재 거리: {currentDistance:F2}미터");
        
        if (currentDistance < skipApproachThreshold)
        {
            // 이미 가까이 있으면 둘 다 너무 귀찮아서 바로 누워버림
        // Debug.Log($"[LazyLazy] 이미 가까이 있음 - 둘 다 너무 귀찮아서 바로 누워버림!");
            
            // 서로 대충 마주보기
            yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.3f));
            
            // 둘 다 동시에 누워버림
        // Debug.Log($"[LazyLazy] 둘 다 귀찮아서 즉시 누워버림");
            pet1.agent.isStopped = true;
            pet2.agent.isStopped = true;
            StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Rest, restDuration * 1.5f, false, false));
            yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Rest, restDuration * 1.5f, false, false));
            
            // 잠시 쉬고 바로 단계5로 이동
            yield return new WaitForSeconds(1f);
            goto SkipToSeparate;
        }
        
        // 0단계: 매우 천천히 일정 거리까지만 접근
        // Debug.Log($"[LazyLazy] 단계0: 서로 매우 천천히 접근 (일정 거리 유지)");
        
        // Pet1이 Pet2에게 접근 (정확한 거리 유지)
        Vector3 direction1 = (pet2.transform.position - pet1.transform.position).normalized;
        Vector3 targetPosition1 = pet2.transform.position - direction1 * approachDistance;
        targetPosition1 = FindValidPositionOnNavMesh(targetPosition1, 10f);
        
        // Pet2가 Pet1에게 접근 (정확한 거리 유지)
        Vector3 direction2 = (pet1.transform.position - pet2.transform.position).normalized;
        Vector3 targetPosition2 = pet1.transform.position - direction2 * approachDistance;
        targetPosition2 = FindValidPositionOnNavMesh(targetPosition2, 10f);
        
        // 부드러운 회전 후 이동 시작
        StartCoroutine(SmoothMoveToPosition(pet1, targetPosition1, pet1.baseSpeed * 0.3f, PetAnimationController.PetAnimationType.Walk));
        yield return StartCoroutine(SmoothMoveToPosition(pet2, targetPosition2, pet2.baseSpeed * 0.3f, PetAnimationController.PetAnimationType.Walk));
        
        // 목표 위치 도달 대기
        float waitTime = 0f;
        float maxWaitTime = 5f;
        while (waitTime < maxWaitTime)
        {
            bool pet1Arrived = !pet1.agent.pathPending && pet1.agent.remainingDistance < 0.5f;
            bool pet2Arrived = !pet2.agent.pathPending && pet2.agent.remainingDistance < 0.5f;
            
            if (pet1Arrived && pet2Arrived)
            {
        // Debug.Log($"[LazyLazy] 둘 다 목표 위치에 도달!");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        // 1단계: 멈춰서 서로 마주보기
        // Debug.Log($"[LazyLazy] 단계1: 멈춰서 서로 마주보기");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 1.5f));
        yield return new WaitForSeconds(1f);
        
        // 2단계: Pet1이 먼저 누움
        // Debug.Log($"[LazyLazy] 단계2: {pet1.petName}이 먼저 누움");
        yield return StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, restDuration, false, false));
        
        // 3단계: Pet2도 누움
        // Debug.Log($"[LazyLazy] 단계3: {pet2.petName}도 따라서 누움");
        yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, restDuration, false, false));
        
        // 4단계: 잠시 쉬고 동시에 일어남
        // Debug.Log($"[LazyLazy] 단계4: 잠시 쉬다가 동시에 일어남");
        yield return new WaitForSeconds(1f);
        
        // 동시에 일어나는 애니메이션 (Idle로 자동 전환)
        // Rest 애니메이션이 끝나면 자동으로 Idle로 전환됨
        
        SkipToSeparate:
        // 5단계: 천천히 각자의 길로 헤어짐
        // Debug.Log($"[LazyLazy] 단계5: 천천히 각자의 길로 헤어짐");
        
        // 서로 반대 방향으로 천천히 이동
        Vector3 pet1Direction = (pet1.transform.position - pet2.transform.position).normalized;
        Vector3 pet2Direction = (pet2.transform.position - pet1.transform.position).normalized;
        
        Vector3 pet1Destination = pet1.transform.position + pet1Direction * separateDistance;
        Vector3 pet2Destination = pet2.transform.position + pet2Direction * separateDistance;
        
        pet1Destination = FindValidPositionOnNavMesh(pet1Destination, 10f);
        pet2Destination = FindValidPositionOnNavMesh(pet2Destination, 10f);
        
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.speed = pet1.baseSpeed * 0.4f;  // 천천히 헤어짐
        pet2.agent.speed = pet2.baseSpeed * 0.4f;
        pet1.agent.SetDestination(pet1Destination);
        pet2.agent.SetDestination(pet2Destination);
        pet1.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        pet2.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        yield return new WaitForSeconds(2f);
        
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        
        // Debug.Log($"<color=blue>[LazyLazy] 반응 완료</color>");
    }

    // ===== 2. Lazy + Shy =====
    private IEnumerator LazyShyReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        // Debug.Log($"[LazyShy] Lazy: {lazyPet.petName}, Shy: {shyPet.petName}");
        
        // 거리 설정
        float approachDistance = 4f;
        float retreatDistance = 5f;
        float skipApproachThreshold = 5f;  // 5미터 이내면 접근 스킵
        
        // 현재 거리 체크
        float currentDistance = Vector3.Distance(lazyPet.transform.position, shyPet.transform.position);
        // Debug.Log($"[LazyShy] 현재 거리: {currentDistance:F2}미터");

        if (currentDistance < skipApproachThreshold)
        {
            // 이미 가까이 있으면 바로 Shy가 놓라는 반응
        // Debug.Log($"[LazyShy] 이미 가까이 있음 - Shy가 바로 놓람!");

            // 서로 마주보기
            yield return StartCoroutine(SmoothlyLookAtEachOther(lazyPet, shyPet, 0.5f));
            yield return new WaitForSeconds(0.3f);
        }
        else
        {
            // 멀리 있으면 Lazy가 접근
        // Debug.Log($"[LazyShy] 단계0: Lazy가 천천히 접근");

            Vector3 direction = (shyPet.transform.position - lazyPet.transform.position).normalized;
            Vector3 targetPosition = shyPet.transform.position - direction * approachDistance;
            targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);

            // 부드러운 회전 후 이동
            yield return StartCoroutine(SmoothMoveToPosition(lazyPet, targetPosition, lazyPet.baseSpeed * 0.4f, PetAnimationController.PetAnimationType.Walk));

            // 접근 대기
            float waitTime = 0f;
            float maxWaitTime = 3f;
            while (waitTime < maxWaitTime)
            {
                if (!lazyPet.agent.pathPending && lazyPet.agent.remainingDistance < 0.5f)
                {
        // Debug.Log($"[LazyShy] Lazy가 목표 위치 도달");
                    break;
                }
                waitTime += Time.deltaTime;
                yield return null;
            }
             // 2단계: Lazy가 누움
        // Debug.Log($"[LazyShy] 단계2: {lazyPet.petName}이 피곤해서 누움");
        lazyPet.agent.isStopped = true;
        lazyPet.animationController.StopContinuousAnimation();
        yield return StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 1f, false, false));
        }
        
       
        
        // 3단계: Shy가 놀라서 도망
        // Debug.Log($"[LazyShy] 단계3: {shyPet.petName}이 놀라서 도망");
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));

        // NavMeshAgent 체크 후 이동
        if (SafeSetNavMeshAgent(shyPet, false)) {
            yield return StartCoroutine(QuickRetreat(shyPet, lazyPet.transform.position, retreatDistance, 1f));

            // 4단계: Shy가 멈춰서 돌아봄
            // Debug.Log($"[LazyShy] 단계4: {shyPet.petName}이 멈춰서 돌아봄");
            SafeSetNavMeshAgent(shyPet, true);
        }

        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, lazyPet, 0.5f));
        yield return new WaitForSeconds(1f);

        // 5단계: Shy가 조심스럽게 다시 접근
        // Debug.Log($"[LazyShy] 단계5: {shyPet.petName}이 조심스럽게 다시 접근");
        Vector3 sniffPoint = lazyPet.transform.position + lazyPet.transform.forward * 2f;
        sniffPoint = FindValidPositionOnNavMesh(sniffPoint, 5f);

        if (SafeSetNavMeshAgent(shyPet, false, shyPet.baseSpeed * 0.3f, sniffPoint)) {
            shyPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            yield return new WaitForSeconds(2f);
            SafeSetNavMeshAgent(shyPet, true);
        } else {
            yield return new WaitForSeconds(2f);
        }
        shyPet.animationController.StopContinuousAnimation();
        
        // 6단계: 냄새 맡기 동작
        // Debug.Log($"[LazyShy] 단계6: {shyPet.petName}이 냄새를 맡음");
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1f, false, false));
        
        // Debug.Log($"<color=blue>[LazyShy] 반응 완료</color>");
    }

    // ===== 3. Lazy + Brave =====
    private IEnumerator LazyBraveReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        // Debug.Log($"[LazyBrave] Lazy: {lazyPet.petName}, Brave: {bravePet.petName}");
        
        // 거리 설정
        float approachDistance = 3f;
        float circleRadius = 4f;
        float skipApproachThreshold = 5f;  // 5미터 이내면 접근 스킵
        
        // 현재 거리 체크
        float currentDistance = Vector3.Distance(lazyPet.transform.position, bravePet.transform.position);
        // Debug.Log($"[LazyBrave] 현재 거리: {currentDistance:F2}미터");
        
        if (currentDistance < skipApproachThreshold)
        {
            // 이미 가까이 있으면 Lazy는 바로 누워버리고 Brave가 당황
        // Debug.Log($"[LazyBrave] 이미 가까이 있음 - Lazy는 즉시 누워버림!");
            
            // 서로 마주보기
            yield return StartCoroutine(SmoothlyLookAtEachOther(lazyPet, bravePet, 0.5f));
            
            // Lazy는 바로 누워버림
        // Debug.Log($"[LazyBrave] {lazyPet.petName}은 귀찮아서 즉시 누움");
            lazyPet.agent.isStopped = true;
            StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Rest, 4f, false, false));
            
            // Brave는 당황해서 점프
        // Debug.Log($"[LazyBrave] {bravePet.petName}은 당황해서 점프");
            yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
            
            // 바로 단계3으로 이동
            goto SkipToCircle;
        }
        
        // 0단계: Brave가 빠르게 접근
        // Debug.Log($"[LazyBrave] 단계0: Brave가 당당하게 빠르게 접근");
        
        Vector3 direction = (lazyPet.transform.position - bravePet.transform.position).normalized;
        Vector3 targetPosition = lazyPet.transform.position - direction * approachDistance;
        targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);
        
        // 부드러운 회전 후 이동
        yield return StartCoroutine(SmoothMoveToPosition(bravePet, targetPosition, bravePet.baseSpeed * 1.5f, PetAnimationController.PetAnimationType.Run));
        
        // 2단계: Lazy는 무반응으로 누움
        // Debug.Log($"[LazyBrave] 단계2: {lazyPet.petName}은 귀찮아서 누움");
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
        // Debug.Log($"[LazyBrave] Brave가 도착");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        bravePet.agent.isStopped = true;
        bravePet.animationController.StopContinuousAnimation();
        
        // 1단계: 서로 마주보기
        // Debug.Log($"[LazyBrave] 단계1: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(lazyPet, bravePet, 0.5f));
        yield return new WaitForSeconds(0.5f);
        
        SkipToCircle:
        // 3단계: Brave가 Lazy 주위를 돔
        // Debug.Log($"[LazyBrave] 단계3: {bravePet.petName}이 주위를 돌며 살펴봄");
        yield return StartCoroutine(CircleAroundTarget(bravePet, lazyPet, circleRadius, 3f));
        
        // 4단계: Brave가 점프하며 자랑
        // Debug.Log($"[LazyBrave] 단계4: {bravePet.petName}이 점프하며 자랑");
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        
        // 5단계: Lazy는 계속 무시
        // Debug.Log($"[LazyBrave] 단계5: {lazyPet.petName}은 계속 무시");
        yield return new WaitForSeconds(1f);
        
        // 6단계: Brave가 흥미를 잃고 떠남
        // Debug.Log($"[LazyBrave] 단계6: {bravePet.petName}이 흥미를 잃고 떠남");
        bravePet.agent.isStopped = false;
        bravePet.agent.speed = bravePet.baseSpeed;
        
        // Debug.Log($"<color=blue>[LazyBrave] 반응 완료</color>");
    }

    // ===== 4. Lazy + Playful =====
    private IEnumerator LazyPlayfulReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        // Debug.Log($"[LazyPlayful] Lazy: {lazyPet.petName}, Playful: {playfulPet.petName}");
        
        // 거리 설정
        float approachDistance = 3f;
        float skipApproachThreshold = 5f;  // 5미터 이내면 접근 스킵
        
        // 현재 거리 체크
        float currentDistance = Vector3.Distance(lazyPet.transform.position, playfulPet.transform.position);
        // Debug.Log($"[LazyPlayful] 현재 거리: {currentDistance:F2}미터");
        
        if (currentDistance < skipApproachThreshold)
        {
            // 이미 가까이 있으면 Lazy는 바로 누워버리고 Playful이 신나서 점프
        // Debug.Log($"[LazyPlayful] 이미 가까이 있음 - 즉시 반응!");
            
            // 서로 마주보기
            yield return StartCoroutine(SmoothlyLookAtEachOther(lazyPet, playfulPet, 0.3f));
            
            // Lazy는 바로 누워버림
        // Debug.Log($"[LazyPlayful] {lazyPet.petName}은 귀찮아서 즉시 누움");
            lazyPet.agent.isStopped = true;
            StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Rest, 3f, false, false));
            
            // Playful은 신나서 연속 점프
        // Debug.Log($"[LazyPlayful] {playfulPet.petName}이 신나서 연속 점프!");
            for (int i = 0; i < 2; i++)
            {
                yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
                yield return new WaitForSeconds(0.2f);
            }
            
            // 바로 단계4로 이동
            goto SkipToCircle;
        }
        
        // 0단계: Playful이 신나게 접근
        // Debug.Log($"[LazyPlayful] 단계0: Playful이 신나게 접근");
        
        Vector3 direction = (lazyPet.transform.position - playfulPet.transform.position).normalized;
        Vector3 targetPosition = lazyPet.transform.position - direction * approachDistance;
        targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);
        
        // 부드러운 회전 후 이동
        yield return StartCoroutine(SmoothMoveToPosition(playfulPet, targetPosition, playfulPet.baseSpeed * 2f, PetAnimationController.PetAnimationType.Run));
        
        // 2단계: Lazy는 귀찮아서 누움
        // Debug.Log($"[LazyPlayful] 단계2: {lazyPet.petName}은 귀찮아서 누움");
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
        // Debug.Log($"[LazyPlayful] Playful이 도착");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        playfulPet.agent.isStopped = true;
        playfulPet.animationController.StopContinuousAnimation();
        
        // 1단계: 서로 마주보기
        // Debug.Log($"[LazyPlayful] 단계1: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(lazyPet, playfulPet, 0.5f));
        yield return new WaitForSeconds(0.5f);
        
        // 3단계: Playful이 연속 점프하며 놀자고 함
        // Debug.Log($"[LazyPlayful] 단계3: {playfulPet.petName}이 점프하며 놀자고 함 (3회)");
        for (int i = 0; i < 3; i++)
        {
            yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(0.3f);
        }
        
        SkipToCircle:
        // 4단계: Playful이 Lazy 주위를 빙빙 돔
        // Debug.Log($"[LazyPlayful] 단계4: {playfulPet.petName}이 주위를 돌며 놀자고 함");
        yield return StartCoroutine(CircleAroundTarget(playfulPet, lazyPet, 3f, 2f));
        
        // 5단계: Lazy는 계속 무시
        // Debug.Log($"[LazyPlayful] 단계5: {lazyPet.petName}은 계속 무시");
        yield return new WaitForSeconds(1f);
        
        // 6단계: Playful이 포기하고 실망하며 떠남
        // Debug.Log($"[LazyPlayful] 단계6: {playfulPet.petName}이 실망하며 떠남");
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1f, false, false));
        
        playfulPet.agent.isStopped = false;
        playfulPet.agent.speed = playfulPet.baseSpeed;
        
        // Debug.Log($"<color=blue>[LazyPlayful] 반응 완료</color>");
    }

    // ===== 5. Shy + Shy =====
    private IEnumerator ShyShyReaction(PetController pet1, PetController pet2)
    {
        // Debug.Log($"[ShyShy] Shy: {pet1.petName}, Shy: {pet2.petName}");
        
        // 거리 설정
        float retreatDistance = 5f;
        float finalFleeDistance = 8f;
        float skipApproachThreshold = 5f;  // 5미터 이내면 바로 놓람
        
        // 현재 거리 체크
        float currentDistance = Vector3.Distance(pet1.transform.position, pet2.transform.position);
        // Debug.Log($"[ShyShy] 현재 거리: {currentDistance:F2}미터");
        
        // 0단계: 서로 발견하고 멈춤
        // Debug.Log($"[ShyShy] 단계0: 서로 발견하고 멈춤");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        
        if (currentDistance < skipApproachThreshold)
        {
            // 이미 가까이 있으면 바로 놀라는 반응
        // Debug.Log($"[ShyShy] 이미 가까이 있음 - 둘 다 바로 놀람!");
            
            // 바로 3단계로: 둘 다 놀라서 점프
        // Debug.Log($"[ShyShy] 둘 다 깜짝 놀라서 점프!");
            StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
            yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
            
            // 바로 도망
            goto SkipToRetreat;
        }
        
        // 1단계: 조심스럽게 서로 쳐다보기
        // Debug.Log($"[ShyShy] 단계1: 조심스럽게 서로 쳐다보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 1f));
        
        // 2단계: 긴 정적
        // Debug.Log($"[ShyShy] 단계2: 긴 정적... (불안한 기다림)");
        // yield return new WaitForSeconds(pauseDuration * 2f);
        
        // 3단계: 둘 다 놀라서 점프
        // Debug.Log($"[ShyShy] 단계3: 둘 다 놀라서 점프");
        StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        
        SkipToRetreat:
        // 4단계: 동시에 뒤로 물러남
        // Debug.Log($"[ShyShy] 단계4: 동시에 뒤로 물러남");
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        
        // 동시에 도망 시작
        StartCoroutine(QuickRetreat(pet1, pet2.transform.position, retreatDistance, 1f));
        yield return StartCoroutine(QuickRetreat(pet2, pet1.transform.position, retreatDistance, 1f));
        
        // 5단계: 다시 돌아봄
        // Debug.Log($"[ShyShy] 단계5: 멈춰서 다시 돌아봄");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));
        yield return new WaitForSeconds(0.5f);
        
        // 6단계: 완전히 반대 방향으로 도망
        // Debug.Log($"[ShyShy] 단계6: 완전히 반대 방향으로 도망");
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
        
        // Debug.Log($"<color=blue>[ShyShy] 반응 완료</color>");
    }

    // ===== 6. Shy + Brave =====
    private IEnumerator ShyBraveReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        // Debug.Log($"[ShyBrave] Shy: {shyPet.petName}, Brave: {bravePet.petName}");
        
        // 거리 설정
        float approachDistance = 4f;
        float firstRetreatDistance = 5f;
        float finalFleeDistance = 10f;
        float skipApproachThreshold = 3f;  // 3미터 이내면 접근 스킵
        
        // 현재 거리 체크
        float currentDistance = Vector3.Distance(shyPet.transform.position, bravePet.transform.position);
        // Debug.Log($"[ShyBrave] 현재 거리: {currentDistance:F2}미터");
        
        if (currentDistance < skipApproachThreshold)
        {
            // 이미 가까이 있으면 Shy가 바로 놀람
        // Debug.Log($"[ShyBrave] 이미 가까이 있음 - Shy가 바로 놀람!");
            
            // 서로 마주보기
            yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, bravePet, 0.5f));
        }
        else
        {
            // 멀리 있으면 Brave가 접근
        // Debug.Log($"[ShyBrave] 단계0: Brave가 당당히 접근");
            
            Vector3 direction = (shyPet.transform.position - bravePet.transform.position).normalized;
            Vector3 targetPosition = shyPet.transform.position - direction * approachDistance;
            targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);
            
            // 부드러운 회전 후 이동
            yield return StartCoroutine(SmoothMoveToPosition(bravePet, targetPosition, bravePet.baseSpeed * 1.3f, PetAnimationController.PetAnimationType.Walk));
            
            // Shy가 불안해함
            yield return new WaitForSeconds(0.5f);
            
            // 1단계: 서로 마주보기
        // Debug.Log($"[ShyBrave] 단계1: 서로 마주보기");
            bravePet.agent.isStopped = true;
            bravePet.animationController.StopContinuousAnimation();
            yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, bravePet, 1f));
            yield return new WaitForSeconds(0.5f);
        }
        
        // 2단계: Shy가 놀라서 첫 번째 도망
        // Debug.Log($"[ShyBrave] 단계2: {shyPet.petName}이 놀라서 도망");
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false));
        
        if (SafeSetNavMeshAgent(shyPet, false)) {
            yield return StartCoroutine(QuickRetreat(shyPet, bravePet.transform.position, firstRetreatDistance, 1f));
        }

        // 3단계: Brave가 천천히 따라감
        // Debug.Log($"[ShyBrave] 단계3: {bravePet.petName}이 천천히 따라감");
        if (SafeSetNavMeshAgent(bravePet, false, bravePet.baseSpeed * 0.7f, shyPet.transform.position)) {
            bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        }

        yield return new WaitForSeconds(1.5f);

        // 4단계: Shy가 멈춰서 돌아봄
        // Debug.Log($"[ShyBrave] 단계4: {shyPet.petName}이 멈춰서 돌아봄");
        SafeSetNavMeshAgent(shyPet, true);
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, bravePet, 0.5f));

        // 5단계: Brave가 점프하며 인사
        // Debug.Log($"[ShyBrave] 단계5: {bravePet.petName}이 점프하며 인사");
        SafeSetNavMeshAgent(bravePet, true);
        bravePet.animationController.StopContinuousAnimation();
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        
        // 6단계: Shy가 완전히 도망
        // Debug.Log($"[ShyBrave] 단계6: {shyPet.petName}이 완전히 도망");
        Vector3 fleeDirection = (shyPet.transform.position - bravePet.transform.position).normalized;
        Vector3 fleePos = shyPet.transform.position + fleeDirection * finalFleeDistance;
        fleePos = FindValidPositionOnNavMesh(fleePos, 10f);
        
        if (SafeSetNavMeshAgent(shyPet, false, shyPet.baseSpeed * 2f, fleePos)) {
            shyPet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        }

        // 7단계: Brave가 잠시 쫓다가 포기
        // Debug.Log($"[ShyBrave] 단계7: {bravePet.petName}이 잠시 쫓다가 포기");
        if (SafeSetNavMeshAgent(bravePet, false, bravePet.baseSpeed * 1.5f, shyPet.transform.position)) {
            bravePet.animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        }

        yield return new WaitForSeconds(1f);

        SafeSetNavMeshAgent(bravePet, true);
        bravePet.animationController.StopContinuousAnimation();
        shyPet.animationController.StopContinuousAnimation();
        
        yield return new WaitForSeconds(0.5f);
        
        // Debug.Log($"<color=blue>[ShyBrave] 반응 완료</color>");
    }

    // ===== 7. Shy + Playful =====
    private IEnumerator ShyPlayfulReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        // Debug.Log($"[ShyPlayful] Shy: {shyPet.petName}, Playful: {playfulPet.petName}");
        
        // 거리 설정
        float approachDistance = 5f;  // Shy가 놀라는 거리 (더 멀리서 반응)
        float retreatDistance = 7f;   // 첫 번째 도망 거리
        float secondRetreatDistance = 10f; // 두 번째 도망 거리
        float skipApproachThreshold = 3f;  // 3미터 이내면 접근 스킵
        
        // 현재 거리 체크
        float currentDistance = Vector3.Distance(shyPet.transform.position, playfulPet.transform.position);
        // Debug.Log($"[ShyPlayful] 현재 거리: {currentDistance:F2}미터");
        
        if (currentDistance < skipApproachThreshold)
        {
            // 이미 가까이 있으면 Shy가 바로 놀람
        // Debug.Log($"[ShyPlayful] 이미 가까이 있음 - Shy가 바로 놀람!");
            
            // 서로 마주보기
            yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, playfulPet, 0.5f));
            yield return new WaitForSeconds(0.3f);
        }
        else
        {
            // 멀리 있으면 Playful이 접근
        // Debug.Log($"[ShyPlayful] 단계0: Playful이 Shy에게 접근 시작");
            
            // Shy의 위치 기준으로 접근 목표 설정 (정확한 거리)
            Vector3 direction = (shyPet.transform.position - playfulPet.transform.position).normalized;
            Vector3 targetPosition = shyPet.transform.position - direction * approachDistance;
            targetPosition = FindValidPositionOnNavMesh(targetPosition, 10f);
            
            // 부드러운 회전 후 이동
            yield return StartCoroutine(SmoothMoveToPosition(playfulPet, targetPosition, playfulPet.baseSpeed * 1.5f, PetAnimationController.PetAnimationType.Walk));
            
            // 목표 위치 도달 대기
            float waitTime = 0f;
            float maxWaitTime = 5f;
            while (waitTime < maxWaitTime)
            {
                // agent가 경로 계산 완료하고 남은 거리 체크
                if (!playfulPet.agent.pathPending && playfulPet.agent.remainingDistance < 0.5f)
                {
        // Debug.Log($"[ShyPlayful] Playful이 목표 위치에 도달!");
                    break;
                }
                waitTime += Time.deltaTime;
                yield return null;
            }
            
            // 1단계: 서로 마주보기
        // Debug.Log($"[ShyPlayful] 단계1: 서로 마주보기");
            yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, playfulPet, 1f));
            yield return new WaitForSeconds(1f);
        }

        // 2단계: Shy가 놀라서 도망 후 돌아보기
        // Debug.Log($"[ShyPlayful] 단계2: Shy가 깜짝 놀라서 도망");
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
        // Debug.Log($"[ShyPlayful] Shy가 멈춰서 Playful을 돌아봄");
        shyPet.agent.isStopped = true;
        yield return StartCoroutine(SmoothlyLookAtEachOther(shyPet, playfulPet, 0.5f));
        yield return new WaitForSeconds(0.5f);
        
        // 3단계: Playful이 점프하며 놀자고 함
        // Debug.Log($"[ShyPlayful] 단계3: Playful이 점프하며 놀자고 신호");
        playfulPet.agent.isStopped = false;
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 1f, true, false));
        
        // 4단계: Shy가 다시 놀라서 더 멀리 도망
        // Debug.Log($"[ShyPlayful] 단계4: Shy가 다시 놀라서 더 멀리 도망");
        shyPet.agent.isStopped = false;
        yield return StartCoroutine(QuickRetreat(shyPet, playfulPet.transform.position, secondRetreatDistance, 1.5f));
        
        // 5단계: Playful이 실망하며 고개 숙임
        // Debug.Log($"[ShyPlayful] 단계5: Playful이 실망하며 고개 숙임");
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 2f, false, false));
        
        // Debug.Log($"<color=blue>[ShyPlayful] 반응 완료</color>");
    }

    // ===== 8. Brave + Brave =====
    private IEnumerator BraveBraveReaction(PetController pet1, PetController pet2)
    {
        // Debug.Log($"[BraveBrave] Brave: {pet1.petName}, Brave: {pet2.petName}");
        
        // 거리 설정
        float meetDistance = 4f;
        float circleRadius = 5f;
        float raceDistance = 8f;
        float skipApproachThreshold = 5f;  // 5미터 이내면 접근 스킵
        
        // meetPoint를 미리 초기화 (가까이 있을 때도 사용 가능하도록)
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        meetPoint = FindValidPositionOnNavMesh(meetPoint, 10f);
        
        // 현재 거리 체크
        float currentDistance = Vector3.Distance(pet1.transform.position, pet2.transform.position);
        // Debug.Log($"[BraveBrave] 현재 거리: {currentDistance:F2}미터");
        
        if (currentDistance < skipApproachThreshold)
        {
            // 이미 가까이 있으면 둘 다 경계태세로 즉시 대치
        // Debug.Log($"[BraveBrave] 이미 가까이 있음 - 즉시 경계태세!");
            
            // 즉시 정면 대치
            pet1.agent.isStopped = true;
            pet2.agent.isStopped = true;
            yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.3f));
            
            // 둘 다 점프하며 경계
        // Debug.Log($"[BraveBrave] 둘 다 경계하며 점프!");
            StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.6f, true, false));
            yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.6f, true, false));
            
            yield return new WaitForSeconds(0.5f);
            
            // 바로 단계2로 이동
            goto SkipToCircle;
        }
        
        // 0단계: 빠르게 서로 접근
        // Debug.Log($"[BraveBrave] 단계0: 당당하게 빠르게 접근");
        
        // 부드러운 회전 후 이동 시작  
        StartCoroutine(SmoothMoveToPosition(pet1, meetPoint, pet1.baseSpeed * 1.5f, PetAnimationController.PetAnimationType.Run));
        yield return StartCoroutine(SmoothMoveToPosition(pet2, meetPoint, pet2.baseSpeed * 1.5f, PetAnimationController.PetAnimationType.Run));
        
        // 접근 대기
        float waitTime = 0f;
        float maxWaitTime = 2f;
        while (waitTime < maxWaitTime)
        {
            if (Vector3.Distance(pet1.transform.position, pet2.transform.position) < meetDistance)
            {
        // Debug.Log($"[BraveBrave] 충분히 가까워짐");
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        // 1단계: 정면 대치 (마주보기)
        // Debug.Log($"[BraveBrave] 단계1: 정면 대치");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 1f));
        yield return new WaitForSeconds(0.5f);
        
        // 2단계로 이어짐
        
        SkipToCircle:
        // 2단계: 서로 주위를 돔 (위엄 과시)
        // Debug.Log($"[BraveBrave] 단계2: 서로 주위를 돌며 위엄 과시");
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, circleRadius, 2.5f));
        
        // 3단계: 동시에 점프하며 자랑
        // Debug.Log($"[BraveBrave] 단계3: 동시에 점프하며 자랑");
        StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        
        // 4단계: 짧은 달리기 시합
        // Debug.Log($"[BraveBrave] 단계4: 짧은 달리기 시합");
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
        // Debug.Log($"[BraveBrave] 단계5: 서로 인정하고 헤어짐");
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        pet1.animationController.StopContinuousAnimation();
        pet2.animationController.StopContinuousAnimation();
        
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));
        
        // Debug.Log($"<color=blue>[BraveBrave] 반응 완료</color>");
    }

    // ===== 9. Brave + Playful =====
    private IEnumerator BravePlayfulReaction(PetController pet1, PetController pet2)
    {
        // 역할 구분
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        // Debug.Log($"[BravePlayful] Brave: {bravePet.petName}, Playful: {playfulPet.petName}");
        
        // 거리 설정
        float meetDistance = 3f;
        float circleRadius = 4f;
        float chaseDistance = 6f;
        float skipApproachThreshold = 5f;  // 5미터 이내면 접근 스킵
        
        // 현재 거리 체크
        float currentDistance = Vector3.Distance(bravePet.transform.position, playfulPet.transform.position);
        // Debug.Log($"[BravePlayful] 현재 거리: {currentDistance:F2}미터");
        
        if (currentDistance < skipApproachThreshold)
        {
            // 이미 가까이 있으면 바로 흥분해서 놀기 시작
        // Debug.Log($"[BravePlayful] 이미 가까이 있음 - 바로 놀기 시작!");
            
            // 서로 마주보기
            yield return StartCoroutine(SmoothlyLookAtEachOther(bravePet, playfulPet, 0.3f));
            
            // Playful이 먼저 신나서 점프
        // Debug.Log($"[BravePlayful] {playfulPet.petName}이 신나서 점프!");
            yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.5f, true, false));
            
            // Brave도 즉시 반응해서 점프
        // Debug.Log($"[BravePlayful] {bravePet.petName}도 즉시 반응!");
            yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 0.5f, true, false));
            
            // 바로 단계5로 이동 (추격전)
            goto SkipToChase;
        }
        
        // 0단계: 서로 마주보기
        // Debug.Log($"[BravePlayful] 단계0: 서로 마주보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(bravePet, playfulPet, 0.5f));
        yield return new WaitForSeconds(0.3f);
        
        // 1단계: 둘 다 빠르게 접근
        // Debug.Log($"[BravePlayful] 단계1: 둘 다 신나게 빠르게 접근");
        
        Vector3 meetPoint = (bravePet.transform.position + playfulPet.transform.position) / 2f;
        meetPoint = FindValidPositionOnNavMesh(meetPoint, 10f);
        
        // 부드러운 회전 후 이동 시작
        StartCoroutine(SmoothMoveToPosition(bravePet, meetPoint, bravePet.baseSpeed * 1.8f, PetAnimationController.PetAnimationType.Run));
        yield return StartCoroutine(SmoothMoveToPosition(playfulPet, meetPoint, playfulPet.baseSpeed * 1.8f, PetAnimationController.PetAnimationType.Run));
        
        // 접근 대기
        float waitTime = 0f;
        float maxWaitTime = 1.5f;
        while (waitTime < maxWaitTime)
        {
            if (Vector3.Distance(bravePet.transform.position, playfulPet.transform.position) < meetDistance)
            {
        // Debug.Log($"[BravePlayful] 충분히 가까워짐");
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
        // Debug.Log($"[BravePlayful] 단계2: 서로 주위를 빙빙 돔");
        yield return StartCoroutine(CircleAroundEachOther(bravePet, playfulPet, circleRadius, 2f));
        
        // 3단계: Playful이 먼저 점프
        // Debug.Log($"[BravePlayful] 단계3: {playfulPet.petName}이 신나서 점프");
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.6f, true, false));
        
        // 4단계: Brave도 따라서 점프
        // Debug.Log($"[BravePlayful] 단계4: {bravePet.petName}도 따라서 점프");
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.6f, true, false));
        
        SkipToChase:
        // 5단계: Playful이 도망가며 추격전 시작
        // Debug.Log($"[BravePlayful] 단계5: 짧은 추격전 시작");
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
        // Debug.Log($"[BravePlayful] 단계6: 역할 바꿔서 추격");
        Vector3 reverseChaseTarget = bravePet.transform.position - chaseDirection * chaseDistance;
        reverseChaseTarget = FindValidPositionOnNavMesh(reverseChaseTarget, 10f);
        
        bravePet.agent.SetDestination(reverseChaseTarget);
        yield return new WaitForSeconds(0.3f);
        playfulPet.agent.SetDestination(bravePet.transform.position);
        
        yield return new WaitForSeconds(1.5f);
        
        // 7단계: 만족하고 헤어짐
        // Debug.Log($"[BravePlayful] 단계7: 만족하고 헤어짐");
        bravePet.agent.isStopped = true;
        playfulPet.agent.isStopped = true;
        bravePet.animationController.StopContinuousAnimation();
        playfulPet.animationController.StopContinuousAnimation();
        
        // Debug.Log($"<color=blue>[BravePlayful] 반응 완료</color>");
    }

    // ===== 10. Playful + Playful =====
    private IEnumerator PlayfulPlayfulReaction(PetController pet1, PetController pet2)
    {
        // Debug.Log($"[PlayfulPlayful] Playful: {pet1.petName}, Playful: {pet2.petName}");
        
        // 거리 설정
        float meetDistance = 3f;
        float circleRadius = 4f;
        float chaseDistance = 7f;
        float skipApproachThreshold = 5f;  // 5미터 이내면 접근 스킵
        
        // 현재 거리 체크
        float currentDistance = Vector3.Distance(pet1.transform.position, pet2.transform.position);
        // Debug.Log($"[PlayfulPlayful] 현재 거리: {currentDistance:F2}미터");
        
        if (currentDistance < skipApproachThreshold)
        {
            // 이미 가까이 있으면 즉시 점프 파티 시작
        // Debug.Log($"[PlayfulPlayful] 이미 가까이 있음 - 즉시 점프 파티!");
            
            // 서로 마주보기
            yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.2f));
            
            // 즉시 동시 점프 파티 (5회)
        // Debug.Log($"[PlayfulPlayful] 즉시 신나게 점프 파티 (5회)!");
            for (int i = 0; i < 5; i++)
            {
                StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Jump, jumpInterval * 0.8f, true, false));
                yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Jump, jumpInterval * 0.8f, true, false));
                yield return new WaitForSeconds(0.1f);
            }
            
            // 바로 단계4로 이동 (추격전)
            goto SkipToChase;
        }
        
        // 0단계: 서로 마주보기
        // Debug.Log($"[PlayfulPlayful] 단계0: 서로 마주보고 흥분");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.3f));
        
        // 1단계: 신나게 달려옴
        // Debug.Log($"[PlayfulPlayful] 단계1: 둘 다 신나게 달려옴");
        
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        meetPoint = FindValidPositionOnNavMesh(meetPoint, 10f);
        
        // 부드러운 회전 후 이동 시작
        StartCoroutine(SmoothMoveToPosition(pet1, meetPoint, pet1.baseSpeed * 2f, PetAnimationController.PetAnimationType.Run));
        yield return StartCoroutine(SmoothMoveToPosition(pet2, meetPoint, pet2.baseSpeed * 2f, PetAnimationController.PetAnimationType.Run));
        
        // 접근 대기
        float waitTime = 0f;
        float maxWaitTime = 1f;
        while (waitTime < maxWaitTime)
        {
            if (Vector3.Distance(pet1.transform.position, pet2.transform.position) < meetDistance)
            {
        // Debug.Log($"[PlayfulPlayful] 충분히 가까워짐");
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
        // Debug.Log($"[PlayfulPlayful] 단계2: 서로 주위를 빠르게 돔");
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, circleRadius, 1.5f));
        
        // 3단계: 동시 점프 파티 (3회)
        // Debug.Log($"[PlayfulPlayful] 단계3: 신나게 점프 파티 (3회)");
        for (int i = 0; i < 3; i++)
        {
            StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(0.2f);
        }
        
        SkipToChase:
        // 4단계: Pet1이 도망가며 추격전 시작
        // Debug.Log($"[PlayfulPlayful] 단계4: {pet1.petName}이 도망가며 추격전 시작");
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
        // Debug.Log($"[PlayfulPlayful] 단계5: 역할 바꿔서 {pet2.petName}이 도망");
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
        // Debug.Log($"[PlayfulPlayful] 단계6: 다시 모여서 점프 파티 (2회)");
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
        // Debug.Log($"[PlayfulPlayful] 단계7: 마지막으로 한 바퀴 돌고 헤어짐");
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, circleRadius, 1f));
        
        // Debug.Log($"<color=blue>[PlayfulPlayful] 반응 완료</color>");
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
    /// 기본 반응 (조합이 없을 때)
    /// </summary>
    private IEnumerator DefaultReaction(PetController pet1, PetController pet2)
    {
        // Debug.Log($"[DefaultReaction] 기본 반응 실행");
        
        // Debug.Log($"[DefaultReaction] 단계1: 간단히 접근");
        // 간단히 접근 후 헤어짐
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        
        yield return new WaitForSeconds(2f);
        
        // Debug.Log($"[DefaultReaction] 단계2: 서로 쳐다보기");
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, lookDuration));
        
        yield return new WaitForSeconds(1f);
        
        // Debug.Log($"<color=blue>[DefaultReaction] 반응 완료</color>");
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
        // Debug.Log($"[ForceCompleteCleanup] 강제 정리 시작");
        
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
        
        // Debug.Log($"[ForceCompleteCleanup] 강제 정리 완료");
    }
    
    /// <summary>
    /// AI 재시작을 지연시키는 코루틴
    /// </summary>
    private IEnumerator DelayedAIRestart(PetController pet, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (pet?.AI != null)
        {
        // Debug.Log($"[DelayedAIRestart] {pet.petName}: AI 재시작");
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
        // Debug.Log($"[QuickRetreat] {pet.petName}이(가) 돌아서 도망 (거리: {distance}, 시간: {duration})");

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
        
        // Debug.Log($"[QuickRetreat] 목표 위치: {retreatTarget}, 현재 위치: {pet.transform.position}");
        
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
}