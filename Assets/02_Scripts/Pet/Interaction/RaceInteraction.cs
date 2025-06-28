// 완전한 RaceInteraction.cs 수정 버전

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class RaceInteraction : BasePetInteraction
{
    public override string InteractionName => "Race";

    // ★★★ 추가: 토끼 깨우기 상태 추적을 위한 클래스 멤버 변수 ★★★
    private bool rabbitShouldWakeUp = false;

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.Race;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        return (pet1.PetType == PetType.Rabbit && pet2.PetType == PetType.Turtle) ||
               (pet1.PetType == PetType.Turtle && pet2.PetType == PetType.Rabbit);
    }

    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
{
    Debug.Log($"[Race] {pet1.petName}와(과) {pet2.petName}의 달리기 시합이 시작됩니다!");

    rabbitShouldWakeUp = false;

    PetController rabbit = (pet1.PetType == PetType.Rabbit) ? pet1 : pet2;
    PetController turtle = (pet1.PetType == PetType.Turtle) ? pet1 : pet2;

    PetOriginalState rabbitState = new PetOriginalState(rabbit);
    PetOriginalState turtleState = new PetOriginalState(turtle);
    Coroutine fixPositionCoroutine = null;

    try
    {
        rabbit.ShowEmotion(EmotionType.Race, 40f);
        turtle.ShowEmotion(EmotionType.Race, 40f);

        // ★★★ 수정: 1. 먼저 결승선 위치를 설정 ★★★
        Vector3 initialCenter = (rabbit.transform.position + turtle.transform.position) / 2;
        Vector3 finishLine = Vector3.zero;
        Vector3 dirToFinish = Vector3.zero;
        float totalRaceDistance = 0f;

        // 결승선 위치 결정
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            if (randomDirection == Vector3.zero) randomDirection = Vector3.forward;
            Vector3 targetFinishLine = initialCenter + randomDirection * 100f;

            if (NavMesh.SamplePosition(targetFinishLine, out NavMeshHit navHit, 120f, NavMesh.AllAreas))
            {
                finishLine = navHit.position;
                totalRaceDistance = Vector3.Distance(initialCenter, finishLine);
                if (totalRaceDistance >= 80f)
                {
                    dirToFinish = (finishLine - initialCenter).normalized;
                    break;
                }
            }
        }
        if (dirToFinish == Vector3.zero)
        {
            dirToFinish = rabbit.transform.forward;
            finishLine = initialCenter + dirToFinish * 100f;
            totalRaceDistance = 100f;
        }

        // ★★★ 수정: 2. 결승선 방향을 기준으로 새로운 출발점 계산 ★★★
        Vector3 startPosition = CalculateOptimalStartPosition(rabbit, turtle, finishLine, dirToFinish);
        Vector3 rabbitStartPos, turtleStartPos;
        CalculateAlignedStartPositions(startPosition, dirToFinish, out rabbitStartPos, out turtleStartPos, 3f);

        // ★★★ 수정: 3. 펫들을 새로운 출발점으로 이동 ★★★
        yield return StartCoroutine(MoveToPositions(rabbit, turtle, rabbitStartPos, turtleStartPos, 10f));

        // ★★★ 수정: 4. 결승선 방향으로 정렬 ★★★
        Quaternion targetRotation = Quaternion.LookRotation(dirToFinish);
        rabbit.transform.rotation = targetRotation;
        turtle.transform.rotation = targetRotation;
        if (rabbit.petModelTransform != null) rabbit.petModelTransform.rotation = targetRotation;
        if (turtle.petModelTransform != null) turtle.petModelTransform.rotation = targetRotation;

        // 고정 위치 코루틴으로 정렬 유지
        fixPositionCoroutine = StartCoroutine(FixPositionDuringInteraction(
            rabbit, turtle, rabbitStartPos, turtleStartPos, targetRotation, targetRotation));
        
        yield return new WaitForSeconds(2f); // 정렬 확인 시간

        if (fixPositionCoroutine != null)
        {
            StopCoroutine(fixPositionCoroutine);
            fixPositionCoroutine = null;
        }

        // ★★★ 5. 경주 시작 ★★★
        rabbit.agent.speed = rabbitState.originalSpeed * 3.5f;
        turtle.agent.speed = turtleState.originalSpeed * 0.8f;

        rabbit.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        turtle.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        // 토끼의 첫 목적지를 '낮잠 위치'로 설정
        float napDistance = totalRaceDistance * 0.4f;
        Vector3 napSpot = startPosition + dirToFinish * napDistance; // ★★★ startPosition 사용 ★★★
        if (NavMesh.SamplePosition(napSpot, out NavMeshHit napHit, 5f, NavMesh.AllAreas))
        {
            napSpot = napHit.position;
        }

        rabbit.agent.isStopped = false;
        turtle.agent.isStopped = false;
        rabbit.agent.SetDestination(napSpot);
        turtle.agent.SetDestination(finishLine);

        // ★★★ 6. 경주 진행 (기존 로직과 동일) ★★★
        bool turtleFinished = false;
        bool rabbitIsAtNapSpot = false;
        bool rabbitIsSleeping = false;
        bool rabbitWokeUp = false;
        float raceStartTime = Time.time;

        while (!turtleFinished)
        {
            // 토끼가 낮잠 위치에 도착했는지 확인
            if (!rabbitIsAtNapSpot && !rabbit.agent.pathPending && rabbit.agent.remainingDistance < 1f)
            {
                rabbitIsAtNapSpot = true;
                rabbitIsSleeping = true;
                rabbit.agent.isStopped = true;

                rabbit.ShowEmotion(EmotionType.Sleepy, 60f);

                fixPositionCoroutine = StartCoroutine(FixPositionDuringInteraction(rabbit, turtle, rabbit.transform.position, turtle.transform.position, rabbit.transform.rotation, turtle.transform.rotation, true, false));
                StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 999f, true, false));
            }

            // 거북이 진행도에 따라 토끼를 깨움
            float turtleProjectedDistance = Vector3.Dot(turtle.transform.position - startPosition, dirToFinish);
            float turtleProgress = Mathf.Clamp01(turtleProjectedDistance / totalRaceDistance);

            if (rabbitIsSleeping && !rabbitWokeUp && turtleProgress >= 0.9f)
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
                    rabbit.agent.speed = rabbitState.originalSpeed * 5.0f;
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

            // 거북이가 결승선에 도착하면 경주 종료
            if (!turtle.agent.pathPending && turtle.agent.remainingDistance < 0.5f)
            {
                turtleFinished = true;
            }

            // 시간 초과 처리
            if (Time.time - raceStartTime > 180f)
            {
                turtle.transform.position = finishLine;
                turtleFinished = true;
            }
            yield return null;
        }

        // ★★★ 7. 경주 종료 및 결과 처리 ★★★
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

