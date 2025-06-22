using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫의 기본 움직임(Idle, Walk, Run, Jump, Rest, LookAround, Play)을
/// 성향(personality)별 가중치에 따라 결정하고 NavMesh를 이용해 이동합니다.
/// </summary>
public class PetMovementController : MonoBehaviour
{
    // ────────────────────────────────────────────────────────────────────
    // 1) 외부 참조 및 내부 상태 변수
    // ────────────────────────────────────────────────────────────────────

    private PetController petController;
    private PetTreeClimbingController treeClimbingController;
    // private PetWaterBehaviorController waterBehaviorController;

    private float behaviorTimer = 0f;
    private float nextBehaviorChange = 0f;
    private BehaviorState currentBehaviorState = BehaviorState.Walking;

    /// <summary>펫이 수행 가능한 행동 목록</summary>
    private enum BehaviorState
    {
        Idle,    // 가만히 대기
        Walking, // 느리게 걷기
        Running, // 빠르게 달리기
        Jumping, // 점프
        Resting, // 쉬기(앉기 등)
        Looking, // 주변 둘러보기
        Playing  // 놀기(제자리 뱅글뱅글, 연속 점프 등)
    }

    /// <summary>성향별 행동 가중치, 지속시간, 속도 배율 저장 클래스</summary>
    private class PersonalityBehavior
    {
        public float idleWeight, walkWeight, runWeight, jumpWeight;
        public float restWeight, lookWeight, playWeight;
        public float behaviorDuration;   // 행동 지속 기본 시간
        public float speedMultiplier;    // 기본 속도 배율
    }
    private PersonalityBehavior pb;

    public bool IsRestingOrIdle => currentBehaviorState == BehaviorState.Resting ||
                                   currentBehaviorState == BehaviorState.Idle;

    private Coroutine currentBehaviorCoroutine = null;

    /// <summary>
    /// 물 속성 펫이 물 vs 육지 목적지를 고를 확률 (0~1).
    /// </summary>
    [Range(0f, 1f)] public float waterDestinationChance = 0.8f;

    // ────────────────────────────────────────────────────────────────────
    // 2) 초기화
    // ────────────────────────────────────────────────────────────────────
    public void Init(PetController controller)
    {
        petController = controller;

        // 서브 컨트롤러 초기화
        treeClimbingController = gameObject.AddComponent<PetTreeClimbingController>();
        treeClimbingController.Init(controller);

        // waterBehaviorController = gameObject.AddComponent<PetWaterBehaviorController>();
        // waterBehaviorController.Init(controller);

        InitializePersonalityBehavior();
        StartCoroutine(DelayedStart());
    }

    // 성향(personality)에 따른 행동 가중치·지속시간·속도 배율 초기화
    private void InitializePersonalityBehavior()
    {
        pb = new PersonalityBehavior();
        switch (petController.personality)
        {
            case PetAIProperties.Personality.Lazy:
                pb.idleWeight = 3; pb.walkWeight = 2; pb.runWeight = 0.1f; pb.jumpWeight = 0.1f;
                pb.restWeight = 10; pb.lookWeight = 2; pb.playWeight = 0.1f;
                pb.behaviorDuration = 5; pb.speedMultiplier = 0.7f;
                break;
            case PetAIProperties.Personality.Shy:
                pb.idleWeight = 2; pb.walkWeight = 3; pb.runWeight = 0.5f; pb.jumpWeight = 0.5f;
                pb.restWeight = 2; pb.lookWeight = 4; pb.playWeight = 0.5f;
                pb.behaviorDuration = 6; pb.speedMultiplier = 0.8f;
                break;
            case PetAIProperties.Personality.Brave:
                pb.idleWeight = 1; pb.walkWeight = 2; pb.runWeight = 4; pb.jumpWeight = 3;
                pb.restWeight = 1; pb.lookWeight = 1; pb.playWeight = 2;
                pb.behaviorDuration = 8; pb.speedMultiplier = 1.2f;
                break;
            default: // Playful
                pb.idleWeight = 0.5f; pb.walkWeight = 2; pb.runWeight = 3; pb.jumpWeight = 4;
                pb.restWeight = 0.5f; pb.lookWeight = 1; pb.playWeight = 5;
                pb.behaviorDuration = 4; pb.speedMultiplier = 1.1f;
                break;
        }
    }

