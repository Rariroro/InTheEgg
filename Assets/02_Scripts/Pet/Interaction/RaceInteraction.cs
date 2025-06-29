// RaceInteraction.cs (수정된 버전 - 주석 추가)

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 토끼와 거북이의 경주 상호작용을 처리하는 클래스입니다.
/// BasePetInteraction을 상속받습니다.
/// </summary>
public class RaceInteraction : BasePetInteraction
{
    // 이 상호작용의 이름을 "Race"로 정의합니다.
    public override string InteractionName => "Race";
    // ★★★ 새로 추가된 부분: 결승선 깃발 프리팹 ★★★
    [Header("Race Visuals")]
    [Tooltip("결승선에 배치될 깃발 프리팹입니다.")]
    public GameObject finishFlagPrefab;
    // ★★★ 여기까지 추가 ★★★
    // ★★★ 추가: 경주 설정을 인스펙터에서 조절하기 위한 변수들 ★★★
    [Header("Race Settings")] // 유니티 인스펙터에서 섹션을 구분하기 위한 헤더입니다.
    [Tooltip("경주의 기본 거리입니다.")] // 인스펙터에서 변수 위에 마우스를 올렸을 때 표시될 설명입니다.
    public float raceDistance = 100f;

    [Tooltip("경주가 성립하기 위한 최소 거리입니다.")]
    public float minRaceDistance = 80f;

    [Tooltip("경주 타임아웃 시간 (초)")]
    public float raceTimeoutSeconds = 180f;

    [Header("Rabbit Settings")]
    [Tooltip("토끼의 경주 시작 시 속도 배율입니다.")]
    public float rabbitStartSpeedMultiplier = 3.5f;

    [Tooltip("토끼가 낮잠을 잘 위치 (전체 경주 거리 대비 비율, 0.0 ~ 1.0)")]
    [Range(0f, 1f)] // 인스펙터에서 값을 슬라이더로 조절할 수 있게 합니다. (0에서 1 사이)
    public float rabbitNapProgress = 0.4f;

    [Tooltip("토끼가 다시 깨어나 전력 질주할 때의 속도 배율입니다.")]
    public float rabbitFinalSprintSpeedMultiplier = 5.0f;

    [Header("Turtle Settings")]
    [Tooltip("거북이의 경주 시 속도 배율입니다.")]
    public float turtleSpeedMultiplier = 0.8f;

    [Tooltip("거북이가 이 지점에 도달하면 토끼가 깨어납니다 (전체 경주 거리 대비 비율, 0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float turtleWakeUpProgress = 0.9f;

    [Header("Safety Settings")]
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;


    [Header("Anti-Bottleneck Settings")]
    [Tooltip("결승선에서 각 펫이 향할 목적지 간격")]
    public float finishLineSpread = 5f;
    [Tooltip("경주 도중 막혔을 때 감지하는 시간")]
    public float stuckDetectionTime = 3f;
    [Tooltip("막혔을 때 우회 시도 반경")]
    public float detourRadius = 8f;
    // 토끼가 깨어나야 하는지를 외부(FixPositionDuringInteraction)에서 알 수 있도록 하는 플래그 변수입니다.
    private bool rabbitShouldWakeUp = false;

    /// <summary>
    /// 이 상호작용의 타입을 InteractionType.Race로 결정합니다.
    /// </summary>
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.Race;
    }

    /// <summary>
    /// 두 펫이 각각 토끼와 거북이일 때만 경주 상호작용이 가능하도록 조건을 설정합니다.
    /// </summary>
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 한 쪽이 토끼이고 다른 한 쪽이 거북이인 경우 true를 반환합니다.
        return (pet1.PetType == PetType.Rabbit && pet2.PetType == PetType.Turtle) ||
               (pet1.PetType == PetType.Turtle && pet2.PetType == PetType.Rabbit);
    }


    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[Race] {pet1.petName}와(과) {pet2.petName}의 달리기 시합이 시작됩니다!");

