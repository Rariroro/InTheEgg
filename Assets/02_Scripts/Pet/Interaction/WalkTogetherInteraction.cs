// WalkTogetherInteraction.cs (최적화된 버전)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class WalkTogetherInteraction : BasePetInteraction
{
    public override string InteractionName => "WalkTogether";

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

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.WalkTogether;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;
        
        return (type1 == PetType.Monkey && type2 == PetType.Gorilla) || 
               (type1 == PetType.Gorilla && type2 == PetType.Monkey);
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}가 함께 걷기 시작했습니다!");

        // NavMeshAgent 준비 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(pet1, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(pet2, agentSafetyTimeout));

        if (!IsAgentSafelyReady(pet1) || !IsAgentSafelyReady(pet2))
        {
            Debug.LogError($"[{InteractionName}] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(pet1, pet2);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);

        try
        {
            // 감정 표현
            pet1.ShowEmotion(EmotionType.Friend, 30f);
            pet2.ShowEmotion(EmotionType.Friend, 30f);

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
            pet1.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
            pet2.GetComponent<PetAnimationController>()?.StopContinuousAnimation();

            // 공통 종료 처리
            EndInteraction(pet1, pet2);
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

        var pet1Anim = pet1.GetComponent<PetAnimationController>();
        var pet2Anim = pet2.GetComponent<PetAnimationController>();

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

        var pet1Anim = pet1.GetComponent<PetAnimationController>();
        var pet2Anim = pet2.GetComponent<PetAnimationController>();

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
                pathUpdateCount++;
                lastPathUpdateTime = elapsedTime;

                // 새로운 걷기 방향 설정
                Vector3 midPoint = (pet1.transform.position + pet2.transform.position) / 2f;
                float randomAngle = Random.Range(-maxDirectionChangeAngle, maxDirectionChangeAngle);
                Vector3 walkDirection = Quaternion.Euler(0, randomAngle, 0) * 
                                      (pet1.transform.forward + pet2.transform.forward).normalized;

                // 측면 벡터 계산
                Vector3 sideDirection = Vector3.Cross(Vector3.up, walkDirection).normalized;

                // 목적지 거리
                float targetDistance = Random.Range(minWalkDistance, maxWalkDistance);

                // 중앙 목적지 계산
                Vector3 centerTarget = midPoint + walkDirection * targetDistance;

                // 각 펫의 목적지 계산 (나란히 걷도록)
                Vector3 pet1Target = centerTarget - sideDirection * (petSpacing / 2f);
                Vector3 pet2Target = centerTarget + sideDirection * (petSpacing / 2f);

                // NavMesh 보정
                pet1Target = FindValidPositionOnNavMesh(pet1Target, navMeshSearchRadius);
                pet2Target = FindValidPositionOnNavMesh(pet2Target, navMeshSearchRadius);

                // 두 펫을 움직이게 설정
                pet1.agent.isStopped = false;
                pet2.agent.isStopped = false;
                pet1.agent.SetDestination(pet1Target);
                pet2.agent.SetDestination(pet2Target);

                Debug.Log($"[{InteractionName}] 새 목적지 설정: 펫1({pet1Target}), 펫2({pet2Target})");
            }

            // 특별 이벤트 체크
            if (elapsedTime >= nextEventTime && Random.value < specialEventChance)
            {
                yield return StartCoroutine(PerformWalkEvent(pet1, pet2));
                nextEventTime = elapsedTime + Random.Range(eventMinInterval, eventMaxInterval);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 걷기 종료 - 에이전트 정지
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
        int eventType = Random.Range(0, 6);
        var pet1Anim = pet1.GetComponent<PetAnimationController>();
        var pet2Anim = pet2.GetComponent<PetAnimationController>();

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
                float originalSpeed1 = pet1.agent.speed;
                float originalSpeed2 = pet2.agent.speed;
                
                pet1.agent.speed *= 1.8f;
                pet2.agent.speed *= 1.8f;
                
                // 뛰기 애니메이션
                pet1Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
                pet2Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
                
                yield return new WaitForSeconds(3f);
                
                // 다시 걷기 속도로
                pet1.agent.speed = originalSpeed1;
                pet2.agent.speed = originalSpeed2;
                pet1Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
                pet2Anim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
                break;

            case 2: // 서로 바라보며 교감
                Debug.Log($"[{InteractionName}] 서로를 바라보며 교감합니다.");
                pet1.agent.isStopped = true;
                pet2.agent.isStopped = true;
                
                yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));
                
                pet1.ShowEmotion(EmotionType.Love, 3f);
                pet2.ShowEmotion(EmotionType.Love, 3f);
                
                yield return new WaitForSeconds(2f);
                
                pet1.agent.isStopped = false;
                pet2.agent.isStopped = false;
                break;

            case 3: // 한 펫이 앞서가기
                Debug.Log($"[{InteractionName}] 한 펫이 앞서갑니다.");
                PetController leadPet = Random.value > 0.5f ? pet1 : pet2;
                PetController followPet = leadPet == pet1 ? pet2 : pet1;
                
                leadPet.agent.speed *= 1.3f;
                // leadPet.ShowEmotion(EmotionType.Cheer, 3f);
                // followPet.ShowEmotion(EmotionType.Surprised, 3f);
                
                yield return new WaitForSeconds(2f);
                
                // 뒤처진 펫이 따라잡기
                followPet.agent.speed *= 1.5f;
                yield return new WaitForSeconds(1f);
                
                // 다시 속도 맞추기
                float syncedSpeed = Mathf.Min(pet1.baseSpeed, pet2.baseSpeed) * walkSpeedMultiplier;
                pet1.agent.speed = syncedSpeed;
                pet2.agent.speed = syncedSpeed;
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

            case 5: // 놀이하듯 서로 돌기
                Debug.Log($"[{InteractionName}] 서로 주위를 빙글빙글 돕니다.");
                pet1.agent.isStopped = true;
                pet2.agent.isStopped = true;
                
                // 서로의 주위를 도는 위치 설정
                Vector3 midPoint = (pet1.transform.position + pet2.transform.position) / 2f;
                float radius = petSpacing;
                
                // 원형으로 이동
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * 90f * Mathf.Deg2Rad;
                    Vector3 pet1NewPos = midPoint + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    Vector3 pet2NewPos = midPoint - new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    
                    pet1.agent.SetDestination(pet1NewPos);
                    pet2.agent.SetDestination(pet2NewPos);
                    pet1.agent.isStopped = false;
                    pet2.agent.isStopped = false;
                    
                    yield return new WaitForSeconds(0.7f);
                }
                
                pet1.ShowEmotion(EmotionType.Happy, 2f);
                pet2.ShowEmotion(EmotionType.Happy, 2f);
                break;
        }
    }

    /// <summary>
    /// NavMeshAgent가 안전하게 준비되었는지 확인
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }
}