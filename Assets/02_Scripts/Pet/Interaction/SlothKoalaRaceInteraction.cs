// Pet.zip/Interaction/SlothKoalaRaceInteraction.cs (오류 수정 및 최적화 완료 버전)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SlothKoalaRaceInteraction : BasePetInteraction
{
    public override string InteractionName => "SlothKoalaRace";

    [Header("Finish Line Visuals")]
    [Tooltip("결승선에 표시될 프리팹입니다.")]
    public GameObject finishLinePrefab;
    [Tooltip("결승선 마커의 지면으로부터 높이입니다.")]
    public float markerHeightOffset = 1f;
    [Tooltip("결승선 마커의 애니메이션 속도입니다.")]
    public float markerBobSpeed = 2f;
    [Tooltip("결승선 마커의 애니메이션 폭입니다.")]
    public float markerBobAmount = 0.5f;

    [Header("Race Settings")]
    [Tooltip("경주의 기본 거리입니다.")]
    public float raceDistance = 40f;
    [Tooltip("경주가 성립하기 위한 최소 거리입니다.")]
    public float minRaceDistance = 20f;
    [Tooltip("경주가 이 시간(초)을 초과하면 무승부 처리됩니다.")]
    public float raceTimeoutSeconds = 120f;
    [Tooltip("결승선에서 각 펫이 갈라질 거리입니다.")]
    public float finishLineSpread = 2f;

    [Header("Racer Settings")]
    [Tooltip("나무늘보의 경주 시 속도 배율입니다.")]
    public float slothSpeedMultiplier = 0.5f;
    [Tooltip("코알라의 경주 시 속도 배율입니다.")]
    public float koalaSpeedMultiplier = 0.45f;

    [Header("Spectator Settings")]
    [Tooltip("경주를 관람할 최대 관중 수입니다.")]
    public int maxSpectators = 2;
    [Tooltip("경주 출발점으로부터 관중을 찾을 최대 반경입니다.")]
    public float spectatorSearchRadius = 25f;
    [Tooltip("관중이 1차로 지루해지기 시작하는 시간(초)입니다.")]
    public float boredomTimePhase1 = 8f;
    [Tooltip("관중이 2차로 완전히 지루해져 잠드는 시간(초)입니다.")]
    public float boredomTimePhase2 = 20f;

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.SlothKoalaRace;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        return (pet1.PetType == PetType.Sloth && pet2.PetType == PetType.Koala) ||
               (pet1.PetType == PetType.Koala && pet2.PetType == PetType.Sloth);
    }

    // Pet.zip/Interaction/SlothKoalaRaceInteraction.cs

    // ... 다른 변수들은 그대로 ...

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[SlothKoalaRace] {pet1.petName}와(과) {pet2.petName}의 세상에서 가장 느린 달리기 시합이 시작됩니다!");

        PetController sloth = (pet1.PetType == PetType.Sloth) ? pet1 : pet2;
        PetController koala = (pet1.PetType == PetType.Koala) ? pet1 : pet2;

        yield return StartCoroutine(WaitUntilAgentIsReady(sloth, 3f));
        yield return StartCoroutine(WaitUntilAgentIsReady(koala, 3f));

        if (!IsAgentSafelyReady(sloth) || !IsAgentSafelyReady(koala))
        {
            Debug.LogError("[SlothKoalaRace] NavMeshAgent 준비 실패로 경주를 중단합니다.");
            EndInteraction(sloth, koala);
            yield break;
        }

        PetOriginalState slothState = new PetOriginalState(sloth);
        PetOriginalState koalaState = new PetOriginalState(koala);
        GameObject finishMarkerInstance = null;

        List<PetController> spectators = new List<PetController>();
        List<PetOriginalState> spectatorStates = new List<PetOriginalState>();

        try
        {
            sloth.ShowEmotion(EmotionType.Race, raceTimeoutSeconds);
            koala.ShowEmotion(EmotionType.Race, raceTimeoutSeconds);

            Vector3 startPosition = (sloth.transform.position + koala.transform.position) / 2;
            Vector3 finishLine = Vector3.zero;
            Vector3 dirToFinish = Vector3.zero;

            for (int i = 0; i < 10; i++)
            {
                Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                Vector3 targetFinish = startPosition + randomDir * raceDistance;
                if (NavMesh.SamplePosition(targetFinish, out NavMeshHit hit, raceDistance, NavMesh.AllAreas))
                {
                    if (Vector3.Distance(startPosition, hit.position) >= minRaceDistance)
                    {
                        finishLine = hit.position;
                        dirToFinish = (finishLine - startPosition).normalized;
                        break;
                    }
                }
            }

            if (dirToFinish == Vector3.zero)
            {
                Debug.LogWarning("[SlothKoalaRace] 적절한 결승선을 찾지 못해 경주를 취소합니다.");
                yield break;
            }

            // ★★★ 수정: 결승선 프리팹의 방향을 경주로를 바라보도록 설정 (토끼/거북이 경주와 유사한 로직)
            if (finishLinePrefab != null)
            {
                Vector3 markerPos = finishLine + Vector3.up * markerHeightOffset;
                // 결승선이 달려오는 선수들을 바라보도록 회전 값을 설정합니다.
            Quaternion arrowRotation = Quaternion.Euler(0, 0, 180);
                finishMarkerInstance = Instantiate(finishLinePrefab, markerPos, arrowRotation);
                StartCoroutine(AnimateFinishMarker(finishMarkerInstance));
            }
            
            // 관중 찾기
            FindSpectators(sloth, koala, startPosition, spectators, spectatorStates);

            // 출발선으로 이동
            Vector3 slothStartPos, koalaStartPos;
            CalculateStartPositions(sloth, koala, out slothStartPos, out koalaStartPos, 3f);
            yield return StartCoroutine(MoveToPositions(sloth, koala, slothStartPos, koalaStartPos, 15f));
            
            // 출발 방향으로 정렬
            LookAtEachOther(sloth, koala);
            yield return StartCoroutine(SmoothRotateToDirection(sloth, koala, Quaternion.LookRotation(dirToFinish)));

            // ★★★ 수정: 관중을 순간이동 시키는 대신, 자연스럽게 달려가도록 코루틴을 실행합니다.
            if (spectators.Count > 0)
            {
                yield return StartCoroutine(MoveAndPositionSpectatorsCoroutine(spectators, startPosition, finishLine));
            }


            // 관중 응원 시작
            yield return new WaitForSeconds(1.0f);
            foreach (var spectator in spectators)
            {
                spectator.ShowEmotion(EmotionType.Cheer, raceTimeoutSeconds);
                StartCoroutine(spectator.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.5f, false, false));
            }
            yield return new WaitForSeconds(2.0f);

            // ... 이하 경주 진행 로직은 기존과 동일 ...
            Debug.Log("[SlothKoalaRace] 경주 시작!");
            sloth.agent.speed = slothState.originalSpeed * slothSpeedMultiplier;
            koala.agent.speed = koalaState.originalSpeed * koalaSpeedMultiplier;
            sloth.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            koala.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            Vector3 slothFinishDest, koalaFinishDest;
            CreateSeparateFinishDestinations(finishLine, dirToFinish, out slothFinishDest, out koalaFinishDest);

            sloth.agent.SetDestination(slothFinishDest);
            koala.agent.SetDestination(koalaFinishDest);
            sloth.agent.isStopped = false;
            koala.agent.isStopped = false;

            bool raceFinished = false;
            float raceStartTime = Time.time;
            float spectatorBoredomTimer = 0f;
            bool boredomPhase1Triggered = false, boredomPhase2Triggered = false;

            while (!raceFinished)
            {
                spectatorBoredomTimer += Time.deltaTime;

                if (!boredomPhase1Triggered && spectatorBoredomTimer > boredomTimePhase1)
                {
                    boredomPhase1Triggered = true;
                    Debug.Log("[SlothKoalaRace] 관중들이 지루해하기 시작합니다...");
                    foreach (var spec in spectators)
                    {
                        spec.ShowEmotion(EmotionType.Confused, raceTimeoutSeconds);
                        StartCoroutine(spec.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 5.0f, false, false));
                    }
                }

                if (!boredomPhase2Triggered && spectatorBoredomTimer > boredomTimePhase2)
                {
                    boredomPhase2Triggered = true;
                    Debug.Log("[SlothKoalaRace] 관중들이 완전히 지루해져서 잠들어버립니다...");
                    foreach (var spec in spectators)
                    {
                        spec.ShowEmotion(EmotionType.Sleepy, raceTimeoutSeconds);
                        StartCoroutine(spec.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 99f, true, false));
                    }
                }

                bool slothArrived = !sloth.agent.pathPending && sloth.agent.remainingDistance < 0.5f;
                bool koalaArrived = !koala.agent.pathPending && koala.agent.remainingDistance < 0.5f;

                if (slothArrived || koalaArrived)
                {
                    raceFinished = true;
                    PetController winner = slothArrived ? sloth : koala;
                    PetController loser = (winner == sloth) ? koala : sloth;
                    
                    Debug.Log($"[SlothKoalaRace] 경주 결과: {winner.petName}의 아슬아슬한 승리!");

                    winner.ShowEmotion(EmotionType.Victory, 10f);
                    loser.ShowEmotion(EmotionType.Defeat, 10f);

                    yield return StartCoroutine(PlayWinnerLoserAnimations(winner, loser,
                        PetAnimationController.PetAnimationType.Jump,
                        PetAnimationController.PetAnimationType.Rest));

                    break;
                }

                if (Time.time - raceStartTime > raceTimeoutSeconds)
                {
                    Debug.LogWarning("[SlothKoalaRace] 경주 시간이 너무 길어져 무승부 처리됩니다.");
                    raceFinished = true;
                    
                    sloth.ShowEmotion(EmotionType.Confused, 10f);
                    koala.ShowEmotion(EmotionType.Confused, 10f);
                    
                    break;
                }

                yield return null;
            }

            sloth.agent.isStopped = true;
            koala.agent.isStopped = true;

            if (!boredomPhase1Triggered)
            {
                foreach (var spec in spectators)
                {
                    StartCoroutine(spec.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.5f, true, false));
                }
            }

            yield return new WaitForSeconds(3.0f);
        }
        finally
        {
            Debug.Log("[SlothKoalaRace] 상호작용 정리 시작.");
            if (finishMarkerInstance != null)
            {
                Destroy(finishMarkerInstance);
            }
            slothState.Restore(sloth);
            koalaState.Restore(koala);
            RestoreSpectators(spectators, spectatorStates);
            EndInteraction(sloth, koala);
            Debug.Log("[SlothKoalaRace] 상호작용 정리 완료.");
        }
    }