/// <summary>
/// 결승선과 방향을 고려해서 최적의 출발 지점을 계산합니다.
/// </summary>
private Vector3 CalculateOptimalStartPosition(PetController pet1, PetController pet2, Vector3 finishLine, Vector3 raceDirection)
{
    Vector3 currentCenter = (pet1.transform.position + pet2.transform.position) / 2;
    
    // 결승선에서 역방향으로 적절한 거리만큼 떨어진 지점을 출발점으로 설정
    Vector3 idealStartPosition = finishLine - raceDirection * 100f;
    
    // NavMesh에서 유효한 위치 찾기
    if (NavMesh.SamplePosition(idealStartPosition, out NavMeshHit hit, 20f, NavMesh.AllAreas))
    {
        return hit.position;
    }
    
    // 유효한 위치를 찾지 못하면 현재 중심점 사용
    return currentCenter;
}

/// <summary>
/// 결승선 방향에 맞춰서 두 펫이 나란히 설 위치를 계산합니다.
/// </summary>
private void CalculateAlignedStartPositions(Vector3 startCenter, Vector3 raceDirection, 
    out Vector3 pet1Pos, out Vector3 pet2Pos, float spacing = 3f)
{
    // 경주 방향에 수직인 방향으로 펫들을 배치
    Vector3 sideDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;
    
    pet1Pos = startCenter - sideDirection * (spacing / 2);
    pet2Pos = startCenter + sideDirection * (spacing / 2);
    
    // NavMesh 위의 유효한 위치로 보정
    pet1Pos = FindValidPositionOnNavMesh(pet1Pos, spacing * 2);
    pet2Pos = FindValidPositionOnNavMesh(pet2Pos, spacing * 2);
    
    Debug.Log($"[Race] 정렬된 출발점 계산 완료: Pet1({pet1Pos}), Pet2({pet2Pos})");
}
    // ★★★ 수정된 FixPositionDuringInteraction 메서드 ★★★
    private IEnumerator FixPositionDuringInteraction(PetController pet1, PetController pet2, Vector3 pos1, Vector3 pos2, Quaternion rot1, Quaternion rot2, bool fixPet1 = true, bool fixPet2 = true)
    {
        while (pet1.isInteracting && pet2.isInteracting && !rabbitShouldWakeUp)
        {
            if (fixPet1)
            {
                pet1.transform.position = pos1;
                pet1.transform.rotation = rot1;
                if (pet1.petModelTransform) pet1.petModelTransform.rotation = rot1;
            }
            if (fixPet2)
            {
                pet2.transform.position = pos2;
                pet2.transform.rotation = rot2;
                if (pet2.petModelTransform) pet2.petModelTransform.rotation = rot2;
            }
            yield return null;
        }
        
        Debug.Log("[Race] FixPositionDuringInteraction 코루틴이 종료되었습니다.");
    }
}