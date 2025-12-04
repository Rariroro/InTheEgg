// WalkTogetherInteraction.cs (최적화된 버전)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class WalkTogetherInteraction : BasePetInteraction
{
    public override string InteractionName => "WalkTogether";

    // 우선순위: 50 (11순위)
    public override int Priority => 50;

    [Header("걷기 설정")]
    [Tooltip("함께 걷는 총 시간")]
    public float walkDuration = 15f;

    [Tooltip("펫들 사이의 간격")]
    public float petSpacing = 2.5f;

    [Tooltip("걷기 속도 배율")]
    [Range(0.5f, 1f)]
    public float walkSpeedMultiplier = 0.8f;

    [Header("경로 설정")]
    [Tooltip("새 목적지까지의 최소 거리")]
    public float minWalkDistance = 8f;

    [Tooltip("새 목적지까지의 최대 거리")]
    public float maxWalkDistance = 50f;

    [Tooltip("목적지 도착 판정 거리")]
    public float arrivalDistance = 2f;

    [Tooltip("경로 갱신 간격")]
    public float pathUpdateInterval = 5f;

    [Tooltip("방향 변경 최대 각도")]
    public float maxDirectionChangeAngle = 45f;

    [Header("이벤트 설정")]
    [Tooltip("특별 이벤트 발생 확률")]
    [Range(0f, 1f)]
    public float specialEventChance = 0.3f;

    [Tooltip("이벤트 간 최소 간격")]
    public float eventMinInterval = 3f;

    [Tooltip("이벤트 간 최대 간격")]
    public float eventMaxInterval = 6f;

    [Header("안전 설정")]
    [Tooltip("시작 위치 이동 타임아웃")]
    public float moveToStartTimeout = 5f;

    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;

    [Tooltip("NavMesh 검색 반경")]
    public float navMeshSearchRadius = 55f;

    [Tooltip("경로 찾기 재시도 횟수")]
    public int pathfindingRetries = 3;

    [Header("곡선 경로 설정")]
    [Tooltip("경로 곡률 (0=직선, 1=최대 곡선)")]
    [Range(0f, 1f)]
    public float pathCurvature = 0.3f;

    [Tooltip("경로 변동성 (곡선 폭)")]
    public float pathVariation = 5f;

    // 애니메이션 컨트롤러 캐싱
    private PetAnimationController pet1Anim;
    private PetAnimationController pet2Anim;

    // 원래 속도 저장
    private float originalSpeed1;
    private float originalSpeed2;

    // 이벤트 진행 중 플래그
    private bool isEventInProgress = false;

    // 유효한 펫 조합 (HashSet으로 O(1) 룩업)
    private static readonly HashSet<(PetType, PetType)> ValidPairs = new()
    {
        (PetType.Monkey, PetType.Gorilla), (PetType.Gorilla, PetType.Monkey),
        (PetType.Anteater, PetType.Malayan), (PetType.Malayan, PetType.Anteater),
        (PetType.Pangolin, PetType.Armadillo), (PetType.Armadillo, PetType.Pangolin),
        (PetType.Elephant, PetType.Giraffe), (PetType.Giraffe, PetType.Elephant),
        (PetType.Elk, PetType.Deer), (PetType.Deer, PetType.Elk)
    };

    /// <summary>
    /// 강제 종료 시 WalkTogether 고유 리소스를 정리합니다.
    /// </summary>
    protected override void OnForceCleanup()
    {
        Debug.Log("[WalkTogether] OnForceCleanup 호출됨 - 고유 리소스 정리 시작");

        // 이벤트 플래그 초기화
        isEventInProgress = false;

        // 애니메이션 컨트롤러 정리
        if (pet1Anim != null)
        {
            pet1Anim.StopContinuousAnimation();
            pet1Anim = null;
        }
        if (pet2Anim != null)
        {
            pet2Anim.StopContinuousAnimation();
            pet2Anim = null;
        }

        Debug.Log("[WalkTogether] OnForceCleanup 완료 - 고유 리소스 정리됨");
    }

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.WalkTogether;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // HashSet을 사용한 O(1) 룩업으로 최적화
        return ValidPairs.Contains((pet1.PetType, pet2.PetType));
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}가 함께 걷기 시작했습니다!");

        // NavMeshAgent 준비 확인 (재시도 로직 포함)
        bool pet1Ready = false;
        bool pet2Ready = false;

        // 첫 번째 시도
        yield return StartCoroutine(WaitUntilAgentIsReady(pet1, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(pet2, agentSafetyTimeout));

        pet1Ready = IsAgentSafelyReady(pet1);
        pet2Ready = IsAgentSafelyReady(pet2);

        // 첫 번째 시도 실패 시 agent 재활성화 후 재시도
        if (!pet1Ready || !pet2Ready)
        {
            Debug.LogWarning($"[{InteractionName}] NavMeshAgent 준비 실패. 재시도합니다. (pet1: {pet1Ready}, pet2: {pet2Ready})");

            // Agent 재활성화 시도
            if (!pet1Ready && pet1.agent != null)
            {
                pet1.agent.enabled = false;
                yield return new WaitForSeconds(0.2f);
                pet1.agent.enabled = true;
                pet1.agent.Warp(pet1.transform.position);
            }

            if (!pet2Ready && pet2.agent != null)
            {
                pet2.agent.enabled = false;
                yield return new WaitForSeconds(0.2f);
                pet2.agent.enabled = true;
                pet2.agent.Warp(pet2.transform.position);
            }

            // 두 번째 시도
            yield return StartCoroutine(WaitUntilAgentIsReady(pet1, agentSafetyTimeout));
            yield return StartCoroutine(WaitUntilAgentIsReady(pet2, agentSafetyTimeout));

            pet1Ready = IsAgentSafelyReady(pet1);
            pet2Ready = IsAgentSafelyReady(pet2);
        }

        if (!pet1Ready || !pet2Ready)
        {
            Debug.LogError($"[{InteractionName}] NavMeshAgent 준비 실패로 상호작용을 중단합니다. (pet1: {pet1.petName} ready: {pet1Ready}, pet2: {pet2.petName} ready: {pet2Ready})");
            yield break;
        }

        // 애니메이션 컨트롤러 캐싱
        pet1Anim = pet1.GetComponent<PetAnimationController>();
        pet2Anim = pet2.GetComponent<PetAnimationController>();

        if (pet1Anim == null || pet2Anim == null)
        {
            Debug.LogError($"[{InteractionName}] PetAnimationController를 찾을 수 없습니다.");
            EndInteraction(pet1, pet2);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);
        originalSpeed1 = pet1.agent.speed;
        originalSpeed2 = pet2.agent.speed;

        try
        {
            // 감정 표현 (짧은 시간으로 변경)
            pet1.ShowEmotion(EmotionType.Friend, 5f);
            pet2.ShowEmotion(EmotionType.Friend, 5f);

            // 1. 준비 단계
            yield return StartCoroutine(PrepareWalkPhase(pet1, pet2));

            // 2. 메인 걷기 단계
            yield return StartCoroutine(MainWalkPhase(pet1, pet2));

            Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 함께 걷기 완료");
        }
        finally
        {
            // 최종 정리
            Debug.Log($"[{InteractionName}] 상호작용 정리 시작.");

            // 원래 상태 복원
            pet1State.Restore(pet1);
            pet2State.Restore(pet2);

            // 애니메이션 정리
            if (pet1Anim != null) pet1Anim.StopContinuousAnimation();
            if (pet2Anim != null) pet2Anim.StopContinuousAnimation();

            // 이벤트 플래그 초기화
            isEventInProgress = false;

            // 주의: EndInteraction은 BasePetInteraction.InteractionLifecycle에서 자동 호출됨
            // 여기서 직접 호출하면 중복 호출 발생
            Debug.Log($"[{InteractionName}] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 1단계: 걷기 준비
    /// </summary>
    private IEnumerator PrepareWalkPhase(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] 1단계: 걷기 준비");

        // 시작 위치 계산 (나란히 서기)
        Vector3 pet1Position, pet2Position;
        CalculateStartPositions(pet1, pet2, out pet1Position, out pet2Position, petSpacing);

        // 걷기 전 시작 위치로 이동
        yield return StartCoroutine(MoveToPositions(pet1, pet2, pet1Position, pet2Position, moveToStartTimeout));

        // 서로 마주보고 인사
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));

        // 인사 애니메이션 (점프)
        StartCoroutine(pet1Anim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 1f, false, false));
        yield return StartCoroutine(pet2Anim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 1f, false, false));

        // 잠시 대기
        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// 2단계: 메인 걷기
    /// </summary>
    private IEnumerator MainWalkPhase(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] 2단계: 메인 걷기");

        // 속도 동기화
        float syncedSpeed = Mathf.Min(pet1.baseSpeed, pet2.baseSpeed) * walkSpeedMultiplier;
        pet1.agent.speed = syncedSpeed;
        pet2.agent.speed = syncedSpeed;

        // 걷기 애니메이션 활성화
        pet1Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        pet2Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        // NavMeshAgent가 자동으로 회전하도록 설정
        pet1.agent.updateRotation = true;
        pet2.agent.updateRotation = true;

        float elapsedTime = 0f;
        int pathUpdateCount = 0;
        float lastPathUpdateTime = 0f;
        float nextEventTime = Random.Range(eventMinInterval, eventMaxInterval);

        while (elapsedTime < walkDuration)
        {
            // 이벤트 진행 중이 아닐 때만 경로 업데이트
            if (!isEventInProgress)
            {
                bool shouldUpdatePath = false;

                if (pathUpdateCount == 0) // 처음에는 무조건 경로 설정
                {
                    shouldUpdatePath = true;
                }
                else
                {
                    // 두 펫 모두 목적지에 가까워지면 새 경로 설정
                    bool pet1NearDestination = !pet1.agent.pathPending && pet1.agent.remainingDistance < arrivalDistance;
                    bool pet2NearDestination = !pet2.agent.pathPending && pet2.agent.remainingDistance < arrivalDistance;

                    if (pet1NearDestination && pet2NearDestination)
                    {
                        shouldUpdatePath = true;
                    }

                    // 또는 일정 시간이 지나면 경로 갱신
                    if (elapsedTime - lastPathUpdateTime > pathUpdateInterval)
                    {
                        shouldUpdatePath = true;
                    }
                }

                if (shouldUpdatePath)
                {
                    yield return StartCoroutine(UpdateWalkPath(pet1, pet2));
                    pathUpdateCount++;
                    lastPathUpdateTime = elapsedTime;
                }
            }

            // 특별 이벤트 체크
            if (!isEventInProgress && elapsedTime >= nextEventTime && Random.value < specialEventChance)
            {
                isEventInProgress = true;
                yield return StartCoroutine(PerformWalkEvent(pet1, pet2));
                isEventInProgress = false;
                nextEventTime = elapsedTime + Random.Range(eventMinInterval, eventMaxInterval);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 걷기 종료
        yield return StartCoroutine(EndWalkPhase(pet1, pet2));
    }

    /// <summary>
    /// 경로 업데이트 (곡선 경로 지원)
    /// </summary>
    private IEnumerator UpdateWalkPath(PetController pet1, PetController pet2)
    {
        // 새로운 걷기 방향 설정
        Vector3 midPoint = (pet1.transform.position + pet2.transform.position) / 2f;
        float randomAngle = Random.Range(-maxDirectionChangeAngle, maxDirectionChangeAngle);
        Vector3 walkDirection = Quaternion.Euler(0, randomAngle, 0) *
                              (pet1.transform.forward + pet2.transform.forward).normalized;

        // 측면 벡터 계산
        Vector3 sideDirection = Vector3.Cross(Vector3.up, walkDirection).normalized;

        // 목적지 거리
        float targetDistance = Random.Range(minWalkDistance, maxWalkDistance);

        // 곡선 방향 결정 (랜덤하게 좌우 변경)
        int curveDirection = Random.value > 0.5f ? 1 : -1;

        // 경로 찾기 재시도
        for (int retry = 0; retry < pathfindingRetries; retry++)
        {
            // 중앙 목적지 계산
            Vector3 centerTarget = midPoint + walkDirection * targetDistance;

            // 각 펫의 목적지 계산 (나란히 걷도록)
            Vector3 pet1Target = centerTarget - sideDirection * (petSpacing / 2f);
            Vector3 pet2Target = centerTarget + sideDirection * (petSpacing / 2f);

            // NavMesh 보정
            pet1Target = FindValidPositionOnNavMesh(pet1Target, navMeshSearchRadius);
            pet2Target = FindValidPositionOnNavMesh(pet2Target, navMeshSearchRadius);

            // 경로 유효성 검사
            NavMeshPath path1 = new NavMeshPath();
            NavMeshPath path2 = new NavMeshPath();

            if (pet1.agent.CalculatePath(pet1Target, path1) && path1.status == NavMeshPathStatus.PathComplete &&
                pet2.agent.CalculatePath(pet2Target, path2) && path2.status == NavMeshPathStatus.PathComplete)
            {
                pet1.agent.isStopped = false;
                pet2.agent.isStopped = false;

                // 곡선 경로가 활성화된 경우: 중간 지점을 거쳐 최종 목적지로 이동
                if (pathCurvature > 0.01f)
                {
                    // 곡선 경로의 중간 웨이포인트 생성 (같은 방향으로 곡선)
                    List<Vector3> pet1Waypoints = GenerateCurvedPath(pet1.transform.position, pet1Target, curveDirection);
                    List<Vector3> pet2Waypoints = GenerateCurvedPath(pet2.transform.position, pet2Target, curveDirection);

                    // 중간 지점으로 먼저 이동 후 최종 목적지로
                    if (pet1Waypoints.Count > 1 && pet2Waypoints.Count > 1)
                    {
                        Vector3 pet1Mid = pet1Waypoints[0];
                        Vector3 pet2Mid = pet2Waypoints[0];

                        // 중간 지점 유효성 검사
                        NavMeshPath midPath1 = new NavMeshPath();
                        NavMeshPath midPath2 = new NavMeshPath();

                        if (pet1.agent.CalculatePath(pet1Mid, midPath1) && midPath1.status == NavMeshPathStatus.PathComplete &&
                            pet2.agent.CalculatePath(pet2Mid, midPath2) && midPath2.status == NavMeshPathStatus.PathComplete)
                        {
                            // 중간 지점으로 이동
                            pet1.agent.SetDestination(pet1Mid);
                            pet2.agent.SetDestination(pet2Mid);

                            // 중간 지점 도착 대기
                            float waitTime = 0f;
                            float maxWaitTime = 5f;
                            while (waitTime < maxWaitTime)
                            {
                                bool pet1Arrived = !pet1.agent.pathPending && pet1.agent.remainingDistance < arrivalDistance;
                                bool pet2Arrived = !pet2.agent.pathPending && pet2.agent.remainingDistance < arrivalDistance;

                                if (pet1Arrived && pet2Arrived) break;

                                waitTime += Time.deltaTime;
                                yield return null;
                            }

                            // 최종 목적지로 이동
                            pet1.agent.SetDestination(pet1Target);
                            pet2.agent.SetDestination(pet2Target);

                            Debug.Log($"[{InteractionName}] 곡선 경로 설정 성공");
                            yield break;
                        }
                    }
                }

                // 곡선 실패 또는 비활성화 시 직선 경로
                pet1.agent.SetDestination(pet1Target);
                pet2.agent.SetDestination(pet2Target);

                Debug.Log($"[{InteractionName}] 새 목적지 설정 성공");
                yield break;
            }

            // 실패 시 거리를 줄여서 재시도
            targetDistance *= 0.7f;
        }

        Debug.LogWarning($"[{InteractionName}] 유효한 경로를 찾을 수 없어 현재 위치 유지");
    }

    /// <summary>
    /// 걷기 종료 단계
    /// </summary>
    private IEnumerator EndWalkPhase(PetController pet1, PetController pet2)
    {
        // 에이전트 정지
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;

        // 애니메이션 정지
        pet1Anim.StopContinuousAnimation();
        pet2Anim.StopContinuousAnimation();

        // 마무리 인사
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));

        // 헤어지기 전 감정 표현
        pet1.ShowEmotion(EmotionType.Happy, 3f);
        pet2.ShowEmotion(EmotionType.Happy, 3f);

        // 작별 인사 (점프)
        StartCoroutine(pet1Anim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 1f, false, false));
        yield return StartCoroutine(pet2Anim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 1f, false, false));
    }

    /// <summary>
    /// 걷기 중 특별 이벤트
    /// </summary>
    private IEnumerator PerformWalkEvent(PetController pet1, PetController pet2)
    {
        // 이벤트 중 원래 속도 저장
        float eventSpeed1 = pet1.agent.speed;
        float eventSpeed2 = pet2.agent.speed;

        int eventType = Random.Range(0, 6);

        switch (eventType)
        {
            case 0: // 잠시 멈춰서 주변 구경
                Debug.Log($"[{InteractionName}] 잠시 멈춰서 주변을 구경합니다.");
                pet1.agent.isStopped = true;
                pet2.agent.isStopped = true;
                
                pet1Anim.StopContinuousAnimation();
                pet2Anim.StopContinuousAnimation();
                
                // 주변을 둘러보는 동작
                pet1.ShowEmotion(EmotionType.Surprised, 2f);
                pet2.ShowEmotion(EmotionType.Surprised, 2f);
                
                yield return new WaitForSeconds(2f);
                
                // 다시 걷기 시작
                pet1.agent.isStopped = false;
                pet2.agent.isStopped = false;
                pet1Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
                pet2Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
                break;

            case 1: // 잠시 뛰기
                Debug.Log($"[{InteractionName}] 잠시 뛰어갑니다!");
                
                // 속도 증가
                pet1.agent.speed = eventSpeed1 * 1.8f;
                pet2.agent.speed = eventSpeed2 * 1.8f;
                
                // 뛰기 애니메이션
                pet1Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
                pet2Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
                
                yield return new WaitForSeconds(3f);
                
                // 다시 걷기 속도로
                pet1.agent.speed = eventSpeed1;
                pet2.agent.speed = eventSpeed2;
                pet1Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
                pet2Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
                break;

            case 2: // 서로 바라보며 교감
                Debug.Log($"[{InteractionName}] 서로를 바라보며 교감합니다.");

                // 펫 사이 거리 확보
                float currentDistance = Vector3.Distance(pet1.transform.position, pet2.transform.position);
                if (currentDistance < petSpacing)
                {
                    Vector3 midPoint = (pet1.transform.position + pet2.transform.position) / 2f;
                    Vector3 dir1 = (pet1.transform.position - midPoint).normalized;
                    Vector3 dir2 = (pet2.transform.position - midPoint).normalized;

                    Vector3 newPos1 = midPoint + dir1 * (petSpacing / 2f);
                    Vector3 newPos2 = midPoint + dir2 * (petSpacing / 2f);

                    newPos1 = FindValidPositionOnNavMesh(newPos1, 3f);
                    newPos2 = FindValidPositionOnNavMesh(newPos2, 3f);

                    pet1.agent.SetDestination(newPos1);
                    pet2.agent.SetDestination(newPos2);

                    yield return new WaitForSeconds(0.5f);
                }

                pet1.agent.isStopped = true;
                pet2.agent.isStopped = true;

                yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));

                pet1.ShowEmotion(EmotionType.Happy, 3f);
                pet2.ShowEmotion(EmotionType.Happy, 3f);

                yield return new WaitForSeconds(2f);

                pet1.agent.isStopped = false;
                pet2.agent.isStopped = false;
                break;

            case 3: // 한 펫이 앞서가기
                Debug.Log($"[{InteractionName}] 한 펫이 앞서갑니다.");
                PetController leadPet = Random.value > 0.5f ? pet1 : pet2;
                PetController followPet = leadPet == pet1 ? pet2 : pet1;
                float leadSpeed = leadPet == pet1 ? eventSpeed1 : eventSpeed2;
                float followSpeed = followPet == pet1 ? eventSpeed1 : eventSpeed2;
                
                leadPet.agent.speed = leadSpeed * 1.3f;
                leadPet.ShowEmotion(EmotionType.Happy, 3f);
                followPet.ShowEmotion(EmotionType.Surprised, 3f);
                
                yield return new WaitForSeconds(2f);
                
                // 뒤처진 펫이 따라잡기
                followPet.agent.speed = followSpeed * 1.5f;
                yield return new WaitForSeconds(1f);
                
                // 다시 원래 속도로
                pet1.agent.speed = eventSpeed1;
                pet2.agent.speed = eventSpeed2;
                break;

            case 4: // 동시에 점프
                Debug.Log($"[{InteractionName}] 함께 점프합니다!");
                // 잠시 멈추고
                pet1.agent.isStopped = true;
                pet2.agent.isStopped = true;
                
                // 걷기 애니메이션 중단
                pet1Anim.StopContinuousAnimation();
                pet2Anim.StopContinuousAnimation();
                
                // 동시 점프
                StartCoroutine(pet1Anim.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Jump, 1f, false, false));
                yield return StartCoroutine(pet2Anim.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Jump, 1f, false, false));
                
                // 다시 걷기
                pet1.agent.isStopped = false;
                pet2.agent.isStopped = false;
                pet1Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
                pet2Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
                break;

            case 5: // 간단한 교차 걷기
                Debug.Log($"[{InteractionName}] 위치를 교차하며 걷습니다.");
                
                // 현재 목적지 저장
                Vector3 pet1Dest = pet1.agent.destination;
                Vector3 pet2Dest = pet2.agent.destination;
                
                // 서로의 위치 근처로 이동
                Vector3 crossPoint1 = pet2.transform.position + pet2.transform.right * petSpacing;
                Vector3 crossPoint2 = pet1.transform.position - pet1.transform.right * petSpacing;
                
                // NavMesh 검증
                crossPoint1 = FindValidPositionOnNavMesh(crossPoint1, 5f);
                crossPoint2 = FindValidPositionOnNavMesh(crossPoint2, 5f);
                
                pet1.agent.SetDestination(crossPoint1);
                pet2.agent.SetDestination(crossPoint2);
                
                yield return new WaitForSeconds(2f);
                
                // 원래 목적지로 복귀
                pet1.agent.SetDestination(pet1Dest);
                pet2.agent.SetDestination(pet2Dest);
                
                pet1.ShowEmotion(EmotionType.Happy, 2f);
                pet2.ShowEmotion(EmotionType.Happy, 2f);
                break;
        }

        // 이벤트 후 항상 걷기 애니메이션 보장
        pet1Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        pet2Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        // 속도가 변경되었을 수 있으므로 원래 속도로 복원
        pet1.agent.speed = eventSpeed1;
        pet2.agent.speed = eventSpeed2;
    }

    /// <summary>
    /// NavMeshAgent가 안전하게 준비되었는지 확인
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }

    /// <summary>
    /// 템플릿에서 이 인스턴스로 설정값을 복사합니다 (동시 실행 격리)
    /// </summary>
    public void CopySettingsFrom(WalkTogetherInteraction template)
    {
        if (template == null) return;

        // 걷기 설정
        this.walkDuration = template.walkDuration;
        this.petSpacing = template.petSpacing;
        this.walkSpeedMultiplier = template.walkSpeedMultiplier;

        // 경로 설정
        this.minWalkDistance = template.minWalkDistance;
        this.maxWalkDistance = template.maxWalkDistance;
        this.arrivalDistance = template.arrivalDistance;
        this.pathUpdateInterval = template.pathUpdateInterval;
        this.maxDirectionChangeAngle = template.maxDirectionChangeAngle;

        // 이벤트 설정
        this.specialEventChance = template.specialEventChance;
        this.eventMinInterval = template.eventMinInterval;
        this.eventMaxInterval = template.eventMaxInterval;

        // 안전 설정
        this.moveToStartTimeout = template.moveToStartTimeout;
        this.agentSafetyTimeout = template.agentSafetyTimeout;
        this.navMeshSearchRadius = template.navMeshSearchRadius;
        this.pathfindingRetries = template.pathfindingRetries;

        // 곡선 경로 설정
        this.pathCurvature = template.pathCurvature;
        this.pathVariation = template.pathVariation;
    }

    #region 곡선 경로

    /// <summary>
    /// 2차 베지어 곡선의 점을 계산합니다
    /// </summary>
    /// <param name="t">0~1 사이의 진행도</param>
    /// <param name="p0">시작점</param>
    /// <param name="p1">제어점</param>
    /// <param name="p2">끝점</param>
    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }

    /// <summary>
    /// 곡선 경로의 웨이포인트를 생성합니다
    /// </summary>
    /// <param name="start">시작 위치</param>
    /// <param name="end">목표 위치</param>
    /// <param name="curveDirection">곡선 방향 (1 또는 -1)</param>
    private List<Vector3> GenerateCurvedPath(Vector3 start, Vector3 end, int curveDirection)
    {
        List<Vector3> waypoints = new List<Vector3>();

        // 직선일 경우 그냥 목표만 반환
        if (pathCurvature <= 0.01f)
        {
            waypoints.Add(end);
            return waypoints;
        }

        // 제어점 계산
        Vector3 midPoint = (start + end) / 2f;
        Vector3 direction = (end - start).normalized;
        Vector3 perpendicular = Vector3.Cross(Vector3.up, direction).normalized;

        // 곡률과 변동성을 기반으로 제어점 오프셋 계산
        float curveOffset = pathVariation * pathCurvature * curveDirection;
        Vector3 controlPoint = midPoint + perpendicular * curveOffset;

        // y 좌표는 지형 높이로 유지 (나중에 NavMesh로 보정)
        controlPoint.y = (start.y + end.y) / 2f;

        // 베지어 곡선에서 3개의 웨이포인트 추출 (0.33, 0.66, 1.0)
        for (float t = 0.33f; t <= 1f; t += 0.33f)
        {
            Vector3 point = CalculateQuadraticBezierPoint(t, start, controlPoint, end);
            Vector3 validPoint = FindValidPositionOnNavMesh(point, navMeshSearchRadius);
            waypoints.Add(validPoint);
        }

        return waypoints;
    }

    #endregion
}