    // NavMeshAgent가 완전히 준비될 때까지 대기한 뒤 첫 행동 결정
    private IEnumerator DelayedStart()
    {
        float maxWait = 5f, elapsed = 0f;
        while (elapsed < maxWait)
        {
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
                break;
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        if (!(petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh))
        {
            Debug.LogWarning($"[PetMovementController] {petController.petName}: NavMeshAgent 준비 실패");
            yield break;
        }
        DecideNextBehavior();
    }

    // ────────────────────────────────────────────────────────────────────
    // 3) 매 프레임 호출
    // ────────────────────────────────────────────────────────────────────
    public void UpdateMovement()
    {

        // ★★★ 추가: 나무 위에 있을 때 배고픔 체크 ★★★
    if (petController.isClimbingTree && !treeClimbingController.IsSearchingForTree())
    {
        // 배가 고프면 나무에서 내려오도록 처리
        if (petController.hunger > 70f)
        {
            Debug.Log($"{petController.petName}이(가) 배가 고파서 나무에서 내려오기 시작합니다.");
            StartCoroutine(ForceClimbDownFromTree());
            return;
        }
        return; // 배가 안 고프면 계속 나무에 있음
    }
        if (petController.isActionLocked) return;

        // 들고 있는 상태면 즉시 리턴
        if (petController.isHolding) return;

        // 물 영역 체크 위임
        // waterBehaviorController.CheckWaterArea();
        var sleepingController = petController.GetComponent<PetSleepingController>();
        if (sleepingController != null && sleepingController.IsSleepingOrSeeking())
        {
            return;
        }
        // 나무 체크 위임
        if (!petController.isHolding)
        {
            treeClimbingController.CheckForTreeClimbing();
        }

        // 나무에 올라가 있으면 다른 움직임 처리 스킵
        if (petController.isClimbingTree)
        {
            if (!petController.isSelected)
            {
                return;
            }
            return;
        }

        // 모으기 모드면 즉시 리턴
        if (petController.isGathering)
        {
            if (petController.isGathered && Camera.main != null)
            {
                Vector3 dir = Camera.main.transform.position - transform.position;
                dir.y = 0f;
                if (dir.magnitude > 0.1f)
                {
                    Quaternion target = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, target,
                        petController.rotationSpeed * Time.deltaTime);
                }
            }
            return;
        }

        if (!IsAgentReady()) return;

        behaviorTimer += Time.deltaTime;

        // 현재 행동에 따른 처리
        if (!petController.agent.isStopped &&
            (currentBehaviorState == BehaviorState.Walking || currentBehaviorState == BehaviorState.Running))
        {
            HandleMovement();
        }

        // 행동 전환 시점 도달 체크
        if (behaviorTimer >= nextBehaviorChange)
        {
            DecideNextBehavior();
        }

        // 모델 위치와 회전을 NavMeshAgent와 동기화
        if (petController.petModelTransform != null)
        {
            petController.petModelTransform.position = transform.position;
        }
    }
// ★★★ 새로운 메서드 추가 ★★★
private IEnumerator ForceClimbDownFromTree()
{
    if (!petController.isClimbingTree) yield break;
    
    // 애니메이션 정지
    var animController = petController.GetComponent<PetAnimationController>();
    animController?.StopContinuousAnimation();
    
    // 나무에서 내려오기
    yield return StartCoroutine(treeClimbingController.ClimbDownTree());
    
    // 상태 초기화
    petController.isClimbingTree = false;
    petController.currentTree = null;
    
    // 음식 찾기 시작하도록 다음 행동 결정
    DecideNextBehavior();
}
    // 헬퍼 메서드
    private bool IsAgentReady()
    {
        return petController.agent != null &&
               petController.agent.enabled &&
               petController.agent.isOnNavMesh;
    }

    // 나무 타기 강제 취소 (외부 호출용)
    public void ForceCancelClimbing()
    {
        treeClimbingController.ForceCancelClimbing();
        currentBehaviorState = BehaviorState.Idle;
        behaviorTimer = 0f;
    }

    // 행동 전환 시 호출
    private void DecideNextBehavior()
    {

        // ★★★ 추가: 나무를 찾고 있다면, 새로운 행동을 결정하지 않습니다.
        if (treeClimbingController != null && treeClimbingController.IsSearchingForTree())
        {
            behaviorTimer = 0f;
            return;
        }
        if (petController.isSelected)
        {
            behaviorTimer = 0f;
            return;
        }

        var feedingController = petController.GetComponent<PetFeedingController>();
        var sleepingController = petController.GetComponent<PetSleepingController>();

        if (petController.isClimbingTree)
        {
            behaviorTimer = 0f;
            return;
        }

        if ((feedingController != null && feedingController.IsEatingOrSeeking()) ||
            (sleepingController != null && sleepingController.IsSleepingOrSeeking()))
        {
            behaviorTimer = 0f;
            return;
        }

        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        behaviorTimer = 0f;
        float total = pb.idleWeight + pb.walkWeight + pb.runWeight +
                      pb.jumpWeight + pb.restWeight + pb.lookWeight + pb.playWeight;
        float r = Random.Range(0, total), sum = 0;

        if ((sum += pb.idleWeight) >= r) { SetBehavior(BehaviorState.Idle); return; }
        if ((sum += pb.walkWeight) >= r) { SetBehavior(BehaviorState.Walking); return; }
        if ((sum += pb.runWeight) >= r) { SetBehavior(BehaviorState.Running); return; }
        if ((sum += pb.jumpWeight) >= r) { SetBehavior(BehaviorState.Jumping); return; }
        if ((sum += pb.restWeight) >= r) { SetBehavior(BehaviorState.Resting); return; }
        if ((sum += pb.lookWeight) >= r) { SetBehavior(BehaviorState.Looking); return; }
        { SetBehavior(BehaviorState.Playing); }
    }

