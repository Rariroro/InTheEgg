// 달리기 시합 상호작용 구현 (개선 버전)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class RaceInteraction : BasePetInteraction
{
    public override string InteractionName => "Race";

    // 상호작용 유형 지정
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.Race;
    }

    // 위치와 회전을 고정하는 코루틴 추가
    private IEnumerator FixPositionDuringInteraction(
        PetController pet1, PetController pet2,
        Vector3 pos1, Vector3 pos2,
        Quaternion rot1, Quaternion rot2,
        bool fixPet1 = true, bool fixPet2 = true)
    {
        // 상호작용이 진행되는 동안 위치와 회전 고정
        while (pet1.isInteracting && pet2.isInteracting)
        {
            // 펫1 위치/회전 고정 (필요한 경우)
            if (fixPet1)
            {
                pet1.transform.position = pos1;
                pet1.transform.rotation = rot1;
                // 모델도 함께 고정
                if (pet1.petModelTransform) pet1.petModelTransform.rotation = rot1;
            }

            // 펫2 위치/회전 고정 (필요한 경우)
            if (fixPet2)
            {
                pet2.transform.position = pos2;
                pet2.transform.rotation = rot2;
                // 모델도 함께 고정
                if (pet2.petModelTransform) pet2.petModelTransform.rotation = rot2;
            }

            yield return null;
        }
    }

    // 이 상호작용이 가능한지 확인
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        PetType type1 = pet1.PetType;
        PetType type2 = pet2.PetType;

        // 토끼와 거북이는 경주
        if ((type1 == PetType.Rabbit && type2 == PetType.Turtle) ||
            (type1 == PetType.Turtle && type2 == PetType.Rabbit))
        {
            return true;
        }

        return false;
    }

    // 상호작용 수행
    public override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[Race] {pet1.petName}와(과) {pet2.petName}의 달리기 시합이 시작됩니다!");

        // 토끼와 거북이 식별
        PetController rabbit = null;
        PetController turtle = null;

        // 펫 타입에 따라 토끼와 거북이 식별
        if (pet1.PetType == PetType.Rabbit && pet2.PetType == PetType.Turtle)
        {
            rabbit = pet1;
            turtle = pet2;
        }
        else if (pet2.PetType == PetType.Rabbit && pet1.PetType == PetType.Turtle)
        {
            rabbit = pet2;
            turtle = pet1;
        }
        else
        {
            // 토끼나 거북이가 아닌 경우, 무작위로 역할 배정
            Debug.Log("[Race] 토끼와 거북이가 아닌 펫들입니다. 역할을 임의로 배정합니다.");
            if (Random.value > 0.5f)
            {
                rabbit = pet1;
                turtle = pet2;
            }
            else
            {
                rabbit = pet2;
                turtle = pet1;
            }
        }

        Debug.Log($"[Race] 토끼 역할: {rabbit.petName}, 거북이 역할: {turtle.petName}");

        // 원래 상태 저장 (개선된 헬퍼 클래스 사용)
        PetOriginalState rabbitState = new PetOriginalState(rabbit);
        PetOriginalState turtleState = new PetOriginalState(turtle);

        // 위치 고정 코루틴 참조 저장
        Coroutine fixPositionCoroutine = null;

        try
        {
            // 감정 표현 추가 - 경쟁적인 표정
            rabbit.ShowEmotion(EmotionType.Race, 40f);
            turtle.ShowEmotion(EmotionType.Race, 40f);

            // 1. 출발 지점 준비 - 두 펫을 나란히 배치
            Vector3 startPosition = (rabbit.transform.position + turtle.transform.position) / 2;
            Vector3 raceDirection = Random.insideUnitSphere;
            raceDirection.y = 0;
            raceDirection.Normalize();

            // 출발선 위치 계산
            Vector3 rabbitStartPos, turtleStartPos;
            CalculateStartPositions(rabbit, turtle, out rabbitStartPos, out turtleStartPos, 3f);

            // 공통 MoveToPositions 메소드 사용
            yield return StartCoroutine(MoveToPositions(rabbit, turtle, rabbitStartPos, turtleStartPos, 10f));

            // 2. 결승선 위치 설정 (100 유닛 거리)
            // 2. 결승선 위치 설정 - 충분한 거리 보장
            Vector3 finishLine = Vector3.zero;
            float actualDistance = 0f;
            float minRequiredDistance = 80f; // 최소 80 유닛 거리 보장
            float maxAttempts = 10; // 최대 시도 횟수
            bool validCourseFound = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // 랜덤 방향 생성 (Y축 제외한 수평 방향)
                Vector3 randomDirection = new Vector3(
                    Random.Range(-1f, 1f),
                    0,
                    Random.Range(-1f, 1f)
                ).normalized;

                // 더 긴 거리로 목표점 설정 (120 유닛)
                Vector3 targetFinishLine = startPosition + randomDirection * 120f;

                // NavMesh에서 유효한 위치 찾기
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(targetFinishLine, out navHit, 150f, NavMesh.AllAreas))
                {
                    finishLine = navHit.position;
                    actualDistance = Vector3.Distance(startPosition, finishLine);

                    // 최소 거리 체크
                    if (actualDistance >= minRequiredDistance)
                    {
                        validCourseFound = true;
                        Debug.Log($"[Race] 적절한 코스 발견! 시도 {attempt + 1}회, 거리: {actualDistance:F1} 유닛");
                        break;
                    }
                    else
                    {
                        Debug.Log($"[Race] 코스가 너무 짧음 (시도 {attempt + 1}): {actualDistance:F1} 유닛 < {minRequiredDistance} 유닛");
                    }
                }
                else
                {
                    Debug.Log($"[Race] NavMesh에서 유효한 위치를 찾지 못함 (시도 {attempt + 1})");
                }
            }

            // 적절한 코스를 찾지 못한 경우 기본 설정
            if (!validCourseFound)
            {
                Debug.LogWarning("[Race] 적절한 코스를 찾지 못했습니다. 기본 코스로 설정합니다.");

                // 단순히 앞쪽 방향으로 설정
                Vector3 forwardDirection = rabbit.transform.forward;
                finishLine = startPosition + forwardDirection * 100f;

                // 마지막으로 NavMesh 보정 시도
                NavMeshHit fallbackHit;
                if (NavMesh.SamplePosition(finishLine, out fallbackHit, 120f, NavMesh.AllAreas))
                {
                    finishLine = fallbackHit.position;
                    actualDistance = Vector3.Distance(startPosition, finishLine);
                }
                else
                {
                    // 정말 마지막 수단 - 현재 위치에서 고정된 거리
                    finishLine = startPosition + Vector3.forward * 100f;
                    actualDistance = 100f;
                }

                Debug.Log($"[Race] 기본 코스 설정 완료. 거리: {actualDistance:F1} 유닛");
            }

            Debug.Log($"[Race] 최종 결승선 위치: {finishLine}, 실제 거리: {actualDistance:F1} 유닛");

            // Debug.Log($"[Race] 결승선 위치: {finishLine}, 거리: {Vector3.Distance(startPosition, finishLine)}");

            // 3. 출발 준비 - 결승선 방향으로 회전
            Vector3 dirToFinish = (finishLine - startPosition).normalized;
            dirToFinish.y = 0; // 수평 방향만 고려
            dirToFinish.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(dirToFinish);

            // 두 펫을 결승선 방향으로 회전
            rabbit.transform.rotation = targetRotation;
            turtle.transform.rotation = targetRotation;
            if (rabbit.petModelTransform != null)
                rabbit.petModelTransform.rotation = targetRotation;
            if (turtle.petModelTransform != null)
                turtle.petModelTransform.rotation = targetRotation;

            // 위치 고정 설정 (출발 준비 중)
            Vector3 rabbitFixedPos = rabbit.transform.position;
            Vector3 turtleFixedPos = turtle.transform.position;
            Quaternion rabbitFixedRot = rabbit.transform.rotation;
            Quaternion turtleFixedRot = turtle.transform.rotation;

            // 위치 고정 코루틴 시작
            fixPositionCoroutine = StartCoroutine(
                FixPositionDuringInteraction(
                    rabbit, turtle,
                    rabbitFixedPos, turtleFixedPos,
                    rabbitFixedRot, turtleFixedRot
                )
            );

            // 잠시 대기 (긴장감 조성)
            yield return new WaitForSeconds(1.5f);

            // 위치 고정 코루틴 중지 (경주 시작을 위해)
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
                fixPositionCoroutine = null;
            }

            // 4. 경주 시작 세팅
            // 토끼는 빠르게, 거북이는 느리게 속도 설정
            rabbit.agent.speed = rabbitState.originalSpeed * 3.5f;
            rabbit.agent.acceleration = rabbitState.originalAcceleration * 2f;
            turtle.agent.speed = turtleState.originalSpeed * 0.8f;

            // 달리기 애니메이션 시작
            rabbit.GetComponent<PetAnimationController>().SetContinuousAnimation(2); // 달리기 애니메이션
            turtle.GetComponent<PetAnimationController>().SetContinuousAnimation(1); // 거북이는 걷기 애니메이션

            // 결승선으로 이동 시작
            rabbit.agent.isStopped = false;
            turtle.agent.isStopped = false;
            rabbit.agent.SetDestination(finishLine);
            turtle.agent.SetDestination(finishLine);

            Debug.Log("[Race] 경주 시작! 토끼가 빠르게 출발합니다.");

            // 5. 토끼가 중간지점까지 달린 후 잠들기
            float rabbitSleepThreshold = 0.35f; // 토끼가 전체 거리의 35% 지점에서 잠듦
            bool rabbitIsSleeping = false;
            bool turtleFinished = false;
            bool rabbitWokeUp = false;

            float raceStartTime = Time.time;

            while (!turtleFinished)
            {
                // 현재 각 펫의 경주 진행 상황 계산
                float rabbitProgress = 0f;
                float turtleProgress = 0f;

                // 시작 위치에서 결승선까지의 총 거리 계산
                float totalRaceDistance = Vector3.Distance(startPosition, finishLine);

                if (!rabbit.agent.pathPending)
                {
                    // 결승선 방향으로의 투영 거리 계산 (방향성 고려)
                    float rabbitProjectedDistance = Vector3.Dot(rabbit.transform.position - startPosition, dirToFinish);
                    rabbitProgress = Mathf.Clamp01(rabbitProjectedDistance / totalRaceDistance);
                }

                if (!turtle.agent.pathPending)
                {
                    // 결승선 방향으로의 투영 거리 계산 (방향성 고려)
                    float turtleProjectedDistance = Vector3.Dot(turtle.transform.position - startPosition, dirToFinish);
                    turtleProgress = Mathf.Clamp01(turtleProjectedDistance / totalRaceDistance);
                }

                // 토끼가 중간지점에 도달하고 아직 잠들지 않았다면
                if (!rabbitIsSleeping && rabbitProgress >= rabbitSleepThreshold)
                {
                    // 토끼 잠들기
                    Debug.Log($"[Race] 토끼({rabbit.petName})가 중간에서 잠들어 버렸습니다!");
                    rabbitIsSleeping = true;
                    rabbit.agent.isStopped = true;

                    // 감정 변경 - 졸린 표정
                    rabbit.ShowEmotion(EmotionType.Sleepy, 40f);

                    // 토끼 위치 고정 시작 (잠들어 있는 동안)
                    Vector3 rabbitSleepPos = rabbit.transform.position;
                    Quaternion rabbitSleepRot = rabbit.transform.rotation;

                    fixPositionCoroutine = StartCoroutine(
                        FixPositionDuringInteraction(
                            rabbit, turtle,
                            rabbitSleepPos, turtleFixedPos,
                            rabbitSleepRot, turtleFixedRot,
                            true, false // 토끼만 고정
                        )
                    );

                    // 쉬기/잠자기 애니메이션 재생
                    StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, 999f, true, false));
                }

                // 거북이가 결승선에 가까워지면 (90% 지점)
                if (!rabbitWokeUp && turtleProgress >= 0.9f)
                {
                    // 토끼 깨우기
                    Debug.Log($"[Race] 거북이가 결승선에 가까워지자 토끼({rabbit.petName})가 깨어났습니다!");
                    rabbitWokeUp = true;

                    // 감정 변경 - 놀란 표정
                    rabbit.ShowEmotion(EmotionType.Surprised, 10f);

                    // 위치 고정 코루틴 중지
                    if (fixPositionCoroutine != null)
                    {
                        StopCoroutine(fixPositionCoroutine);
                        fixPositionCoroutine = null;
                    }

                    rabbit.agent.isStopped = false;
                    // ★ 토끼가 깨어났을 때 더 빠른 속도로 설정 (급박함 표현)
                    rabbit.agent.speed = rabbitState.originalSpeed * 5.0f;  // 기존 3.5f에서 5.0f로 증가
                    rabbit.agent.acceleration = rabbitState.originalAcceleration * 3f;  // 기존 2f에서 3f로 증가

                    // 놀람/깨어남 애니메이션 후 달리기
                    StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(3, 1.0f, false, false));
                    yield return new WaitForSeconds(1.0f);
                    rabbit.GetComponent<PetAnimationController>().SetContinuousAnimation(2); // 다시 달리기 애니메이션
                }

                // 거북이가 결승선에 도착했는지 확인
                if (!turtleFinished && !turtle.agent.pathPending && turtle.agent.remainingDistance < 0.5f)
                {
                    turtleFinished = true;
                    Debug.Log($"[Race] 거북이({turtle.petName})가 결승선에 도착했습니다! 승리!");

                    // // 감정 변경 - 승리 표정
                    // turtle.ShowEmotion(EmotionType.Victory, 15f);

                    // 거북이 승리 애니메이션 (점프)
                    StartCoroutine(turtle.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(3, 2.0f, false, false));

                    // 토끼가 아직 결승선에 도착하지 않았다면, 실망 애니메이션
                    if (rabbitProgress < 0.99f)
                    {
                        // 토끼 정지
                        rabbit.agent.isStopped = true;

                        // // 감정 변경 - 패배 표정
                        // rabbit.ShowEmotion(EmotionType.Defeat, 15f);

                        // 토끼 실망/고개 숙임 애니메이션 (앉기 애니메이션으로 대체)
                        StartCoroutine(rabbit.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(4, 3.0f, false, false));
                    }
                }

                // 경주 진행 상황 로깅 (디버깅용)
                if (Time.frameCount % 60 == 0) // 약 1초마다 로깅
                {
                    Debug.Log($"[Race] 진행 상황 - 토끼: {rabbitProgress:P0}, 거북이: {turtleProgress:P0}");
                }

                // 경주 시간이 너무 길어지면 종료 (최대 3분)
                if (Time.time - raceStartTime > 180f)
                {
                    Debug.LogWarning("[Race] 경주 시간이 너무 길어져 강제로 종료합니다.");
                    break;
                }

                yield return null;
            }

            // 추가 연출: 거북이 승리 축하
            yield return new WaitForSeconds(2f);

            // 6. 경주 종료 후 정리
            Debug.Log("[Race] 경주가 종료되었습니다. 거북이의 승리!");
        }
        finally
        {
            // 위치 고정 코루틴 중지
            if (fixPositionCoroutine != null)
            {
                StopCoroutine(fixPositionCoroutine);
            }

            // 감정 말풍선 숨기기
            rabbit.HideEmotion();
            turtle.HideEmotion();

            // 원래 상태로 복원
            rabbitState.Restore(rabbit);
            turtleState.Restore(turtle);

            // 애니메이션 원래대로
            rabbit.GetComponent<PetAnimationController>().StopContinuousAnimation();
            turtle.GetComponent<PetAnimationController>().StopContinuousAnimation();
        }

        // 경주 종료 후 잠시 대기
        yield return new WaitForSeconds(3f);
    }
}