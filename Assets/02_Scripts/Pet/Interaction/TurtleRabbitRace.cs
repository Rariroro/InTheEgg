// RaceInteraction.cs (수정된 버전 - 주석 추가)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 느린 펫 vs 빠른 펫 경주 상호작용을 처리하는 클래스입니다.
/// 이솝우화 "토끼와 거북이" 컨셉: 빠른 펫이 자만하다 느린 펫에게 패배
/// BasePetInteraction을 상속받습니다.
/// </summary>
public class TurtleRabbitRace : BasePetInteraction
{
    // 이 상호작용의 이름을 "TurtleRabbitRace"로 정의합니다.
    public override string InteractionName => "TurtleRabbitRace";

    // 우선순위: 100 (가장 높음)
    public override int Priority => 100;
    // ★★★ 새로 추가된 부분: 결승선 깃발 프리팹 ★★★
    // ★★★ 이 부분을 추가합니다. ★★★
    // ▼▼▼ [수정] 이 부분을 아래 코드로 교체합니다. ▼▼▼
    [Header("Finish Line Visuals")]
    [Tooltip("결승선에 표시될 3D 화살표 프리팹입니다.")]
    public GameObject finishArrowPrefab;

    [Tooltip("화살표가 지면으로부터 떨어질 높이입니다.")]
    public float arrowHeight = 10f;

    [Tooltip("화살표가 위아래로 움직이는 속도입니다.")]
    public float arrowBobSpeed = 2f;

    [Tooltip("화살표가 위아래로 움직이는 폭입니다.")]
    public float arrowBobAmount = 1f;
    // ▲▲▲ [여기까지 수정] ▲▲▲

    // ★★★ 여기까지 추가 ★★★
    // ★★★ 추가: 경주 설정을 인스펙터에서 조절하기 위한 변수들 ★★★
    [Header("Race Settings")] // 유니티 인스펙터에서 섹션을 구분하기 위한 헤더입니다.
    [Tooltip("경주의 기본 거리입니다.")] // 인스펙터에서 변수 위에 마우스를 올렸을 때 표시될 설명입니다.
    public float raceDistance = 100f;

    [Tooltip("경주가 성립하기 위한 최소 거리입니다.")]
    public float minRaceDistance = 80f;

    [Tooltip("경주 타임아웃 시간 (초)")]
    public float raceTimeoutSeconds = 180f;

    [Header("Fast Pet Settings (빠른 펫 - 토끼 역할)")]
    [Tooltip("빠른 펫의 경주 시작 시 속도 배율입니다.")]
    public float fastPetStartSpeedMultiplier = 3.5f;

    [Tooltip("빠른 펫이 낮잠을 잘 위치 (전체 경주 거리 대비 비율, 0.0 ~ 1.0)")]
    [Range(0f, 1f)] // 인스펙터에서 값을 슬라이더로 조절할 수 있게 합니다. (0에서 1 사이)
    public float fastPetNapProgress = 0.4f;

    [Tooltip("빠른 펫이 다시 깨어나 전력 질주할 때의 속도 배율입니다.")]
    public float fastPetFinalSprintSpeedMultiplier = 5.0f;

    [Header("Slow Pet Settings (느린 펫 - 거북이 역할)")]
    [Tooltip("느린 펫의 경주 시 속도 배율입니다.")]
    public float slowPetSpeedMultiplier = 0.8f;

