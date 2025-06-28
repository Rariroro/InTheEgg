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

        // 경주 시작 전, 토끼 깨우기 플래그를 false로 초기화합니다.
        rabbitShouldWakeUp = false;

        // pet1, pet2 중 어떤 펫이 토끼이고 거북이인지 명확히 구분하여 변수에 할당합니다.
        PetController rabbit = (pet1.PetType == PetType.Rabbit) ? pet1 : pet2;
        PetController turtle = (pet1.PetType == PetType.Turtle) ? pet1 : pet2;

        // ★★★★★ 핵심 수정: 상호작용 시작 전, 두 펫의 NavMeshAgent가 준비될 때까지 대기합니다. ★★★★★
        // 이 과정을 통해 NavMeshAgent가 비활성화되어 발생하는 오류를 예방합니다.
        yield return StartCoroutine(WaitUntilAgentIsReady(rabbit));
        yield return StartCoroutine(WaitUntilAgentIsReady(turtle));
        // ★★★★★ 여기까지 추가 ★★★★★

        // 안전 체크: 만약 NavMeshAgent가 여전히 준비되지 않았다면, 경주를 중단합니다.
        if (!IsAgentSafelyReady(rabbit) || !IsAgentSafelyReady(turtle))
        {
            Debug.LogError("[Race] NavMeshAgent 준비 실패로 경주를 중단합니다.");
            yield break; // 코루틴을 즉시 종료합니다.
        }

        // 경주가 끝난 후 펫의 상태(속도, 애니메이터 상태 등)를 원래대로 복원하기 위해 현재 상태를 저장합니다.
        PetOriginalState rabbitState = new PetOriginalState(rabbit);
        PetOriginalState turtleState = new PetOriginalState(turtle);
        // 위치 고정 코루틴을 담을 변수를 선언합니다.
        Coroutine fixPositionCoroutine = null;
        // ★★★ 새로 추가된 부분: 깃발 인스턴스 변수 ★★★
        GameObject finishFlagInstance = null;
        // ★★★ 여기까지 추가 ★★★
        try // try-finally 블록: 경주 도중 어떤 오류가 발생하더라도 finally 부분은 반드시 실행되도록 보장합니다.
        {
            // 펫 머리 위에 '경주' 감정 표현(이모티콘)을 표시합니다.
            rabbit.ShowEmotion(EmotionType.Race, 40f);
            turtle.ShowEmotion(EmotionType.Race, 40f);

            // --- 1. 결승선 위치 설정 ---
            Vector3 initialCenter = (rabbit.transform.position + turtle.transform.position) / 2; // 두 펫의 중간 지점
            Vector3 finishLine = Vector3.zero; // 결승선 위치 (초기화)
            Vector3 dirToFinish = Vector3.zero; // 결승선 방향 (초기화)
            float totalRaceDistance = 0f; // 실제 경주 거리 (초기화)

            // 적절한 결승선 위치를 찾기 위해 최대 10번 시도합니다.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                // 무작위 방향을 생성합니다.
                Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                if (randomDirection == Vector3.zero) randomDirection = Vector3.forward; // 방향이 (0,0,0)이면 z축 방향으로 설정

                // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
                // 중간 지점에서 무작위 방향으로 raceDistance만큼 떨어진 곳을 목표 결승선으로 설정합니다.
                Vector3 targetFinishLine = initialCenter + randomDirection * raceDistance;

                // NavMesh.SamplePosition: targetFinishLine에서 가장 가까운 NavMesh 위의 점을 찾습니다.
                if (NavMesh.SamplePosition(targetFinishLine, out NavMeshHit navHit, raceDistance * 1.2f, NavMesh.AllAreas))
                {
                    finishLine = navHit.position; // 찾은 점을 결승선으로 확정합니다.
                    totalRaceDistance = Vector3.Distance(initialCenter, finishLine);

                    // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
                    // 실제 거리가 최소 경주 거리보다 긴 경우에만 유효한 것으로 판단하고 루프를 빠져나갑니다.
                    if (totalRaceDistance >= minRaceDistance)
                    {
                        dirToFinish = (finishLine - initialCenter).normalized;
                        break;
                    }
                }
            }
            // 만약 10번 시도 후에도 방향을 못찾았다면, 토끼의 앞 방향을 기본값으로 사용합니다.
            if (dirToFinish == Vector3.zero)
            {
                dirToFinish = rabbit.transform.forward;
                // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
                finishLine = initialCenter + dirToFinish * raceDistance;
                totalRaceDistance = raceDistance;
            }
            // ★★★ 새로 추가된 부분: 깃발 생성 ★★★
            if (finishFlagPrefab != null)
            {
                // 결승선 위치에 깃발 프리팹을 생성하고, 방향을 경주 방향과 반대로 설정합니다.
                Quaternion flagRotation = Quaternion.LookRotation(-dirToFinish);
                finishFlagInstance = Instantiate(finishFlagPrefab, finishLine, flagRotation);
                Debug.Log($"[Race] 결승선 깃발을 {finishLine}에 생성했습니다.");
            }
            // ★★★ 여기까지 추가 ★★★
            // --- 2. 결승선 방향을 기준으로 새로운 출발점 계산 ---
            Vector3 startPosition = CalculateOptimalStartPosition(rabbit, turtle, finishLine, dirToFinish);
            Vector3 rabbitStartPos, turtleStartPos;
            // 두 펫이 나란히 서도록 출발 위치를 조정합니다.
            CalculateAlignedStartPositions(startPosition, dirToFinish, out rabbitStartPos, out turtleStartPos, 3f);

            // --- 3. 펫들을 새로운 출발점으로 이동 ---
            yield return StartCoroutine(MoveToPositions(rabbit, turtle, rabbitStartPos, turtleStartPos, 10f));

            // ★★★ 핵심 수정: 펫들의 실제 도착 위치를 가져와서 사용 ★★★
            Vector3 actualRabbitPos = rabbit.transform.position;
            Vector3 actualTurtlePos = turtle.transform.position;

            // --- 4. 결승선 방향으로 정렬 ---
            Quaternion targetRotation = Quaternion.LookRotation(dirToFinish);

            // ★★★ 수정: 회전을 부드럽게 처리 ★★★
            yield return StartCoroutine(SmoothRotateToDirection(rabbit, turtle, targetRotation));

            // ★★★ 수정: 출발 전 위치 고정 시에만 회전 고정 ★★★
            fixPositionCoroutine = StartCoroutine(FixPositionDuringRaceStart(
                rabbit, turtle, actualRabbitPos, actualTurtlePos, targetRotation, targetRotation));

            yield return new WaitForSeconds(2f);

            // 대기 후, 위치 고정 코루틴을 중지하여 펫들이 움직일 수 있게 합니다.
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }

            // --- 5. 경주 시작 ---

            // 경주 중에는 NavMeshAgent가 이동 방향에 따라 자동으로 회전하도록 설정
            rabbit.agent.updateRotation = true;
            turtle.agent.updateRotation = true;
            // 인스펙터에서 설정한 속도 배율을 적용합니다.
            Vector3 rabbitFinishDestination, turtleFinishDestination;
            CreateSeparateFinishDestinations(finishLine, dirToFinish, out rabbitFinishDestination, out turtleFinishDestination);

            rabbit.agent.speed = rabbitState.originalSpeed * rabbitStartSpeedMultiplier;
            turtle.agent.speed = turtleState.originalSpeed * turtleSpeedMultiplier;

            // 각 펫에 맞는 달리기/걷기 애니메이션을 계속 재생하도록 설정합니다.
            rabbit.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            turtle.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            // ★★★ 핵심 개선: 토끼도 처음부터 결승점을 목표로 설정 ★★★
            // 두 펫 모두 NavMeshAgent의 이동을 시작하고, 최종 목표지점인 결승선을 설정합니다.
            rabbit.agent.isStopped = false;
            turtle.agent.isStopped = false;
            rabbit.agent.SetDestination(rabbitFinishDestination);  // 분리된 목적지!
            turtle.agent.SetDestination(turtleFinishDestination);  // 분리된 목적지!

            // 토끼가 낮잠을 잘 위치를 계산합니다. (실제 목적지 설정이 아닌, 거리 체크용)
            float napDistance = totalRaceDistance * rabbitNapProgress;
            Vector3 napCheckPoint = startPosition + dirToFinish * napDistance;

            // --- 6. 경주 진행 ---
            bool turtleFinished = false; // 거북이 도착 여부
            bool rabbitIsSleeping = false; // 토끼가 자는 중인지 여부
            bool rabbitWokeUp = false; // 토끼가 깨어났는지 여부
            float raceStartTime = Time.time; // 타임아웃 체크를 위한 경주 시작 시간 기록

            // 거북이가 도착할 때까지 루프를 계속 실행합니다.
            while (!turtleFinished)
            {
                // ★★★ 개선: 토끼가 낮잠 지점에 도달했는지 거리로 체크 ★★★
                if (!rabbitIsSleeping && !rabbitWokeUp) // 토끼가 아직 자고 있지도, 깨어나지도 않았다면
                {
                    // 토끼가 출발선에서 경주 방향으로 얼마나 나아갔는지 계산합니다.
                    float rabbitProjectedDistance = Vector3.Dot(rabbit.transform.position - startPosition, dirToFinish);

                    // 토끼가 계산된 낮잠 거리 이상으로 진행했다면
                    if (rabbitProjectedDistance >= napDistance)
                    {
                        rabbitIsSleeping = true;

                        // ★★★ 핵심: 목적지 변경 없이 단순히 멈추기만 함 ★★★
                        rabbit.agent.isStopped = true; // 이동을 멈춥니다.

                        // ★★★ 수정: 잠들 때는 회전 고정 ★★★
                        rabbit.agent.updateRotation = false;
                        rabbit.ShowEmotion(EmotionType.Sleepy, 60f); // '졸림' 감정 표현
            // 잠자는 동안에는 위치만 고정하고 회전은 현재 상태 유지
            Vector3 sleepPosition = rabbit.transform.position;
            Quaternion sleepRotation = rabbit.transform.rotation;

                           fixPositionCoroutine = StartCoroutine(FixPositionDuringSleep(
                rabbit, sleepPosition, sleepRotation));

                        // '휴식' 애니메이션을 오랫동안(999초) 재생시켜 잠자는 것처럼 보이게 합니다.
                        StartCoroutine(rabbit.GetComponent<PetAnimationController>()
                            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 999f, true, false));

                        Debug.Log($"[Race] {rabbit.petName}이(가) 낮잠 지점에서 잠들었습니다!");
                    }
                }

                // 거북이의 진행도를 0과 1 사이의 값으로 계산합니다.
                float turtleProjectedDistance = Vector3.Dot(turtle.transform.position - startPosition, dirToFinish);
                float turtleProgress = Mathf.Clamp01(turtleProjectedDistance / totalRaceDistance);

                // 토끼 깨우기 조건: 토끼가 자는 중이고, 아직 깨어나지 않았으며, 거북이의 진행도가 깨우기 지점을 넘었을 때
                if (rabbitIsSleeping && !rabbitWokeUp && turtleProgress >= turtleWakeUpProgress)
                {
                    rabbitShouldWakeUp = true; // 위치 고정 코루틴을 멈추기 위한 플래그 설정
                    rabbitWokeUp = true;

                    // 위치 고정 코루틴이 실행 중이라면 중지시킵니다.
                    if (fixPositionCoroutine != null)
                    {
                        StopCoroutine(fixPositionCoroutine);
                        fixPositionCoroutine = null;
                    }

                    var rabbitAnimController = rabbit.GetComponent<PetAnimationController>();
                    rabbitAnimController.StopContinuousAnimation(); // 잠자는 애니메이션 중지
                    rabbit.ShowEmotion(EmotionType.Scared, 10f); // '놀람' 감정 표현

                    // ★★★ 핵심: 목적지 재설정 없이 단순히 재개만 함 ★★★
                    // NavMeshAgent가 유효한 상태인지 확인 후
                    if (rabbit.agent != null && rabbit.agent.enabled && rabbit.agent.isOnNavMesh)
                    {
                        // 최종 질주 속도로 변경하고, 이동을 다시 시작합니다.
                        rabbit.agent.speed = rabbitState.originalSpeed * rabbitFinalSprintSpeedMultiplier;
                        rabbit.agent.updateRotation = true;  // 자동 회전 재활성화

                        rabbit.agent.isStopped = false;
                    }

                    // 깜짝 놀라 점프하는 애니메이션을 잠깐 재생합니다.
                    yield return StartCoroutine(rabbitAnimController.PlayAnimationWithCustomDuration(
                        PetAnimationController.PetAnimationType.Jump, 0.5f, false, false));

                    yield return new WaitForSeconds(0.2f); // 잠깐의 딜레이
                    rabbitAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run); // 다시 달리기 애니메이션 재생
                    Debug.Log($"[Race] {rabbit.petName}이(가) 잠에서 깨어나 결승선을 향해 전력질주합니다!");
                }

                // 거북이 도착 체크: 경로 계산이 끝나고, 남은 거리가 0.5f 미만일 때 도착으로 간주합니다.
                if (!turtle.agent.pathPending && turtle.agent.remainingDistance < 0.5f)
                {
                    turtleFinished = true;
                }

                // 타임아웃 체크: 경주 시간이 설정된 타임아웃을 초과하면
                if (Time.time - raceStartTime > raceTimeoutSeconds)
                {
                    Debug.LogWarning("[Race] 경주 시간 초과! 거북이를 강제로 결승선으로 이동시킵니다.");
                    turtle.transform.position = finishLine; // 거북이를 결승선으로 순간이동시킵니다.
                    turtleFinished = true; // 경주를 종료시킵니다.
                }

                yield return null; // 다음 프레임까지 대기
            }

            // --- 7. 경주 종료 및 결과 처리 ---
            rabbit.agent.isStopped = true;
            turtle.agent.isStopped = true;

            Debug.Log("[Race] 경주가 종료되었습니다. 거북이의 승리!");
            turtle.ShowEmotion(EmotionType.Victory, 15f); // 거북이는 '승리' 감정 표현
            rabbit.ShowEmotion(EmotionType.Defeat, 15f);  // 토끼는 '패배' 감정 표현

            // 승리/패배 애니메이션을 재생합니다.
            StartCoroutine(turtle.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2.0f, false, false));
            StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 3.0f, false, false));

            yield return new WaitForSeconds(3f); // 결과 연출을 위해 3초 대기
        }
        finally // 이 블록은 try가 성공적으로 끝나든, 중간에 오류로 중단되든 항상 실행됩니다.
        { // ★★★ 수정: 원래 회전 설정으로 복구 ★★★
            if (rabbit != null && rabbit.agent != null)
            {
                rabbit.agent.updateRotation = false; // PetController가 회전 관리하도록 복구
            }
            if (turtle != null && turtle.agent != null)
            {
                turtle.agent.updateRotation = false; // PetController가 회전 관리하도록 복구
            }
            // ★★★ 새로 추가된 부분: 깃발 제거 ★★★
            if (finishFlagInstance != null)
            {
                Destroy(finishFlagInstance);
                Debug.Log("[Race] 결승선 깃발을 제거했습니다.");
            }
            // ★★★ 여기까지 추가 ★★★
            // --- 상태 복구 ---
            rabbitShouldWakeUp = false; // 플래그 초기화
            if (fixPositionCoroutine != null) StopCoroutine(fixPositionCoroutine); // 위치 고정 코루틴 확실히 종료
            rabbit.HideEmotion(); // 감정 표현 숨기기
            turtle.HideEmotion();
            rabbitState.Restore(rabbit); // 토끼의 원래 상태(속도 등)로 복원
            turtleState.Restore(turtle); // 거북이의 원래 상태로 복원
            rabbit.GetComponent<PetAnimationController>().StopContinuousAnimation(); // 연속 애니메이션 중지
            turtle.GetComponent<PetAnimationController>().StopContinuousAnimation();
            if (rabbit != null) rabbit.isInteracting = false; // 상호작용 상태 해제
            if (turtle != null) turtle.isInteracting = false;
            if (PetInteractionManager.Instance != null)
            {
                // 상호작용 관리자에게 상호작용이 끝났음을 알립니다.
                PetInteractionManager.Instance.NotifyInteractionEnded(rabbit, turtle);
            }
        }
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
    /// 경주 시작 전 출발선에서의 위치와 방향 고정 (짧은 시간)
    /// </summary>
    private IEnumerator FixPositionDuringRaceStart(
        PetController pet1, PetController pet2,
        Vector3 pos1, Vector3 pos2,
        Quaternion rot1, Quaternion rot2)
    {
        const float positionTolerance = 0.3f;
        float fixDuration = 2f; // 최대 2초간만 고정
        float startTime = Time.time;

        while (Time.time - startTime < fixDuration && pet1.isInteracting && pet2.isInteracting)
        {
            // 펫1 위치 및 회전 고정
            if (IsAgentSafelyReady(pet1))
            {
                pet1.agent.isStopped = true;
                pet1.agent.updateRotation = false;

                if (Vector3.Distance(pet1.transform.position, pos1) > positionTolerance)
                {
                    pet1.transform.position = Vector3.Lerp(pet1.transform.position, pos1, Time.deltaTime * 5f);
                }

                pet1.transform.rotation = rot1;
                if (pet1.petModelTransform) pet1.petModelTransform.rotation = rot1;
            }

            // 펫2 위치 및 회전 고정
            if (IsAgentSafelyReady(pet2))
            {
                pet2.agent.isStopped = true;
                pet2.agent.updateRotation = false;

                if (Vector3.Distance(pet2.transform.position, pos2) > positionTolerance)
                {
                    pet2.transform.position = Vector3.Lerp(pet2.transform.position, pos2, Time.deltaTime * 5f);
                }

                pet2.transform.rotation = rot2;
                if (pet2.petModelTransform) pet2.petModelTransform.rotation = rot2;
            }

            yield return null;
        }

        Debug.Log("[Race] 출발선 고정이 완료되었습니다.");
    }

    /// <summary>
    /// 토끼가 잠들어 있는 동안의 위치 고정 (회전은 고정하지 않음)
    /// </summary>
    private IEnumerator FixPositionDuringSleep(PetController rabbit, Vector3 sleepPosition, Quaternion sleepRotation)
    {
        const float positionTolerance = 0.5f;

        while (rabbit.isInteracting && !rabbitShouldWakeUp)
        {
            if (IsAgentSafelyReady(rabbit))
            {
                rabbit.agent.isStopped = true;
                rabbit.agent.updateRotation = false;

                // 위치만 고정, 회전은 자연스러운 잠자는 자세 유지
                if (Vector3.Distance(rabbit.transform.position, sleepPosition) > positionTolerance)
                {
                    rabbit.transform.position = Vector3.Lerp(
                        rabbit.transform.position,
                        sleepPosition,
                        Time.deltaTime * 3f);
                }

                // 잠자는 동안에는 회전을 강제로 고정하지 않음
                // rabbit.transform.rotation = sleepRotation; // 이 줄 제거
            }

            yield return null;
        }

        Debug.Log("[Race] 토끼 수면 위치 고정이 종료되었습니다.");
    }
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
    /// 펫의 NavMeshAgent가 유효하고, 활성화되어 있으며, NavMesh 위에 있는지 등 안전 상태를 종합적으로 체크합니다.
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null &&
               pet.agent != null &&
               pet.agent.enabled && // 활성화 되어 있는가?
               pet.agent.isOnNavMesh && // NavMesh 위에 있는가?
               pet.agent.isActiveAndEnabled; // 컴포넌트와 게임 오브젝트가 모두 활성화 되어 있는가?
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