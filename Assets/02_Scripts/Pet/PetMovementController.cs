using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫의 기본 움직임(Idle, Walk, Run, Jump, Rest, LookAround, Play)을
/// 성향(personality)별 가중치에 따라 결정하고 NavMesh를 이용해 이동합니다.
/// PetAnimationController를 이용해 애니메이션을 전환합니다.
/// 물 속성 펫은 물 vs 육지 목적지를 선택할 확률을 조절할 수 있습니다.
/// </summary>
public class PetMovementController : MonoBehaviour
{
    // ────────────────────────────────────────────────────────────────────
    // 1) 외부 참조 및 내부 상태 변수
    // ────────────────────────────────────────────────────────────────────
    
    private PetController petController;       // PetController 스크립트 레퍼런스 :contentReference[oaicite:1]{index=1}
    private float behaviorTimer = 0f;          // 다음 행동 전환까지 경과 시간
    private float nextBehaviorChange = 0f;     // 행동 전환 시점 (초)
    private BehaviorState currentBehaviorState = BehaviorState.Walking;  // 현재 행동 상태
    
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
    private PersonalityBehavior pb;       // 현재 펫의 성향별 설정 저장 객체 :contentReference[oaicite:2]{index=2}
public bool IsRestingOrIdle => currentBehaviorState == BehaviorState.Resting || 
                               currentBehaviorState == BehaviorState.Idle;

    /// <summary>
    /// 물 속성 펫이 물 vs 육지 목적지를 고를 확률 (0~1).
    /// 예: 0.8이면 80% 확률로 물 영역을 선택.
    /// </summary>
    [Range(0f, 1f)] public float waterDestinationChance = 0.8f;

    // ────────────────────────────────────────────────────────────────────
    // 2) 초기화: PetController 주입 & 물 영역 비용 설정 & 성향 초기화
    // ────────────────────────────────────────────────────────────────────
    public void Init(PetController controller)
    {
        petController = controller;  // 외부 PetController 참조 저장

        // NavMeshAgent가 이미 활성화되어 NavMesh 위에 있을 때만 물 영역 비용을 설정
        if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (waterArea != -1)
            {
                // 물 속성 펫은 물 영역 비용 낮게, 비물 속성은 높게 설정
                petController.agent.SetAreaCost(
                    waterArea,
                    petController.habitat == PetAIProperties.Habitat.Water ? 0.5f : 10f
                );
            }
        }

        InitializePersonalityBehavior();      // 성향별 가중치·지속시간 설정 :contentReference[oaicite:3]{index=3}
        StartCoroutine(DelayedStart());       // NavMeshAgent 준비 후 행동 결정 시작 :contentReference[oaicite:4]{index=4}
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
        DecideNextBehavior();  // 첫 행동 결정 :contentReference[oaicite:5]{index=5}
    }

    // ────────────────────────────────────────────────────────────────────
    // 3) 매 프레임 호출: 행동 지속 타이머 & 행동별 처리 & 모델 위치 동기화
    // ────────────────────────────────────────────────────────────────────
    // UpdateMovement() 메서드의 마지막 부분 수정