        // 펫 식별 및 NavMeshAgent 준비 상태 확인 (기존과 동일)
        PetController rabbit = (pet1.PetType == PetType.Rabbit) ? pet1 : pet2;
        PetController turtle = (pet1.PetType == PetType.Turtle) ? pet1 : pet2;

        yield return StartCoroutine(WaitUntilAgentIsReady(rabbit, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(turtle, agentSafetyTimeout));

        if (!IsAgentSafelyReady(rabbit) || !IsAgentSafelyReady(turtle))
        {
            Debug.LogError("[Race] NavMeshAgent 준비 실패로 경주를 중단합니다.");
            // ★★★ 수정: 상호작용 실패 시 즉시 EndInteraction 호출로 안전하게 종료 ★★★
            EndInteraction(rabbit, turtle);
            yield break;
        }

        // 상태 저장 및 변수 초기화
        PetOriginalState rabbitState = new PetOriginalState(rabbit);
        PetOriginalState turtleState = new PetOriginalState(turtle);
        GameObject finishFlagInstance = null;

        try
        {
            rabbit.ShowEmotion(EmotionType.Race, raceTimeoutSeconds);
            turtle.ShowEmotion(EmotionType.Race, raceTimeoutSeconds);

            // --- 1. 결승선 위치 설정 ---
            // (이 부분 로직은 기존과 동일하게 유지)
            Vector3 initialCenter = (rabbit.transform.position + turtle.transform.position) / 2;
            Vector3 finishLine = Vector3.zero;
            Vector3 dirToFinish = Vector3.zero;
            float totalRaceDistance = 0f;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                if (randomDirection == Vector3.zero) randomDirection = Vector3.forward;

                Vector3 targetFinishLine = initialCenter + randomDirection * raceDistance;
                if (NavMesh.SamplePosition(targetFinishLine, out NavMeshHit navHit, raceDistance * 1.2f, NavMesh.AllAreas))
                {
                    finishLine = navHit.position;
                    totalRaceDistance = Vector3.Distance(initialCenter, finishLine);
                    if (totalRaceDistance >= minRaceDistance)
                    {
                        dirToFinish = (finishLine - initialCenter).normalized;
                        break;
                    }
                }
            }
            if (dirToFinish == Vector3.zero)
            {
                dirToFinish = rabbit.transform.forward;
                finishLine = initialCenter + dirToFinish * raceDistance;
                totalRaceDistance = raceDistance;
            }
            if (finishFlagPrefab != null)
            {
                Quaternion flagRotation = Quaternion.LookRotation(-dirToFinish);
                finishFlagInstance = Instantiate(finishFlagPrefab, finishLine, flagRotation);
            }

            // --- 2. 출발점으로 이동 및 정렬 ---
            // (이 부분 로직은 기존과 동일하게 유지)
            Vector3 startPosition = CalculateOptimalStartPosition(rabbit, turtle, finishLine, dirToFinish);
            Vector3 rabbitStartPos, turtleStartPos;
            CalculateAlignedStartPositions(startPosition, dirToFinish, out rabbitStartPos, out turtleStartPos, 3f);
            
            yield return StartCoroutine(MoveToPositions(rabbit, turtle, rabbitStartPos, turtleStartPos, 10f));
            
            Quaternion targetRotation = Quaternion.LookRotation(dirToFinish);
            yield return StartCoroutine(SmoothRotateToDirection(rabbit, turtle, targetRotation));

            // ★★★ 수정: 복잡한 위치 고정 코루틴 대신, isStopped 플래그로 간단하게 출발 대기 상태를 만듭니다. ★★★
            rabbit.agent.isStopped = true;
            turtle.agent.isStopped = true;
            Debug.Log("[Race] 출발선 대기 중...");
            yield return new WaitForSeconds(2.0f); // 2초간 출발 대기

            // --- 5. 경주 시작 ---
            Debug.Log("[Race] 경주 시작!");
            rabbit.agent.updateRotation = true;
            turtle.agent.updateRotation = true;
            rabbit.agent.speed = rabbitState.originalSpeed * rabbitStartSpeedMultiplier;
            turtle.agent.speed = turtleState.originalSpeed * turtleSpeedMultiplier;

            rabbit.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            turtle.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            Vector3 rabbitFinishDestination, turtleFinishDestination;
            CreateSeparateFinishDestinations(finishLine, dirToFinish, out rabbitFinishDestination, out turtleFinishDestination);
            
            // ★★★ 수정: isStopped를 false로 바꿔주는 것만으로 이동이 안전하게 재개됩니다. ★★★
            rabbit.agent.isStopped = false;
            turtle.agent.isStopped = false;
            rabbit.agent.SetDestination(rabbitFinishDestination);
            turtle.agent.SetDestination(turtleFinishDestination);

            float napDistance = totalRaceDistance * rabbitNapProgress;
            
            // --- 6. 경주 진행 ---
            bool turtleFinished = false;
            bool rabbitIsSleeping = false;
            bool rabbitWokeUp = false;
            float raceStartTime = Time.time;

            while (!turtleFinished)
            {
                // 토끼 낮잠 로직
                if (!rabbitIsSleeping && !rabbitWokeUp)
                {
                    float rabbitProjectedDistance = Vector3.Dot(rabbit.transform.position - startPosition, dirToFinish);
                    if (rabbitProjectedDistance >= napDistance)
                    {
                        // ★★★ 수정: 토끼 낮잠 로직을 isStopped로 제어 ★★★
                        rabbitIsSleeping = true;
                        rabbit.agent.isStopped = true;       // 이동만 멈춤
                        rabbit.agent.updateRotation = false; // 제자리에서 빙글빙글 돌지 않도록 회전 멈춤
                        
                        rabbit.ShowEmotion(EmotionType.Sleepy, 60f);
                        StartCoroutine(rabbit.GetComponent<PetAnimationController>()
                            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 999f, true, false));
                        Debug.Log($"[Race] {rabbit.petName}이(가) 낮잠을 잡니다.");
                    }
                }

                // 토끼 깨우기 로직
                float turtleProjectedDistance = Vector3.Dot(turtle.transform.position - startPosition, dirToFinish);
                float turtleProgress = Mathf.Clamp01(turtleProjectedDistance / totalRaceDistance);

                if (rabbitIsSleeping && !rabbitWokeUp && turtleProgress >= turtleWakeUpProgress)
                {
                    // ★★★ 수정: 토끼 깨우기 로직을 isStopped로 제어 ★★★
                    rabbitWokeUp = true;
                    var rabbitAnimController = rabbit.GetComponent<PetAnimationController>();
                    rabbitAnimController.StopContinuousAnimation(); // 잠자는 애니메이션 중지
                    rabbit.ShowEmotion(EmotionType.Scared, 10f);
                    
                    if (IsAgentSafelyReady(rabbit))
                    {
                        rabbit.agent.speed = rabbitState.originalSpeed * rabbitFinalSprintSpeedMultiplier;
                        rabbit.agent.updateRotation = true; // 회전 재개
                        rabbit.agent.isStopped = false;     // 이동 재개
                    }
                    
                    yield return StartCoroutine(rabbitAnimController.PlayAnimationWithCustomDuration(
                        PetAnimationController.PetAnimationType.Jump, 0.5f, false, false));

                    rabbitAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
                    Debug.Log($"[Race] {rabbit.petName}이(가) 잠에서 깨어나 전력질주합니다!");
                }
                
                // 거북이 도착 및 타임아웃 체크 (기존과 동일)
                if (!turtle.agent.pathPending && turtle.agent.remainingDistance < 0.5f)
                {
                    turtleFinished = true;
                }
                if (Time.time - raceStartTime > raceTimeoutSeconds)
                {
                    Debug.LogWarning("[Race] 경주 시간 초과! 거북이를 강제로 결승선으로 이동시킵니다.");
                    if(IsAgentSafelyReady(turtle)) turtle.agent.Warp(turtleFinishDestination);
                    turtleFinished = true;
                }

                yield return null;
            }