    [Tooltip("느린 펫이 이 지점에 도달하면 빠른 펫이 깨어납니다 (전체 경주 거리 대비 비율, 0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float slowPetWakeUpProgress = 0.94f;

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

    // ▼▼▼ [추가] 이 변수를 클래스 상단에 추가합니다. ▼▼▼
    [Header("Arrow Disappearance")]
    [Tooltip("선두 주자가 이 거리 안으로 들어오면 결승선 화살표가 사라지기 시작합니다.")]
    public float arrowDisappearDistance = 15f;
    // ▲▲▲ [여기까지 추가] ▲▲▲

    [Header("Debug Visualization")]
    [Tooltip("Scene 뷰에서 경주 경로와 깨우기 지점을 표시할지 여부")]
    public bool showDebugGizmos = true;

    [Tooltip("깨우기 지점 표시 색상")]
    public Color wakeUpPointColor = Color.yellow;

    [Tooltip("낮잠 지점 표시 색상")]
    public Color napPointColor = Color.blue;

    // 경주 정보 저장용 (PerformInteraction 메서드에서 설정)
    private Vector3 debugStartPosition;
    private Vector3 debugFinishLine;
    private Vector3 debugDirectionToFinish;
    private float debugTotalRaceDistance;
    private bool debugRaceActive = false;
    private PetController debugFastPet;
    private PetController debugSlowPet;

    // 빠른 펫이 깨어나야 하는지를 외부에서 알 수 있도록 하는 플래그 변수입니다.
    private bool fastPetShouldWakeUp = false;
    [Header("Fast Pet Nap Settings (낮잠 설정)")]
    [Tooltip("빠른 펫이 잠들기 전 속도를 줄이는 시간 (초)")]
    public float fastPetSlowDownDuration = 1.0f;

    [Tooltip("빠른 펫이 멈추기 직전의 최소 속도")]
    public float fastPetMinSpeedBeforeSleep = 0.5f;

    // ★★★ 추가: 빠른 펫의 원래 회피 우선순위를 저장할 변수 ★★★
    private int fastPetOriginalPriority;
    
    // ★★★ 수정: static 제거 - 인스턴스별로 화살표 관리 ★★★
    private List<GameObject> myRaceArrows = new List<GameObject>();

    // ★★★ 추가: 현재 인스턴스의 결승선 화살표 참조 ★★★
    private GameObject finishArrowInstance;

    /// <summary>
    /// 템플릿에서 이 인스턴스로 설정값을 복사합니다 (동시 실행 격리)
    /// </summary>
    public void CopySettingsFrom(TurtleRabbitRace template)
    {
        if (template == null) return;

        // Finish Line Visuals
        this.finishArrowPrefab = template.finishArrowPrefab;
        this.arrowHeight = template.arrowHeight;
        this.arrowBobSpeed = template.arrowBobSpeed;
        this.arrowBobAmount = template.arrowBobAmount;

        // Race Settings
        this.raceDistance = template.raceDistance;
        this.minRaceDistance = template.minRaceDistance;
        this.raceTimeoutSeconds = template.raceTimeoutSeconds;

        // Fast Pet Settings
        this.fastPetStartSpeedMultiplier = template.fastPetStartSpeedMultiplier;
        this.fastPetNapProgress = template.fastPetNapProgress;
        this.fastPetFinalSprintSpeedMultiplier = template.fastPetFinalSprintSpeedMultiplier;

        // Slow Pet Settings
        this.slowPetSpeedMultiplier = template.slowPetSpeedMultiplier;
        this.slowPetWakeUpProgress = template.slowPetWakeUpProgress;

        // Safety Settings
        this.agentSafetyTimeout = template.agentSafetyTimeout;

        // Anti-Bottleneck Settings
        this.finishLineSpread = template.finishLineSpread;
        this.stuckDetectionTime = template.stuckDetectionTime;
        this.detourRadius = template.detourRadius;

        // Arrow Disappearance
        this.arrowDisappearDistance = template.arrowDisappearDistance;

        // Fast Pet Nap Settings
        this.fastPetSlowDownDuration = template.fastPetSlowDownDuration;
        this.fastPetMinSpeedBeforeSleep = template.fastPetMinSpeedBeforeSleep;
    }

    /// <summary>
    /// 이 상호작용의 타입을 InteractionType.TurtleRabbitRace로 결정합니다.
    /// </summary>
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.TurtleRabbitRace;
    }

    /// <summary>
    /// 느린 펫과 빠른 펫의 조합일 때 경주 상호작용이 가능하도록 조건을 설정합니다.
    /// 이솝우화 "토끼와 거북이" 컨셉: 빠른 펫이 자만하다 느린 펫에게 패배
    /// </summary>
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 느린 동물 그룹 (거북이 역할)
        PetType[] slowAnimals = {
            PetType.Turtle,   // 거북이
            PetType.Sloth,    // 나무늘보
            PetType.Koala     // 코알라
        };

        // 빠른 동물 그룹 (토끼 역할)
        PetType[] fastAnimals = {
            PetType.Rabbit,   // 토끼
            PetType.Deer,     // 사슴
            PetType.Fox,      // 여우
            PetType.Horse,    // 말
            PetType.Leopard   // 표범
        };

        // 한 쪽은 느린 동물, 다른 쪽은 빠른 동물
        bool pet1SlowPet2Fast = System.Array.Exists(slowAnimals, t => t == pet1.PetType) &&
                                System.Array.Exists(fastAnimals, t => t == pet2.PetType);
        bool pet1FastPet2Slow = System.Array.Exists(fastAnimals, t => t == pet1.PetType) &&
                                System.Array.Exists(slowAnimals, t => t == pet2.PetType);

        return pet1SlowPet2Fast || pet1FastPet2Slow;
    }


    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[Race] {pet1.petName}와(과) {pet2.petName}의 달리기 시합이 시작됩니다!");
        
        // ★★★ 수정: 이 인스턴스의 화살표만 정리 ★★★
        foreach (var arrow in myRaceArrows)
        {
            if (arrow != null) Destroy(arrow);
        }
        myRaceArrows.Clear();

        // 펫 식별: 느린 펫과 빠른 펫 동적 구분
        PetType[] slowAnimals = { PetType.Turtle, PetType.Sloth, PetType.Koala };
        PetType[] fastAnimals = { PetType.Rabbit, PetType.Deer, PetType.Fox, PetType.Horse, PetType.Leopard };