public void UpdateMovement()
{
    Debug.Log("#PetMovementController/UpdateMovement");
    
    // 🎯 모으기 모드 특별 처리
    if (petController.isGathered)
    {
        if (Camera.main != null)
        {
            // ★ 부모 오브젝트를 카메라 방향으로 회전
            Vector3 dir = Camera.main.transform.position - transform.position;
            dir.y = 0f;
            
            if (dir.magnitude > 0.1f)
            {
                Quaternion target = Quaternion.LookRotation(dir);
                // 부모 오브젝트 회전
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    target,
                    petController.rotationSpeed * Time.deltaTime
                );
            }
        }
        return;
    }

    // NavMeshAgent 준비 상태가 아니면 종료
    if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        return;

    behaviorTimer += Time.deltaTime;  // 행동 지속 시간 누적

    // 현재 행동에 따른 처리
    if (!petController.agent.isStopped &&
        (currentBehaviorState == BehaviorState.Walking || currentBehaviorState == BehaviorState.Running))
    {
        HandleMovement();  // 목적지 도착 시 새 목적지 설정
    }

    // 행동 전환 시점 도달 체크
    if (behaviorTimer >= nextBehaviorChange)
    {
        DecideNextBehavior();  // 다음 행동 결정
    }

    // ★ 모델 위치와 회전을 NavMeshAgent와 동기화 (수정된 부분)
    if (petController.petModelTransform != null)
    {
        // 위치 동기화
        petController.petModelTransform.position = transform.position;
        
        // ★ 회전 동기화 - 이동 중이고 쉬는/대기 상태가 아닐 때만 NavMeshAgent 회전 따라가기
        // ★ 회전 동기화 - 부모 오브젝트 회전
    if (!petController.agent.isStopped && 
        petController.agent.hasPath && 
        petController.agent.remainingDistance > 0.1f &&
        currentBehaviorState != BehaviorState.Resting &&
        currentBehaviorState != BehaviorState.Idle)
    {
        Vector3 moveDirection = petController.agent.velocity.normalized;
        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            // ★ 부모 오브젝트 회전
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                petController.rotationSpeed * Time.deltaTime
            );
        }
    }
    }
}

    // 행동 전환 시 호출: 가중치 기반 랜덤으로 다음 행동 선정
    private void DecideNextBehavior()
    {

 // [수정] 새로운 행동을 결정하기 전, 더 중요한 행동(식사, 수면)을 하는지 확인
        var feedingController = petController.GetComponent<PetFeedingController>();
        var sleepingController = petController.GetComponent<PetSleepingController>();

        if ((feedingController != null && feedingController.IsEatingOrSeeking()) ||
            (sleepingController != null && sleepingController.IsSleepingOrSeeking()))
        {
            // 밥을 먹으러 가거나 잠을 자러 가는 중이면, 새로운 기본 행동을 결정하지 않음
            behaviorTimer = 0f; // 타이머만 초기화해서 곧 다시 체크하도록 함
            return;
        }
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        behaviorTimer = 0f;
        float total = pb.idleWeight + pb.walkWeight + pb.runWeight +
                      pb.jumpWeight + pb.restWeight + pb.lookWeight + pb.playWeight;
        float r = Random.Range(0, total), sum = 0;

        if ((sum += pb.idleWeight) >= r)      { SetBehavior(BehaviorState.Idle);    return; }
        if ((sum += pb.walkWeight) >= r)      { SetBehavior(BehaviorState.Walking); return; }
        if ((sum += pb.runWeight) >= r)       { SetBehavior(BehaviorState.Running); return; }
        if ((sum += pb.jumpWeight) >= r)      { SetBehavior(BehaviorState.Jumping); return; }
        if ((sum += pb.restWeight) >= r)      { SetBehavior(BehaviorState.Resting); return; }
        if ((sum += pb.lookWeight) >= r)      { SetBehavior(BehaviorState.Looking); return; }
                                              { SetBehavior(BehaviorState.Playing);              }
    }

    // 행동 상태 전환: NavMeshAgent 속성·애니메이션 적용 :contentReference[oaicite:8]{index=8}
    private void SetBehavior(BehaviorState state)
    {                Debug.Log("#PetMovementController/SetBehavior");

        // 모으기 상태면 행동 변경하지 않음
        if (petController.isGathering) return;

        // Agent 준비 확인
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[PetMovementController] {petController.petName}: NavMeshAgent 미준비");
            return;
        }

        currentBehaviorState = state;
        nextBehaviorChange = pb.behaviorDuration + Random.Range(-1f, 1f);

        // 이동 일시정지
        try { petController.agent.isStopped = true; }
        catch { /* 예외 무시 */ }

        var anim = petController.GetComponent<PetAnimationController>();
        switch (state)
        {
            case BehaviorState.Idle:
                anim?.SetContinuousAnimation(0);
                break;
            case BehaviorState.Walking:
                SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier, false);
                SetRandomDestination();
                anim?.SetContinuousAnimation(1);
                break;
            case BehaviorState.Running:
                SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier * 1.5f, false);
                SetRandomDestination();
                anim?.SetContinuousAnimation(2);
                break;
            case BehaviorState.Jumping:
                StartCoroutine(PerformJump());
                break;
            case BehaviorState.Resting:
                anim?.SetContinuousAnimation(5);
                break;
            case BehaviorState.Looking:
                StartCoroutine(LookAround());
                break;
            case BehaviorState.Playing:
                StartCoroutine(PerformPlay());
                break;
        }
    }

    // NavMeshAgent 속도·정지 상태를 안전하게 설정 (모으기 상태 무시) :contentReference[oaicite:9]{index=9}
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

    // 목적지 도착 시 새 랜덤 목적지 설정 :contentReference[oaicite:10]{index=10}
    private void HandleMovement()
    {
        if (!petController.agent.pathPending && petController.agent.remainingDistance < 1f)
            SetRandomDestination();
    }

    // 점프 애니메이션 재생 코루틴 :contentReference[oaicite:11]{index=11}
    private IEnumerator PerformJump()
    {
        yield return new WaitForSeconds(0.2f);
        var anim = petController.GetComponent<PetAnimationController>();
        if (anim != null)
            yield return StartCoroutine(anim.PlayAnimationWithCustomDuration(3, 1f, true, false));
    }

    // 주변 둘러보기 코루틴 :contentReference[oaicite:12]{index=12}
   private IEnumerator LookAround()
{
    var anim = petController.GetComponent<PetAnimationController>();
    anim?.SetContinuousAnimation(1);

    for (int i = 0; i < 2; i++)
    {
        // ★ 부모 오브젝트 회전
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

        // 왼쪽으로 회전
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

    // 놀기 행동 코루틴 :contentReference[oaicite:13]{index=13}
    private IEnumerator PerformPlay()
    {
        var anim = petController.GetComponent<PetAnimationController>();
        int type = Random.Range(0, 3);

        if (type == 0)
        {
            // 제자리 뛰기
            SafeSetAgentMovement(petController.baseSpeed, true);
            anim?.SetContinuousAnimation(2);
            yield return new WaitForSeconds(3f);
            anim?.StopContinuousAnimation();
        }
        else if (type == 1)
        {
            // 연속 점프
            if (anim != null)
                for (int i = 0; i < 3; i++)
                    yield return StartCoroutine(anim.PlayAnimationWithCustomDuration(3, 0.8f, true, false));
        }
        else
        {
            // 뛰면서 이동 후 멈춤
            SafeSetAgentMovement(petController.baseSpeed * 2f, false);
            anim?.SetContinuousAnimation(2);
            SetRandomDestination();
            yield return new WaitForSeconds(2f);
            SafeSetAgentMovement(petController.baseSpeed, true);
            anim?.StopContinuousAnimation();
            yield return new WaitForSeconds(0.5f);
        }
        // 놀기 후 기본 속도·이동 재설정
        SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier, false);
    }

    // ────────────────────────────────────────────────────────────────────
    // 4) 랜덤 위치 샘플링 후 목적지 설정 :contentReference[oaicite:14]{index=14}
    // ────────────────────────────────────────────────────────────────────
    public void SetRandomDestination()
    {
        // Agent 준비 확인
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        int waterArea = NavMesh.GetAreaFromName("Water");
        int mask;

        // 물 속성 펫: 물 vs 전체 NavMesh 확률 선택
        if (petController.habitat == PetAIProperties.Habitat.Water && waterArea != -1)
        {
            mask = (Random.value < waterDestinationChance)
                ? (1 << waterArea)     // 물 영역만
                : NavMesh.AllAreas;    // 전체
        }
        else
        {
            // 육지 펫: 물 영역 제외
            mask = (waterArea != -1)
                ? (NavMesh.AllAreas & ~(1 << waterArea))
                : NavMesh.AllAreas;
        }

        // 반경 30 이내 랜덤 샘플링
        Vector3 dir = Random.insideUnitSphere * 50f + transform.position;
        if (NavMesh.SamplePosition(dir, out NavMeshHit hit, 50f, mask))
        {
            try
            {
                petController.agent.SetDestination(hit.position);
                var anim = petController.GetComponent<PetAnimationController>();
                // 목적지 설정 후 걷기/뛰기 애니메이션 적용
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
