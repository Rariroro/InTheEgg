// SkunkDefenseInteraction.cs (최적화된 버전)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;
public class SkunkDefenseInteraction : BasePetInteraction
{
    public override string InteractionName => "SkunkDefense";

    // 우선순위: 75 (6순위)
    public override int Priority => 75;

    [Header("거리 설정")]
    [Tooltip("상호작용 시 유지할 거리")]
    public float interactionDistance = 3f;

    [Tooltip("공격자가 접근하는 속도 배율")]
    public float attackerApproachSpeedMultiplier = 1.2f;

    [Tooltip("접근 시도 최대 시간")]
    public float approachTimeout = 8f;

    [Header("방어 설정")]
    [Tooltip("스컹크가 돌아서는 시간")]
    public float skunkTurnDuration = 0.5f;

    [Tooltip("스컹크 방어 애니메이션 시간")]
    public float skunkDefenseAnimationDuration = 2.0f;

    [Tooltip("공격자가 물러나는 거리")]
    public float attackerRetreatDistance = 10f;

    [Tooltip("공격자가 물러나는 속도 배율")]
    public float attackerRetreatSpeedMultiplier = 2.0f;

    [Header("도망 설정")]
    [Tooltip("스컹크가 도망가는 거리")]
    public float skunkEscapeDistance = 15f;

    [Tooltip("스컹크가 도망가는 속도 배율")]
    public float skunkEscapeSpeedMultiplier = 1.5f;

    [Tooltip("도망 시도 최대 시간")]
    public float escapeTimeout = 6f;

    [Header("시각 효과")]
    [Tooltip("스컹크 방구 파티클 프리팹")]
    public GameObject skunkSprayParticlePrefab;

    [Tooltip("파티클 생성 위치 오프셋 (스컹크 기준)")]
    public Vector3 particleOffset = new Vector3(0, 0.5f, -1f);

    [Tooltip("파티클 지속 시간")]
    public float particleDuration = 3f;

    [Header("감정 표현 설정")]
    [Tooltip("기본 감정 표현 지속 시간")]
    public float emotionDuration = 30f;

    [Header("안전 설정")]
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;

    // 기본 위치 이동(중간 지점으로 모이기)을 하지 않고, 공격자가 직접 접근하도록 설정
    public override bool ShouldPerformInitialMovement => false;

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.ChaseAndRun;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 한 쪽이 스컹크인지 확인
        bool hasSkunk = pet1.PetType == PetType.Skunk || pet2.PetType == PetType.Skunk;
        if (!hasSkunk) return false;

        PetController otherPet = (pet1.PetType == PetType.Skunk) ? pet2 : pet1;

        // 고기를 먹는 육식동물과의 상호작용 (Fish만 먹는 동물은 제외)
        bool isMeatEater = (otherPet.diet & PetAIProperties.DietaryFlags.Meat) != 0;
        return isMeatEater;
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 방어 상호작용 시작!");

        // 역할 식별
        PetController attacker = null;
        PetController skunk = null;

        if (pet1.PetType == PetType.Skunk)
        {
            skunk = pet1;
            attacker = pet2;
        }
        else if (pet2.PetType == PetType.Skunk)
        {
            skunk = pet2;
            attacker = pet1;
        }
        else
        {
            Debug.LogError($"[{InteractionName}] 스컹크가 없는 상호작용이 시작되었습니다.");
            yield break;
        }

        Debug.Log($"[{InteractionName}] 공격자: {attacker.petName}, 스컹크: {skunk.petName}");

        // NavMeshAgent 준비 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(attacker, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(skunk, agentSafetyTimeout));