    // 행동 상태 전환
    private void SetBehavior(BehaviorState state)
    {
        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }

        if (petController.isGathering) return;
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[PetMovementController] {petController.petName}: NavMeshAgent 미준비");
            return;
        }

        currentBehaviorState = state;
        nextBehaviorChange = pb.behaviorDuration + Random.Range(-1f, 1f);

        try { petController.agent.isStopped = true; }
        catch { /* 예외 무시 */ }

        var anim = petController.GetComponent<PetAnimationController>();

        if (anim != null)
        {
            anim.StopContinuousAnimation();
        }

        switch (state)
        {
            case BehaviorState.Idle:
                anim?.SetContinuousAnimation(0);
                break;

            case BehaviorState.Walking:
                SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier, false);
                SetRandomDestination();
                break;

            case BehaviorState.Running:
                SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier * 1.5f, false);
                SetRandomDestination();
                break;

            case BehaviorState.Jumping:
                currentBehaviorCoroutine = StartCoroutine(PerformJump());
                break;

            case BehaviorState.Resting:
                anim?.SetContinuousAnimation(5);
                break;

            case BehaviorState.Looking:
                currentBehaviorCoroutine = StartCoroutine(LookAround());
                break;

            case BehaviorState.Playing:
                currentBehaviorCoroutine = StartCoroutine(PerformPlay());
                break;
        }

        // 물에 있으면 속도 재조정
        petController.AdjustSpeedForWater();
    }

    // 새 메서드 추가
    public void ForceStopCurrentBehavior()
    {
        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }

        currentBehaviorState = BehaviorState.Idle;
        behaviorTimer = 0f;
    }

    private void SafeSetAgentMovement(float speed, bool isStopped)
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;
        if (petController.isGathering) return;

        try
        {
            petController.agent.speed = speed;
            petController.agent.isStopped = isStopped;
        }
        catch { /* 예외 무시 */ }
    }

    private void HandleMovement()
    {
        if (!petController.agent.pathPending && petController.agent.remainingDistance < 1f)
            SetRandomDestination();
    }

    private IEnumerator PerformJump()
    {
        yield return new WaitForSeconds(0.2f);
        var anim = petController.GetComponent<PetAnimationController>();
        if (anim != null)
            yield return StartCoroutine(anim.PlayAnimationWithCustomDuration(3, 1f, true, false));
    }

    private IEnumerator LookAround()
    {
        var anim = petController.GetComponent<PetAnimationController>();
        anim?.SetContinuousAnimation(1);

        for (int i = 0; i < 2; i++)
        {
            float t = 0f;
            Quaternion start = transform.rotation;
            Quaternion end = start * Quaternion.Euler(0, 45, 0);

            while (t < 1f)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            t = 0f;
            start = transform.rotation;
            end = start * Quaternion.Euler(0, -90, 0);

            while (t < 1f)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
        }

        anim?.StopContinuousAnimation();
    }

    private IEnumerator PerformPlay()
    {
        var anim = petController.GetComponent<PetAnimationController>();
        int type = Random.Range(0, 3);

        if (type == 0)
        {
            SafeSetAgentMovement(petController.baseSpeed, true);
            anim?.SetContinuousAnimation(2);
            yield return new WaitForSeconds(3f);
            anim?.StopContinuousAnimation();
        }
        else if (type == 1)
        {
            if (anim != null)
                for (int i = 0; i < 3; i++)
                    yield return StartCoroutine(anim.PlayAnimationWithCustomDuration(3, 0.8f, true, false));
        }
        else
        {
            SafeSetAgentMovement(petController.baseSpeed * 2f, false);
            anim?.SetContinuousAnimation(2);
            SetRandomDestination();
            yield return new WaitForSeconds(2f);
            SafeSetAgentMovement(petController.baseSpeed, true);
            anim?.StopContinuousAnimation();
            yield return new WaitForSeconds(0.5f);
        }
        SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier, false);
    }

    public void SetRandomDestination()
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        int waterArea = NavMesh.GetAreaFromName("Water");
        int mask;

        if (petController.habitat == PetAIProperties.Habitat.Water && waterArea != -1)
        {
            mask = (Random.value < waterDestinationChance)
                ? (1 << waterArea)
                : NavMesh.AllAreas;
        }
        else
        {
            mask = (waterArea != -1)
                ? (NavMesh.AllAreas & ~(1 << waterArea))
                : NavMesh.AllAreas;
        }

        Vector3 dir = Random.insideUnitSphere * 50f + transform.position;
        if (NavMesh.SamplePosition(dir, out NavMeshHit hit, 50f, mask))
        {
            try
            {
                petController.agent.SetDestination(hit.position);
                var anim = petController.GetComponent<PetAnimationController>();
                if (anim != null)
                {
                    if (currentBehaviorState == BehaviorState.Walking) anim.SetContinuousAnimation(1);
                    else if (currentBehaviorState == BehaviorState.Running) anim.SetContinuousAnimation(2);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PetMovementController] {petController.petName}: SetDestination 실패 - {e.Message}");
            }
        }
    }
}