            // --- 7. 경주 종료 및 결과 처리 ---
            if(IsAgentSafelyReady(rabbit)) rabbit.agent.isStopped = true;
            if(IsAgentSafelyReady(turtle)) turtle.agent.isStopped = true;

            Debug.Log("[Race] 경주가 종료되었습니다. 거북이의 승리!");
            turtle.ShowEmotion(EmotionType.Victory, 15f);
            rabbit.ShowEmotion(EmotionType.Defeat, 15f);

            StartCoroutine(turtle.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2.0f, false, false));
            StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 3.0f, false, false));

            yield return new WaitForSeconds(3f);
        }
        finally
        {
            // ★★★ 수정: finally 블록을 대폭 간소화합니다. ★★★
            // 복잡한 NavMeshAgent 복구 로직을 제거합니다.
            // PetOriginalState 복원과 EndInteraction 호출만으로 충분합니다.
            Debug.Log("[Race] 상호작용 정리 시작.");
            
            if (finishFlagInstance != null)
            {
                Destroy(finishFlagInstance);
            }

            // PetOriginalState가 NavMeshAgent의 속성(speed, acceleration 등)을 원래대로 복원합니다.
            rabbitState.Restore(rabbit);
            turtleState.Restore(turtle);
            
            // 모든 상호작용의 공통 종료 처리를 호출합니다. 
            // 이 메서드는 isInteracting 플래그 해제, 감정 숨기기, 다음 행동 준비 등을 안전하게 처리합니다.
            EndInteraction(rabbit, turtle);
            Debug.Log("[Race] 상호작용 정리 완료.");
        }
    }

    // ... (IsAgentSafelyReady, CalculateOptimalStartPosition, CalculateAlignedStartPositions 등 다른 헬퍼 메서드는 그대로 유지) ...

    // ★★★ 중요: 아래 메서드는 BasePetInteraction.cs에 이미 존재하므로 RaceInteraction.cs 에서는 제거하거나,
    // BasePetInteraction.cs 에 아직 없다면 추가해야 합니다. 여기서는 이미 존재한다고 가정합니다. ★★★
    /*
    private IEnumerator WaitUntilAgentIsReady(PetController pet, float timeout)
    {
        // ...
    }
    */
    
    // 안전하게 NavMeshAgent가 준비되었는지 확인하는 헬퍼 메서드
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }
    private void CreateSeparateFinishDestinations(Vector3 finishLine, Vector3 raceDirection,
        out Vector3 rabbitFinish, out Vector3 turtleFinish)
    {
        // 결승선에서 양옆으로 분리된 목적지 생성
        Vector3 sideDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;

        Vector3 leftFinish = finishLine - sideDirection * (finishLineSpread / 2);
        Vector3 rightFinish = finishLine + sideDirection * (finishLineSpread / 2);

        // NavMesh 유효 위치로 보정
        rabbitFinish = FindValidPositionOnNavMesh(leftFinish, finishLineSpread);
        turtleFinish = FindValidPositionOnNavMesh(rightFinish, finishLineSpread);

        Debug.Log($"[Race] 분리된 결승 목적지 설정: 토끼({rabbitFinish}), 거북이({turtleFinish})");
    }

    // RaceInteraction.cs에 추가할 새로운 메서드들

   
    /// <summary>
    /// 펫들을 지정된 방향으로 부드럽게 회전시키는 코루틴
    /// </summary>
    private IEnumerator SmoothRotateToDirection(PetController pet1, PetController pet2, Quaternion targetRotation)
    {
        float rotationDuration = 1f; // 회전에 걸리는 시간
        float elapsedTime = 0f;

        Quaternion pet1StartRotation = pet1.transform.rotation;
        Quaternion pet2StartRotation = pet2.transform.rotation;

        while (elapsedTime < rotationDuration)
        {
            float t = elapsedTime / rotationDuration;

            // 부드러운 회전 보간
            pet1.transform.rotation = Quaternion.Slerp(pet1StartRotation, targetRotation, t);
            pet2.transform.rotation = Quaternion.Slerp(pet2StartRotation, targetRotation, t);

            // 펫 모델도 함께 회전
            if (pet1.petModelTransform != null)
                pet1.petModelTransform.rotation = pet1.transform.rotation;
            if (pet2.petModelTransform != null)
                pet2.petModelTransform.rotation = pet2.transform.rotation;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 회전값 확실히 설정
        pet1.transform.rotation = targetRotation;
        pet2.transform.rotation = targetRotation;
        if (pet1.petModelTransform != null) pet1.petModelTransform.rotation = targetRotation;
        if (pet2.petModelTransform != null) pet2.petModelTransform.rotation = targetRotation;

        Debug.Log("[Race] 출발선 방향 정렬 완료");
    }


    // ★★★ 새로 추가할 메서드들 ★★★

    /// <summary>
    /// 펫의 NavMeshAgent가 안전하게 준비될 때까지 지정된 시간 동안 기다리는 코루틴입니다.
    /// </summary>
    /// <param name="pet">체크할 펫</param>
    /// <param name="timeout">최대 대기 시간(초)</param>
    private IEnumerator SafeWaitForAgentReady(PetController pet, float timeout)
    {
        float timer = 0f;
        while (timer < timeout)
        {
            if (IsAgentSafelyReady(pet))
            {
                yield break; // 준비 완료 시 코루틴 종료
            }

            // 에이전트 복구 시도: 만약 agent가 비활성화 상태라면 활성화를 시도합니다.
            if (pet.agent != null && !pet.agent.enabled)
            {
                try
                {
                    pet.agent.enabled = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Race] {pet.petName} NavMeshAgent 활성화 실패: {e.Message}");
                }
            }

            timer += Time.deltaTime; // 타이머 증가
            yield return null; // 다음 프레임까지 대기
        }

        Debug.LogWarning($"[Race] {pet.petName}의 NavMeshAgent가 {timeout}초 내에 준비되지 않았습니다.");
    }

   
    /// <summary>
    /// 결승선과 방향을 고려해서 최적의 출발 지점을 계산합니다.
    /// </summary>
    private Vector3 CalculateOptimalStartPosition(PetController pet1, PetController pet2, Vector3 finishLine, Vector3 raceDirection)
    {
        // 현재 두 펫의 중간 지점
        Vector3 currentCenter = (pet1.transform.position + pet2.transform.position) / 2;

        // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
        // 결승선에서 경주 거리만큼 뒤로 온 지점을 이상적인 출발점으로 설정합니다.
        Vector3 idealStartPosition = finishLine - raceDirection * raceDistance;

        // 이상적인 출발점 근처의 NavMesh 위 유효한 위치를 찾습니다.
        if (NavMesh.SamplePosition(idealStartPosition, out NavMeshHit hit, 20f, NavMesh.AllAreas))
        {
            return hit.position; // 찾았다면 해당 위치를 반환
        }

        // 못찾았다면 현재 중간 지점을 그대로 반환
        return currentCenter;
    }

    /// <summary>
    /// 중앙 출발점을 기준으로 두 펫이 나란히 설 수 있도록 양옆으로 위치를 계산합니다.
    /// </summary>
    private void CalculateAlignedStartPositions(Vector3 startCenter, Vector3 raceDirection, out Vector3 pet1Pos, out Vector3 pet2Pos, float spacing = 3f)
    {
        // 경주 진행 방향에 수직인 옆 방향을 계산합니다.
        Vector3 sideDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;
        // 중앙점에서 옆 방향으로 각각 반만큼 떨어진 위치를 계산합니다.
        pet1Pos = startCenter - sideDirection * (spacing / 2);
        pet2Pos = startCenter + sideDirection * (spacing / 2);
        // 계산된 각 위치가 NavMesh 위에 있도록 보정합니다.
        pet1Pos = FindValidPositionOnNavMesh(pet1Pos, spacing * 2);
        pet2Pos = FindValidPositionOnNavMesh(pet2Pos, spacing * 2);
        Debug.Log($"[Race] 정렬된 출발점 계산 완료: Pet1({pet1Pos}), Pet2({pet2Pos})");
    }


}