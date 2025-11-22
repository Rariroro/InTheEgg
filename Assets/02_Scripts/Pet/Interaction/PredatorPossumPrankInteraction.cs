// PredatorPossumPrankInteraction.cs (최적화 및 확장 버전)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;
public class PredatorPossumPrankInteraction : BasePetInteraction
{
    // 상호작용 이름 변경
    public override string InteractionName => "PredatorPossumPrank";

    // 우선순위를 설정하지 않아 기본값 50 사용

    [Header("Interaction Settings")]
    [Tooltip("포식자가 잠들 위치를 찾을 때의 반경입니다.")]
    public float sleepSpotRadius = 3f;
    [Tooltip("주머니쥐가 장난치기 위해 포식자에게 접근할 거리입니다.")]
    public float prankApproachDistance = 2f;
    [Tooltip("포식자가 잠드는 데 걸리는 시간입니다.")]
    public float predatorSleepDuration = 5f;
    [Tooltip("주머니쥐가 죽은 척을 유지하는 시간입니다.")]
    public float playDeadDuration = 8f;
    [Tooltip("포식자가 죽은 척하는 주머니쥐를 살펴보는 시간입니다.")]
    public float predatorInspectDuration = 3f;
    [Tooltip("포식자가 흥미를 잃고 떠날 때 이동할 거리입니다.")]
    public float predatorLeaveDistance = 15f;

    [Header("Animation Timings")]
    [Tooltip("주머니쥐가 장난치는 애니메이션 시간입니다.")]
    public float prankAnimationDuration = 2.5f;
    [Tooltip("포식자가 놀라서 깨어나는 애니메이션 시간입니다.")]
    public float wakeUpAnimationDuration = 1.5f;
    [Tooltip("포식자가 화내는 애니메이션 시간입니다.")]
    public float angryAnimationDuration = 2.0f;
    [Tooltip("주머니쥐가 일어나서 기뻐하는 애니메이션 시간입니다.")]
    public float celebrationDuration = 2.0f;

    [Header("Safety Settings")]
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;

