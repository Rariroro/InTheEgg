// RaceInteraction.cs (수정된 버전)

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class RaceInteraction : BasePetInteraction
{
    public override string InteractionName => "Race";

    // ★★★ 추가: 경주 설정을 인스펙터에서 조절하기 위한 변수들 ★★★
    [Header("Race Settings")]
    [Tooltip("경주의 기본 거리입니다.")]
    public float raceDistance = 100f;
    [Tooltip("경주가 성립하기 위한 최소 거리입니다.")]
    public float minRaceDistance = 80f;
    [Tooltip("경주 타임아웃 시간 (초)")]
    public float raceTimeoutSeconds = 180f;

    [Header("Rabbit Settings")]
    [Tooltip("토끼의 경주 시작 시 속도 배율입니다.")]
    public float rabbitStartSpeedMultiplier = 3.5f;
    [Tooltip("토끼가 낮잠을 잘 위치 (전체 경주 거리 대비 비율, 0.0 ~ 1.0)")]
    [Range(0f, 1f)]
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
    // 토끼 깨우기 상태 추적을 위한 클래스 멤버 변수
    private bool rabbitShouldWakeUp = false;

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.Race;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 동일한 로직 유지
        return (pet1.PetType == PetType.Rabbit && pet2.PetType == PetType.Turtle) ||
               (pet1.PetType == PetType.Turtle && pet2.PetType == PetType.Rabbit);
    }

    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[Race] {pet1.petName}와(과) {pet2.petName}의 달리기 시합이 시작됩니다!");

        rabbitShouldWakeUp = false;

        PetController rabbit = (pet1.PetType == PetType.Rabbit) ? pet1 : pet2;
        PetController turtle = (pet1.PetType == PetType.Turtle) ? pet1 : pet2;
 // ★★★★★ 핵심 수정: 상호작용 시작 전, 두 펫의 NavMeshAgent가 준비될 때까지 대기합니다. ★★★★★
    yield return StartCoroutine(WaitUntilAgentIsReady(rabbit));
    yield return StartCoroutine(WaitUntilAgentIsReady(turtle));
    // ★★★★★ 여기까지 추가 ★★★★★

     // 준비 실패 시 경주 중단
    if (!IsAgentSafelyReady(rabbit) || !IsAgentSafelyReady(turtle))
    {
        Debug.LogError("[Race] NavMeshAgent 준비 실패로 경주를 중단합니다.");
        yield break;
    }
        PetOriginalState rabbitState = new PetOriginalState(rabbit);
        PetOriginalState turtleState = new PetOriginalState(turtle);
        Coroutine fixPositionCoroutine = null;

        try
        {
            rabbit.ShowEmotion(EmotionType.Race, 40f);
            turtle.ShowEmotion(EmotionType.Race, 40f);

            // 1. 결승선 위치 설정
            Vector3 initialCenter = (rabbit.transform.position + turtle.transform.position) / 2;
            Vector3 finishLine = Vector3.zero;
            Vector3 dirToFinish = Vector3.zero;
            float totalRaceDistance = 0f;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                if (randomDirection == Vector3.zero) randomDirection = Vector3.forward;
                // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
                Vector3 targetFinishLine = initialCenter + randomDirection * raceDistance;

                if (NavMesh.SamplePosition(targetFinishLine, out NavMeshHit navHit, raceDistance * 1.2f, NavMesh.AllAreas))
                {
                    finishLine = navHit.position;
                    totalRaceDistance = Vector3.Distance(initialCenter, finishLine);
                    // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
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
                // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
                finishLine = initialCenter + dirToFinish * raceDistance;
                totalRaceDistance = raceDistance;
            }

            // 2. 결승선 방향을 기준으로 새로운 출발점 계산
            Vector3 startPosition = CalculateOptimalStartPosition(rabbit, turtle, finishLine, dirToFinish);
            Vector3 rabbitStartPos, turtleStartPos;
            CalculateAlignedStartPositions(startPosition, dirToFinish, out rabbitStartPos, out turtleStartPos, 3f);

            // 3. 펫들을 새로운 출발점으로 이동
            yield return StartCoroutine(MoveToPositions(rabbit, turtle, rabbitStartPos, turtleStartPos, 10f));

            // 4. 결승선 방향으로 정렬
            Quaternion targetRotation = Quaternion.LookRotation(dirToFinish);
            rabbit.transform.rotation = targetRotation;
            turtle.transform.rotation = targetRotation;
            if (rabbit.petModelTransform != null) rabbit.petModelTransform.rotation = targetRotation;
            if (turtle.petModelTransform != null) turtle.petModelTransform.rotation = targetRotation;

            fixPositionCoroutine = StartCoroutine(FixPositionDuringInteraction(
                rabbit, turtle, rabbitStartPos, turtleStartPos, targetRotation, targetRotation));
            
            yield return new WaitForSeconds(2f);

            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }

            // 5. 경주 시작
            // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
            rabbit.agent.speed = rabbitState.originalSpeed * rabbitStartSpeedMultiplier;
            turtle.agent.speed = turtleState.originalSpeed * turtleSpeedMultiplier;

            rabbit.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            turtle.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            // 토끼의 첫 목적지를 '낮잠 위치'로 설정
            // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
            float napDistance = totalRaceDistance * rabbitNapProgress;
            Vector3 napSpot = startPosition + dirToFinish * napDistance;
            if (NavMesh.SamplePosition(napSpot, out NavMeshHit napHit, 5f, NavMesh.AllAreas))
            {
                napSpot = napHit.position;
            }

            rabbit.agent.isStopped = false;
            turtle.agent.isStopped = false;
            rabbit.agent.SetDestination(napSpot);
            turtle.agent.SetDestination(finishLine);

            // 6. 경주 진행
            bool turtleFinished = false;
            bool rabbitIsAtNapSpot = false;
            bool rabbitIsSleeping = false;
            bool rabbitWokeUp = false;
            float raceStartTime = Time.time;

            while (!turtleFinished)
            {
                if (!rabbitIsAtNapSpot && !rabbit.agent.pathPending && rabbit.agent.remainingDistance < 1f)
                {
                    rabbitIsAtNapSpot = true;
                    rabbitIsSleeping = true;
                    rabbit.agent.isStopped = true;
                    rabbit.ShowEmotion(EmotionType.Sleepy, 60f);
                    fixPositionCoroutine = StartCoroutine(FixPositionDuringInteraction(rabbit, turtle, rabbit.transform.position, turtle.transform.position, rabbit.transform.rotation, turtle.transform.rotation, true, false));
                    StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 999f, true, false));
                }

                float turtleProjectedDistance = Vector3.Dot(turtle.transform.position - startPosition, dirToFinish);
                float turtleProgress = Mathf.Clamp01(turtleProjectedDistance / totalRaceDistance);

                // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
                if (rabbitIsSleeping && !rabbitWokeUp && turtleProgress >= turtleWakeUpProgress)
                {
                    rabbitShouldWakeUp = true;
                    rabbitWokeUp = true;

                    if (fixPositionCoroutine != null) 
                    {
                        StopCoroutine(fixPositionCoroutine);
                        fixPositionCoroutine = null;
                    }

                    var rabbitAnimController = rabbit.GetComponent<PetAnimationController>();
                    rabbitAnimController.StopContinuousAnimation();
                    rabbit.ShowEmotion(EmotionType.Scared, 10f);

                    if (rabbit.agent != null && rabbit.agent.enabled && rabbit.agent.isOnNavMesh)
                    {
                        rabbit.agent.ResetPath();
                        // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
                        rabbit.agent.speed = rabbitState.originalSpeed * rabbitFinalSprintSpeedMultiplier;
                        rabbit.agent.isStopped = false;
                    }

                    yield return StartCoroutine(rabbitAnimController.PlayAnimationWithCustomDuration(
                        PetAnimationController.PetAnimationType.Jump, 0.5f, false, false));

                    if (rabbit.agent != null && rabbit.agent.enabled && rabbit.agent.isOnNavMesh)
                    {
                        rabbit.agent.SetDestination(finishLine);
                        yield return new WaitForSeconds(0.2f);
                        rabbitAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
                        Debug.Log($"[Race] {rabbit.petName}이(가) 잠에서 깨어나 결승선을 향해 달립니다!");
                    }
                }

                if (!turtle.agent.pathPending && turtle.agent.remainingDistance < 0.5f)
                {
                    turtleFinished = true;
                }

                // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
                if (Time.time - raceStartTime > raceTimeoutSeconds)
                {
                    Debug.LogWarning("[Race] 경주 시간 초과! 거북이를 강제로 결승선으로 이동시킵니다.");
                    turtle.transform.position = finishLine;
                    turtleFinished = true;
                }
                yield return null;
            }

            // 7. 경주 종료 및 결과 처리
            rabbit.agent.isStopped = true;
            turtle.agent.isStopped = true;

            Debug.Log("[Race] 경주가 종료되었습니다. 거북이의 승리!");
            turtle.ShowEmotion(EmotionType.Victory, 15f);
            rabbit.ShowEmotion(EmotionType.Defeat, 15f);

            StartCoroutine(turtle.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2.0f, false, false));
            StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 3.0f, false, false));

            yield return new WaitForSeconds(3f);
        }
        finally
        {
            // 이하 로직은 변경 없음 (매우 중요)
            rabbitShouldWakeUp = false;
            if (fixPositionCoroutine != null) StopCoroutine(fixPositionCoroutine);
            rabbit.HideEmotion();
            turtle.HideEmotion();
            rabbitState.Restore(rabbit);
            turtleState.Restore(turtle);
            rabbit.GetComponent<PetAnimationController>().StopContinuousAnimation();
            turtle.GetComponent<PetAnimationController>().StopContinuousAnimation();
            if (rabbit != null) rabbit.isInteracting = false;
            if (turtle != null) turtle.isInteracting = false;
            if (PetInteractionManager.Instance != null)
            {
                PetInteractionManager.Instance.NotifyInteractionEnded(rabbit, turtle);
            }
        }
    }
