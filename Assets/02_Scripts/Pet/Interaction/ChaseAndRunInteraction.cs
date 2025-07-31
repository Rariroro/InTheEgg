// ChaseAndRunInteraction.cs (개선된 버전)
using System.Collections;
using System.Collections.Generic;
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

    [Header("새로운 재미 요소")]
    [Tooltip("180도 회전 페이크 확률")]
    [Range(0f, 1f)]
    public float uturnChance = 0.1f;

    [Tooltip("정지 페이크 확률")]
    [Range(0f, 1f)]
    public float stopFakeChance = 0.15f;

    [Tooltip("예측 샷 시도 확률")]
    [Range(0f, 1f)]
    public float predictShotChance = 0.2f;

    [Tooltip("잡기 성공 거리")]
    public float catchDistance = 1.5f;

    [Tooltip("지구력 감소 속도")]
    public float staminaDecreaseRate = 0.05f;

    [Tooltip("지구력 회복 속도")]
    public float staminaRecoveryRate = 0.1f;

    [Tooltip("최소 속도 배율 (지쳤을 때)")]
    public float minSpeedMultiplier = 0.5f;

    [Header("Visual Effects")]
    [Tooltip("먼지 파티클 프리팹")]
    public GameObject dustParticlePrefab;

    [Tooltip("잡기 성공 파티클 프리팹")]
    public GameObject catchParticlePrefab;

    [Tooltip("주변 펫 놀람 반경")]
    public float scareRadius = 5f;

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

    // 지구력 추적
    private float chaserStamina = 1f;
    private float runnerStamina = 1f;

    // 먼지 파티클 인스턴스
    private GameObject chaserDustParticles;
    private GameObject runnerDustParticles;

    // 추격 성공 여부
    private bool chaseCaught = false;

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
        // Debug.Log($"[ChaseAndRun] {pet1.petName}와(과) {pet2.petName} 사이의 쫓고 쫓기기 상호작용 시작!");

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

        // Debug.Log($"[ChaseAndRun] 쫓는 펫: {chaser.petName}, 도망가는 펫: {runner.petName}");

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

        // 초기화
        chaserStamina = 1f;
        runnerStamina = 1f;
        chaseCaught = false;

        try
        {
            // 감정 표현
            chaser.ShowEmotion(EmotionType.Love, chaseDuration + 5f);
            runner.ShowEmotion(EmotionType.Surprised, chaseDuration + 5f);

            // 1. 준비 단계
            yield return StartCoroutine(PreparePhase(chaser, runner));

            // 2. 추격 단계
            yield return StartCoroutine(ChasePhase(chaser, runner));

            // 3. 종료 단계
            if (chaseCaught)
            {
                yield return StartCoroutine(CaughtPhase(chaser, runner));
            }
            else
            {
                yield return StartCoroutine(EscapeSuccessPhase(chaser, runner));
            }
        }
        finally
        {
            // Debug.Log("[ChaseAndRun] 상호작용 정리 시작.");

            // 파티클 정리
            if (chaserDustParticles != null) Destroy(chaserDustParticles);
            if (runnerDustParticles != null) Destroy(runnerDustParticles);

            // 원래 상태 복원
            chaserState.Restore(chaser);
            runnerState.Restore(runner);

            // 상호작용 종료
            EndInteraction(chaser, runner);
            // Debug.Log("[ChaseAndRun] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 추격 시작 전 준비 단계
    /// </summary>
    private IEnumerator PreparePhase(PetController chaser, PetController runner)
    {
        // Debug.Log("[ChaseAndRun] 1단계: 추격 준비");

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
        // Debug.Log("[ChaseAndRun] 2단계: 추격 시작!");

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

        // 먼지 파티클 생성
        CreateDustParticles(chaser, runner);

        float chaseTimer = 0f;
        float lastDirectionChangeTime = 0f;
        float lastSpecialMoveTime = 0f;
        int chasePhase = 0;

        while (chaseTimer < chaseDuration && !chaseCaught)
        {
            float distance = Vector3.Distance(chaser.transform.position, runner.transform.position);
            float updateInterval = distance < panicDistance ? closeUpdateInterval : normalUpdateInterval;

            // 지구력 업데이트
            UpdateStamina(chaser, runner, Time.deltaTime);

            // 속도 조정 (지구력 반영)
            UpdateChasePhase(chaser, runner, distance, ref chasePhase);
            
            // 속도에 따른 애니메이션 업데이트
            UpdateChaseAnimations(chaser, runner, chaserAnim, runnerAnim);

            // 잡기 체크
            if (distance <= catchDistance)
            {
                chaseCaught = true;
                // Debug.Log($"[ChaseAndRun] {chaser.petName}이(가) {runner.petName}을(를) 잡았습니다!");
                break;
            }

            // 특별 동작들
            if (chaseTimer - lastSpecialMoveTime > 2f) // 2초마다 특별 동작 가능
            {
                // 도망자의 특별 동작
                if (Random.value < uturnChance && distance > catchDistance * 2)
                {
                    yield return StartCoroutine(PerformUTurn(runner, chaser));
                    lastSpecialMoveTime = chaseTimer;
                }
                else if (Random.value < stopFakeChance && distance > catchDistance * 3)
                {
                    yield return StartCoroutine(PerformStopFake(runner, chaser));
                    lastSpecialMoveTime = chaseTimer;
                }

                // 추격자의 예측 샷
                if (Random.value < predictShotChance && distance < farDistance)
                {
                    yield return StartCoroutine(PerformPredictShot(chaser, runner));
                    lastSpecialMoveTime = chaseTimer;
                }
            }

            // 일반 도망 방향 변경
            if (ShouldChangeDirection(distance, chaseTimer, lastDirectionChangeTime))
            {
                UpdateRunnerDestination(runner, chaser);
                lastDirectionChangeTime = chaseTimer;
            }

            // 쫓는 펫 목적지 업데이트
            if (!chaser.agent.pathPending)
            {
                chaser.agent.SetDestination(runner.transform.position);
            }

            // 주변 펫들 놀라게 하기
            ScareNearbyPets(runner, scareRadius);

            // 회전 처리
            chaser.HandleRotation();
            runner.HandleRotation();

            chaseTimer += updateInterval;
            yield return new WaitForSeconds(updateInterval);
        }

        // Debug.Log($"[ChaseAndRun] 추격 종료 (시간: {chaseTimer}초, 잡기 성공: {chaseCaught})");
    }

    /// <summary>
    /// 180도 회전 페이크
    /// </summary>
    private IEnumerator PerformUTurn(PetController runner, PetController chaser)
    {
        // Debug.Log($"[ChaseAndRun] {runner.petName}이(가) 180도 회전 페이크!");
        
        // 감정 표현
        runner.ShowEmotion(EmotionType.Confused, 2f);
        
        // 현재 방향의 반대로 급회전
        Vector3 currentDirection = (runner.transform.position - chaser.transform.position).normalized;
        Vector3 oppositeDirection = -currentDirection;
        Vector3 uTurnTarget = runner.transform.position + oppositeDirection * 10f;
        
        uTurnTarget = FindValidPositionOnNavMesh(uTurnTarget, 15f);
        runner.agent.SetDestination(uTurnTarget);
        
        // 급회전 애니메이션
        runner.agent.angularSpeed = runner.baseAngularSpeed * 3f;
        yield return new WaitForSeconds(0.5f);
        runner.agent.angularSpeed = runner.baseAngularSpeed;
          // 먼지 파티클 생성
        CreateDustParticles(chaser, runner);
    }

    /// <summary>
    /// 정지 페이크
    /// </summary>
    private IEnumerator PerformStopFake(PetController runner, PetController chaser)
    {
        // Debug.Log($"[ChaseAndRun] {runner.petName}이(가) 정지 페이크!");
        
        // 속도를 서서히 줄이면서 멈춤
        float originalSpeed = runner.agent.speed;
        float slowDownTime = 0.3f;
        float elapsedTime = 0f;
        
        // 감속 과정
        while (elapsedTime < slowDownTime)
        {
            runner.agent.speed = Mathf.Lerp(originalSpeed, 0f, elapsedTime / slowDownTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 완전히 멈춤
        runner.agent.isStopped = true;
        runner.agent.velocity = Vector3.zero;
        
        // 애니메이션은 UpdateChaseAnimations가 자동으로 처리
        CreateDustParticles(chaser, runner);

        // 추격자 혼란
        chaser.ShowEmotion(EmotionType.Love, 2f);
        
        yield return new WaitForSeconds(0.8f);
        
        // 다시 도망 - 가속
        runner.agent.isStopped = false;
        runner.ShowEmotion(EmotionType.Confused, 2f);
        
        // 속도를 서서히 증가
        float accelerateTime = 0.5f;
        elapsedTime = 0f;
        float targetSpeed = runner.baseSpeed * runnerBaseSpeedMultiplier * GetStaminaSpeedMultiplier(runnerStamina);
        
        while (elapsedTime < accelerateTime)
        {
            runner.agent.speed = Mathf.Lerp(0f, targetSpeed, elapsedTime / accelerateTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        runner.agent.speed = targetSpeed;
        
        // 새로운 방향으로 도망
        UpdateRunnerDestination(runner, chaser);
    }

    /// <summary>
    /// 예측 샷 시도
    /// </summary>
    private IEnumerator PerformPredictShot(PetController chaser, PetController runner)
    {
        // Debug.Log($"[ChaseAndRun] {chaser.petName}이(가) 예측 샷 시도!");
        
        // 도망자의 예상 위치 계산
        Vector3 runnerVelocity = runner.agent.velocity;
        float predictTime = 1.5f;
        Vector3 predictedPosition = runner.transform.position + runnerVelocity * predictTime;
        
        predictedPosition = FindValidPositionOnNavMesh(predictedPosition, 20f);
        
        // 예측 위치로 이동
        chaser.agent.SetDestination(predictedPosition);
        chaser.agent.speed = chaser.baseSpeed * chaserSprintSpeedMultiplier * 1.5f; // 더 빠르게
                // CreateDustParticles(chaser, runner);

        yield return new WaitForSeconds(1.5f);
        
        // 대부분 실패
        if (Random.value > 0.2f) // 80% 실패
        {
            // Debug.Log($"[ChaseAndRun] 예측 샷 실패!");
            chaser.ShowEmotion(EmotionType.Love, 2f);
        }
        
        // 속도 원래대로
        chaser.agent.speed = chaser.baseSpeed * GetStaminaSpeedMultiplier(chaserStamina) * chaserBaseSpeedMultiplier;
    }

    /// <summary>
    /// 지구력 업데이트
    /// </summary>
    private void UpdateStamina(PetController chaser, PetController runner, float deltaTime)
    {
        // 달리면 지구력 감소
        chaserStamina = Mathf.Max(0f, chaserStamina - staminaDecreaseRate * deltaTime);
        runnerStamina = Mathf.Max(0f, runnerStamina - staminaDecreaseRate * deltaTime);
        
        // 지쳐서 헐떡거리는 감정
        if (chaserStamina < 0.3f && Random.value < 0.01f)
        {
            chaser.ShowEmotion(EmotionType.Sleepy, 2f);
        }
        if (runnerStamina < 0.3f && Random.value < 0.01f)
        {
            runner.ShowEmotion(EmotionType.Sleepy, 2f);
        }
    }

    /// <summary>
    /// 지구력에 따른 속도 배율 계산
    /// </summary>
    private float GetStaminaSpeedMultiplier(float stamina)
    {
        return Mathf.Lerp(minSpeedMultiplier, 1f, stamina);
    }

    /// <summary>
    /// 추격 단계 업데이트 (지구력 반영)
    /// </summary>
    private void UpdateChasePhase(PetController chaser, PetController runner, float distance, ref int phase)
    {
        float chaserStaminaMult = GetStaminaSpeedMultiplier(chaserStamina);
        float runnerStaminaMult = GetStaminaSpeedMultiplier(runnerStamina);
        
        if (distance < panicDistance && phase != 2)
        {
            // 매우 가까움
            runner.agent.speed = runner.baseSpeed * runnerPanicSpeedMultiplier * runnerStaminaMult;
            chaser.agent.speed = chaser.baseSpeed * chaserBaseSpeedMultiplier * chaserStaminaMult;
            phase = 2;
            // Debug.Log("[ChaseAndRun] 매우 가까워짐! 긴급 도망!");
        }
        else if (distance > farDistance && phase != 3)
        {
            // 멀어짐
            runner.agent.speed = runner.baseSpeed * runnerBaseSpeedMultiplier * runnerStaminaMult;
            chaser.agent.speed = chaser.baseSpeed * chaserSprintSpeedMultiplier * chaserStaminaMult;
            phase = 3;
            // Debug.Log("[ChaseAndRun] 거리가 멀어짐! 추격 가속!");
        }
        else if (distance >= panicDistance && distance <= farDistance && phase != 1)
        {
            // 적정 거리
            runner.agent.speed = runner.baseSpeed * runnerBaseSpeedMultiplier * runnerStaminaMult;
            chaser.agent.speed = chaser.baseSpeed * chaserBaseSpeedMultiplier * chaserStaminaMult;
            phase = 1;
        }
    }

    /// <summary>
    /// 먼지 파티클 생성
    /// </summary>
    private void CreateDustParticles(PetController chaser, PetController runner)
    {
        if (dustParticlePrefab != null)
        {
            // 추격자 먼지
            chaserDustParticles = Instantiate(dustParticlePrefab, chaser.transform);
            chaserDustParticles.transform.localPosition = new Vector3(0, 0.1f, -0.5f);
            
            // 도망자 먼지
            runnerDustParticles = Instantiate(dustParticlePrefab, runner.transform);
            runnerDustParticles.transform.localPosition = new Vector3(0, 0.1f, -0.5f);
        }
    }

    /// <summary>
    /// 주변 펫들을 놀라게 함
    /// </summary>
    private void ScareNearbyPets(PetController runner, float radius)
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(runner.transform.position, radius);
        
        foreach (Collider col in nearbyColliders)
        {
            PetController nearbyPet = col.GetComponent<PetController>();
            if (nearbyPet != null && nearbyPet != runner && 
                !nearbyPet.State.IsInteracting && Random.value < 0.1f) // 10% 확률
            {
                nearbyPet.ShowEmotion(EmotionType.Surprised, 2f);
                
                // 살짝 피하는 애니메이션
                var nearbyAnim = nearbyPet.GetComponent<PetAnimationController>();
                if (nearbyAnim != null)
                {
                    StartCoroutine(nearbyAnim.PlayAnimationWithCustomDuration(
                        PetAnimationController.PetAnimationType.Jump, 0.5f, true, false));
                }
            }
        }
    }

    /// <summary>
    /// 잡기 성공 단계
    /// </summary>
    private IEnumerator CaughtPhase(PetController chaser, PetController runner)
    {
        // Debug.Log("[ChaseAndRun] 3단계: 잡기 성공!");
        
        // 두 펫 정지
        chaser.agent.isStopped = true;
        runner.agent.isStopped = true;
        
        // 파티클 효과
        if (catchParticlePrefab != null)
        {
            Vector3 midPoint = (chaser.transform.position + runner.transform.position) / 2f;
            GameObject catchEffect = Instantiate(catchParticlePrefab, midPoint, Quaternion.identity);
            Destroy(catchEffect, 3f);
        }
        
        // 감정 표현
        chaser.ShowEmotion(EmotionType.Victory, 5f);
        runner.ShowEmotion(EmotionType.Defeat, 5f);
        
        // 함께 구르며 장난치기
        var chaserAnim = chaser.GetComponent<PetAnimationController>();
        var runnerAnim = runner.GetComponent<PetAnimationController>();
        
        // 서로를 향해 이동
        yield return StartCoroutine(MoveToPositions(chaser, runner, 
            (chaser.transform.position + runner.transform.position) / 2f,
            (chaser.transform.position + runner.transform.position) / 2f, 2f));
        
        // 구르기 애니메이션 (Attack 애니메이션 활용)
        for (int i = 0; i < 3; i++)
        {
            yield return StartCoroutine(PlaySimultaneousAnimations(
                chaser, runner,
                PetAnimationController.PetAnimationType.Attack,
                PetAnimationController.PetAnimationType.Damage,
                1.5f));
            
            yield return new WaitForSeconds(0.5f);
        }
        
        // 마지막 즐거운 감정
        chaser.ShowEmotion(EmotionType.Happy, 5f);
        runner.ShowEmotion(EmotionType.Happy, 5f);
        
        yield return new WaitForSeconds(2f);
    }

    /// <summary>
    /// 도망 성공 단계
    /// </summary>
    private IEnumerator EscapeSuccessPhase(PetController chaser, PetController runner)
    {
        // Debug.Log("[ChaseAndRun] 3단계: 도망 성공!");
        
        // 쫓던 펫 멈춤
        chaser.agent.isStopped = true;
        chaser.ShowEmotion(EmotionType.Defeat, 10f);
        
        var chaserAnim = chaser.GetComponent<PetAnimationController>();
                var runnerAnim = runner.GetComponent<PetAnimationController>();
 yield return StartCoroutine(runnerAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Idle, chaserRestDuration, true, false));
        yield return StartCoroutine(chaserAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, chaserRestDuration, false, false));
         
        // 도망자 승리 포즈
        runner.ShowEmotion(EmotionType.Victory, 10f);
        runner.agent.isStopped = true;
        
        
         // ▼▼▼▼▼ [수정된 부분] ▼▼▼▼▼
    // 뒤돌아서 추격자를 보며 승리 포즈 (부드러운 회전으로 변경)
    // 기존의 LookAtOther 대신 SmoothlyLookAtEachOther 코루틴을 호출합니다.
    yield return StartCoroutine(SmoothlyLookAtEachOther(runner, chaser, 0.7f));
    // ▲▲▲▲▲ [여기까지 수정] ▲▲▲▲▲
    
        
        // 승리 점프
        yield return StartCoroutine(runnerAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 1.5f, true, false));
        
        // 도발하는 듯한 동작
        runner.ShowEmotion(EmotionType.Joke, 5f);
        yield return StartCoroutine(runnerAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Attack, 1f, true, false));
        
        yield return new WaitForSeconds(1f);
        
        // 유유히 걸어가기
        runnerAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        runner.agent.isStopped = false;
        runner.agent.speed = runner.baseSpeed * 0.5f;
        
        Vector3 escapeDirection = (runner.transform.position - chaser.transform.position).normalized;
        Vector3 escapeTarget = runner.transform.position + escapeDirection * 10f;
        escapeTarget = FindValidPositionOnNavMesh(escapeTarget, 20f);
        runner.agent.SetDestination(escapeTarget);
        
        yield return new WaitForSeconds(3f);
        
        runnerAnim.StopContinuousAnimation();
        chaserAnim.StopContinuousAnimation();
    }

    // 기존 헬퍼 메서드들은 그대로 유지...
    private bool ShouldChangeDirection(float distance, float currentTime, float lastChangeTime)
    {
        if (currentTime - lastChangeTime > directionChangeInterval)
            return true;

        if (distance < panicDistance * 0.8f && Random.value > 0.5f)
            return true;

        return false;
    }

    private void UpdateRunnerDestination(PetController runner, PetController chaser)
    {
        Vector3 baseRunDirection = (runner.transform.position - chaser.transform.position).normalized;
        Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        randomOffset *= Random.Range(0.3f, 0.7f);
        
        Vector3 finalDirection = (baseRunDirection + randomOffset).normalized;
        Vector3 runTarget = runner.transform.position + finalDirection * Random.Range(10f, 20f);
        
        runTarget = FindValidPositionOnNavMesh(runTarget, 20f);
        runner.agent.SetDestination(runTarget);
        
        // Debug.Log($"[ChaseAndRun] {runner.petName} 방향 변경");
    }

    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }
    
    /// <summary>
    /// 속도에 따라 애니메이션을 동적으로 업데이트
    /// </summary>
    private void UpdateChaseAnimations(PetController chaser, PetController runner, 
        PetAnimationController chaserAnim, PetAnimationController runnerAnim)
    {
        // 속도 임계값 설정 (기본 속도의 10%)
        float idleThreshold = 0.1f;
        float walkThreshold = 0.5f;
        
        // Chaser 애니메이션 업데이트
        if (chaser.agent.velocity.magnitude < chaser.baseSpeed * idleThreshold)
        {
            // 거의 멈춰있음
            if (chaserAnim != null && !IsPlayingAnimation(chaser, PetAnimationController.PetAnimationType.Idle))
            {
                chaserAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            }
        }
        else if (chaser.agent.velocity.magnitude < chaser.baseSpeed * walkThreshold)
        {
            // 천천히 움직임
            if (chaserAnim != null && !IsPlayingAnimation(chaser, PetAnimationController.PetAnimationType.Walk))
            {
                chaserAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            }
        }
        else
        {
            // 빠르게 움직임
            if (chaserAnim != null && !IsPlayingAnimation(chaser, PetAnimationController.PetAnimationType.Run))
            {
                chaserAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            }
        }
        
        // Runner 애니메이션 업데이트
        if (runner.agent.velocity.magnitude < runner.baseSpeed * idleThreshold)
        {
            // 거의 멈춰있음
            if (runnerAnim != null && !IsPlayingAnimation(runner, PetAnimationController.PetAnimationType.Idle))
            {
                runnerAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            }
        }
        else if (runner.agent.velocity.magnitude < runner.baseSpeed * walkThreshold)
        {
            // 천천히 움직임
            if (runnerAnim != null && !IsPlayingAnimation(runner, PetAnimationController.PetAnimationType.Walk))
            {
                runnerAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            }
        }
        else
        {
            // 빠르게 움직임
            if (runnerAnim != null && !IsPlayingAnimation(runner, PetAnimationController.PetAnimationType.Run))
            {
                runnerAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            }
        }
    }
    
    /// <summary>
    /// 현재 재생 중인 애니메이션 확인
    /// </summary>
    private bool IsPlayingAnimation(PetController pet, PetAnimationController.PetAnimationType animType)
    {
        if (pet.animator == null) return false;
        return pet.animator.GetInteger("animation") == (int)animType;
    }
}