// SlowRaceInteraction.cs - 느린 펫들(나무늘보, 코알라, 거북이)의 경주
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SlowRaceInteraction : BasePetInteraction
{
    public override string InteractionName => "SlowRace";


    // 우선순위: 90 (3순위)
    public override int Priority => 90;

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
    [Tooltip("거북이의 경주 시 속도 배율입니다.")]
    public float turtleSpeedMultiplier = 0.4f;

    [Header("Spectator Settings")]
    [Tooltip("경주를 관람할 최대 관중 수입니다.")]
    public int maxSpectators = 2;
    [Tooltip("경주 출발점으로부터 관중을 찾을 최대 반경입니다.")]
    public float spectatorSearchRadius = 25f;
    [Tooltip("관중이 1차로 지루해지기 시작하는 시간(초)입니다.")]
    public float boredomTimePhase1 = 8f;
    [Tooltip("관중이 2차로 졸려하기 시작하는 시간(초)입니다.")]
    public float boredomTimePhase2 = 10f;
    [Tooltip("관중이 3차로 완전히 잠드는 시간(초)입니다.")]
    public float boredomTimePhase3 = 40f;

    [Header("Marker Disappearance")]
    [Tooltip("선두 주자가 이 거리 안으로 들어오면 결승선 마커가 사라지기 시작합니다.")]
    public float markerDisappearDistance = 10f;

    // 관중 리스트를 클래스 필드로 관리 (강제 종료 시 정리를 위해)
    private List<PetController> activeSpectators = new List<PetController>();
    private List<PetOriginalState> activeSpectatorStates = new List<PetOriginalState>();

    // 마커 관련 필드 (강제 종료 시 정리를 위해 클래스 필드로 관리)
    private Coroutine markerBobbingCoroutine = null;
    private GameObject finishMarkerInstance = null;

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.SlowRace;
    }

    /// <summary>
    /// 강제 종료 시 SlowRace 고유 리소스(마커, 관중)를 정리합니다.
    /// </summary>
    protected override void OnForceCleanup()
    {
        Debug.Log("[SlowRace] OnForceCleanup 호출됨 - 고유 리소스 정리 시작");

        // 1. 마커 코루틴 중지
        if (markerBobbingCoroutine != null)
        {
            StopCoroutine(markerBobbingCoroutine);
            markerBobbingCoroutine = null;
        }

        // 2. 마커 오브젝트 파괴
        if (finishMarkerInstance != null)
        {
            Destroy(finishMarkerInstance);
            finishMarkerInstance = null;
        }

        // 3. 모든 관중 상태 복원
        RestoreAllActiveSpectators();

        Debug.Log("[SlowRace] OnForceCleanup 완료 - 고유 리소스 정리됨");
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 나무늘보, 코알라, 거북이 중 서로 다른 2마리면 경주 가능
        bool isPet1SlowRacer = pet1.PetType == PetType.Sloth || pet1.PetType == PetType.Koala || pet1.PetType == PetType.Turtle;
        bool isPet2SlowRacer = pet2.PetType == PetType.Sloth || pet2.PetType == PetType.Koala || pet2.PetType == PetType.Turtle;
        return isPet1SlowRacer && isPet2SlowRacer && pet1.PetType != pet2.PetType;
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[SlowRace] {pet1.petName}와(과) {pet2.petName}의 세상에서 가장 느린 달리기 시합이 시작됩니다!");

        // 나무늘보, 코알라, 거북이 중 2마리가 경주
        PetController racer1 = pet1;
        PetController racer2 = pet2;

        yield return StartCoroutine(WaitUntilAgentIsReady(racer1, 3f));
        yield return StartCoroutine(WaitUntilAgentIsReady(racer2, 3f));

        if (!IsAgentSafelyReady(racer1) || !IsAgentSafelyReady(racer2))
        {
            Debug.LogError("[SlowRace] NavMeshAgent 준비 실패로 경주를 중단합니다.");
            EndInteraction(racer1, racer2);
            yield break;
        }

        PetOriginalState racer1State = new PetOriginalState(racer1);
        PetOriginalState racer2State = new PetOriginalState(racer2);

        // 클래스 필드 초기화 (이전 경주에서 남은 값 정리)
        finishMarkerInstance = null;
        markerBobbingCoroutine = null;
        bool isMarkerDisappearing = false;

        // 클래스 필드로 관리되는 관중 리스트 초기화
        activeSpectators.Clear();
        activeSpectatorStates.Clear();

        try
        {
            racer1.ShowEmotion(EmotionType.Race, raceTimeoutSeconds);
            racer2.ShowEmotion(EmotionType.Race, raceTimeoutSeconds);

            Vector3 startPosition = (racer1.transform.position + racer2.transform.position) / 2;
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
                Debug.LogWarning("[SlowRace] 적절한 결승선을 찾지 못해 경주를 취소합니다.");
                // 상태 복원 후 종료 (try 블록 내부이므로 finally가 실행되지만 명시적으로 처리)
                racer1.HideEmotion();
                racer2.HideEmotion();
                yield break;
            }

            // ★★★ 수정: 결승선 프리팹의 방향을 경주로를 바라보도록 설정 (토끼/거북이 경주와 유사한 로직)
            if (finishLinePrefab != null)
            {
                Vector3 markerPos = finishLine + Vector3.up * markerHeightOffset;
                // 결승선이 달려오는 선수들을 바라보도록 회전 값을 설정합니다.
            Quaternion arrowRotation = Quaternion.Euler(0, 0, 180);
                finishMarkerInstance = Instantiate(finishLinePrefab, markerPos, arrowRotation);
                // ▼▼▼ [수정] 코루틴을 변수에 저장하여 나중에 중지할 수 있도록 합니다. ▼▼▼
                markerBobbingCoroutine = StartCoroutine(AnimateFinishMarker(finishMarkerInstance));
                // ▲▲▲ [여기까지 수정] ▲▲▲
            }
            
            // 관중 찾기
            FindSpectators(racer1, racer2, startPosition, activeSpectators, activeSpectatorStates);

            // 출발선으로 이동
            Vector3 racer1StartPos, racer2StartPos;
            CalculateStartPositions(racer1, racer2, out racer1StartPos, out racer2StartPos, 3f);
            yield return StartCoroutine(MoveToPositions(racer1, racer2, racer1StartPos, racer2StartPos, 15f));

            // 출발 방향으로 정렬
            LookAtEachOther(racer1, racer2);
            yield return StartCoroutine(SmoothRotateToDirection(racer1, racer2, Quaternion.LookRotation(dirToFinish)));

            // ★★★ 수정: 관중을 순간이동 시키는 대신, 자연스럽게 달려가도록 코루틴을 실행합니다.
            if (activeSpectators.Count > 0)
            {
                yield return StartCoroutine(MoveAndPositionSpectatorsCoroutine(activeSpectators, startPosition, finishLine));
            }


            // 관중 응원 시작
            yield return new WaitForSeconds(1.0f);
            foreach (var spectator in activeSpectators)
            {
                spectator.ShowEmotion(EmotionType.Cheer, raceTimeoutSeconds);
                StartCoroutine(spectator.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.5f, false, false));
            }
            yield return new WaitForSeconds(2.0f);

            // ... 이하 경주 진행 로직은 기존과 동일 ...
            Debug.Log("[SlowRace] 경주 시작!");
            racer1.agent.speed = racer1.baseSpeed * GetSpeedMultiplier(racer1.PetType);
            racer2.agent.speed = racer2.baseSpeed * GetSpeedMultiplier(racer2.PetType);
            racer1.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            racer2.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            Vector3 racer1FinishDest, racer2FinishDest;
            CreateSeparateFinishDestinations(finishLine, dirToFinish, out racer1FinishDest, out racer2FinishDest);

            racer1.agent.SetDestination(racer1FinishDest);
            racer2.agent.SetDestination(racer2FinishDest);
            racer1.agent.isStopped = false;
            racer2.agent.isStopped = false;

            bool raceFinished = false;
            float raceStartTime = Time.time;
            float spectatorBoredomTimer = 0f;
            bool boredomPhase1Triggered = false, boredomPhase2Triggered = false, boredomPhase3Triggered = false;

            while (!raceFinished)
            {
                // ★ racer 홀드 상태 체크 - 잡히면 경주 즉시 종료
                if ((racer1 != null && (racer1.State.IsHolding || racer1.State.IsSelected)) ||
                    (racer2 != null && (racer2.State.IsHolding || racer2.State.IsSelected)))
                {
                    Debug.Log("[SlowRace] 경주 중인 펫이 잡혀서 경주를 종료합니다.");
                    break; // 루프 종료 → finally 실행됨
                }

                // 관중 홀드 상태 체크 - 잡힌 관중은 경주에서 빠짐
                for (int i = activeSpectators.Count - 1; i >= 0; i--)
                {
                    var spec = activeSpectators[i];
                    if (spec != null && (spec.State.IsHolding || spec.State.IsSelected))
                    {
                        Debug.Log($"[SlowRace] 관중 {spec.petName}이(가) 잡혀서 경주에서 빠집니다.");
                        RestoreSingleSpectator(spec, activeSpectatorStates[i]);
                        activeSpectators.RemoveAt(i);
                        activeSpectatorStates.RemoveAt(i);
                    }
                }

                // 선두 주자가 결승선에 가까워졌는지 체크하는 로직
                if (!isMarkerDisappearing && finishMarkerInstance != null)
                {
                    // 각 주자와 결승선 사이의 거리를 계산
                    float racer1DistToFinish = Vector3.Distance(racer1.transform.position, finishLine);
                    float racer2DistToFinish = Vector3.Distance(racer2.transform.position, finishLine);

                    // 두 주자 중 더 가까운 거리가 설정한 값보다 작아지면
                    if (Mathf.Min(racer1DistToFinish, racer2DistToFinish) < markerDisappearDistance)
                    {
                        isMarkerDisappearing = true; // 중복 실행 방지
                        
                        // 기존의 상하 움직임 애니메이션은 중지
                        if (markerBobbingCoroutine != null)
                        {
                            StopCoroutine(markerBobbingCoroutine);
                        }
                        
                        // 사라지는 애니메이션 시작
                        StartCoroutine(DisappearFinishMarker(finishMarkerInstance));
                        Debug.Log("[SlowRace] 주자가 결승선에 근접하여 마커가 사라지기 시작합니다.");
                    }
                }
                // ▲▲▲ [여기까지 추가] ▲▲▲
                spectatorBoredomTimer += Time.deltaTime;

                // 디버그: 타이머 값 확인 (5초마다 출력)
                // if (Mathf.FloorToInt(spectatorBoredomTimer) % 5 == 0 && Mathf.FloorToInt(spectatorBoredomTimer) != Mathf.FloorToInt(spectatorBoredomTimer - Time.deltaTime))
                // {
                //     Debug.Log($"[SlowRace] 타이머: {spectatorBoredomTimer:F1}초, Phase1목표: {boredomTimePhase1}, Phase2목표: {boredomTimePhase2}");
                // }

                if (!boredomPhase1Triggered && spectatorBoredomTimer > boredomTimePhase1)
                {
                    boredomPhase1Triggered = true;
                    Debug.Log("[SlowRace] 관중들이 지루해하기 시작합니다...");
                    foreach (var spec in activeSpectators)
                    {
                        spec.ShowEmotion(EmotionType.Confused, raceTimeoutSeconds);
                        StartCoroutine(spec.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Idle, 99f, true, false));
                    }
                }

                if (!boredomPhase2Triggered && spectatorBoredomTimer > boredomTimePhase2)
                {
                    boredomPhase2Triggered = true;
                    Debug.Log("[SlowRace] 관중들이 졸려하기 시작합니다...");
                    foreach (var spec in activeSpectators)
                    {
                        spec.ShowEmotion(EmotionType.Sleepy, raceTimeoutSeconds);
                        StartCoroutine(spec.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 99f, true, false));
                    }
                }

                if (!boredomPhase3Triggered && spectatorBoredomTimer > boredomTimePhase3)
                {
                    boredomPhase3Triggered = true;
                    Debug.Log("[SlowRace] 관중들이 완전히 잠들어버립니다...");
                    foreach (var spec in activeSpectators)
                    {
                        spec.ShowEmotion(EmotionType.Sleep, raceTimeoutSeconds);
                        StartCoroutine(spec.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 99f, true, false));
                    }
                }

                bool racer1Arrived = !racer1.agent.pathPending && racer1.agent.remainingDistance < 0.5f;
                bool racer2Arrived = !racer2.agent.pathPending && racer2.agent.remainingDistance < 0.5f;

                if (racer1Arrived || racer2Arrived)
                {
                    raceFinished = true;
                    PetController winner = racer1Arrived ? racer1 : racer2;
                    PetController loser = (winner == racer1) ? racer2 : racer1;
                    
                    Debug.Log($"[SlowRace] 경주 결과: {winner.petName}의 아슬아슬한 승리!");

                    winner.ShowEmotion(EmotionType.Victory, 10f);
                    loser.ShowEmotion(EmotionType.Defeat, 10f);

                    yield return StartCoroutine(PlayWinnerLoserAnimations(winner, loser,
                        PetAnimationController.PetAnimationType.Jump,
                        PetAnimationController.PetAnimationType.Rest));

                    break;
                }

                if (Time.time - raceStartTime > raceTimeoutSeconds)
                {
                    Debug.LogWarning("[SlowRace] 경주 시간이 너무 길어져 무승부 처리됩니다.");
                    raceFinished = true;

                    racer1.ShowEmotion(EmotionType.Confused, 10f);
                    racer2.ShowEmotion(EmotionType.Confused, 10f);

                    break;
                }

                yield return null;
            }

            racer1.agent.isStopped = true;
            racer2.agent.isStopped = true;

            if (!boredomPhase1Triggered)
            {
                foreach (var spec in activeSpectators)
                {
                    StartCoroutine(spec.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.5f, true, false));
                }
            }

            yield return new WaitForSeconds(3.0f);
        }
        finally
        {
            Debug.Log("[SlowRace] 상호작용 정리 시작.");

            // ★ 마커 애니메이션 코루틴 먼저 중지
            if (markerBobbingCoroutine != null)
            {
                StopCoroutine(markerBobbingCoroutine);
                markerBobbingCoroutine = null;
            }

            // 마커 오브젝트 파괴
            if (finishMarkerInstance != null)
            {
                Destroy(finishMarkerInstance);
                finishMarkerInstance = null;
            }

            // NavMeshAgent 상태 복원
            racer1State.Restore(racer1);
            racer2State.Restore(racer2);

            // 모든 관중 상태 복원 (클래스 필드 사용)
            RestoreAllActiveSpectators();

            // 상호작용 종료 (SafeResumePet 호출됨)
            EndInteraction(racer1, racer2);
            Debug.Log("[SlowRace] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 관중들을 결승선 뒤편의 응원 위치로 자연스럽게 이동시키는 코루틴입니다.
    /// </summary>
    private IEnumerator MoveAndPositionSpectatorsCoroutine(List<PetController> spectators, Vector3 startPos, Vector3 finishLine)
    {
        if (spectators.Count == 0)
        {
            yield break;
        }

        Debug.Log("[SlowRace] 관중들이 응원 위치로 달려갑니다!");

        Vector3 raceDirection = (finishLine - startPos).normalized;
        Vector3 sideDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;
        
        Vector3 cheeringAreaCenter = finishLine + raceDirection * 8f;

        List<bool> arrivedSpectators = new List<bool>(new bool[spectators.Count]);

        // ▼▼▼ [수정됨] 이동 명령 전, 모든 관중의 NavMeshAgent 준비 상태를 먼저 확인합니다. ▼▼▼
        for (int i = 0; i < spectators.Count; i++)
        {
            var spec = spectators[i];
            yield return StartCoroutine(WaitUntilAgentIsReady(spec, 3f)); // BasePetInteraction의 헬퍼 코루틴 재사용

            if (!IsAgentSafelyReady(spec))
            {
                Debug.LogError($"[SlowRace] 관중 {spec.petName}의 NavMeshAgent가 준비되지 않아 이동시킬 수 없습니다. 이 관중은 이동을 건너뜁니다.");
                arrivedSpectators[i] = true; // 이동 불가하므로 도착 처리
            }
        }
        // ▲▲▲ [여기까지 수정] ▲▲▲

        // 각 관중의 목표 위치를 계산하고 이동을 시작시킵니다.
        for (int i = 0; i < spectators.Count; i++)
        {
            // ▼▼▼ [수정됨] 이미 도착 처리된 관중은 건너뜁니다. ▼▼▼
            if (arrivedSpectators[i])
            {
                continue;
            }
            // ▲▲▲ [여기까지 수정] ▲▲▲

            var spec = spectators[i];
            
            float sideOffset = Random.Range(4f, 8f);
            Vector3 spectatorPos = cheeringAreaCenter + (sideDirection * sideOffset * ((i % 2 == 0) ? 1 : -1));

            if (NavMesh.SamplePosition(spectatorPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                spec.agent.updateRotation = true;
                spec.agent.speed = spec.baseSpeed * 3.5f;
                spec.agent.acceleration = spec.baseAcceleration * 3.5f;
                spec.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
                spec.agent.SetDestination(hit.position);
                spec.agent.isStopped = false;
            }
            else
            {
                // ▼▼▼ [수정됨] 위치 찾기 실패 시, 상세 로그를 남기고 도착 처리합니다. ▼▼▼
                Debug.LogWarning($"[SlowRace] 관중 '{spec.petName}'를 위한 유효한 응원 위치({spectatorPos})를 NavMesh에서 찾지 못했습니다. 이 관중은 이동하지 않습니다.");
                arrivedSpectators[i] = true;
                // ▲▲▲ [여기까지 수정] ▲▲▲
            }
        }

        // 모든 관중이 도착할 때까지 대기합니다.
        float timeout = 20f;
        float timer = 0f;
        while (arrivedSpectators.Contains(false) && timer < timeout)
        {
            for (int i = 0; i < spectators.Count; i++)
            {
                if (arrivedSpectators[i]) continue;

                var spec = spectators[i];
                // ▼▼▼ [수정됨] 에이전트가 비활성화 되었을 경우를 대비한 안전장치 추가 ▼▼▼
                if (!IsAgentSafelyReady(spec))
                {
                    arrivedSpectators[i] = true;
                    continue;
                }
                // ▲▲▲ [여기까지 수정] ▲▲▲

                if (!spec.agent.pathPending && spec.agent.remainingDistance < 1.0f)
                {
                    arrivedSpectators[i] = true;
                    spec.agent.isStopped = true;
                    spec.agent.updateRotation = false;
                    StartCoroutine(SmoothlyLookAt(spec, startPos, 0.7f));
                    spec.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
                }
            }
            timer += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log("[SlowRace] 모든 관중이 응원 위치에 도착했습니다.");
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
                !potentialSpectator.State.IsInteracting && !potentialSpectator.State.IsHolding &&
                potentialSpectator.Needs.Hunger < 70f && potentialSpectator.Needs.Sleepiness < 70f)
            {
                // ★★★ Activity 시스템에서 자동으로 처리됨 ★★★
                spectators.Add(potentialSpectator);
                spectatorStates.Add(new PetOriginalState(potentialSpectator));
                // ★ [Phase 4] PetState를 통한 상태 업데이트
                potentialSpectator.State.StartInteraction(null); // 관중은 특정 파트너가 없음
                
                Debug.Log($"[SlowRace] {potentialSpectator.petName}을(를) 관중으로 모집!");

                if (spectators.Count >= maxSpectators) break;
            }
        }
    }
    
    /// <summary>
    /// 개별 관중의 상태를 복원합니다. (SafeResumePet 수준으로 강화)
    /// </summary>
    private void RestoreSingleSpectator(PetController spec, PetOriginalState state)
    {
        if (spec == null) return;

        Debug.Log($"[SlowRace] 관중 {spec.petName}의 상태 복원 시작");

        // 1. 상호작용 상태 종료
        spec.State.EndInteraction();
        spec.State.SetInteractionLogic(null);

        // 2. 감정 숨기기
        spec.HideEmotion();

        // 3. 애니메이션 상태 초기화
        var animController = spec.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.StopAllCoroutines();
            animController.StopContinuousAnimation();
            animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
        }

        // 4. NavMeshAgent 상태 복원 (잡히지 않은 경우에만)
        if (spec.agent != null && !spec.State.IsHolding)
        {
            state?.Restore(spec);

            if (spec.agent.enabled && spec.agent.isOnNavMesh)
            {
                spec.agent.isStopped = false;
                spec.agent.ResetPath();
                spec.agent.speed = spec.baseSpeed;
                spec.agent.acceleration = spec.baseAcceleration;
            }
        }

        // 5. AI 재평가 (잡히지 않은 경우에만)
        if (spec.AI != null && !spec.State.IsHolding)
        {
            spec.AI.InterruptAndResetAI();
        }

        // 6. 이동 재개 (잡히지 않은 경우에만)
        var movementController = spec.GetComponent<PetMovementController>();
        if (movementController != null && !spec.State.IsHolding)
        {
            movementController.DecideNextBehavior();
        }

        Debug.Log($"[SlowRace] 관중 {spec.petName}의 상태 복원 완료");
    }

    /// <summary>
    /// 모든 활성 관중들의 상태를 복원하고 리스트를 정리합니다.
    /// </summary>
    private void RestoreAllActiveSpectators()
    {
        for (int i = 0; i < activeSpectators.Count; i++)
        {
            if (i < activeSpectatorStates.Count)
            {
                RestoreSingleSpectator(activeSpectators[i], activeSpectatorStates[i]);
            }
        }
        activeSpectators.Clear();
        activeSpectatorStates.Clear();
    }

    /// <summary>
/// 결승선 마커를 자연스럽게 작아지며 사라지게 하는 코루틴입니다.
/// </summary>
/// <param name="marker">사라지게 할 마커 게임 오브젝트</param>
private IEnumerator DisappearFinishMarker(GameObject marker)
{
    if (marker == null) yield break;

    Vector3 initialScale = marker.transform.localScale;
    float duration = 1.5f; // 사라지는 데 걸리는 시간
    float elapsedTime = 0f;

    while (elapsedTime < duration)
    {
        // 시간이 지남에 따라 스케일을 0으로 만듭니다.
        marker.transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsedTime / duration);
        elapsedTime += Time.deltaTime;
        yield return null;
    }

    // 애니메이션이 끝난 후 오브젝트를 파괴합니다.
    Destroy(marker);
}

    /// <summary>
    /// 펫 타입에 따른 속도 배율을 반환합니다.
    /// </summary>
    private float GetSpeedMultiplier(PetType petType)
    {
        return petType switch
        {
            PetType.Sloth => slothSpeedMultiplier,
            PetType.Koala => koalaSpeedMultiplier,
            PetType.Turtle => turtleSpeedMultiplier,
            _ => 0.5f
        };
    }
}