    // 기본 위치 이동(중간 지점으로 모이기)을 하지 않고, 공격자가 직접 접근하도록 설정
    public override bool ShouldPerformInitialMovement => false;

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.ChaseAndRun; // 기존 유형 활용
    }

    /// <summary>
    /// 상호작용 가능 여부 확인 - 주머니쥐와 모든 육식동물
    /// </summary>
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 한 쪽이 주머니쥐인지 확인
        bool hasPossum = pet1.PetType == PetType.Possum || pet2.PetType == PetType.Possum;
        if (!hasPossum) return false;

        PetController otherPet = (pet1.PetType == PetType.Possum) ? pet2 : pet1;

        // 고기를 먹는 육식동물과의 상호작용 (Fish만 먹는 동물은 제외)
        bool isMeatEater = (otherPet.diet & PetAIProperties.DietaryFlags.Meat) != 0;
        return isMeatEater;
    }

    /// <summary>
    /// 상호작용의 전체 흐름을 관리하는 메인 코루틴
    /// </summary>
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 장난 상호작용 시작!");

        // 역할 식별
        PetController possum = (pet1.PetType == PetType.Possum) ? pet1 : pet2;
        PetController predator = (pet1.PetType == PetType.Possum) ? pet2 : pet1;

        // NavMeshAgent 준비 상태 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(possum, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(predator, agentSafetyTimeout));

        if (!IsAgentSafelyReady(possum) || !IsAgentSafelyReady(predator))
        {
            Debug.LogError($"[{InteractionName}] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(possum, predator);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState possumState = new PetOriginalState(possum);
        PetOriginalState predatorState = new PetOriginalState(predator);

        try
        {
            // 0. 위치 및 방향 준비 (거리 조정 + 서로 마주보기)
            yield return StartCoroutine(PreparePositionAndFacing(predator, possum));

            // 감정 표현
            predator.ShowEmotion(EmotionType.Hungry, 30f);
            possum.ShowEmotion(EmotionType.Scared, 30f);

            // 짧은 대기 (감정 표현이 보이도록)
            yield return new WaitForSeconds(0.5f);

            // 각 단계별 코루틴 순차 실행
            yield return StartCoroutine(PredatorAttackPhase(predator, possum));
            yield return StartCoroutine(PossumPlayDeadPhase(possum, predator));
            yield return StartCoroutine(PredatorInspectPhase(predator, possum));
            yield return StartCoroutine(PredatorLeavePhase(predator, possum));
            yield return StartCoroutine(PossumCelebratePhase(possum, predator));

            Debug.Log($"[{InteractionName}] {possum.petName}의 장난이 성공적으로 끝났습니다!");
        }
        finally
        {
            // 상호작용 종료 시 정리 작업
            Debug.Log($"[{InteractionName}] 상호작용 정리 시작.");
            EndInteraction(possum, predator); // BasePetInteraction의 정리 메서드 호출
            Debug.Log($"[{InteractionName}] 상호작용 정리 완료.");
        }
    }

    #region Interaction Phases

    /// <summary>
    /// 0단계: 초기 위치 및 방향 준비 (거리 조정 + 서로 마주보기)
    /// </summary>
    private IEnumerator PreparePositionAndFacing(PetController predator, PetController possum)
    {
        Debug.Log($"[{InteractionName}] 0단계: 위치 및 방향 준비 시작");

        // 1. 거리 계산
        float predatorMultiplier = predator.Profile.GetInteractionDistanceMultiplier();
        float possumMultiplier = possum.Profile.GetInteractionDistanceMultiplier();
        float averageMultiplier = (predatorMultiplier + possumMultiplier) / 2f;
        float targetDistance = prankApproachDistance * averageMultiplier;
        float currentDistance = Vector3.Distance(predator.transform.position, possum.transform.position);

        Debug.Log($"[{InteractionName}] 현재 거리: {currentDistance:F2}m, 목표 거리: {targetDistance:F2}m");

        // 2. 주머니쥐가 먼저 포식자를 바라보도록 설정
        Vector3 directionToPredator = (predator.transform.position - possum.transform.position).normalized;
        directionToPredator.y = 0;
        if (directionToPredator != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPredator);
            possum.transform.rotation = targetRotation;
            if (possum.petModelTransform != null)
            {
                possum.petModelTransform.rotation = possum.transform.rotation;
            }
        }

        // 주머니쥐는 제자리에서 경계
        possum.agent.isStopped = true;
        possum.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);

        // 3. 거리 조정 필요 여부 확인
        bool needsMovement = currentDistance < targetDistance * 0.8f || currentDistance > targetDistance * 1.5f;

        if (needsMovement)
        {
            Vector3 targetPosition;

            if (currentDistance < targetDistance * 0.8f)
            {
                // 너무 가까움 → 포식자를 뒤로 이동
                Debug.Log($"[{InteractionName}] 너무 가까움 → 포식자를 뒤로 {targetDistance:F2}m 거리로 이동");
                Vector3 directionAway = (predator.transform.position - possum.transform.position).normalized;
                if (directionAway == Vector3.zero)
                {
                    directionAway = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                }
                targetPosition = possum.transform.position + directionAway * targetDistance;
            }
            else
            {
                // 너무 멀음 → 포식자를 앞으로 이동
                Debug.Log($"[{InteractionName}] 너무 멀음 → 포식자를 앞으로 {targetDistance:F2}m 거리로 이동");
                targetPosition = possum.transform.position + possum.transform.forward * targetDistance;
            }

            // NavMesh 상 유효한 위치 찾기
            targetPosition = FindValidPositionOnNavMesh(targetPosition, targetDistance * 1.5f);

            // 포식자 이동 시작
            predator.agent.isStopped = false;
            predator.agent.speed = predator.baseSpeed * 1.2f;
            predator.agent.SetDestination(targetPosition);
            predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            // 이동 완료 대기
            float timer = 0f;
            float timeout = 10f;
            while (timer < timeout)
            {
                // 주머니쥐가 포식자를 부드럽게 계속 바라봄
                directionToPredator = (predator.transform.position - possum.transform.position).normalized;
                directionToPredator.y = 0;
                if (directionToPredator.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(directionToPredator);
                    possum.transform.rotation = Quaternion.Slerp(possum.transform.rotation, targetRot, Time.deltaTime * 5f);
                    if (possum.petModelTransform != null)
                    {
                        possum.petModelTransform.rotation = possum.transform.rotation;
                    }
                }

                // 도착 체크
                if (!predator.agent.pathPending && predator.agent.remainingDistance < 0.5f)
                {
                    Debug.Log($"[{InteractionName}] 위치 조정 완료");
                    break;
                }

                predator.HandleRotation();
                timer += Time.deltaTime;
                yield return null;
            }

            // 포식자 정지
            predator.agent.isStopped = true;
            predator.GetComponent<PetAnimationController>().StopContinuousAnimation();
            yield return new WaitForSeconds(0.2f);
        }
        else
        {
            Debug.Log($"[{InteractionName}] 거리 적절함 ({currentDistance:F2}m) → 이동 생략");
            predator.agent.isStopped = true;
        }

        // 4. 서로 정확히 마주보기
        Debug.Log($"[{InteractionName}] 서로 마주보기 시작");
        yield return StartCoroutine(SmoothlyLookAtEachOther(predator, possum, 1f));

        Debug.Log($"[{InteractionName}] 위치 및 방향 준비 완료");
    }

    /// <summary>
    /// 1단계: 포식자 공격 시도
    /// </summary>
    private IEnumerator PredatorAttackPhase(PetController predator, PetController possum)
    {
        Debug.Log($"[{InteractionName}] 1단계: {predator.petName}이(가) 공격을 시도합니다.");

        // 포식자 감정 변경
        predator.ShowEmotion(EmotionType.Hungry, 3f);

        // 공격 애니메이션
        var predatorAnim = predator.GetComponent<PetAnimationController>();
        yield return StartCoroutine(predatorAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Attack));

        // 주머니쥐 놀람 반응
        possum.ShowEmotion(EmotionType.Scared, 3f);
    }

    /// <summary>
    /// 2단계: 주머니쥐가 죽은 척 하는 단계
    /// </summary>
    private IEnumerator PossumPlayDeadPhase(PetController possum, PetController predator)
    {
        Debug.Log($"[{InteractionName}] 2단계: {possum.petName}이(가) 재빨리 죽은 척을 합니다!");

        // 주머니쥐 감정 변경
        possum.ShowEmotion(EmotionType.Scared, playDeadDuration + 5f);

        // 죽은 척 애니메이션
        var possumAnim = possum.GetComponent<PetAnimationController>();
        yield return StartCoroutine(possumAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Die));

        // 죽은 척 유지
        possumAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Die);
    }

    /// <summary>
    /// 3단계: 포식자가 주머니쥐를 살펴보는 단계
    /// </summary>
    private IEnumerator PredatorInspectPhase(PetController predator, PetController possum)
    {
        Debug.Log($"[{InteractionName}] 3단계: {predator.petName}이(가) {possum.petName}을(를) 살펴봅니다.");

        var predatorAnim = predator.GetComponent<PetAnimationController>();

        // 포식자 감정 변경
        predator.ShowEmotion(EmotionType.Scared, predatorInspectDuration + 4f);

        // 죽은 척하는 주머니쥐 쪽으로 시선 돌리기
        yield return StartCoroutine(SmoothlyLookAt(predator, possum.transform.position, 0.5f));

        // 더 가까이 다가가기
        Vector3 touchPosition = possum.transform.position - (possum.transform.position - predator.transform.position).normalized * 1.5f;
        predator.agent.SetDestination(FindValidPositionOnNavMesh(touchPosition, 2f));
        predator.agent.isStopped = false;
        predatorAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return StartCoroutine(WaitForMovement(predator, 3f));
        predatorAnim.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAt(predator, possum.transform.position, 0.3f));

        // 건드려보기
        Debug.Log($"[{InteractionName}] {predator.petName}이(가) {possum.petName}을(를) 툭 쳐보며 반응을 살핍니다.");
        yield return StartCoroutine(predatorAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1.5f, true, false));

        yield return new WaitForSeconds(1f);
    }

    /// <summary>
    /// 4단계: 포식자가 흥미를 잃고 떠나는 단계
    /// </summary>
    private IEnumerator PredatorLeavePhase(PetController predator, PetController possum)
    {
        Debug.Log($"[{InteractionName}] 4단계: {predator.petName}이(가) 흥미를 잃고 떠납니다.");

        var predatorAnim = predator.GetComponent<PetAnimationController>();

        // 포식자 감정 변경
        predator.ShowEmotion(EmotionType.Sad, 5f);
        yield return new WaitForSeconds(1f);

        // 멀리 떠나기
        Vector3 leaveDirection = (predator.transform.position - possum.transform.position).normalized;
        Vector3 leaveTarget = predator.transform.position + leaveDirection * predatorLeaveDistance;

        predator.agent.SetDestination(FindValidPositionOnNavMesh(leaveTarget, predatorLeaveDistance + 5f));
        predator.agent.isStopped = false;
        predatorAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        // 충분히 멀어질 때까지 대기
        yield return StartCoroutine(WaitForMovement(predator, 10f, 10f));
        predatorAnim.StopContinuousAnimation();
    }

    /// <summary>
    /// 5단계: 주머니쥐가 일어나서 기뻐하는 단계
    /// </summary>
    private IEnumerator PossumCelebratePhase(PetController possum, PetController predator)
    {
        Debug.Log($"[{InteractionName}] 5단계: {possum.petName}이(가) 일어나서 장난 성공을 자축합니다!");
        var possumAnim = possum.GetComponent<PetAnimationController>();
        var predatorAnim = predator.GetComponent<PetAnimationController>();

        // 주머니쥐가 '죽은 척' 애니메이션을 멈추고 일어납니다.
        possumAnim.StopContinuousAnimation();

        // 잠시 주변을 살피는 연출
        yield return StartCoroutine(SmoothlyLookAt(possum, possum.transform.position + possum.transform.right, 0.5f));
        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(SmoothlyLookAt(possum, possum.transform.position - possum.transform.right, 0.7f));
        yield return new WaitForSeconds(0.3f);
        
        // ★★★ 수정: 축하하기 전에 서로 마주보도록 변경합니다. ★★★
        Debug.Log($"[{InteractionName}] {possum.petName}과(와) {predator.petName}이(가) 서로 마주봅니다.");
        yield return StartCoroutine(SmoothlyLookAtEachOther(possum, predator, 1.0f));

        // 두 펫이 동시에 기뻐합니다.
        possum.ShowEmotion(EmotionType.Happy, celebrationDuration * 2);
        predator.ShowEmotion(EmotionType.Happy, celebrationDuration * 2);
        
        // 두 펫이 동시에 점프하며 축하합니다.
        StartCoroutine(possumAnim.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, celebrationDuration, true, false));
        StartCoroutine(predatorAnim.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, celebrationDuration, true, false));

        // 애니메이션이 끝날 때까지 대기
        yield return new WaitForSeconds(celebrationDuration + 0.5f);
    }

    #endregion

    #region Helper Coroutines

    /// <summary>
    /// 펫이 목적지까지 이동하기를 기다리는 헬퍼 코루틴
    /// </summary>
    private IEnumerator WaitForMovement(PetController pet, float timeout, float targetDistance = 0.5f)
    {
        float timer = 0f;
        while (timer < timeout)
        {
            if (!pet.agent.pathPending && pet.agent.remainingDistance <= targetDistance)
            {
                pet.agent.isStopped = true;
                yield break;
            }
            timer += Time.deltaTime;
            yield return null;
        }
        pet.agent.isStopped = true;
    }

    /// <summary>
    /// 펫이 목표 지점을 부드럽게 바라보게 하는 헬퍼 코루틴
    /// </summary>
    private IEnumerator SmoothlyLookAt(PetController pet, Vector3 target, float duration)
    {
        Quaternion startRotation = pet.transform.rotation;
        Vector3 direction = (target - pet.transform.position).normalized;
        direction.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        float timer = 0f;
        while (timer < duration)
        {
            pet.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, timer / duration);
            if (pet.petModelTransform) pet.petModelTransform.rotation = pet.transform.rotation;
            timer += Time.deltaTime;
            yield return null;
        }

        pet.transform.rotation = targetRotation;
        if (pet.petModelTransform) pet.petModelTransform.rotation = targetRotation;
    }

    /// <summary>
    /// NavMeshAgent가 안전하게 준비되었는지 확인하는 헬퍼 메서드
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }

    #endregion
}