// ChaseAndRunInteraction.cs (최적화된 버전)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class ChaseAndRunInteraction : BasePetInteraction
{
    public override string InteractionName => "ChaseAndRun";

    [Header("Chase Settings")]
    [Tooltip("추격 지속 시간")]
    public float chaseDuration = 20f;

    [Tooltip("쫓는 펫의 기본 속도 배율")]
    public float chaserBaseSpeedMultiplier = 1.7f;

    [Tooltip("쫓는 펫의 스프린트 속도 배율")]
    public float chaserSprintSpeedMultiplier = 2.8f;

    [Tooltip("도망가는 펫의 기본 속도 배율")]
    public float runnerBaseSpeedMultiplier = 1.9f;

    [Tooltip("도망가는 펫의 공포 속도 배율 (매우 가까울 때)")]
    public float runnerPanicSpeedMultiplier = 2.5f;

    [Header("Behavior Settings")]
    [Tooltip("매우 가까운 거리로 판단하는 기준")]
    public float panicDistance = 3f;

    [Tooltip("멀어진 거리로 판단하는 기준")]
    public float farDistance = 10f;

    [Tooltip("방향 변경 주기")]
    public float directionChangeInterval = 3f;

    [Tooltip("일반 업데이트 간격")]
    public float normalUpdateInterval = 0.3f;

    [Tooltip("근접 시 업데이트 간격")]
    public float closeUpdateInterval = 0.1f;

    [Tooltip("순간 가속 확률 (0~1)")]
    [Range(0f, 1f)]
    public float sprintChance = 0.05f;

    [Tooltip("순간 가속 지속 시간")]
    public float sprintDuration = 0.8f;

    [Header("End Chase Settings")]
    [Tooltip("추격 종료 후 쫓는 펫이 쉬는 시간")]
    public float chaserRestDuration = 3f;

    [Tooltip("추격 종료 후 도망가는 펫이 안전거리")]
    public float safeDistance = 10f;

    [Tooltip("추격 종료 후 최대 도망 시간")]
    public float maxEscapeTime = 5f;

    [Header("Safety Settings")]
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.ChaseAndRun;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;

        // 고양이와 개는 쫓고 쫓김
        if ((type1 == PetType.Cat && type2 == PetType.Dog) || 
            (type1 == PetType.Dog && type2 == PetType.Cat))
        {
            return true;
        }

        return false;
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[ChaseAndRun] {pet1.petName}와(과) {pet2.petName} 사이의 쫓고 쫓기기 상호작용 시작!");

        // 역할 결정
        PetController chaser = null;
        PetController runner = null;

        if (pet1.PetType == PetType.Dog && pet2.PetType == PetType.Cat)
        {
            chaser = pet1;
            runner = pet2;
        }
        else if (pet1.PetType == PetType.Cat && pet2.PetType == PetType.Dog)
        {
            chaser = pet2;
            runner = pet1;
        }
        else
        {
            // 기본값
            chaser = Random.value > 0.5f ? pet1 : pet2;
            runner = chaser == pet1 ? pet2 : pet1;
        }

        Debug.Log($"[ChaseAndRun] 쫓는 펫: {chaser.petName}, 도망가는 펫: {runner.petName}");

        // NavMeshAgent 준비 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(chaser, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(runner, agentSafetyTimeout));

        if (!IsAgentSafelyReady(chaser) || !IsAgentSafelyReady(runner))
        {
            Debug.LogError("[ChaseAndRun] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(chaser, runner);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState chaserState = new PetOriginalState(chaser);
        PetOriginalState runnerState = new PetOriginalState(runner);

        try
        {
            // 감정 표현
            chaser.ShowEmotion(EmotionType.Love, chaseDuration + 5f);
            runner.ShowEmotion(EmotionType.Angry, chaseDuration + 5f);

            // 1. 준비 단계
            yield return StartCoroutine(PreparePhase(chaser, runner));

            // 2. 추격 단계
            yield return StartCoroutine(ChasePhase(chaser, runner));

            // 3. 종료 단계
            yield return StartCoroutine(EndPhase(chaser, runner));
        }
        finally
        {
            Debug.Log("[ChaseAndRun] 상호작용 정리 시작.");

            // 원래 상태 복원
            chaserState.Restore(chaser);
            runnerState.Restore(runner);

            // 상호작용 종료
            EndInteraction(chaser, runner);
            Debug.Log("[ChaseAndRun] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 추격 시작 전 준비 단계
    /// </summary>
    private IEnumerator PreparePhase(PetController chaser, PetController runner)
    {
        Debug.Log("[ChaseAndRun] 1단계: 추격 준비");

        // 서로 마주보기
        yield return StartCoroutine(SmoothlyLookAtEachOther(chaser, runner, 0.5f));

        // 긴장감 조성
        yield return new WaitForSeconds(1.0f);

        // 애니메이션 설정
        chaser.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
        runner.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);

        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// 메인 추격 단계
    /// </summary>
    private IEnumerator ChasePhase(PetController chaser, PetController runner)
    {
        Debug.Log("[ChaseAndRun] 2단계: 추격 시작!");

        // 초기 설정
        chaser.agent.isStopped = false;
        runner.agent.isStopped = false;
        chaser.agent.updateRotation = true;
        runner.agent.updateRotation = true;

        // 가속도 증가
        chaser.agent.acceleration = chaser.baseAcceleration * 2f;
        runner.agent.acceleration = runner.baseAcceleration * 2f;

        // 애니메이션 설정
        var chaserAnim = chaser.GetComponent<PetAnimationController>();
        var runnerAnim = runner.GetComponent<PetAnimationController>();
        chaserAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        runnerAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);

        float chaseTimer = 0f;
        float lastDirectionChangeTime = 0f;
        int chasePhase = 0; // 0: 시작, 1: 추격 중, 2: 근접, 3: 멀어짐

        while (chaseTimer < chaseDuration)
        {
            float distance = Vector3.Distance(chaser.transform.position, runner.transform.position);
            float updateInterval = distance < panicDistance ? closeUpdateInterval : normalUpdateInterval;

            // 거리에 따른 속도 조정 및 단계 변경
            UpdateChasePhase(chaser, runner, distance, ref chasePhase);

            // 도망 방향 변경
            if (ShouldChangeDirection(distance, chaseTimer, lastDirectionChangeTime))
            {
                UpdateRunnerDestination(runner, chaser);
                lastDirectionChangeTime = chaseTimer;
            }

            // 쫓는 펫 목적지 업데이트
            chaser.agent.SetDestination(runner.transform.position);

            // 순간 가속 체크
            if (ShouldSprint(distance))
            {
                yield return StartCoroutine(PerformSprint(chaser));
            }

            // 회전 처리
            chaser.HandleRotation();
            runner.HandleRotation();

            chaseTimer += updateInterval;
            yield return new WaitForSeconds(updateInterval);
        }

        Debug.Log($"[ChaseAndRun] 추격 종료 (시간: {chaseDuration}초)");
    }

    /// <summary>
    /// 추격 종료 단계
    /// </summary>
    private IEnumerator EndPhase(PetController chaser, PetController runner)
    {
        Debug.Log("[ChaseAndRun] 3단계: 추격 종료");

        // 쫓던 펫 멈춤
        chaser.agent.isStopped = true;
        chaser.ShowEmotion(EmotionType.Sleepy, 10f);

        var chaserAnim = chaser.GetComponent<PetAnimationController>();
        yield return StartCoroutine(chaserAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, chaserRestDuration, false, false));

        // 도망가는 펫 계속 도망
        runner.ShowEmotion(EmotionType.Scared, 10f);
        
        Vector3 escapeDirection = (runner.transform.position - chaser.transform.position).normalized;
        Vector3 escapeTarget = runner.transform.position + escapeDirection * 20f;
        escapeTarget = FindValidPositionOnNavMesh(escapeTarget, 20f);
        
        runner.agent.SetDestination(escapeTarget);

        // 안전 거리까지 도망
        float escapeTimer = 0f;
        float initialDistance = Vector3.Distance(chaser.transform.position, runner.transform.position);
        float targetDistance = initialDistance + safeDistance;

        while (escapeTimer < maxEscapeTime)
        {
            float currentDistance = Vector3.Distance(chaser.transform.position, runner.transform.position);
            
            if (currentDistance >= targetDistance || 
                (!runner.agent.pathPending && runner.agent.remainingDistance <= runner.agent.stoppingDistance))
            {
                Debug.Log($"[ChaseAndRun] {runner.petName}이(가) 안전 거리 확보 (거리: {currentDistance})");
                break;
            }

            runner.HandleRotation();
            escapeTimer += Time.deltaTime;
            yield return null;
        }

        // 도망 펫 진정
        var runnerAnim = runner.GetComponent<PetAnimationController>();
        runnerAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        yield return new WaitForSeconds(1.5f);
        
        runnerAnim.StopContinuousAnimation();
        chaserAnim.StopContinuousAnimation();
    }

    /// <summary>
    /// 추격 단계 업데이트
    /// </summary>
    private void UpdateChasePhase(PetController chaser, PetController runner, float distance, ref int phase)
    {
        if (distance < panicDistance && phase != 2)
        {
            // 매우 가까움
            runner.agent.speed = runner.baseSpeed * runnerPanicSpeedMultiplier;
            chaser.agent.speed = chaser.baseSpeed * chaserBaseSpeedMultiplier;
            phase = 2;
            Debug.Log("[ChaseAndRun] 매우 가까워짐! 긴급 도망!");
        }
        else if (distance > farDistance && phase != 3)
        {
            // 멀어짐
            runner.agent.speed = runner.baseSpeed * runnerBaseSpeedMultiplier;
            chaser.agent.speed = chaser.baseSpeed * chaserSprintSpeedMultiplier;
            phase = 3;
            Debug.Log("[ChaseAndRun] 거리가 멀어짐! 추격 가속!");
        }
        else if (distance >= panicDistance && distance <= farDistance && phase != 1)
        {
            // 적정 거리
            runner.agent.speed = runner.baseSpeed * runnerBaseSpeedMultiplier;
            chaser.agent.speed = chaser.baseSpeed * chaserBaseSpeedMultiplier;
            phase = 1;
        }
    }

    /// <summary>
    /// 방향 변경 여부 결정
    /// </summary>
    private bool ShouldChangeDirection(float distance, float currentTime, float lastChangeTime)
    {
        // 정기적인 방향 변경
        if (currentTime - lastChangeTime > directionChangeInterval)
            return true;

        // 긴급 방향 변경 (50% 확률)
        if (distance < panicDistance * 0.8f && Random.value > 0.5f)
            return true;

        return false;
    }

    /// <summary>
    /// 도망 방향 업데이트
    /// </summary>
    private void UpdateRunnerDestination(PetController runner, PetController chaser)
    {
        Vector3 baseRunDirection = (runner.transform.position - chaser.transform.position).normalized;
        Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        randomOffset *= Random.Range(0.3f, 0.7f);
        
        Vector3 finalDirection = (baseRunDirection + randomOffset).normalized;
        Vector3 runTarget = runner.transform.position + finalDirection * Random.Range(10f, 20f);
        
        runTarget = FindValidPositionOnNavMesh(runTarget, 20f);
        runner.agent.SetDestination(runTarget);
        
        Debug.Log($"[ChaseAndRun] {runner.petName} 방향 변경");
    }

    /// <summary>
    /// 순간 가속 여부 결정
    /// </summary>
    private bool ShouldSprint(float distance)
    {
        return distance > panicDistance && distance < farDistance && Random.value < sprintChance;
    }

    /// <summary>
    /// 순간 가속 수행
    /// </summary>
    private IEnumerator PerformSprint(PetController chaser)
    {
        float originalSpeed = chaser.agent.speed;
        chaser.agent.speed = chaser.baseSpeed * chaserSprintSpeedMultiplier * 1.3f;
        
        Debug.Log($"[ChaseAndRun] {chaser.petName} 순간 가속!");
        
        yield return new WaitForSeconds(sprintDuration);
        
        chaser.agent.speed = originalSpeed;
    }

    /// <summary>
    /// NavMeshAgent 안전 체크
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }
}