// PredatorMoleInteraction.cs (최적화된 육식동물-두더지 상호작용)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;
public class PredatorMoleInteraction : BasePetInteraction
{
    public override string InteractionName => "PredatorMoleHunt";

    // 우선순위: 70 (7순위)
    public override int Priority => 70;

    // 상태 추적을 위한 클래스 레벨 변수
    private bool _moleIsHidden = false;

    [Header("거리 설정")]
    [Tooltip("상호작용 시 유지할 거리")]
    public float interactionDistance = 5.0f;

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

    // 기본 위치 이동(중간 지점으로 모이기)을 하지 않고, 공격자가 직접 접근하도록 설정
    public override bool ShouldPerformInitialMovement => false;

    [Header("Visual Settings")]
    [Tooltip("두더지가 주변을 둘러보는 시간")]
    public float lookAroundDuration = 2.0f;

    [Tooltip("두더지가 회전하는 속도")]
    public float moleRotationSpeed = 100f;
    // ▼▼▼ [추가] 이 부분을 클래스 상단 변수 선언 영역에 추가합니다. ▼▼▼
    [Tooltip("두더지가 땅을 팔 때 생성될 먼지 파티클 프리팹입니다.")]
    public GameObject burrowParticlePrefab;
    // ▲▲▲ [여기까지 추가] ▲▲▲

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

        // 다른 쪽이 고기를 먹는 육식동물인지 확인 (Fish만 먹는 동물은 제외)
        PetController otherPet = (pet1.PetType == PetType.Mole) ? pet2 : pet1;
        bool isMeatEater = (otherPet.diet & PetAIProperties.DietaryFlags.Meat) != 0;

        return isMeatEater;
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
            // 0. 위치 및 방향 준비 (거리 조정 + 서로 마주보기)
            yield return StartCoroutine(PreparePositionAndFacing(predator, mole));

            // 감정 표현
            predator.ShowEmotion(EmotionType.Hungry, 30f);
            mole.ShowEmotion(EmotionType.Scared, 30f);

            // 짧은 대기 (감정 표현이 보이도록)
            yield return new WaitForSeconds(0.5f);

            // 1. 공격 시도 단계
            yield return StartCoroutine(AttackAttemptPhase(predator, mole));

            // 2. 두더지 숨기 단계
            moleBurrowPosition = mole.transform.position;
            yield return StartCoroutine(BurrowPhase(mole));

            // 3. 포식자 반응 단계
            yield return StartCoroutine(PredatorDigPhase(predator, moleBurrowPosition));

            // 4. 포식자 떠나기 단계
            yield return StartCoroutine(PredatorLeavePhase(predator, moleBurrowPosition));

            // 5. 두더지 재등장 단계
            yield return StartCoroutine(MoleEmergePhase(mole, moleBurrowPosition));

            Debug.Log($"[PredatorMoleHunt] {mole.petName}이(가) 위기를 모면했습니다!");

