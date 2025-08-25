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
        // NavMeshAgent 준비 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(pet1, 3f));
        yield return StartCoroutine(WaitUntilAgentIsReady(pet2, 3f));

        if (!pet1.agent.enabled || !pet2.agent.enabled)
        {
            EndInteraction(pet1, pet2);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);

        try
        {
            // 성격 조합 확인
            string combination = GetPersonalityCombination(pet1.personality, pet2.personality);
            Debug.Log($"[PersonalityReaction] {pet1.petName}({pet1.personality}) & {pet2.petName}({pet2.personality}) - {combination}");

            // 성격 조합에 따른 반응 실행
            yield return StartCoroutine(ExecuteReactionPattern(combination, pet1, pet2));
        }
        finally
        {
            // 원래 상태 복원
            pet1State.Restore(pet1);
            pet2State.Restore(pet2);
            
            // 상호작용 종료
            EndInteraction(pet1, pet2);
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
        // 서로 멈춤
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        
        // 서로 쳐다보기
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, lookDuration));
        
        // 천천히 서로 다가감
        float distance = CalculateDistanceBySize(pet1, pet2);
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.speed = pet1.baseSpeed * 0.5f; // 천천히
        pet2.agent.speed = pet2.baseSpeed * 0.5f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        
        yield return new WaitForSeconds(2f);
        
        // 한 펫이 누움
        yield return StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 3f, false, false));
        
        // 다른 펫도 누움 (동시에)
        StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 2f, false, false));
        
        yield return new WaitForSeconds(2f);
        
        // 일어나서 각자 길 감 (자동으로 Idle로 복귀)
    }

    // ===== 2. Lazy + Shy =====
    private IEnumerator LazyShyReaction(PetController pet1, PetController pet2)
    {
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        
        // 천천히 접근
        float distance = CalculateDistanceBySize(pet1, pet2);
        Vector3 approachPoint = shyPet.transform.position + (lazyPet.transform.position - shyPet.transform.position).normalized * distance;
        
        lazyPet.agent.speed = lazyPet.baseSpeed * 0.5f;
        lazyPet.agent.SetDestination(approachPoint);
        
        yield return new WaitForSeconds(1.5f);
        
        // Lazy는 누움
        lazyPet.agent.isStopped = true;
        StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 3f, false, false));
        
        // Shy는 뒷걸음질
        Vector3 backStep = shyPet.transform.position - lazyPet.transform.forward * 2f;
        shyPet.agent.SetDestination(backStep);
        yield return new WaitForSeconds(1f);
        
        // Shy가 조심스럽게 다시 접근
        shyPet.agent.speed = shyPet.baseSpeed * 0.3f;
        Vector3 sniffPoint = lazyPet.transform.position + lazyPet.transform.forward * 1.5f;
        shyPet.agent.SetDestination(sniffPoint);
        
        yield return new WaitForSeconds(2f);
        
        // 잠시 멈춤 (냄새 맡기 연출)
        shyPet.agent.isStopped = true;
        yield return new WaitForSeconds(1f);
        
        // 헤어짐
        // Lazy는 자동으로 Idle로 복귀
    }

    // ===== 3. Lazy + Brave =====
    private IEnumerator LazyBraveReaction(PetController pet1, PetController pet2)
    {
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        
        // Brave가 빠르게 접근
        float distance = CalculateDistanceBySize(pet1, pet2);
        bravePet.agent.speed = bravePet.baseSpeed * 1.5f;
        bravePet.agent.SetDestination(lazyPet.transform.position);
        
        // Lazy는 무반응으로 누움
        lazyPet.agent.isStopped = true;
        StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 4f, false, false));
        
        yield return new WaitForSeconds(2f);
        
        // Brave가 주위를 돔
        yield return StartCoroutine(CircleAroundTarget(bravePet, lazyPet, distance, 3f));
        
        // Lazy 계속 무시
        yield return new WaitForSeconds(1f);
        
        // Brave 흥미 잃고 떠남
        bravePet.agent.speed = bravePet.baseSpeed;
        // Lazy는 자동으로 Idle로 복귀
    }

    // ===== 4. Lazy + Playful =====
    private IEnumerator LazyPlayfulReaction(PetController pet1, PetController pet2)
    {
        PetController lazyPet = pet1.personality == PetTraits.Personality.Lazy ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        
        // Playful이 신나게 접근
        playfulPet.agent.speed = playfulPet.baseSpeed * 2f;
        playfulPet.agent.SetDestination(lazyPet.transform.position);
        
        yield return new WaitForSeconds(1f);
        
        // Lazy는 누움
        lazyPet.agent.isStopped = true;
        StartCoroutine(lazyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 3f, false, false));
        
        // Playful이 점프하며 놀자고 함
        for (int i = 0; i < 3; i++)
        {
            yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return new WaitForSeconds(0.3f);
        }
        
        // Lazy 무시
        yield return new WaitForSeconds(1f);
        
        // Playful 포기하고 떠남
        playfulPet.agent.speed = playfulPet.baseSpeed;
        // Lazy는 자동으로 Idle로 복귀
    }

    // ===== 5. Shy + Shy =====
    private IEnumerator ShyShyReaction(PetController pet1, PetController pet2)
    {
        // 서로 발견하고 멈춤
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        
        // 서로 쳐다보기
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, lookDuration));
        
        // 긴 정적
        yield return new WaitForSeconds(pauseDuration * 1.5f);
        
        // 동시에 뒷걸음
        Vector3 pet1Flee = pet1.transform.position - (pet2.transform.position - pet1.transform.position).normalized * 3f;
        Vector3 pet2Flee = pet2.transform.position - (pet1.transform.position - pet2.transform.position).normalized * 3f;
        
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.speed = pet1.baseSpeed * 0.8f;
        pet2.agent.speed = pet2.baseSpeed * 0.8f;
        
        pet1.agent.SetDestination(pet1Flee);
        pet2.agent.SetDestination(pet2Flee);
        
        yield return new WaitForSeconds(1f);
        
        // 서로 다른 방향으로 도망
        Vector3 pet1Run = pet1.transform.position + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * fleeDistance;
        Vector3 pet2Run = pet2.transform.position + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * fleeDistance;
        
        pet1.agent.speed = pet1.baseSpeed * 1.5f;
        pet2.agent.speed = pet2.baseSpeed * 1.5f;
        
        pet1.agent.SetDestination(pet1Run);
        pet2.agent.SetDestination(pet2Run);
        
        yield return new WaitForSeconds(2f);
    }

    // ===== 6. Shy + Brave =====
    private IEnumerator ShyBraveReaction(PetController pet1, PetController pet2)
    {
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        
        // Brave가 당당히 접근
        bravePet.agent.speed = bravePet.baseSpeed * 1.2f;
        bravePet.agent.SetDestination(shyPet.transform.position);
        
        yield return new WaitForSeconds(0.5f);
        
        // Shy는 뒷걸음
        Vector3 retreatPos = shyPet.transform.position - (bravePet.transform.position - shyPet.transform.position).normalized * 3f;
        shyPet.agent.SetDestination(retreatPos);
        
        yield return new WaitForSeconds(1f);
        
        // Brave가 천천히 따라감
        bravePet.agent.speed = bravePet.baseSpeed * 0.7f;
        bravePet.agent.SetDestination(shyPet.transform.position);
        
        yield return new WaitForSeconds(1.5f);
        
        // Shy 도망
        Vector3 fleePos = shyPet.transform.position + (shyPet.transform.position - bravePet.transform.position).normalized * fleeDistance;
        shyPet.agent.speed = shyPet.baseSpeed * 2f;
        shyPet.agent.SetDestination(fleePos);
        
        // Brave는 잠시 쫓다가 포기
        yield return new WaitForSeconds(1f);
        bravePet.agent.isStopped = true;
        
        yield return new WaitForSeconds(1f);
        bravePet.agent.isStopped = false;
    }

    // ===== 7. Shy + Playful =====
    private IEnumerator ShyPlayfulReaction(PetController pet1, PetController pet2)
    {
        PetController shyPet = pet1.personality == PetTraits.Personality.Shy ? pet1 : pet2;
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        
        // Playful이 뛰어옴
        playfulPet.agent.speed = playfulPet.baseSpeed * 2f;
        playfulPet.agent.SetDestination(shyPet.transform.position);
        
        yield return new WaitForSeconds(0.5f);
        
        // Shy 깜짝 놀라 뒷걸음
        yield return StartCoroutine(shyPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.3f, true, false)); // 놀람 표현
        
        Vector3 stepBack = shyPet.transform.position - playfulPet.transform.forward * 2f;
        shyPet.agent.SetDestination(stepBack);
        
        yield return new WaitForSeconds(1f);
        
        // Playful이 점프하며 놀자고 함
        for (int i = 0; i < 2; i++)
        {
            yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
        }
        
        // Shy 계속 뒷걸음
        Vector3 furtherBack = shyPet.transform.position - playfulPet.transform.forward * 3f;
        shyPet.agent.SetDestination(furtherBack);
        
        yield return new WaitForSeconds(1f);
        
        // Playful 혼자 놀다가 떠남
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 1f, true, false));
    }

    // ===== 8. Brave + Brave =====
    private IEnumerator BraveBraveReaction(PetController pet1, PetController pet2)
    {
        // 빠르게 접근
        float distance = CalculateDistanceBySize(pet1, pet2) * 1.5f;
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.speed = pet1.baseSpeed * 1.5f;
        pet2.agent.speed = pet2.baseSpeed * 1.5f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        
        yield return new WaitForSeconds(1.5f);
        
        // 서로 정면 대치
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, lookDuration));
        
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
        
        // 짧은 달리기 시합
        Vector3 raceDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        Vector3 raceTarget = meetPoint + raceDirection * 10f;
        
        pet1.agent.speed = pet1.baseSpeed * 2f;
        pet2.agent.speed = pet2.baseSpeed * 2f;
        
        pet1.agent.SetDestination(raceTarget);
        pet2.agent.SetDestination(raceTarget);
        
        yield return new WaitForSeconds(2f);
        
        // 서로 인정하고 헤어짐
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));
    }

    // ===== 9. Brave + Playful =====
    private IEnumerator BravePlayfulReaction(PetController pet1, PetController pet2)
    {
        // 둘 다 빠르게 접근
        float distance = CalculateDistanceBySize(pet1, pet2);
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.speed = pet1.baseSpeed * 1.8f;
        pet2.agent.speed = pet2.baseSpeed * 1.8f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        
        yield return new WaitForSeconds(1f);
        
        // 서로 주위를 빙빙 돔
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, distance, 2f));
        
        // Playful이 점프
        PetController playfulPet = pet1.personality == PetTraits.Personality.Playful ? pet1 : pet2;
        PetController bravePet = pet1.personality == PetTraits.Personality.Brave ? pet1 : pet2;
        
        yield return StartCoroutine(playfulPet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.5f, true, false));
        
        // Brave도 점프
        yield return StartCoroutine(bravePet.animationController.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 0.5f, true, false));
        
        // 짧은 추격전
        Vector3 chaseDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        
        playfulPet.agent.SetDestination(playfulPet.transform.position + chaseDirection * 5f);
        yield return new WaitForSeconds(0.3f);
        bravePet.agent.SetDestination(playfulPet.transform.position);
        
        yield return new WaitForSeconds(chaseDuration);
        
        // 만족하고 헤어짐
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
    }

    // ===== 10. Playful + Playful =====
    private IEnumerator PlayfulPlayfulReaction(PetController pet1, PetController pet2)
    {
        // 신나게 달려옴
        float distance = CalculateDistanceBySize(pet1, pet2);
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.speed = pet1.baseSpeed * 2f;
        pet2.agent.speed = pet2.baseSpeed * 2f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        
        yield return new WaitForSeconds(1f);
        
        // 서로 주위를 돔
        yield return StartCoroutine(CircleAroundEachOther(pet1, pet2, distance, 1.5f));
        
        // 연속 점프
        for (int i = 0; i < 3; i++)
        {
            StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
        }
        
        // 짧은 추격전
        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        
        pet1.agent.SetDestination(pet1.transform.position + randomDir * 5f);
        pet2.agent.SetDestination(pet1.transform.position);
        
        yield return new WaitForSeconds(1.5f);
        
        // 역할 바꿔서 추격
        pet2.agent.SetDestination(pet2.transform.position - randomDir * 5f);
        pet1.agent.SetDestination(pet2.transform.position);
        
        yield return new WaitForSeconds(1.5f);
        
        // 다시 점프 파티
        for (int i = 0; i < 2; i++)
        {
            StartCoroutine(pet1.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
            yield return StartCoroutine(pet2.animationController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, jumpInterval, true, false));
        }
        
        // 신나게 놀다 헤어짐
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;
    }

    // ===== 헬퍼 메서드들 =====

    /// <summary>
    /// 기본 반응 (조합이 없을 때)
    /// </summary>
    private IEnumerator DefaultReaction(PetController pet1, PetController pet2)
    {
        // 간단히 접근 후 헤어짐
        float distance = CalculateDistanceBySize(pet1, pet2);
        Vector3 meetPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        
        pet1.agent.SetDestination(meetPoint);
        pet2.agent.SetDestination(meetPoint);
        
        yield return new WaitForSeconds(2f);
        
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, lookDuration));
        
        yield return new WaitForSeconds(1f);
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
}