        PetController fastPet = System.Array.Exists(fastAnimals, t => t == pet1.PetType) ? pet1 : pet2;
        PetController slowPet = System.Array.Exists(slowAnimals, t => t == pet1.PetType) ? pet1 : pet2;

        yield return StartCoroutine(WaitUntilAgentIsReady(fastPet, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(slowPet, agentSafetyTimeout));

        if (!IsAgentSafelyReady(fastPet) || !IsAgentSafelyReady(slowPet))
        {
            Debug.LogError("[TurtleRabbitRace] NavMeshAgent 준비 실패로 경주를 중단합니다.");
            // ★★★ 수정: 상호작용 실패 시 즉시 EndInteraction 호출로 안전하게 종료 ★★★
            EndInteraction(fastPet, slowPet);
            yield break;
        }

        // 상태 저장 및 변수 초기화
        PetOriginalState fastPetOriginalState = new PetOriginalState(fastPet);
        PetOriginalState slowPetOriginalState = new PetOriginalState(slowPet);
        // ★★★ 인스턴스 참조 변수 초기화 ★★★
        finishArrowInstance = null;
        // ▼▼▼ [추가] 화살표 애니메이션 코루틴과 상태를 관리할 변수를 추가합니다. ▼▼▼
        Coroutine arrowBobbingCoroutine = null;
        bool isArrowDisappearing = false;
        // ▲▲▲ [여기까지 추가] ▲▲▲


        try
        {
            fastPet.ShowEmotion(EmotionType.Race, raceTimeoutSeconds);
            slowPet.ShowEmotion(EmotionType.Race, raceTimeoutSeconds);

            // --- 1. 결승선 위치 설정 ---
            // (이 부분 로직은 기존과 동일하게 유지)
            Vector3 initialCenter = (fastPet.transform.position + slowPet.transform.position) / 2;
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
            // ▼▼▼ [수정] 깃발/마커 생성 로직을 화살표 생성 로직으로 교체합니다. ▼▼▼
            if (dirToFinish != Vector3.zero && finishArrowPrefab != null)
            {
                // 화살표의 위치는 결승선 위 공중입니다.
                Vector3 arrowPosition = finishLine + Vector3.up * arrowHeight;

                // 화살표가 아래를 향하도록 Z축으로 180도 회전합니다.
                Quaternion arrowRotation = Quaternion.Euler(0, 0, 180);

                // 화살표 인스턴스 생성
                finishArrowInstance = Instantiate(finishArrowPrefab, arrowPosition, arrowRotation);
                
                // ★★★ 추가: 활성 화살표 리스트에 추가 ★★★
                myRaceArrows.Add(finishArrowInstance);


                // ▼▼▼ [수정] 코루틴을 변수에 저장하여 나중에 중지할 수 있도록 합니다. ▼▼▼
                arrowBobbingCoroutine = StartCoroutine(AnimateFinishArrow(finishArrowInstance));
                // ▲▲▲ [여기까지 수정] ▲▲▲
            }
            // ▲▲▲ [여기까지 수정] ▲▲▲

            // ★★★ 수정된 부분 시작 ★★★
            // --- 2. 출발점으로 이동 및 정렬 (부드러운 이동) ---

            // 자동 회전 비활성화
            fastPet.agent.updateRotation = false;
            slowPet.agent.updateRotation = false;

            Vector3 startPosition = CalculateOptimalStartPosition(fastPet, slowPet, finishLine, dirToFinish);
            Vector3 fastPetStartPos, slowPetStartPos;
            CalculateAlignedStartPositions(startPosition, dirToFinish, out fastPetStartPos, out slowPetStartPos, 3f);

            // 디버그 정보 저장 (OnDrawGizmos용)
            debugStartPosition = startPosition;
            debugFinishLine = finishLine;
            debugDirectionToFinish = dirToFinish;
            debugTotalRaceDistance = totalRaceDistance;
            debugRaceActive = true;
            debugFastPet = fastPet;
            debugSlowPet = slowPet;

            // 1단계: 출발선 근처로 이동
            yield return StartCoroutine(MoveToPositions(fastPet, slowPet, fastPetStartPos, slowPetStartPos, 10f));

            // 2단계: 미세 조정 - 정확한 위치로 부드럽게 이동
            yield return StartCoroutine(FineTunePositions(fastPet, slowPet, fastPetStartPos, slowPetStartPos, dirToFinish));

            // 3단계: 출발 대기
            fastPet.agent.isStopped = true;
            slowPet.agent.isStopped = true;
            fastPet.agent.velocity = Vector3.zero;
            slowPet.agent.velocity = Vector3.zero;

            Debug.Log("[Race] 출발선에서 대기 중...");

            // 카운트다운
            for (int i = 0; i < 3; i++)
            {
                Debug.Log($"[Race] {3 - i}...");
                yield return new WaitForSeconds(1f);
            }
            // ★★★ 수정된 부분 끝 ★★★


            // --- 5. 경주 시작 ---
            Debug.Log("[Race] 경주 시작!");
            fastPet.agent.updateRotation = true;
            slowPet.agent.updateRotation = true;
            // baseSpeed 사용으로 수정 (다른 활동의 영향을 받지 않도록)
            fastPet.agent.speed = fastPet.baseSpeed * fastPetStartSpeedMultiplier;
            slowPet.agent.speed = slowPet.baseSpeed * slowPetSpeedMultiplier;
            // acceleration도 비례하여 설정 (자연스러운 가속)
            fastPet.agent.acceleration = fastPet.baseAcceleration * fastPetStartSpeedMultiplier;
            slowPet.agent.acceleration = slowPet.baseAcceleration * slowPetSpeedMultiplier;

            fastPet.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            slowPet.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            Vector3 fastPetFinishDestination, slowPetFinishDestination;
            CreateSeparateFinishDestinations(finishLine, dirToFinish, out fastPetFinishDestination, out slowPetFinishDestination);

            // ★★★ 수정: isStopped를 false로 바꿔주는 것만으로 이동이 안전하게 재개됩니다. ★★★
            fastPet.agent.isStopped = false;
            slowPet.agent.isStopped = false;
            fastPet.agent.SetDestination(fastPetFinishDestination);
            slowPet.agent.SetDestination(slowPetFinishDestination);

            float napDistance = totalRaceDistance * fastPetNapProgress;

            // --- 6. 경주 진행 ---
            bool slowPetFinished = false;
            bool fastPetIsSleeping = false;
            bool fastPetWokeUp = false;
            float raceStartTime = Time.time;

            while (!slowPetFinished)
            {
                // ▼▼▼ [추가] 선두 주자가 결승선에 가까워졌는지 체크하는 로직 ▼▼▼
                if (!isArrowDisappearing && finishArrowInstance != null)
                {
                    // 각 주자와 결승선 사이의 거리를 계산
                    float fastPetDistToFinish = Vector3.Distance(fastPet.transform.position, finishLine);
                    float slowPetDistToFinish = Vector3.Distance(slowPet.transform.position, finishLine);

                    // 두 주자 중 더 가까운 거리가 설정한 값보다 작아지면
                    if (Mathf.Min(fastPetDistToFinish, slowPetDistToFinish) < arrowDisappearDistance)
                    {
                        isArrowDisappearing = true; // 중복 실행 방지

                        // 기존의 상하 움직임 애니메이션은 중지
                        if (arrowBobbingCoroutine != null)
                        {
                            StopCoroutine(arrowBobbingCoroutine);
                        }

                        // 사라지는 애니메이션 시작
                        StartCoroutine(DisappearFinishArrow(finishArrowInstance));
                        Debug.Log("[Race] 주자가 결승선에 근접하여 화살표가 사라지기 시작합니다.");
                    }
                }
                // ▲▲▲ [여기까지 추가] ▲▲▲
                // 토끼 낮잠 로직
                // 토끼 낮잠 로직
                if (!fastPetIsSleeping && !fastPetWokeUp)
                {
                    float fastPetProjectedDistance = Vector3.Dot(fastPet.transform.position - startPosition, dirToFinish);
                    if (fastPetProjectedDistance >= napDistance)
                    {
                        // ▼▼▼▼▼ [수정된 부분] 토끼가 자연스럽게 멈추고 자는 로직 ▼▼▼▼▼
                        fastPetIsSleeping = true;

                        // 자연스럽게 속도를 줄이며 멈추는 코루틴 시작
                        StartCoroutine(SlowDownAndSleep(fastPet));

                        Debug.Log($"[Race] {fastPet.petName}이(가) 속도를 줄이며 잠들 준비를 합니다.");
                        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
                    }
                }

                // 토끼 깨우기 로직
                float slowPetProjectedDistance = Vector3.Dot(slowPet.transform.position - startPosition, dirToFinish);
                float slowPetProgress = Mathf.Clamp01(slowPetProjectedDistance / totalRaceDistance);

                if (fastPetIsSleeping && !fastPetWokeUp && slowPetProgress >= slowPetWakeUpProgress)
                {
                    // ★★★ 수정: 토끼 깨우기 로직 - 애니메이션 후 이동 시작 ★★★
                    fastPetWokeUp = true;
                    var fastPetAnimController = fastPet.GetComponent<PetAnimationController>();

                    // 1. 애니메이션과 감정 먼저 변경
                    fastPetAnimController.StopContinuousAnimation(); // 잠자는 애니메이션 중지
                    fastPet.HideEmotion(); // 잠자기 파티클 제거
                    fastPet.ShowEmotion(EmotionType.Scared, 10f); // 놀란 감정 표시

                    // 2. 일어나는 애니메이션 재생 (정지 상태에서)
                    yield return StartCoroutine(fastPetAnimController.PlayAnimationWithCustomDuration(
                        PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));

                    // 3. 애니메이션 완료 후 이동 시작
                    if (IsAgentSafelyReady(fastPet))
                    {
                        fastPet.agent.avoidancePriority = fastPetOriginalPriority;
                        fastPet.agent.speed = fastPet.baseSpeed * fastPetFinalSprintSpeedMultiplier;
                        fastPet.agent.acceleration = fastPet.baseAcceleration * fastPetFinalSprintSpeedMultiplier;
                        fastPet.agent.updateRotation = true; // 회전 재개
                        fastPet.agent.isStopped = false;     // 이동 재개 (마지막에!)
                    }

                    // 4. Run 애니메이션 시작
                    fastPetAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
                    Debug.Log($"[Race] {fastPet.petName}이(가) 잠에서 깨어나 전력질주합니다!");
                }

                // 거북이 도착 및 타임아웃 체크 (기존과 동일)
                if (!slowPet.agent.pathPending && slowPet.agent.remainingDistance < 0.5f)
                {
                    slowPetFinished = true;
                }
                if (Time.time - raceStartTime > raceTimeoutSeconds)
                {
                    Debug.LogWarning("[Race] 경주 시간 초과! 거북이를 강제로 결승선으로 이동시킵니다.");
                    if (IsAgentSafelyReady(slowPet)) slowPet.agent.Warp(slowPetFinishDestination);
                    slowPetFinished = true;
                }

                yield return null;
            }

            // --- 7. 경주 종료 및 결과 처리 ---
            if (IsAgentSafelyReady(fastPet)) fastPet.agent.isStopped = true;
            if (IsAgentSafelyReady(slowPet)) slowPet.agent.isStopped = true;

            Debug.Log("[Race] 경주가 종료되었습니다. 거북이의 승리!");
            slowPet.ShowEmotion(EmotionType.Victory, 15f);
            fastPet.ShowEmotion(EmotionType.Defeat, 15f);

            StartCoroutine(slowPet.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 2.0f, false, false));
            StartCoroutine(fastPet.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 3.0f, false, false));