// Pet.zip/Interaction/SlothKoalaRaceInteraction.cs

    /// <summary>
    /// 관중들을 결승선 뒤편의 응원 위치로 자연스럽게 이동시키는 코루틴입니다.
    /// </summary>
    private IEnumerator MoveAndPositionSpectatorsCoroutine(List<PetController> spectators, Vector3 startPos, Vector3 finishLine)
    {
        if (spectators.Count == 0)
        {
            yield break;
        }

        Debug.Log("[SlothKoalaRace] 관중들이 응원 위치로 달려갑니다!");

        Vector3 raceDirection = (finishLine - startPos).normalized;
        Vector3 sideDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;
        
        // 응원 장소를 결승선 '뒤'로 설정합니다.
        Vector3 cheeringAreaCenter = finishLine + raceDirection * 8f; // 결승선보다 8미터 뒤

        List<bool> arrivedSpectators = new List<bool>(new bool[spectators.Count]);

        // 각 관중의 목표 위치를 계산하고 이동을 시작시킵니다.
        for (int i = 0; i < spectators.Count; i++)
        {
            var spec = spectators[i];
            
            // 응원 위치를 결승선 주변에 좌/우로 배치합니다.
            float sideOffset = Random.Range(4f, 8f);
            Vector3 spectatorPos = cheeringAreaCenter + (sideDirection * sideOffset * ((i % 2 == 0) ? 1 : -1));

            if (NavMesh.SamplePosition(spectatorPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                // ★★★ 수정 1: NavMeshAgent가 회전을 제어하도록 설정 ★★★
                spec.agent.updateRotation = true; // 달려가는 동안에는 에이전트가 방향을 자동으로 맞춥니다.
                
                // 매우 빠른 속도로 달려가도록 설정
                spec.agent.speed = spec.baseSpeed * 3.5f;
                spec.agent.acceleration = spec.baseAcceleration * 3.5f;
                spec.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
                spec.agent.SetDestination(hit.position);
                spec.agent.isStopped = false;
            }
            else
            {
                // 만약 위치를 못찾으면 도착한 것으로 간주
                arrivedSpectators[i] = true; 
            }
        }

        // 모든 관중이 도착할 때까지 대기합니다.
        float timeout = 20f; // 최대 대기 시간
        float timer = 0f;
        while (arrivedSpectators.Contains(false) && timer < timeout)
        {
            for (int i = 0; i < spectators.Count; i++)
            {
                if (arrivedSpectators[i]) continue;

                var spec = spectators[i];
                if (!spec.agent.pathPending && spec.agent.remainingDistance < 1.0f)
                {
                    arrivedSpectators[i] = true;
                    spec.agent.isStopped = true;
                    
                    // ★★★ 수정 2: 도착 후에는 다시 수동 회전 모드로 변경 ★★★
                    spec.agent.updateRotation = false; // 직접 회전을 제어하기 위해 false로 설정합니다.

                    // ★★★ 수정 3: 부드럽게 선수들을 바라보는 코루틴 호출 ★★★
                    StartCoroutine(SmoothlyLookAt(spec, startPos, 0.7f));
                    
                    spec.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
                }
            }
            timer += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log("[SlothKoalaRace] 모든 관중이 응원 위치에 도착했습니다.");
    }
    
    /// <summary>
    /// ★★★ 새로 추가된 헬퍼 메서드 ★★★
    /// 지정된 펫이 목표 지점을 부드럽게 바라보도록 회전시키는 코루틴입니다.
    /// </summary>
    /// <param name="pet">회전할 펫</param>
    /// <param name="targetPoint">바라볼 목표 위치</param>
    /// <param name="duration">회전에 걸리는 시간</param>
    private IEnumerator SmoothlyLookAt(PetController pet, Vector3 targetPoint, float duration)
    {
        if (pet == null) yield break;

        Vector3 direction = (targetPoint - pet.transform.position).normalized;
        direction.y = 0; // Y축 회전은 무시합니다.
        if (direction == Vector3.zero) yield break;

        Quaternion startRotation = pet.transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        float elapsedTime = 0f;
        while(elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            // Slerp를 사용하여 부드러운 회전 보간
            pet.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t * t * (3f - 2f * t)); // Ease-in-out 효과
            
            // 모델이 별도로 회전하는 경우를 대비하여 모델의 회전도 맞춰줍니다.
            if (pet.petModelTransform != null) 
            {
                pet.petModelTransform.rotation = pet.transform.rotation;
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 회전을 정확하게 설정합니다.
        pet.transform.rotation = targetRotation;
        if (pet.petModelTransform != null) 
        {
            pet.petModelTransform.rotation = targetRotation;
        }
    }
    // --- 헬퍼 메서드들 ---

    // ★★★ 수정된 부분 ★★★: 컴파일 오류 해결을 위해 IsAgentSafelyReady 메서드 추가
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }

    private IEnumerator AnimateFinishMarker(GameObject marker)
    {
        if (marker == null) yield break;
        Vector3 startPos = marker.transform.position;
        while (marker != null)
        {
            float yOffset = Mathf.Sin(Time.time * markerBobSpeed) * markerBobAmount;
            marker.transform.position = startPos + new Vector3(0, yOffset, 0);
            yield return null;
        }
    }

    private IEnumerator SmoothRotateToDirection(PetController pet1, PetController pet2, Quaternion targetRotation)
    {
        float duration = 1.0f;
        float elapsed = 0f;
        Quaternion startRot1 = pet1.transform.rotation;
        Quaternion startRot2 = pet2.transform.rotation;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            pet1.transform.rotation = Quaternion.Slerp(startRot1, targetRotation, t);
            pet2.transform.rotation = Quaternion.Slerp(startRot2, targetRotation, t);
            if(pet1.petModelTransform != null) pet1.petModelTransform.rotation = pet1.transform.rotation;
            if(pet2.petModelTransform != null) pet2.petModelTransform.rotation = pet2.transform.rotation;
            elapsed += Time.deltaTime;
            yield return null;
        }
        pet1.transform.rotation = targetRotation;
        pet2.transform.rotation = targetRotation;
    }
    
    private void CreateSeparateFinishDestinations(Vector3 finishLine, Vector3 raceDirection, out Vector3 dest1, out Vector3 dest2)
    {
        Vector3 sideDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;
        dest1 = FindValidPositionOnNavMesh(finishLine - sideDirection * (finishLineSpread / 2));
        dest2 = FindValidPositionOnNavMesh(finishLine + sideDirection * (finishLineSpread / 2));
    }

    private void FindSpectators(PetController pet1, PetController pet2, Vector3 centerPos, List<PetController> spectators, List<PetOriginalState> spectatorStates)
    {
        Collider[] hits = Physics.OverlapSphere(centerPos, spectatorSearchRadius);
        foreach (var hit in hits)
        {
            PetController potentialSpectator = hit.GetComponent<PetController>();
            if (potentialSpectator != null && potentialSpectator != pet1 && potentialSpectator != pet2 &&
                !potentialSpectator.isInteracting && !potentialSpectator.isHolding &&
                potentialSpectator.hunger < 70f && potentialSpectator.sleepiness < 70f)
            {
                potentialSpectator.InterruptAndResetAI();
                spectators.Add(potentialSpectator);
                spectatorStates.Add(new PetOriginalState(potentialSpectator));
                potentialSpectator.isInteracting = true;
                
                Debug.Log($"[SlothKoalaRace] {potentialSpectator.petName}을(를) 관중으로 모집!");

                if (spectators.Count >= maxSpectators) break;
            }
        }
    }
    
    // ★★★ 수정된 부분 ★★★: pet1, pet2 변수 참조 오류 해결
    private void PositionSpectators(List<PetController> spectators, Vector3 startPos, Vector3 finishLine)
    {
        if (spectators.Count == 0) return;
        Vector3 raceDirection = (finishLine - startPos).normalized;
        Vector3 sideDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;
        Vector3 midPoint = (startPos + finishLine) / 2;

        for (int i = 0; i < spectators.Count; i++)
        {
            float sideOffset = Random.Range(5f, 10f);
            float forwardOffset = Random.Range(-5f, 5f);
            Vector3 spectatorPos = midPoint + (sideDirection * sideOffset * ((i % 2 == 0) ? 1 : -1)) + (raceDirection * forwardOffset);
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spectatorPos, out hit, 10f, NavMesh.AllAreas))
            {
                spectators[i].agent.Warp(hit.position);
                // 경주 트랙의 중앙을 바라보도록 수정
                Vector3 lookTarget = midPoint + raceDirection * forwardOffset;
                Vector3 directionToTrack = (lookTarget - spectators[i].transform.position).normalized;
                if (directionToTrack != Vector3.zero)
                {
                    spectators[i].transform.rotation = Quaternion.LookRotation(directionToTrack);
                }
            }
        }
    }
    
    private void RestoreSpectators(List<PetController> spectators, List<PetOriginalState> spectatorStates)
    {
        for (int i = 0; i < spectators.Count; i++)
        {
            PetController spec = spectators[i];
            if (spec != null)
            {
                spec.HideEmotion();
                spectatorStates[i].Restore(spec);
                spec.isInteracting = false;
                spec.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
                spec.GetComponent<PetMovementController>()?.DecideNextBehavior();
                Debug.Log($"[SlothKoalaRace] 관중 {spec.petName}의 상태를 복원했습니다.");
            }
        }
    }
}