            // 성공 감정 표현
            // mole.ShowEmotion(EmotionType.Happy, 5f);
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
            Debug.Log("[PredatorMoleHunt] 상호작용 정리 완룼.");
        }
    }

    /// <summary>
    /// 0단계: 초기 위치 및 방향 준비 (거리 조정 + 서로 마주보기)
    /// </summary>
    private IEnumerator PreparePositionAndFacing(PetController predator, PetController mole)
    {
        Debug.Log($"[PredatorMoleHunt] 0단계: 위치 및 방향 준비 시작");

        // 1. 거리 계산
        float predatorMultiplier = predator.Profile.GetInteractionDistanceMultiplier();
        float moleMultiplier = mole.Profile.GetInteractionDistanceMultiplier();
        float averageMultiplier = (predatorMultiplier + moleMultiplier) / 2f;
        float targetDistance = interactionDistance * averageMultiplier;
        float currentDistance = Vector3.Distance(predator.transform.position, mole.transform.position);

        Debug.Log($"[PredatorMoleHunt] 현재 거리: {currentDistance:F2}m, 목표 거리: {targetDistance:F2}m");

        // 2. 두더지가 먼저 포식자를 바라보도록 설정
        Vector3 directionToPredator = (predator.transform.position - mole.transform.position).normalized;
        directionToPredator.y = 0;
        if (directionToPredator != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPredator);
            mole.transform.rotation = targetRotation;
            if (mole.petModelTransform != null)
            {
                mole.petModelTransform.rotation = mole.transform.rotation;
            }
        }

        // 두더지는 제자리에서 경계
        mole.agent.isStopped = true;
        mole.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);

        // 3. 거리 조정 필요 여부 확인
        bool needsMovement = currentDistance < targetDistance * 0.8f || currentDistance > targetDistance * 1.5f;

        if (needsMovement)
        {
            Vector3 targetPosition;

            if (currentDistance < targetDistance * 0.8f)
            {
                // 너무 가까움 → 포식자를 뒤로 이동
                Debug.Log($"[PredatorMoleHunt] 너무 가까움 → 포식자를 뒤로 {targetDistance:F2}m 거리로 이동");
                Vector3 directionAway = (predator.transform.position - mole.transform.position).normalized;
                if (directionAway == Vector3.zero)
                {
                    directionAway = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                }
                targetPosition = mole.transform.position + directionAway * targetDistance;
            }
            else
            {
                // 너무 멀음 → 포식자를 앞으로 이동
                Debug.Log($"[PredatorMoleHunt] 너무 멀음 → 포식자를 앞으로 {targetDistance:F2}m 거리로 이동");
                targetPosition = mole.transform.position + mole.transform.forward * targetDistance;
            }

            // NavMesh 상 유효한 위치 찾기
            targetPosition = FindValidPositionOnNavMesh(targetPosition, targetDistance * 1.5f);

            // 포식자 이동 시작
            predator.agent.isStopped = false;
            predator.agent.speed = predator.baseSpeed * predatorChaseSpeedMultiplier;
            predator.agent.SetDestination(targetPosition);
            predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            // 이동 완료 대기
            float timer = 0f;
            while (timer < maxChaseTime)
            {
                // 두더지가 포식자를 부드럽게 계속 바라봄
                directionToPredator = (predator.transform.position - mole.transform.position).normalized;
                directionToPredator.y = 0;
                if (directionToPredator.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(directionToPredator);
                    mole.transform.rotation = Quaternion.Slerp(mole.transform.rotation, targetRot, Time.deltaTime * 5f);
                    if (mole.petModelTransform != null)
                    {
                        mole.petModelTransform.rotation = mole.transform.rotation;
                    }
                }

                // 도착 체크
                if (!predator.agent.pathPending && predator.agent.remainingDistance < 0.5f)
                {
                    Debug.Log($"[PredatorMoleHunt] 위치 조정 완료");
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
            Debug.Log($"[PredatorMoleHunt] 거리 적절함 ({currentDistance:F2}m) → 이동 생략");
            predator.agent.isStopped = true;
        }

        // 4. 서로 정확히 마주보기
        Debug.Log($"[PredatorMoleHunt] 서로 마주보기 시작");
        yield return StartCoroutine(SmoothlyLookAtEachOther(predator, mole, 1f));

        Debug.Log($"[PredatorMoleHunt] 위치 및 방향 준비 완료");
    }

    /// <summary>
    /// 1단계: 공격 시도
    /// </summary>
    private IEnumerator AttackAttemptPhase(PetController predator, PetController mole)
    {
        Debug.Log($"[PredatorMoleHunt] 1단계: {predator.petName}이(가) 공격을 시도합니다.");

        // 포식자 감정 변경
        predator.ShowEmotion(EmotionType.Hungry, 3f);

        // 공격 애니메이션
        var predatorAnim = predator.GetComponent<PetAnimationController>();
        yield return StartCoroutine(predatorAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Attack));

        // 두더지 피격 반응
        mole.ShowEmotion(EmotionType.Scared, 3f);
        var moleAnim = mole.GetComponent<PetAnimationController>();
        yield return StartCoroutine(moleAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Damage, 0.5f, false, false));
    }
    // ▼▼▼ [추가] 이 코루틴을 PredatorMoleInteraction 클래스 내부에 추가합니다. ▼▼▼
    /// <summary>
    /// 지정된 시간 동안 여러 개의 흙먼지 파티클을 연속으로 생성합니다.
    /// </summary>
    /// <param name="position">파티클이 생성될 위치</param>
    private IEnumerator SpawnBurrowParticles(Vector3 position)
    {
        if (burrowParticlePrefab == null)
        {
            yield break; // 프리팹이 없으면 실행하지 않음
        }

        int particleCount = 7; // 생성할 파티클 개수 (이 값을 조절하세요)
        float totalDuration = 2.3f; // 총 몇 초에 걸쳐 생성할지 (이 값을 조절하세요)
        float delay = totalDuration / particleCount;

        for (int i = 0; i < particleCount; i++)
        {
            // 약간의 무작위 위치를 추가하여 더 자연스럽게 만듭니다.
            Vector3 randomOffset = new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.2f, 0.2f));
            Instantiate(burrowParticlePrefab, position + randomOffset, Quaternion.identity);
            yield return new WaitForSeconds(delay);
        }
    }
    // ▲▲▲ [여기까지 추가] ▲▲▲
    // 2. 두더지 숨기 단계
    private IEnumerator BurrowPhase(PetController mole)
    {
        Debug.Log($"[PredatorMoleHunt] 2단계: {mole.petName}이(가) 땅을 파고 들어갑니다!");

        Vector3 burrowPosition = mole.transform.position;
        StartCoroutine(SpawnBurrowParticles(burrowPosition));

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

    // 3. 포식자 땅파기 단계
    private IEnumerator PredatorDigPhase(PetController predator, Vector3 burrowPosition)
    {
        Debug.Log($"[PredatorMoleHunt] 3단계: {predator.petName}이(가) 두더지를 찾기 시작합니다.");

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

    // 4. 포식자 떠나기 단계
    private IEnumerator PredatorLeavePhase(PetController predator, Vector3 burrowPosition)
    {
        Debug.Log($"[PredatorMoleHunt] 4단계: {predator.petName}이(가) 포기하고 떠납니다.");

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

    // 5. 두더지 재등장 단계
    private IEnumerator MoleEmergePhase(PetController mole, Vector3 burrowPosition)
    {
        if (!_moleIsHidden) yield break;

        Debug.Log($"[PredatorMoleHunt] 5단계: {mole.petName}이(가) 땅에서 나옵니다.");
        StartCoroutine(SpawnBurrowParticles(burrowPosition));

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
        mole.ShowEmotion(EmotionType.Happy, 3f);
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