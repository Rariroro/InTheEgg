// PredatorMoleInteraction.cs (최적화된 육식동물-두더지 상호작용)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class PredatorMoleInteraction : BasePetInteraction
{
    public override string InteractionName => "PredatorMoleHunt";
    
    // 상태 추적을 위한 클래스 레벨 변수
    private bool _moleIsHidden = false;

    [Header("Chase Settings")]
    [Tooltip("포식자가 두더지에게 접근했다고 판단하는 거리")]
    public float catchDistance = 3.0f;
    
    [Tooltip("추격 최대 시간 (초)")]
    public float maxChaseTime = 10f;
    
    [Tooltip("포식자의 추격 속도 배율")]
    public float predatorChaseSpeedMultiplier = 1.8f;
    
    [Tooltip("두더지의 도망 속도 배율")]
    public float moleEscapeSpeedMultiplier = 1.6f;

    [Header("Burrow Settings")]
    [Tooltip("두더지가 땅속으로 숨는 깊이")]
    public float burrowDepth = 2.0f;
    
    [Tooltip("두더지가 땅속으로 들어가는 시간")]
    public float burrowAnimationTime = 1.0f;
    
    [Tooltip("두더지가 땅에서 나오는 시간")]
    public float emergeAnimationTime = 1.5f;
    
    [Tooltip("포식자가 두더지 구멍을 파는 시간")]
    public float predatorDigTime = 3.0f;

    [Header("Safety Settings")]
    [Tooltip("포식자가 멀어져야 하는 최소 거리")]
    public float safeDistanceForMole = 8.0f;
    
    [Tooltip("포식자가 포기하고 이동하는 거리")]
    public float predatorLeaveDistance = 15f;
    
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;

    [Header("Visual Settings")]
    [Tooltip("두더지가 주변을 둘러보는 시간")]
    public float lookAroundDuration = 2.0f;
    
    [Tooltip("두더지가 회전하는 속도")]
    public float moleRotationSpeed = 100f;

    // 상호작용 타입 결정
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.ChaseAndRun;
    }

    // 상호작용 가능 여부 확인 - 모든 육식동물과 두더지 간 가능
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 한 쪽이 두더지인지 확인
        bool hasMole = pet1.PetType == PetType.Mole || pet2.PetType == PetType.Mole;
        if (!hasMole) return false;

        // 다른 쪽이 육식동물인지 확인
        PetController otherPet = (pet1.PetType == PetType.Mole) ? pet2 : pet1;
        bool isPredator = (otherPet.diet & (PetAIProperties.DietaryFlags.Meat | PetAIProperties.DietaryFlags.Fish)) != 0;

        return isPredator;
    }

    // 메인 상호작용 수행
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[PredatorMoleHunt] {pet1.petName}와(과) {pet2.petName}의 포식자-두더지 상호작용 시작!");

        // 역할 식별
        PetController mole = (pet1.PetType == PetType.Mole) ? pet1 : pet2;
        PetController predator = (pet1.PetType == PetType.Mole) ? pet2 : pet1;

        // NavMeshAgent 준비 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(predator, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(mole, agentSafetyTimeout));

        if (!IsAgentSafelyReady(predator) || !IsAgentSafelyReady(mole))
        {
            Debug.LogError("[PredatorMoleHunt] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(predator, mole);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState predatorState = new PetOriginalState(predator);
        PetOriginalState moleState = new PetOriginalState(mole);

        // 두더지 숨김 처리를 위한 변수
        Vector3 moleBurrowPosition = Vector3.zero;
        _moleIsHidden = false; // 초기화

        try
        {
            // 감정 표현
            predator.ShowEmotion(EmotionType.Hungry, 30f);
            mole.ShowEmotion(EmotionType.Scared, 30f);

            // 1. 추격 단계
            yield return StartCoroutine(ChasePhase(predator, mole));

            // 2. 공격 단계
            yield return StartCoroutine(AttackPhase(predator, mole));

            // 3. 두더지 숨기 단계
            moleBurrowPosition = mole.transform.position;
            yield return StartCoroutine(BurrowPhase(mole));

            // 4. 포식자 반응 단계
            yield return StartCoroutine(PredatorDigPhase(predator, moleBurrowPosition));

            // 5. 포식자 떠나기 단계
            yield return StartCoroutine(PredatorLeavePhase(predator, moleBurrowPosition));

            // 6. 두더지 재등장 단계
            yield return StartCoroutine(MoleEmergePhase(mole, moleBurrowPosition));

            Debug.Log($"[PredatorMoleHunt] {mole.petName}이(가) 위기를 모면했습니다!");
            
            // 성공 감정 표현
            mole.ShowEmotion(EmotionType.Happy, 5f);
            yield return new WaitForSeconds(2f);
        }
        finally
        {
            // 최종 정리
            Debug.Log("[PredatorMoleHunt] 상호작용 정리 시작.");

            // 두더지가 여전히 숨어있다면 원래 위치로 복원
            if (_moleIsHidden && mole.agent != null)
            {
                mole.transform.position = moleBurrowPosition;
                if (!mole.agent.enabled) mole.agent.enabled = true;
            }

            // 원래 상태 복원
            predatorState.Restore(predator);
            moleState.Restore(mole);

            // 애니메이션 정리
            predator.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
            mole.GetComponent<PetAnimationController>()?.StopContinuousAnimation();

            // 공통 종료 처리
            EndInteraction(predator, mole);
            Debug.Log("[PredatorMoleHunt] 상호작용 정리 완료.");
        }
    }

    // 1. 추격 단계
    private IEnumerator ChasePhase(PetController predator, PetController mole)
    {
        Debug.Log($"[PredatorMoleHunt] 1단계: {predator.petName}이(가) {mole.petName}을(를) 추격합니다.");

        // NavMeshAgent 활성화
        if (predator.agent != null) predator.agent.isStopped = false;
        if (mole.agent != null) mole.agent.isStopped = false;

        // 속도 설정
        predator.agent.speed = predator.baseSpeed * predatorChaseSpeedMultiplier;
        predator.agent.acceleration = predator.baseAcceleration * 1.5f;
        predator.agent.updateRotation = true;

        mole.agent.speed = mole.baseSpeed * moleEscapeSpeedMultiplier;
        mole.agent.acceleration = mole.baseAcceleration * 1.4f;
        mole.agent.updateRotation = true;

        // 애니메이션 설정
        predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        mole.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);

        float chaseTimer = 0f;
        bool caught = false;

        while (chaseTimer < maxChaseTime && !caught)
        {
            // 두더지 도망 방향 계산
            Vector3 escapeDirection = (mole.transform.position - predator.transform.position).normalized;
            Vector3 escapeTarget = mole.transform.position + escapeDirection * 10f;

            if (NavMesh.SamplePosition(escapeTarget, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            {
                mole.agent.SetDestination(hit.position);
            }

            // 포식자 추적
            predator.agent.SetDestination(mole.transform.position);

            // 거리 체크
            float distance = Vector3.Distance(predator.transform.position, mole.transform.position);
            if (distance < catchDistance)
            {
                caught = true;
                Debug.Log($"[PredatorMoleHunt] {predator.petName}이(가) {mole.petName}에게 접근했습니다!");
            }

            // 회전 처리
            predator.HandleRotation();
            mole.HandleRotation();

            chaseTimer += Time.deltaTime;
            yield return null;
        }

        // 시간 초과 시 강제 접근
        if (!caught)
        {
            Debug.Log("[PredatorMoleHunt] 추격 시간 초과. 공격 단계로 진행합니다.");
            Vector3 teleportPos = predator.transform.position + predator.transform.forward * 2.5f;
            teleportPos = FindValidPositionOnNavMesh(teleportPos, 3f);
            mole.agent.Warp(teleportPos);
        }

        // 이동 중지
        predator.agent.isStopped = true;
        mole.agent.isStopped = true;
    }

    // 2. 공격 단계
    // 수정 후 AttackPhase
    private IEnumerator AttackPhase(PetController predator, PetController mole)
    {
        Debug.Log($"[PredatorMoleHunt] 2단계: {predator.petName}이(가) {mole.petName}을(를) 공격합니다!");

        // 달리기 애니메이션 정지
        predator.GetComponent<PetAnimationController>().StopContinuousAnimation();
        mole.GetComponent<PetAnimationController>().StopContinuousAnimation();

        // 부드럽게 서로를 마주보도록 회전 (병렬 실행)
        StartCoroutine(SmoothTurnAround(predator, mole, 0.5f));
        yield return StartCoroutine(SmoothTurnAround(mole, predator, 0.5f)); // mole의 회전이 끝날 때까지 대기

        // 회전 후 잠시 대기하여 자연스러운 멈춤 연출
        yield return new WaitForSeconds(0.2f);

        // 공격 및 피격 애니메이션 재생
        var predatorAnim = predator.GetComponent<PetAnimationController>();
        var moleAnim = mole.GetComponent<PetAnimationController>();

        // 포식자 공격
        yield return StartCoroutine(predatorAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Attack, 0.5f, false, false));

        // 두더지 피격
        yield return StartCoroutine(moleAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Damage, 0.5f, false, false));
    }

    // 3. 두더지 숨기 단계
    private IEnumerator BurrowPhase(PetController mole)
    {
        Debug.Log($"[PredatorMoleHunt] 3단계: {mole.petName}이(가) 땅을 파고 들어갑니다!");

        Vector3 burrowPosition = mole.transform.position;
        
        // NavMeshAgent 비활성화
        mole.agent.enabled = false;

        // 땅 파기 애니메이션
        var moleAnim = mole.GetComponent<PetAnimationController>();
        yield return StartCoroutine(moleAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 2.0f, false, false));

        // 땅속으로 숨기
        Vector3 hiddenPosition = burrowPosition;
        hiddenPosition.y -= burrowDepth;

        float elapsedTime = 0f;
        while (elapsedTime < burrowAnimationTime)
        {
            mole.transform.position = Vector3.Lerp(burrowPosition, hiddenPosition, elapsedTime / burrowAnimationTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        mole.transform.position = hiddenPosition;
        _moleIsHidden = true; // 클래스 레벨 변수 설정
    }

    // 4. 포식자 땅파기 단계 (수정된 버전)
private IEnumerator PredatorDigPhase(PetController predator, Vector3 burrowPosition)
{
    Debug.Log($"[PredatorMoleHunt] 4단계: {predator.petName}이(가) 두더지를 찾기 시작합니다.");

    var predatorAnim = predator.GetComponent<PetAnimationController>();
    
    // 1. 먼저 구멍 위치로 천천히 접근
    predator.agent.isStopped = false;
    predator.agent.speed = predator.baseSpeed * 0.5f;
    predator.agent.SetDestination(burrowPosition);
    predatorAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
    
    // 구멍에 도착할 때까지 대기
    while (!predator.agent.pathPending && predator.agent.remainingDistance > 1f)
    {
        predator.HandleRotation();
        yield return null;
    }
    
    predator.agent.isStopped = true;
    predatorAnim.StopContinuousAnimation();
    
    // 2. 냄새 맡기 동작
    yield return StartCoroutine(SniffAroundPhase(predator, burrowPosition));
    
    // 3. 첫 번째 땅 파기 시도
    predator.transform.LookAt(new Vector3(burrowPosition.x, predator.transform.position.y, burrowPosition.z));
    yield return StartCoroutine(predatorAnim.PlayAnimationWithCustomDuration(
        PetAnimationController.PetAnimationType.Eat, 2.0f, false, false));
    
    // 실패 후 혼란
    predator.ShowEmotion(EmotionType.Confused, 2f);
    yield return new WaitForSeconds(0.5f);
    
    // 4. 주변을 돌아다니며 여러 곳 파보기
    yield return StartCoroutine(SearchMultipleLocations(predator, burrowPosition));
    
    // 5. 마지막으로 원래 구멍으로 돌아와서 한 번 더 시도
    // yield return StartCoroutine(FinalDigAttempt(predator, burrowPosition));
    
    // 완전히 포기
    predator.ShowEmotion(EmotionType.Sad, 3f);
    yield return new WaitForSeconds(1f);
}

// 새로운 헬퍼 메서드들 추가

// 냄새 맡기 동작
private IEnumerator SniffAroundPhase(PetController predator, Vector3 centerPosition)
{
    Debug.Log($"[PredatorMoleHunt] {predator.petName}이(가) 냄새를 맡으며 추적합니다.");
    
    var predatorAnim = predator.GetComponent<PetAnimationController>();
    
    // 고개를 숙이고 냄새 맡는 동작 (Eat 애니메이션 활용)
    predatorAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Eat);
    
    // 원을 그리며 냄새 맡기
    float radius = 1.5f;
    float angleStep = 90f; // 90도씩 4번
    
    for (int i = 0; i < 4; i++)
    {
        float angle = i * angleStep;
        Vector3 targetPos = centerPosition + Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
        
        // 천천히 이동
        float moveTime = 1f;
        float elapsedTime = 0f;
        Vector3 startPos = predator.transform.position;
        
        while (elapsedTime < moveTime)
        {
            predator.transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / moveTime);
            
            // 중심점을 바라보도록 회전
            Vector3 lookDir = centerPosition - predator.transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                predator.transform.rotation = Quaternion.LookRotation(lookDir);
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        yield return new WaitForSeconds(0.3f);
    }
    
    predatorAnim.StopContinuousAnimation();
}

