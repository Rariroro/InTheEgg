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

        // ★★★ 추가: 플래그 초기화 ★★★
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

            // 1. 출발 지점 준비
            Vector3 startPosition = (rabbit.transform.position + turtle.transform.position) / 2;
            Vector3 rabbitStartPos, turtleStartPos;
            CalculateStartPositions(rabbit, turtle, out rabbitStartPos, out turtleStartPos, 3f);
            yield return StartCoroutine(MoveToPositions(rabbit, turtle, rabbitStartPos, turtleStartPos, 10f));

            // 2. 결승선 위치 설정
            Vector3 finishLine = Vector3.zero;
            Vector3 dirToFinish = Vector3.zero;
            float totalRaceDistance = 0f;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                if (randomDirection == Vector3.zero) randomDirection = Vector3.forward;
                Vector3 targetFinishLine = startPosition + randomDirection * 100f;

                if (NavMesh.SamplePosition(targetFinishLine, out NavMeshHit navHit, 120f, NavMesh.AllAreas))
                {
                    finishLine = navHit.position;
                    totalRaceDistance = Vector3.Distance(startPosition, finishLine);
                    if (totalRaceDistance >= 80f)
                    {
                        dirToFinish = (finishLine - startPosition).normalized;
                        break;
                    }
                }
            }
            if (dirToFinish == Vector3.zero)
            {
                dirToFinish = rabbit.transform.forward;
                finishLine = startPosition + dirToFinish * 100f;
                totalRaceDistance = 100f;
            }

            // 3. 출발 준비
            Quaternion targetRotation = Quaternion.LookRotation(dirToFinish);
            rabbit.transform.rotation = targetRotation;
            turtle.transform.rotation = targetRotation;

            fixPositionCoroutine = StartCoroutine(FixPositionDuringInteraction(rabbit, turtle, rabbit.transform.position, turtle.transform.position, targetRotation, targetRotation));
            yield return new WaitForSeconds(1.5f);

            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }

            // 4. 경주 시작
            rabbit.agent.speed = rabbitState.originalSpeed * 3.5f;
            turtle.agent.speed = turtleState.originalSpeed * 0.8f;

            rabbit.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            turtle.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            // 토끼의 첫 목적지를 '낮잠 위치'로 설정
            float napDistance = totalRaceDistance * 0.4f;
            Vector3 napSpot = startPosition + dirToFinish * napDistance;
            if (NavMesh.SamplePosition(napSpot, out NavMeshHit napHit, 5f, NavMesh.AllAreas))
            {
                napSpot = napHit.position;
            }

            rabbit.agent.isStopped = false;
            turtle.agent.isStopped = false;
            rabbit.agent.SetDestination(napSpot);
            turtle.agent.SetDestination(finishLine);

            // 5. 경주 진행
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
                    // ★★★ 핵심 수정: 플래그 설정으로 FixPositionCoroutine을 중지시킴 ★★★
                    rabbitShouldWakeUp = true;
                    rabbitWokeUp = true;

                    if (fixPositionCoroutine != null) 
                    {
                        StopCoroutine(fixPositionCoroutine);
                        fixPositionCoroutine = null;
                    }

                    // ★★★ 기존 애니메이션 완전히 중지 ★★★
                    var rabbitAnimController = rabbit.GetComponent<PetAnimationController>();
                    rabbitAnimController.StopContinuousAnimation();

                    rabbit.ShowEmotion(EmotionType.Scared, 10f);

                    // ★★★ NavMeshAgent 상태를 애니메이션 재생 전에 먼저 준비 ★★★
                    if (rabbit.agent != null && rabbit.agent.enabled && rabbit.agent.isOnNavMesh)
                    {
                        rabbit.agent.ResetPath();
                        rabbit.agent.speed = rabbitState.originalSpeed * 5.0f;
                        rabbit.agent.isStopped = false;
                    }

                    // ★★★ 점프 애니메이션 재생 (짧게, Idle로 돌아가지 않음) ★★★
                    yield return StartCoroutine(rabbitAnimController.PlayAnimationWithCustomDuration(
                        PetAnimationController.PetAnimationType.Jump, 0.5f, false, false));

                    // ★★★ NavMeshAgent 목적지 설정 ★★★
                    if (rabbit.agent != null && rabbit.agent.enabled && rabbit.agent.isOnNavMesh)
                    {
                        rabbit.agent.SetDestination(finishLine);
                        yield return new WaitForSeconds(0.2f); // 경로 계산 대기

                        // ★★★ 달리기 애니메이션 설정 ★★★
                        rabbitAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);

                        Debug.Log($"[Race] {rabbit.petName}이(가) 잠에서 깨어나 결승선을 향해 달립니다!");
                        Debug.Log($"[Race Debug] 토끼 위치: {rabbit.transform.position}, 결승선: {finishLine}");
                        Debug.Log($"[Race Debug] remainingDistance: {rabbit.agent.remainingDistance:F2}");
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

            // 6. 경주 종료 및 결과 처리
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
            // ★★★ 정리 작업 ★★★
            rabbitShouldWakeUp = false; // 플래그 초기화
            
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