        if (!IsAgentSafelyReady(attacker) || !IsAgentSafelyReady(skunk))
        {
            Debug.LogError($"[{InteractionName}] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(attacker, skunk);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState attackerState = new PetOriginalState(attacker);
        PetOriginalState skunkState = new PetOriginalState(skunk);

        try
        {
            // 0. 위치 및 방향 준비 (거리 조정 + 서로 마주보기)
            yield return StartCoroutine(PreparePositionAndFacing(attacker, skunk));

            // 감정 표현
            attacker.ShowEmotion(EmotionType.Hungry, emotionDuration);
            skunk.ShowEmotion(EmotionType.Surprised, emotionDuration);

            // 짧은 대기 (감정 표현이 보이도록)
            yield return new WaitForSeconds(0.5f);

            // 1. 공격 시도 단계
            yield return StartCoroutine(AttackAttemptPhase(attacker, skunk));

            // 2. 스컹크 방어 단계
            yield return StartCoroutine(SkunkDefensePhase(skunk, attacker));

            // 3. 공격자 후퇴 단계
            yield return StartCoroutine(AttackerRetreatPhase(attacker, skunk));

            // 4. 스컹크 도망 단계
            yield return StartCoroutine(SkunkEscapePhase(skunk, attacker));

            // 5. 승리 표현 단계
            yield return StartCoroutine(VictoryCelebrationPhase(skunk, attacker));

            Debug.Log($"[{InteractionName}] 상호작용 완료!");
        }
        finally
        {
            // 최종 정리
            Debug.Log($"[{InteractionName}] 상호작용 정리 시작.");

            // 원래 상태 복원
            attackerState.Restore(attacker);
            skunkState.Restore(skunk);

            // 애니메이션 정리
            attacker.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
            skunk.GetComponent<PetAnimationController>()?.StopContinuousAnimation();

            // 공통 종료 처리
            EndInteraction(attacker, skunk);
            Debug.Log($"[{InteractionName}] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 0단계: 초기 위치 및 방향 준비 (거리 조정 + 서로 마주보기)
    /// </summary>
    private IEnumerator PreparePositionAndFacing(PetController attacker, PetController skunk)
    {
        Debug.Log($"[{InteractionName}] 0단계: 위치 및 방향 준비 시작");

        // 1. 거리 계산 (한 번만)
        float attackerMultiplier = attacker.Profile.GetInteractionDistanceMultiplier();
        float skunkMultiplier = skunk.Profile.GetInteractionDistanceMultiplier();
        float averageMultiplier = (attackerMultiplier + skunkMultiplier) / 2f;
        float targetDistance = interactionDistance * averageMultiplier;
        float currentDistance = Vector3.Distance(attacker.transform.position, skunk.transform.position);

        Debug.Log($"[{InteractionName}] 현재 거리: {currentDistance:F2}m, 목표 거리: {targetDistance:F2}m");

        // 2. 스컹크가 먼저 공격자를 바라보도록 설정 (방향 기준 설정)
        Vector3 directionToAttacker = (attacker.transform.position - skunk.transform.position).normalized;
        directionToAttacker.y = 0;
        if (directionToAttacker != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToAttacker);
            skunk.transform.rotation = targetRotation;
            if (skunk.petModelTransform != null)
            {
                skunk.petModelTransform.rotation = skunk.transform.rotation;
            }
        }

        // 스컹크는 제자리에서 경계
        skunk.agent.isStopped = true;
        skunk.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);

        // 3. 거리 조정 필요 여부 확인
        bool needsMovement = currentDistance < targetDistance * 0.8f || currentDistance > targetDistance * 1.5f;

        if (needsMovement)
        {
            Vector3 targetPosition;

            if (currentDistance < targetDistance * 0.8f)
            {
                // 너무 가까움 → 공격자를 뒤로 이동
                Debug.Log($"[{InteractionName}] 너무 가까움 → 공격자를 뒤로 {targetDistance:F2}m 거리로 이동");

                Vector3 directionAway = (attacker.transform.position - skunk.transform.position).normalized;
                if (directionAway == Vector3.zero)
                {
                    // 위치가 겹쳤을 경우 랜덤 방향
                    directionAway = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                }
                targetPosition = skunk.transform.position + directionAway * targetDistance;
            }
            else
            {
                // 너무 멀음 → 공격자를 앞으로 이동
                Debug.Log($"[{InteractionName}] 너무 멀음 → 공격자를 앞으로 {targetDistance:F2}m 거리로 이동");

                // 스컹크 정면 기준으로 접근 (스컹크가 바라보는 방향의 반대편)
                targetPosition = skunk.transform.position + skunk.transform.forward * targetDistance;
            }

            // NavMesh 상 유효한 위치 찾기
            targetPosition = FindValidPositionOnNavMesh(targetPosition, targetDistance * 1.5f);

            // 공격자 이동 시작
            attacker.agent.isStopped = false;
            attacker.agent.speed = attacker.baseSpeed * attackerApproachSpeedMultiplier;
            attacker.agent.SetDestination(targetPosition);
            attacker.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            // 이동 완료 대기
            float timer = 0f;
            while (timer < approachTimeout)
            {
                // 스컹크가 공격자를 부드럽게 계속 바라봄
                directionToAttacker = (attacker.transform.position - skunk.transform.position).normalized;
                directionToAttacker.y = 0;
                if (directionToAttacker.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(directionToAttacker);
                    skunk.transform.rotation = Quaternion.Slerp(skunk.transform.rotation, targetRot, Time.deltaTime * 5f);
                    if (skunk.petModelTransform != null)
                    {
                        skunk.petModelTransform.rotation = skunk.transform.rotation;
                    }
                }

                // 도착 체크
                if (!attacker.agent.pathPending && attacker.agent.remainingDistance < 0.5f)
                {
                    Debug.Log($"[{InteractionName}] 위치 조정 완료");
                    break;
                }

                attacker.HandleRotation();
                timer += Time.deltaTime;
                yield return null;
            }

            // 공격자 정지
            attacker.agent.isStopped = true;
            attacker.GetComponent<PetAnimationController>().StopContinuousAnimation();

            yield return new WaitForSeconds(0.2f); // 짧은 대기
        }
        else
        {
            Debug.Log($"[{InteractionName}] 거리 적절함 ({currentDistance:F2}m) → 이동 생략");
            attacker.agent.isStopped = true;
        }

        // 4. 서로 정확히 마주보기
        Debug.Log($"[{InteractionName}] 서로 마주보기 시작");
        yield return StartCoroutine(SmoothlyLookAtEachOther(attacker, skunk, 1f));

        Debug.Log($"[{InteractionName}] 위치 및 방향 준비 완료");
    }

    /// <summary>
    /// 1단계: 공격 시도
    /// </summary>
    private IEnumerator AttackAttemptPhase(PetController attacker, PetController skunk)
    {
        Debug.Log($"[{InteractionName}] 1단계: {attacker.petName}이(가) 공격을 시도합니다.");

        // 공격자 감정 변경
        attacker.ShowEmotion(EmotionType.Hungry, 3f);

        // 공격 애니메이션 (한 번만 재생)
        var attackerAnim = attacker.GetComponent<PetAnimationController>();
        yield return StartCoroutine(attackerAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Attack));

        // 스컹크 놀람 반응
        skunk.ShowEmotion(EmotionType.Scared, 3f);
    }

    /// <summary>
    /// 2단계: 스컹크 방어
    /// </summary>
    private IEnumerator SkunkDefensePhase(PetController skunk, PetController attacker)
    {
        Debug.Log($"[{InteractionName}] 2단계: {skunk.petName}이(가) 방어태세를 취합니다!");

        // 스컹크 감정 변경
        skunk.ShowEmotion(EmotionType.Angry, 5f);

        var skunkAnim = skunk.GetComponent<PetAnimationController>();

        // 스컹크가 180도 회전 (꼬리를 공격자에게 향하도록)
        Vector3 directionToAttacker = (attacker.transform.position - skunk.transform.position).normalized;
        Quaternion originalRotation = skunk.transform.rotation;
        Quaternion backwardRotation = Quaternion.LookRotation(-directionToAttacker);

        // 부드럽게 뒤돌기
        float elapsedTime = 0f;
        while (elapsedTime < skunkTurnDuration)
        {
            float t = elapsedTime / skunkTurnDuration;
            skunk.transform.rotation = Quaternion.Slerp(originalRotation, backwardRotation, t);
            
            if (skunk.petModelTransform != null)
            {
                skunk.petModelTransform.rotation = skunk.transform.rotation;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 파티클 효과 생성
        if (skunkSprayParticlePrefab != null)
        {
            Vector3 particlePos = skunk.transform.position + skunk.transform.TransformDirection(particleOffset);
            // 스컹크가 바라보는 반대 방향(뒤쪽)으로 분사
            GameObject particles = Instantiate(skunkSprayParticlePrefab, particlePos,
                Quaternion.LookRotation(-skunk.transform.forward));
            Destroy(particles, particleDuration);
        }

        // 방어 애니메이션 (방구 뀌기 - 한 번만 재생)
        yield return StartCoroutine(skunkAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Attack));

        // 파티클이 충분히 퍼질 때까지 잠시 대기
        yield return new WaitForSeconds(2.0f);

        // 다시 원래 방향으로 돌아오기
        elapsedTime = 0f;
        while (elapsedTime < skunkTurnDuration)
        {
            float t = elapsedTime / skunkTurnDuration;
            skunk.transform.rotation = Quaternion.Slerp(backwardRotation, originalRotation, t);
            
            if (skunk.petModelTransform != null)
            {
                skunk.petModelTransform.rotation = skunk.transform.rotation;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }


    /// <summary>
    /// 3단계: 공격자 후퇴
    /// </summary>
    private IEnumerator AttackerRetreatPhase(PetController attacker, PetController skunk)
    {
        Debug.Log($"[{InteractionName}] 3단계: {attacker.petName}이(가) 놀라서 후퇴합니다!");

        // 공격자 감정 변경 - 스컹크 방구를 맞아서 어지럽고 역겨움
        attacker.ShowEmotion(EmotionType.Dizzy, 8f);

        // 후퇴 위치 계산
        Vector3 retreatDirection = (attacker.transform.position - skunk.transform.position).normalized;
        Vector3 retreatTarget = attacker.transform.position + retreatDirection * attackerRetreatDistance;
        retreatTarget = FindValidPositionOnNavMesh(retreatTarget, attackerRetreatDistance * 1.5f);

        // 어지러워하며 비틀거리며 후퇴
        attacker.agent.isStopped = false;
        attacker.agent.speed = attacker.baseSpeed * attackerRetreatSpeedMultiplier;
        attacker.agent.SetDestination(retreatTarget);
        attacker.GetComponent<PetAnimationController>().SetContinuousAnimation(
            PetAnimationController.PetAnimationType.Run);

        // 후퇴하면서 비틀거리는 효과
        float timer = 0f;
        float zigzagTimer = 0f;
        Vector3 originalForward = attacker.transform.forward;
        
        while (timer < 4f)
        {
            if (!attacker.agent.pathPending && attacker.agent.remainingDistance < 0.5f)
            {
                Debug.Log($"[{InteractionName}] {attacker.petName}이(가) 후퇴 완료!");
                break;
            }

            // 지그재그로 비틀거리는 효과 (어지러움 표현)
            zigzagTimer += Time.deltaTime * 3f;
            float zigzagOffset = Mathf.Sin(zigzagTimer) * 0.5f;
            Vector3 sideDirection = Vector3.Cross(retreatDirection, Vector3.up);
            Vector3 zigzagTarget = retreatTarget + sideDirection * zigzagOffset * 3f;
            
            // NavMesh 상의 유효한 위치 찾기
            if (NavMesh.SamplePosition(zigzagTarget, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                attacker.agent.SetDestination(hit.position);
            }

            attacker.HandleRotation();
            timer += Time.deltaTime;
            yield return null;
        }

        // 정지
        attacker.agent.isStopped = true;
        attacker.GetComponent<PetAnimationController>().StopContinuousAnimation();

        // 헥헥거리는 애니메이션
        yield return StartCoroutine(attacker.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 2f, false, false));
    }

    /// <summary>
    /// 4단계: 스컹크 도망
    /// </summary>
    private IEnumerator SkunkEscapePhase(PetController skunk, PetController attacker)
    {
        Debug.Log($"[{InteractionName}] 4단계: {skunk.petName}이(가) 안전한 곳으로 이동합니다.");

        // 스컹크 감정 변경 - 안심
        skunk.ShowEmotion(EmotionType.Happy, 10f);

        // 도망 방향 설정 (공격자 반대 방향)
        Vector3 escapeDirection = (skunk.transform.position - attacker.transform.position).normalized;
        // 약간의 랜덤성 추가
        escapeDirection += new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f));
        escapeDirection.Normalize();

        Vector3 escapeTarget = skunk.transform.position + escapeDirection * skunkEscapeDistance;
        escapeTarget = FindValidPositionOnNavMesh(escapeTarget, skunkEscapeDistance * 1.5f);

        // 이동 시작
        skunk.agent.isStopped = false;
        skunk.agent.speed = skunk.baseSpeed * skunkEscapeSpeedMultiplier;
        skunk.agent.SetDestination(escapeTarget);
        skunk.GetComponent<PetAnimationController>().SetContinuousAnimation(
            PetAnimationController.PetAnimationType.Walk);

        // 도망 완료 대기
        float timer = 0f;
        while (timer < escapeTimeout)
        {
            if (!skunk.agent.pathPending && skunk.agent.remainingDistance < 1f)
            {
                Debug.Log($"[{InteractionName}] {skunk.petName}이(가) 안전하게 도망쳤습니다.");
                break;
            }

            skunk.HandleRotation();
            timer += Time.deltaTime;
            yield return null;
        }

        // 정지
        skunk.agent.isStopped = true;
        skunk.GetComponent<PetAnimationController>().StopContinuousAnimation();
    }
    

    /// <summary>
    /// 5단계: 승리 표현
    /// </summary>
    private IEnumerator VictoryCelebrationPhase(PetController skunk, PetController attacker)
    {
        Debug.Log($"[{InteractionName}] 5단계: 승리의 기쁨!");

        // 공격자가 Rest 애니메이션 중이면 Idle로 전환
        var attackerAnim = attacker.GetComponent<PetAnimationController>();
        if (attackerAnim != null)
        {
            attackerAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            // 애니메이션 전환 대기
            yield return new WaitForSeconds(0.5f);
        }

        // 공격자를 향해 돌아보기
        yield return StartCoroutine(SmoothlyLookAtEachOther(skunk, attacker, 0.5f));

        // 감정 표현
        skunk.ShowEmotion(EmotionType.Victory, 5f);
        attacker.ShowEmotion(EmotionType.Defeat, 5f);

        // 스컹크 승리 점프
        var skunkAnim = skunk.GetComponent<PetAnimationController>();
        yield return StartCoroutine(skunkAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Jump));
        yield return StartCoroutine(attackerAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Eat));

        yield return new WaitForSeconds(1f);
    }

    /// <summary>
    /// NavMeshAgent가 안전하게 준비되었는지 확인
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }
}