// PredatorPossumPrankInteraction.cs (최적화 및 확장 버전)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class PredatorPossumPrankInteraction : BasePetInteraction
{
    // 상호작용 이름 변경
    public override string InteractionName => "PredatorPossumPrank";

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

        // 다른 한 쪽이 포식자인지 확인
        PetController otherPet = (pet1.PetType == PetType.Possum) ? pet2 : pet1;
        // 육식 또는 어식 식성을 가졌는지 확인
        bool isPredator = (otherPet.diet & (PetAIProperties.DietaryFlags.Meat | PetAIProperties.DietaryFlags.Fish)) != 0;

        // 포식자가 주머니쥐 자신인 경우는 제외
        if (otherPet.PetType == PetType.Possum) return false;

        return isPredator;
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
            // 각 단계별 코루틴 순차 실행
            yield return StartCoroutine(PredatorSleepPhase(predator, possum));
            yield return StartCoroutine(PossumApproachAndPrankPhase(possum, predator));
            yield return StartCoroutine(PredatorWakeAndReactPhase(predator));
            yield return StartCoroutine(PossumPlayDeadPhase(possum, predator));
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
    /// 1. 포식자가 잠드는 단계
    /// </summary>
   /// <summary>
    /// 1. 포식자가 잠드는 단계
    /// </summary>
    private IEnumerator PredatorSleepPhase(PetController predator, PetController possum)
    {
        Debug.Log($"[{InteractionName}] 1단계: {predator.petName}이(가) 잠들 준비를 합니다.");
        Vector3 sleepSpot = FindInteractionSpot(predator, possum, sleepSpotRadius);

        // 잠들 위치로 이동 (포식자만 이동)
        predator.agent.SetDestination(sleepSpot);
        predator.agent.isStopped = false;
        yield return StartCoroutine(WaitForMovement(predator, 5f));

        predator.ShowEmotion(EmotionType.Sleepy, 30f); // 오랫동안 졸린 감정 표시
        predator.agent.isStopped = true;

        // ★★★ 수정: 잠자는 애니메이션을 다음 행동 전까지 '계속' 재생하도록 변경 ★★★
        predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);

        Debug.Log($"[{InteractionName}] {predator.petName}이(가) 잠들었습니다.");
        // 애니메이션이 끝날 때까지 기다리지 않고 다음 단계로 바로 진행합니다.
    }

    /// <summary>
    /// 2. 주머니쥐가 접근하여 장난치는 단계
    /// </summary>
    private IEnumerator PossumApproachAndPrankPhase(PetController possum, PetController predator)
    {
        Debug.Log($"[{InteractionName}] 2단계: {possum.petName}이(가) 살금살금 접근합니다.");

        // 포식자 앞으로 이동
        Vector3 prankPosition = predator.transform.position + predator.transform.forward * prankApproachDistance;
        possum.agent.SetDestination(prankPosition);
        possum.agent.isStopped = false;
        possum.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        yield return StartCoroutine(WaitForMovement(possum, 8f));
        possum.GetComponent<PetAnimationController>().StopContinuousAnimation();
        
        // 장난치기
        Debug.Log($"[{InteractionName}] {possum.petName}이(가) {predator.petName}에게 장난을 칩니다.");
        yield return StartCoroutine(SmoothlyLookAt(possum, predator.transform.position, 0.5f));

        possum.ShowEmotion(EmotionType.Joke, prankAnimationDuration);
        yield return StartCoroutine(possum.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, prankAnimationDuration, true, false));
    }

    /// <summary>
    /// 3. 포식자가 깨어나 반응하는 단계
    /// </summary>
    private IEnumerator PredatorWakeAndReactPhase(PetController predator)
    {
        Debug.Log($"[{InteractionName}] 3단계: {predator.petName}이(가) 깜짝 놀라 깨어납니다!");
        var predatorAnim = predator.GetComponent<PetAnimationController>();

        // ★★★ 추가: 계속 재생 중이던 '잠자기' 애니메이션을 먼저 중지합니다. ★★★
        predatorAnim.StopContinuousAnimation();

        predator.ShowEmotion(EmotionType.Surprised, wakeUpAnimationDuration);
        yield return StartCoroutine(predatorAnim.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, wakeUpAnimationDuration, true, false));

        Debug.Log($"[{InteractionName}] {predator.petName}이(가) 화를 냅니다!");
        predator.ShowEmotion(EmotionType.Angry, angryAnimationDuration);
        yield return StartCoroutine(predatorAnim.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, angryAnimationDuration, true, false));
    }

    /// <summary>
    /// 4. 주머니쥐가 죽은 척 하는 단계
    /// </summary>
    private IEnumerator PossumPlayDeadPhase(PetController possum, PetController predator)
    {
        Debug.Log($"[{InteractionName}] 4단계: {possum.petName}이(가) 재빨리 죽은 척을 합니다!");
        // possum.ShowEmotion(EmotionType.Scared, playDeadDuration + 5f); // 감정 표현 시간 연장

        // ★★★ 수정: '죽은 척' 애니메이션을 중단 명령이 있을 때까지 계속 재생합니다.
        possum.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Die);

        // 이 코루틴은 즉시 종료되어, 주머니쥐가 죽은 척하는 동안 포식자가 다음 행동을 할 수 있게 합니다.
        yield return null;
    }

   /// <summary>
    /// 5. 포식자가 흥미를 잃고 떠나는 단계
    /// </summary>
    private IEnumerator PredatorLeavePhase(PetController predator, PetController possum)
    {
        Debug.Log($"[{InteractionName}] 5단계: {predator.petName}이(가) 주머니쥐를 살펴보다가 흥미를 잃습니다.");
        var predatorAnim = predator.GetComponent<PetAnimationController>();

        // 1. 죽은 척 하는 주머니쥐 쪽으로 시선 돌리기
        yield return StartCoroutine(SmoothlyLookAt(predator, possum.transform.position, 0.5f));
        predator.ShowEmotion(EmotionType.Scared, predatorInspectDuration + 4f);

        // ★★★ 추가: 더 가까이 다가가 건드려보는 행동 ★★★
        Vector3 touchPosition = possum.transform.position - (possum.transform.position - predator.transform.position).normalized * 1.5f;
        predator.agent.SetDestination(FindValidPositionOnNavMesh(touchPosition, 2f));
        predator.agent.isStopped = false;
        predatorAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        // 가까이 갈 때까지 대기
        yield return StartCoroutine(WaitForMovement(predator, 3f));
        predatorAnim.StopContinuousAnimation();
        yield return StartCoroutine(SmoothlyLookAt(predator, possum.transform.position, 0.3f));

        // 건드려보는 애니메이션 (Attack 애니메이션 활용)
        Debug.Log($"[{InteractionName}] {predator.petName}이(가) {possum.petName}을(를) 툭 쳐보며 반응을 살핍니다.");
        yield return StartCoroutine(predatorAnim.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 1.5f, true, false));
        // ★★★ 여기까지 추가 ★★★

        // 반응이 없자 슬퍼하며 떠나기
        Debug.Log($"[{InteractionName}] {predator.petName}이(가) 자리를 떠납니다.");
        predator.ShowEmotion(EmotionType.Sad, 5f);
        yield return new WaitForSeconds(1.0f); // 슬픈 감정 표현 시간

        // 멀리 떠나는 로직 (기존과 동일)
        Vector3 leaveDirection = (predator.transform.position - possum.transform.position).normalized;
        Vector3 leaveTarget = predator.transform.position + leaveDirection * predatorLeaveDistance;

        predator.agent.SetDestination(FindValidPositionOnNavMesh(leaveTarget, predatorLeaveDistance + 5f));
        predator.agent.isStopped = false;
        predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        // 포식자가 충분히 멀어질 때까지 대기
        yield return StartCoroutine(WaitForMovement(predator, 10f, 10f));
    }

   /// <summary>
    /// 6. 주머니쥐가 일어나서 기뻐하는 단계
    /// </summary>
    private IEnumerator PossumCelebratePhase(PetController possum, PetController predator)
    {
        Debug.Log($"[{InteractionName}] 6단계: {possum.petName}이(가) 일어나서 장난 성공을 자축합니다!");
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