// 여러 위치 탐색
private IEnumerator SearchMultipleLocations(PetController predator, Vector3 burrowPosition)
{
    Debug.Log($"[PredatorMoleHunt] {predator.petName}이(가) 주변 여러 곳을 파봅니다.");
    
    var predatorAnim = predator.GetComponent<PetAnimationController>();
    
    // 3-4곳을 랜덤하게 파보기
    int searchAttempts = Random.Range(3, 5);
    float searchRadius = 3f;
    
    for (int i = 0; i < searchAttempts; i++)
    {
        // 랜덤 위치 선택
        Vector2 randomCircle = Random.insideUnitCircle * searchRadius;
        Vector3 searchPos = burrowPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        // NavMesh 위의 유효한 위치 찾기
        if (NavMesh.SamplePosition(searchPos, out NavMeshHit hit, searchRadius * 1.5f, NavMesh.AllAreas))
        {
            searchPos = hit.position;
        }
        
        // 해당 위치로 빠르게 이동
        predator.agent.isStopped = false;
        predator.agent.speed = predator.baseSpeed * 0.8f;
        predator.agent.SetDestination(searchPos);
        predatorAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        
        // 도착 대기
        while (!predator.agent.pathPending && predator.agent.remainingDistance > 0.5f)
        {
            predator.HandleRotation();
            yield return null;
        }
        
        predator.agent.isStopped = true;
        predatorAnim.StopContinuousAnimation();
        
        // 땅 파기
        yield return StartCoroutine(predatorAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1.5f, false, false));
        
        // 실패 반응 (점점 더 좌절)
        if (i < searchAttempts - 1)
        {
            // 주변을 둘러보기
            yield return StartCoroutine(QuickLookAround(predator));
        }
    }
}
/// <summary>
    /// 지정된 펫이 목표 펫을 향해 일정 시간 동안 부드럽게 회전하는 코루틴입니다.
    /// </summary>
    /// <param name="petToTurn">회전할 펫</param>
    /// <param name="target">바라볼 목표 펫</param>
    /// <param name="duration">회전에 걸리는 시간 (초)</param>
    private IEnumerator SmoothTurnAround(PetController petToTurn, PetController target, float duration)
    {
        // 펫의 이동과 애니메이션을 잠시 정지하여 회전에 집중하도록 합니다.
        petToTurn.agent.isStopped = true;
        petToTurn.GetComponent<PetAnimationController>()?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);

        Quaternion startRotation = petToTurn.transform.rotation;
        Vector3 directionToTarget = (target.transform.position - petToTurn.transform.position).normalized;
        directionToTarget.y = 0; // 수평으로만 회전하도록 Y축 고정

        // 바라볼 방향이 없는 경우(위치가 겹친 경우 등) 코루틴 중단
        if (directionToTarget == Vector3.zero) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            // Quaternion.Slerp를 사용하여 부드러운 구면 선형 보간을 수행합니다.
            float t = elapsedTime / duration;
            petToTurn.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            
            // 모델이 별도로 회전하는 경우를 대비하여 모델의 회전도 맞춰줍니다.
            if (petToTurn.petModelTransform != null)
            {
                petToTurn.petModelTransform.rotation = petToTurn.transform.rotation;
            }

            elapsedTime += Time.deltaTime;
            yield return null; // 다음 프레임까지 대기
        }

        // 최종 회전값을 정확하게 설정합니다.
        petToTurn.transform.rotation = targetRotation;
        if (petToTurn.petModelTransform != null)
        {
            petToTurn.petModelTransform.rotation = targetRotation;
        }
    }