// ★★★ 새로 추가할 메서드들 ★★★
private IEnumerator SafeWaitForAgentReady(PetController pet, float timeout)
{
    float timer = 0f;
    while (timer < timeout)
    {
        if (IsAgentSafelyReady(pet))
        {
            yield break; // 준비 완료
        }
        
        // 에이전트 복구 시도
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
        
        timer += Time.deltaTime;
        yield return null;
    }
    
    Debug.LogWarning($"[Race] {pet.petName}의 NavMeshAgent가 {timeout}초 내에 준비되지 않았습니다.");
}

private bool IsAgentSafelyReady(PetController pet)
{
    return pet != null && 
           pet.agent != null && 
           pet.agent.enabled && 
           pet.agent.isOnNavMesh &&
           pet.agent.isActiveAndEnabled;
}
    /// <summary>
    /// 결승선과 방향을 고려해서 최적의 출발 지점을 계산합니다.
    /// </summary>
    private Vector3 CalculateOptimalStartPosition(PetController pet1, PetController pet2, Vector3 finishLine, Vector3 raceDirection)
    {
        Vector3 currentCenter = (pet1.transform.position + pet2.transform.position) / 2;
        
        // ★★★ 수정: 하드코딩된 값 대신 인스펙터 변수 사용 ★★★
        Vector3 idealStartPosition = finishLine - raceDirection * raceDistance;
        
        if (NavMesh.SamplePosition(idealStartPosition, out NavMeshHit hit, 20f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        return currentCenter;
    }

    // 이하 다른 메서드들은 변경 없음
    private void CalculateAlignedStartPositions(Vector3 startCenter, Vector3 raceDirection, out Vector3 pet1Pos, out Vector3 pet2Pos, float spacing = 3f)
    {
        Vector3 sideDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;
        pet1Pos = startCenter - sideDirection * (spacing / 2);
        pet2Pos = startCenter + sideDirection * (spacing / 2);
        pet1Pos = FindValidPositionOnNavMesh(pet1Pos, spacing * 2);
        pet2Pos = FindValidPositionOnNavMesh(pet2Pos, spacing * 2);
        Debug.Log($"[Race] 정렬된 출발점 계산 완료: Pet1({pet1Pos}), Pet2({pet2Pos})");
    }

    // ★★★ 안전한 위치 고정 코루틴으로 수정 ★★★
private IEnumerator FixPositionDuringInteraction(
    PetController pet1, PetController pet2, 
    Vector3 pos1, Vector3 pos2, 
    Quaternion rot1, Quaternion rot2, 
    bool fixPet1 = true, bool fixPet2 = true)
{
    while (pet1.isInteracting && pet2.isInteracting && !rabbitShouldWakeUp)
    {
        // ★★★ 핵심 수정: NavMeshAgent 상태 체크 후 위치 고정 ★★★
        if (fixPet1 && IsAgentSafelyReady(pet1))
        {
            // NavMeshAgent를 일시 정지하고 수동으로 위치 설정
            pet1.agent.isStopped = true;
            pet1.transform.position = pos1;
            pet1.transform.rotation = rot1;
            if (pet1.petModelTransform) pet1.petModelTransform.rotation = rot1;
        }
        
        if (fixPet2 && IsAgentSafelyReady(pet2))
        {
            pet2.agent.isStopped = true;
            pet2.transform.position = pos2;
            pet2.transform.rotation = rot2;
            if (pet2.petModelTransform) pet2.petModelTransform.rotation = rot2;
        }
        
        yield return null;
    }
    
    Debug.Log("[Race] FixPositionDuringInteraction 코루틴이 안전하게 종료되었습니다.");
}
}