            yield return new WaitForSeconds(3f);
        }
        finally
        {
            // 디버그 표시 종료
            debugRaceActive = false;

            // ★★★ 수정: finally 블록을 대폭 간소화합니다. ★★★
            // 복잡한 NavMeshAgent 복구 로직을 제거합니다.
            // PetOriginalState 복원과 EndInteraction 호출만으로 충분합니다.
            Debug.Log("[Race] 상호작용 정리 시작.");

            // ▼▼▼ [수정] 생성된 화살표를 파괴하는 로직으로 교체합니다. ▼▼▼
            if (finishArrowInstance != null)
            {
                Destroy(finishArrowInstance);
                myRaceArrows.Remove(finishArrowInstance);
            }
            if (IsAgentSafelyReady(fastPet))
            {
                fastPet.agent.avoidancePriority = fastPetOriginalPriority;
            }
            // PetOriginalState가 NavMeshAgent의 속성(speed, acceleration 등)을 원래대로 복원합니다.
            fastPetOriginalState.Restore(fastPet);
            slowPetOriginalState.Restore(slowPet);

            // 모든 상호작용의 공통 종료 처리를 호출합니다. 
            // 이 메서드는 isInteracting 플래그 해제, 감정 숨기기, 다음 행동 준비 등을 안전하게 처리합니다.
            EndInteraction(fastPet, slowPet);
            Debug.Log("[Race] 상호작용 정리 완료.");
        }
    }
    // Pet.zip/Interaction/RaceInteraction.cs

    // ... PerformInteraction 메서드 아래에 다음 코루틴을 추가 ...

    /// <summary>
    /// 결승선 화살표가 위아래로 부드럽게 움직이는 애니메이션을 처리하는 코루틴입니다.
    /// </summary>
    /// <param name="arrow">애니메이션을 적용할 화살표 게임 오브젝트</param>
    private IEnumerator AnimateFinishArrow(GameObject arrow)
    {
        if (arrow == null) yield break;

        Vector3 startPosition = arrow.transform.position;
        // 각 화살표 애니메이션 시작 타이밍을 다르게 하여 단조로움을 피합니다.
        float randomOffset = Random.Range(0f, 2f * Mathf.PI);

        // 화살표가 파괴(Destroy)되기 전까지 무한 반복
        while (arrow != null)
        {
            // Sin 함수를 이용해 부드러운 상하 움직임(Bobbing) 생성
            float yOffset = Mathf.Sin((Time.time + randomOffset) * arrowBobSpeed) * arrowBobAmount;
            arrow.transform.position = startPosition + new Vector3(0, yOffset, 0);

            yield return null; // 다음 프레임까지 대기
        }
    }
    /// <summary>
    /// 결승선 화살표를 자연스럽게 작아지며 사라지게 하는 코루틴입니다.
    /// </summary>
    /// <param name="arrow">사라지게 할 화살표 게임 오브젝트</param>
    private IEnumerator DisappearFinishArrow(GameObject arrow)
    {
        if (arrow == null) yield break;

        Vector3 initialScale = arrow.transform.localScale;
        float duration = 1.5f; // 사라지는 데 걸리는 시간
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // 시간이 지남에 따라 스케일을 0으로 만듭니다.
            arrow.transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 애니메이션이 끝난 후 오브젝트를 파괴합니다.
        Destroy(arrow);
        myRaceArrows.Remove(arrow);
    }
    // ... (다른 헬퍼 메서드들) ...
    /// <summary>
    /// 펫들을 정확한 출발 위치로 부드럽게 미세 조정하는 코루틴
    /// </summary>
    private IEnumerator FineTunePositions(PetController fastPet, PetController slowPet,
        Vector3 fastPetTarget, Vector3 slowPetTarget, Vector3 raceDirection)
    {
        float adjustmentTime = 2f; // 조정에 걸리는 시간
        float elapsedTime = 0f;

        // 현재 위치 저장
        Vector3 fastPetStartPos = fastPet.transform.position;
        Vector3 slowPetStartPos = slowPet.transform.position;

        // 목표 회전 계산
        Quaternion targetRotation = Quaternion.LookRotation(raceDirection);
        Quaternion fastPetStartRot = fastPet.transform.rotation;
        Quaternion slowPetStartRot = slowPet.transform.rotation;

        // 애니메이션을 Idle로 설정
        fastPet.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
        slowPet.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);

        // NavMeshAgent 일시 정지
        fastPet.agent.isStopped = true;
        slowPet.agent.isStopped = true;

        while (elapsedTime < adjustmentTime)
        {
            float t = elapsedTime / adjustmentTime;
            // Smooth step 함수로 더 부드러운 움직임
            float smoothT = t * t * (3f - 2f * t);

            // 위치 보간
            fastPet.transform.position = Vector3.Lerp(fastPetStartPos, fastPetTarget, smoothT);
            slowPet.transform.position = Vector3.Lerp(slowPetStartPos, slowPetTarget, smoothT);

            // 회전 보간
            fastPet.transform.rotation = Quaternion.Slerp(fastPetStartRot, targetRotation, smoothT);
            slowPet.transform.rotation = Quaternion.Slerp(slowPetStartRot, targetRotation, smoothT);

            // 펫 모델도 함께 회전
            if (fastPet.petModelTransform != null)
                fastPet.petModelTransform.rotation = fastPet.transform.rotation;
            if (slowPet.petModelTransform != null)
                slowPet.petModelTransform.rotation = slowPet.transform.rotation;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 위치와 회전 확정
        fastPet.transform.position = fastPetTarget;
        slowPet.transform.position = slowPetTarget;
        fastPet.transform.rotation = targetRotation;
        slowPet.transform.rotation = targetRotation;

        if (fastPet.petModelTransform != null)
            fastPet.petModelTransform.rotation = targetRotation;
        if (slowPet.petModelTransform != null)
            slowPet.petModelTransform.rotation = targetRotation;

        // NavMeshAgent 위치 동기화
        if (IsAgentSafelyReady(fastPet))
        {
            fastPet.agent.nextPosition = fastPetTarget;
        }
        if (IsAgentSafelyReady(slowPet))
        {
            slowPet.agent.nextPosition = slowPetTarget;
        }

        Debug.Log($"[Race] 출발 위치 미세 조정 완료. 간격: {Vector3.Distance(fastPet.transform.position, slowPet.transform.position):F2}m");
    }
    // RaceInteraction.cs에 새로운 메서드 추가

    /// <summary>
    /// 토끼가 자연스럽게 속도를 줄이며 멈춘 후 잠드는 코루틴
    /// </summary>
    private IEnumerator SlowDownAndSleep(PetController fastPet)
    {
        if (!IsAgentSafelyReady(fastPet)) yield break;
        // ★★★ 수정: 잠들기 직전에 회피 우선순위를 최하위로 변경 ★★★
        fastPetOriginalPriority = fastPet.agent.avoidancePriority; // 원래 우선순위 저장
        fastPet.agent.avoidancePriority = 99; // 길을 막지 않도록 우선순위 최하위(99)로 설정

        Debug.Log($"[Race] {fastPet.petName}의 회피 우선순위를 99로 낮춥니다. (길막 방지)");

        // 경주 속도 사용 (현재 agent.speed가 아닌 설정된 경주 속도)
        float currentSpeed = fastPet.baseSpeed * fastPetStartSpeedMultiplier;
        float slowDownDuration = fastPetSlowDownDuration; // 2.0f 대신
        float elapsedTime = 0f;

        // 1. 속도를 서서히 줄이기
        while (elapsedTime < slowDownDuration)
        {
            if (!IsAgentSafelyReady(fastPet)) break;

            float t = elapsedTime / slowDownDuration;
            // EaseOutCubic 커브를 사용하여 자연스러운 감속
            float easeT = 1f - Mathf.Pow(1f - t, 3f);

            fastPet.agent.speed = Mathf.Lerp(currentSpeed, fastPetMinSpeedBeforeSleep, easeT); // 0.5f 대신

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 2. 완전히 멈추기
        if (IsAgentSafelyReady(fastPet))
        {
            fastPet.agent.isStopped = true;
            fastPet.agent.velocity = Vector3.zero;
            fastPet.agent.updateRotation = false;
        }

        // 3. 하품 애니메이션 (선택사항)
        var animController = fastPet.GetComponent<PetAnimationController>();
        // if (animController != null)
        // {
        //     // 하품이나 기지개를 펴는 애니메이션이 있다면 먼저 재생
        //     yield return StartCoroutine(animController.PlayAnimationWithCustomDuration(
        //         PetAnimationController.PetAnimationType.Jump, 1.0f, true, false));
        // }

        // 4. 잠자는 감정 표현과 애니메이션 시작
        fastPet.ShowEmotion(EmotionType.Sleep, 60f);

        if (animController != null && fastPet.animator != null)
        {
            // 애니메이션 속도 1.5배로 설정하여 빠르게 눕기
            fastPet.animator.speed = 1.5f;

            // 비동기로 Rest 애니메이션 시작 (999초)
            StartCoroutine(animController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Rest, 999f, true, false));

            // 1초 후 속도를 정상으로 되돌리기
            StartCoroutine(ResetAnimationSpeedAfterDelay(fastPet, 1.0f));
        }

        Debug.Log($"[Race] {fastPet.petName}이(가) 편안하게 잠들었습니다.");
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
        out Vector3 fastPetFinish, out Vector3 slowPetFinish)
    {
        // 결승선에서 양옆으로 분리된 목적지 생성
        Vector3 sideDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;

        Vector3 leftFinish = finishLine - sideDirection * (finishLineSpread / 2);
        Vector3 rightFinish = finishLine + sideDirection * (finishLineSpread / 2);

        // NavMesh 유효 위치로 보정
        fastPetFinish = FindValidPositionOnNavMesh(leftFinish, finishLineSpread);
        slowPetFinish = FindValidPositionOnNavMesh(rightFinish, finishLineSpread);

        Debug.Log($"[Race] 분리된 결승 목적지 설정: 토끼({fastPetFinish}), 거북이({slowPetFinish})");
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

    private void CalculateAlignedStartPositions(Vector3 startCenter, Vector3 raceDirection,
     out Vector3 pet1Pos, out Vector3 pet2Pos, float spacing = 3f)
    {
        // 경주 진행 방향에 수직인 옆 방향을 계산
        Vector3 sideDirection = Vector3.Cross(Vector3.up, raceDirection).normalized;

        // ★★★ 수정: 더 정확한 위치 계산 ★★★
        // 중앙점에서 정확히 spacing/2 만큼 떨어진 위치
        Vector3 leftPos = startCenter - sideDirection * (spacing / 2);
        Vector3 rightPos = startCenter + sideDirection * (spacing / 2);

        // NavMesh 위의 가장 가까운 유효한 위치 찾기
        NavMeshHit leftHit, rightHit;
        if (NavMesh.SamplePosition(leftPos, out leftHit, 2f, NavMesh.AllAreas))
        {
            pet1Pos = leftHit.position;
        }
        else
        {
            pet1Pos = leftPos;
        }

        if (NavMesh.SamplePosition(rightPos, out rightHit, 2f, NavMesh.AllAreas))
        {
            pet2Pos = rightHit.position;
        }
        else
        {
            pet2Pos = rightPos;
        }

        // Y 좌표를 동일하게 맞춤 (지형 높이 차이 보정)
        float avgY = (pet1Pos.y + pet2Pos.y) / 2f;
        pet1Pos.y = avgY;
        pet2Pos.y = avgY;

        Debug.Log($"[Race] 정렬된 출발점: 간격={Vector3.Distance(pet1Pos, pet2Pos):F2}m");
    }
    
    /// <summary>
    /// 애니메이션 속도를 일정 시간 후에 리셋하는 헬퍼 코루틴
    /// </summary>
    private IEnumerator ResetAnimationSpeedAfterDelay(PetController pet, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (pet != null && pet.animator != null)
        {
            pet.animator.speed = 1.0f;  // 정상 속도로 복귀
            Debug.Log($"[Race] {pet.petName}의 애니메이션 속도를 정상으로 복귀했습니다.");
        }
    }

    /// <summary>
    /// Scene 뷰에서 경주 경로와 중요 지점들을 시각화
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying || !debugRaceActive)
            return;

        // 1. 경주 경로 표시 (시작점 → 결승점)
        Gizmos.color = Color.green;
        Gizmos.DrawLine(debugStartPosition, debugFinishLine);

        // 시작점 표시
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(debugStartPosition, 1f);
        Gizmos.DrawRay(debugStartPosition, Vector3.up * 3f);

        // 결승선 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(debugFinishLine, 1f);
        Gizmos.DrawRay(debugFinishLine, Vector3.up * 5f);

        // 2. 토끼 낮잠 지점 표시 (40% 지점)
        Vector3 napPoint = debugStartPosition + debugDirectionToFinish * (debugTotalRaceDistance * fastPetNapProgress);
        Gizmos.color = napPointColor;
        Gizmos.DrawWireSphere(napPoint, 2f);
        Gizmos.DrawRay(napPoint, Vector3.up * 10f);

        // 낮잠 지점 텍스트 (Unity Editor 전용)
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(napPoint + Vector3.up * 12f,
            $"NAP POINT ({(fastPetNapProgress * 100):F0}%)",
            new GUIStyle() {
                normal = new GUIStyleState() { textColor = napPointColor },
                fontSize = 14,
                fontStyle = FontStyle.Bold
            });
        #endif

        // 3. 거북이 깨우기 지점 표시 (94% 지점)
        Vector3 wakeUpPoint = debugStartPosition + debugDirectionToFinish * (debugTotalRaceDistance * slowPetWakeUpProgress);
        Gizmos.color = wakeUpPointColor;
        Gizmos.DrawWireSphere(wakeUpPoint, 2f);
        Gizmos.DrawRay(wakeUpPoint, Vector3.up * 10f);

        // 깨우기 지점 평면 표시 (경주 방향에 수직)
        Vector3 perpendicular = Vector3.Cross(debugDirectionToFinish, Vector3.up).normalized;
        Gizmos.color = new Color(wakeUpPointColor.r, wakeUpPointColor.g, wakeUpPointColor.b, 0.3f);
        for (int i = -5; i <= 5; i++)
        {
            Vector3 lineStart = wakeUpPoint + perpendicular * i * 2f;
            Vector3 lineEnd = wakeUpPoint + perpendicular * i * 2f + Vector3.up * 10f;
            Gizmos.DrawLine(lineStart, lineEnd);
        }

        #if UNITY_EDITOR
        UnityEditor.Handles.Label(wakeUpPoint + Vector3.up * 12f,
            $"WAKE UP POINT ({(slowPetWakeUpProgress * 100):F0}%)",
            new GUIStyle() {
                normal = new GUIStyleState() { textColor = wakeUpPointColor },
                fontSize = 14,
                fontStyle = FontStyle.Bold
            });
        #endif

        // 4. 실시간 진행 상황 표시
        if (debugFastPet != null && debugSlowPet != null && debugTotalRaceDistance > 0)
        {
            // 토끼 현재 위치와 진행도
            float fastPetProjectedDistance = Vector3.Dot(debugFastPet.transform.position - debugStartPosition, debugDirectionToFinish);
            float fastPetProgress = Mathf.Clamp01(fastPetProjectedDistance / debugTotalRaceDistance);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(debugFastPet.transform.position + Vector3.up * 5f, Vector3.one * 0.5f);

            // 거북이 현재 위치와 진행도
            float slowPetProjectedDistance = Vector3.Dot(debugSlowPet.transform.position - debugStartPosition, debugDirectionToFinish);
            float slowPetProgress = Mathf.Clamp01(slowPetProjectedDistance / debugTotalRaceDistance);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(debugSlowPet.transform.position + Vector3.up * 5f, Vector3.one * 0.5f);

            #if UNITY_EDITOR
            // 진행도 텍스트
            UnityEditor.Handles.Label(debugFastPet.transform.position + Vector3.up * 7f,
                $"Fast: {(fastPetProgress * 100):F1}%",
                new GUIStyle() {
                    normal = new GUIStyleState() { textColor = Color.cyan },
                    fontSize = 12
                });

            UnityEditor.Handles.Label(debugSlowPet.transform.position + Vector3.up * 7f,
                $"Slow: {(slowPetProgress * 100):F1}%",
                new GUIStyle() {
                    normal = new GUIStyleState() { textColor = Color.magenta },
                    fontSize = 12
                });
            #endif
        }
    }

    /// <summary>
    /// 컴포넌트가 파괴될 때 남아있는 화살표 정리
    /// </summary>
    private void OnDestroy()
    {
        if (finishArrowInstance != null)
        {
            Destroy(finishArrowInstance);
            myRaceArrows.Remove(finishArrowInstance);
        }
    }


}