// 빠른 둘러보기
private IEnumerator QuickLookAround(PetController predator)
{
    float lookDuration = 1f;
    float lookTimer = 0f;
    Quaternion startRotation = predator.transform.rotation;
    
    predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
    
    while (lookTimer < lookDuration)
    {
        // 좌우로 빠르게 회전
        float angle = Mathf.Sin(lookTimer * 6f) * 45f;
        predator.transform.rotation = startRotation * Quaternion.Euler(0, angle, 0);
        
        lookTimer += Time.deltaTime;
        yield return null;
    }
    
    predator.transform.rotation = startRotation;
}

// 마지막 시도
private IEnumerator FinalDigAttempt(PetController predator, Vector3 burrowPosition)
{
    Debug.Log($"[PredatorMoleHunt] {predator.petName}이(가) 마지막으로 원래 구멍을 다시 파봅니다.");
    
    var predatorAnim = predator.GetComponent<PetAnimationController>();
    
    // 원래 구멍으로 돌아가기
    predator.agent.isStopped = false;
    predator.agent.speed = predator.baseSpeed * 0.6f;
    predator.agent.SetDestination(burrowPosition);
    predatorAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
    
    while (!predator.agent.pathPending && predator.agent.remainingDistance > 0.5f)
    {
        predator.HandleRotation();
        yield return null;
    }
    
    predator.agent.isStopped = true;
    predatorAnim.StopContinuousAnimation();
    
    // 더 깊게 파려는 시도 (더 긴 애니메이션)
    predator.transform.LookAt(new Vector3(burrowPosition.x, predator.transform.position.y, burrowPosition.z));
    yield return StartCoroutine(predatorAnim.PlayAnimationWithCustomDuration(
        PetAnimationController.PetAnimationType.Attack, 2.5f, false, false));
}

    // 5. 포식자 떠나기 단계
    private IEnumerator PredatorLeavePhase(PetController predator, Vector3 burrowPosition)
    {
        Debug.Log($"[PredatorMoleHunt] 5단계: {predator.petName}이(가) 포기하고 떠납니다.");

        // 랜덤 방향으로 이동
        Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        Vector3 leaveTarget = predator.transform.position + randomDirection * predatorLeaveDistance;

        if (NavMesh.SamplePosition(leaveTarget, out NavMeshHit hit, predatorLeaveDistance * 1.5f, NavMesh.AllAreas))
        {
            leaveTarget = hit.position;
        }

        // 이동 시작
        predator.agent.isStopped = false;
        predator.agent.speed = predator.baseSpeed * 0.8f;
        predator.agent.SetDestination(leaveTarget);
        predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        // 안전 거리까지 대기
        float maxWaitTime = 10f;
        float waitTimer = 0f;

        while (Vector3.Distance(predator.transform.position, burrowPosition) < safeDistanceForMole)
        {
            predator.HandleRotation();

            if (!predator.agent.pathPending && predator.agent.remainingDistance < 1f)
            {
                break;
            }

            waitTimer += Time.deltaTime;
            if (waitTimer > maxWaitTime)
            {
                Debug.LogWarning("[PredatorMoleHunt] 포식자가 충분히 멀어지지 않았지만 시간 초과.");
                break;
            }

            yield return null;
        }

        predator.GetComponent<PetAnimationController>().StopContinuousAnimation();
    }

    // 6. 두더지 재등장 단계
    private IEnumerator MoleEmergePhase(PetController mole, Vector3 burrowPosition)
    {
        if (!_moleIsHidden) yield break;

        Debug.Log($"[PredatorMoleHunt] 6단계: {mole.petName}이(가) 땅에서 나옵니다.");

        // 원래 위치로 이동
        Vector3 hiddenPosition = mole.transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < emergeAnimationTime)
        {
            mole.transform.position = Vector3.Lerp(hiddenPosition, burrowPosition, elapsedTime / emergeAnimationTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        mole.transform.position = burrowPosition;
        _moleIsHidden = false; // 더 이상 숨어있지 않음

        // NavMeshAgent 재활성화
        mole.agent.enabled = true;

        // 나오는 애니메이션
        var moleAnim = mole.GetComponent<PetAnimationController>();
        yield return StartCoroutine(moleAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 1.0f, false, false));

        // 주변 살피기
        yield return StartCoroutine(LookAroundPhase(mole));

        // 안도의 애니메이션
        mole.ShowEmotion(EmotionType.Happy, 5f);
        yield return StartCoroutine(moleAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Rest, 1.5f, false, false));
    }

    // 두더지가 주변을 살피는 동작
    private IEnumerator LookAroundPhase(PetController mole)
    {
        mole.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
        
        float lookTimer = 0f;
        Quaternion originalRotation = mole.transform.rotation;

        while (lookTimer < lookAroundDuration)
        {
            float angle = Mathf.Sin(lookTimer * 2f) * 60f;
            mole.transform.rotation = originalRotation * Quaternion.Euler(0, angle, 0);
            
            lookTimer += Time.deltaTime;
            yield return null;
        }

        mole.transform.rotation = originalRotation;
    }

    // NavMeshAgent 안전 체크
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }
}