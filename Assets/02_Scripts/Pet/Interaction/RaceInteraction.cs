// 달리기 시합 상호작용 구현 (거북이 승리 고정 및 방향 문제 해결 버전)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class RaceInteraction : BasePetInteraction
{
    public override string InteractionName => "Race";

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.Race;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;

        return (type1 == PetType.Rabbit && type2 == PetType.Turtle) ||
               (type1 == PetType.Turtle && type2 == PetType.Rabbit);
    }

    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[Race] {pet1.petName}와(과) {pet2.petName}의 달리기 시합이 시작됩니다!");

        PetController rabbit = (pet1.PetType == PetType.Rabbit) ? pet1 : pet2;
        PetController turtle = (pet1.PetType == PetType.Turtle) ? pet1 : pet2;

        Debug.Log($"[Race] 토끼 역할: {rabbit.petName}, 거북이 역할: {turtle.petName}");

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
            bool validCourseFound = false;
            float minRequiredDistance = 80f;

            for (int attempt = 0; attempt < 10 && !validCourseFound; attempt++)
            {
                Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                Vector3 targetFinishLine = startPosition + randomDirection * 120f;

                NavMeshHit navHit;
                if (NavMesh.SamplePosition(targetFinishLine, out navHit, 150f, NavMesh.AllAreas))
                {
                    finishLine = navHit.position;
                    if (Vector3.Distance(startPosition, finishLine) >= minRequiredDistance)
                    {
                        validCourseFound = true;
                    }
                }
            }
            if (!validCourseFound)
            {
                finishLine = startPosition + rabbit.transform.forward * 100f;
            }

            // 3. 출발 준비
            Vector3 dirToFinish = (finishLine - startPosition).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(dirToFinish);
            rabbit.transform.rotation = targetRotation;
            turtle.transform.rotation = targetRotation;

            fixPositionCoroutine = StartCoroutine(FixPositionDuringInteraction(rabbit, turtle, rabbit.transform.position, turtle.transform.position, rabbit.transform.rotation, turtle.transform.rotation));
            yield return new WaitForSeconds(1.5f);
            StopCoroutine(fixPositionCoroutine);
            fixPositionCoroutine = null;

            // 4. 경주 시작
            rabbit.agent.speed = rabbitState.originalSpeed * 3.5f;
            rabbit.agent.acceleration = rabbitState.originalAcceleration * 2f;
            turtle.agent.speed = turtleState.originalSpeed * 0.8f;

            rabbit.GetComponent<PetAnimationController>().SetContinuousAnimation(2);
            turtle.GetComponent<PetAnimationController>().SetContinuousAnimation(1);

            rabbit.agent.isStopped = false;
            turtle.agent.isStopped = false;
            rabbit.agent.SetDestination(finishLine);
            turtle.agent.SetDestination(finishLine);

            // 5. 경주 진행 (거북이 승리 보장 로직)
            bool turtleFinished = false;
            bool rabbitIsSleeping = false;
            bool rabbitWokeUp = false;
            float raceStartTime = Time.time;
            float totalRaceDistance = Vector3.Distance(startPosition, finishLine);

            while (!turtleFinished)
            {
                float rabbitProjectedDistance = Vector3.Dot(rabbit.transform.position - startPosition, dirToFinish);
                float rabbitProgress = Mathf.Clamp01(rabbitProjectedDistance / totalRaceDistance);
                
                float turtleProjectedDistance = Vector3.Dot(turtle.transform.position - startPosition, dirToFinish);
                float turtleProgress = Mathf.Clamp01(turtleProjectedDistance / totalRaceDistance);

                if (!rabbitIsSleeping && rabbitProgress >= 0.35f)
                {
                    rabbitIsSleeping = true;
                    rabbit.agent.isStopped = true;
                    rabbit.ShowEmotion(EmotionType.Sleepy, 40f);
                    fixPositionCoroutine = StartCoroutine(FixPositionDuringInteraction(rabbit, turtle, rabbit.transform.position, turtle.transform.position, rabbit.transform.rotation, turtle.transform.rotation, true, false));
                    StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, 999f, true, false));
                }

                if (rabbitIsSleeping && !rabbitWokeUp && turtleProgress >= 0.9f)
                {
                    rabbitWokeUp = true;
                    rabbit.ShowEmotion(EmotionType.Surprised, 10f);
                    if (fixPositionCoroutine != null) StopCoroutine(fixPositionCoroutine);
                    fixPositionCoroutine = null;
                    rabbit.agent.isStopped = false;
                    rabbit.agent.speed = rabbitState.originalSpeed * 5.0f;
                    rabbit.agent.acceleration = rabbitState.originalAcceleration * 3f;
                    yield return StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(3, 1.0f, false, false));
                    rabbit.GetComponent<PetAnimationController>().SetContinuousAnimation(2);
                }

                if (!turtle.agent.pathPending && turtle.agent.remainingDistance < 0.5f)
                {
                    turtleFinished = true;
                }

                if (Time.time - raceStartTime > 180f)
                {
                    turtle.transform.position = finishLine;
                    turtleFinished = true;
                }
                yield return null;
            }

            // 6. 경주 종료
            rabbit.agent.isStopped = true;
            turtle.agent.isStopped = true;

            Debug.Log("[Race] 경주가 종료되었습니다. 거북이의 승리!");
            turtle.ShowEmotion(EmotionType.Victory, 15f);
            rabbit.ShowEmotion(EmotionType.Defeat, 15f);
            StartCoroutine(turtle.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(3, 2.0f, false, false));
            StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(4, 3.0f, false, false));
            
            yield return new WaitForSeconds(3f);
        }
        finally
        {
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
            }
            rabbit.HideEmotion();
            turtle.HideEmotion();
            rabbitState.Restore(rabbit);
            turtleState.Restore(turtle);
            rabbit.GetComponent<PetAnimationController>().StopContinuousAnimation();
            turtle.GetComponent<PetAnimationController>().StopContinuousAnimation();
        }
    }

    private IEnumerator FixPositionDuringInteraction(PetController pet1, PetController pet2, Vector3 pos1, Vector3 pos2, Quaternion rot1, Quaternion rot2, bool fixPet1 = true, bool fixPet2 = true)
    {
        while (pet1.isInteracting && pet2.isInteracting)
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
    }
}