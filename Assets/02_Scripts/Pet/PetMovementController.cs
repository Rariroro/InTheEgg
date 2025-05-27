using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫의 기본 움직임(Idle, Walk, Run, Jump, Rest, LookAround, Play)을
/// 성향(personality)별 가중치에 따라 결정하고 NavMesh를 이용해 이동합니다.
/// PetAnimationController를 이용해 애니메이션을 전환합니다.
/// 물 속성 펫의 물/육지 이동 비율을 조정할 수 있습니다.
/// </summary>
public class PetMovementController : MonoBehaviour
{
    // 외부에서 주입된 PetController 참조 (NavMeshAgent, baseSpeed, personality, habitat 등 포함)
    private PetController petController;

    // 다음 행동 전환까지의 타이머 및 간격
    private float behaviorTimer = 0f;
    private float nextBehaviorChange = 0f;
    private BehaviorState currentBehaviorState = BehaviorState.Walking;

    /// <summary>
    /// 펫이 수행할 수 있는 행동 상태 목록
    /// </summary>
    private enum BehaviorState
    {
        Idle,       // 대기
        Walking,    // 걷기
        Running,    // 뛰기
        Jumping,    // 점프
        Resting,    // 쉬기
        Looking,    // 주변 둘러보기
        Playing     // 놀기
    }

    /// <summary>
    /// 성향별 행동 가중치, 지속시간, 속도 배율 저장 클래스
    /// </summary>
    private class PersonalityBehavior
    {
        public float idleWeight, walkWeight, runWeight, jumpWeight;
        public float restWeight, lookWeight, playWeight;
        public float behaviorDuration;   // 행동 지속 시간 기본값
        public float speedMultiplier;    // 기본 속도 배율
    }

    // 현재 펫의 성향에 따른 행동 설정
    private PersonalityBehavior pb;

    /// <summary>
    /// 물 속성 펫이 물 vs 육지 목적지를 고를 확률 (0~1)
    /// 예: 0.8f이면 80% 확률로 물 영역 목적지 설정
    /// </summary>
    [Range(0f, 1f)] public float waterDestinationChance = 0.8f;

    /// <summary>
    /// 초기화 메서드: PetController 주입, NavMesh 물 영역 비용 설정, 성향별 가중치 초기화, 딜레이 코루틴 실행
    /// </summary>
    public void Init(PetController controller)
    {
        // PetController 레퍼런스 저장
        petController = controller;

        // NavMeshAgent가 준비되어 있으면 물 영역 비용 설정
        if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (waterArea != -1)
            {
                // 물 속성 펫은 비용 낮게, 비물 속성 펫은 비용 높게 설정
                petController.agent.SetAreaCost(
                    waterArea,
                    petController.habitat == PetAIProperties.Habitat.Water ? 0.5f : 10f
                );
            }
        }

        // 펫의 성향별 행동 가중치 초기화
        InitializePersonalityBehavior();

        // NavMeshAgent 준비를 기다리는 코루틴 시작
        StartCoroutine(DelayedStart());
    }

    /// <summary>
    /// 펫의 personality 값에 따라 행동 가중치, 지속시간, 속도 배율 설정
    /// </summary>
    private void InitializePersonalityBehavior()
    {
        pb = new PersonalityBehavior();
        switch (petController.personality)
        {
            case PetAIProperties.Personality.Lazy:
                pb.idleWeight   = 3;    pb.walkWeight  = 2;    pb.runWeight   = 0.1f;
                pb.jumpWeight   = 0.1f; pb.restWeight  = 5;    pb.lookWeight  = 2;
                pb.playWeight   = 0.1f; pb.behaviorDuration = 5; pb.speedMultiplier = 0.7f;
                break;
            case PetAIProperties.Personality.Shy:
                pb.idleWeight   = 2;    pb.walkWeight  = 3;    pb.runWeight   = 0.5f;
                pb.jumpWeight   = 0.5f; pb.restWeight  = 2;    pb.lookWeight  = 4;
                pb.playWeight   = 0.5f; pb.behaviorDuration = 3; pb.speedMultiplier = 0.8f;
                break;
            case PetAIProperties.Personality.Brave:
                pb.idleWeight   = 1;    pb.walkWeight  = 2;    pb.runWeight   = 4;
                pb.jumpWeight   = 3;    pb.restWeight  = 1;    pb.lookWeight  = 1;
                pb.playWeight   = 2;    pb.behaviorDuration = 4; pb.speedMultiplier = 1.2f;
                break;
            default: // Playful
                pb.idleWeight   = 0.5f; pb.walkWeight  = 2;    pb.runWeight   = 3;
                pb.jumpWeight   = 4;    pb.restWeight  = 0.5f; pb.lookWeight  = 1;
                pb.playWeight   = 5;    pb.behaviorDuration = 2; pb.speedMultiplier = 1.1f;
                break;
        }
    }

    /// <summary>
    /// NavMeshAgent 준비 대기: 최대 5초 동안 isOnNavMesh 체크
    /// 준비 완료 후 물 영역 비용 설정 및 초기 행동 결정
    /// </summary>
    private IEnumerator DelayedStart()
    {
        float maxWaitTime = 5f;
        float waitTime = 0f;

        // 일정 간격으로 NavMeshAgent 준비 상태 확인
        while (waitTime < maxWaitTime)
        {
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
                break;

            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        // 준비되지 않았다면 경고 후 코루틴 종료
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[PetMovementController] {petController.petName}: NavMeshAgent가 준비되지 않았습니다.");
            yield break;
        }

        // 다시 물 영역 비용 설정 시도 (예외 처리 포함)
        try
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (waterArea != -1)
            {
                petController.agent.SetAreaCost(
                    waterArea,
                    petController.habitat == PetAIProperties.Habitat.Water ? 0.5f : 10f
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PetMovementController] {petController.petName}: SetAreaCost 실패 - {e.Message}");
        }

        // 초기 행동 결정
        DecideNextBehavior();
    }

    /// <summary>
    /// 매 프레임 호출: 행동 타이머 갱신, 행동 전환, 이동 처리, 모델 위치 동기화
    /// 모이기 상태일 때는 이동 로직 무시
    /// </summary>
    public void UpdateMovement()
    {
        // 모이기 중이거나 모임 완료 상태면 이동 로직 건너뜀
        if (petController.isGathering || petController.isGathered) return;
        if (petController.agent == null || !petController.agent.isOnNavMesh || !petController.agent.enabled) return;

        // 행동 타이머 갱신
        behaviorTimer += Time.deltaTime;
        if (behaviorTimer >= nextBehaviorChange)
            DecideNextBehavior();

        // NavMeshAgent가 멈춰있지 않으면 걷기/뛰기로 이동 처리
        if (!petController.agent.isStopped)
        {
            if (currentBehaviorState == BehaviorState.Walking || currentBehaviorState == BehaviorState.Running)
                HandleMovement();
        }

        // 실제 모델 트랜스폼 위치 동기화
        if (petController.petModelTransform != null)
            petController.petModelTransform.position = transform.position;
    }

    /// <summary>
    /// 다음 행동 상태를 확률 기반으로 결정하여 SetBehavior 호출
    /// </summary>
    private void DecideNextBehavior()
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        behaviorTimer = 0;
        float total = pb.idleWeight + pb.walkWeight + pb.runWeight + pb.jumpWeight
                    + pb.restWeight + pb.lookWeight + pb.playWeight;
        float r = Random.Range(0, total);
        float sum = 0;

        // 누적 가중치 비교로 상태 결정
        if ((sum += pb.idleWeight) >= r)      { SetBehavior(BehaviorState.Idle);    return; }
        if ((sum += pb.walkWeight) >= r)      { SetBehavior(BehaviorState.Walking); return; }
        if ((sum += pb.runWeight) >= r)       { SetBehavior(BehaviorState.Running); return; }
        if ((sum += pb.jumpWeight) >= r)      { SetBehavior(BehaviorState.Jumping); return; }
        if ((sum += pb.restWeight) >= r)      { SetBehavior(BehaviorState.Resting); return; }
        if ((sum += pb.lookWeight) >= r)      { SetBehavior(BehaviorState.Looking); return; }
        SetBehavior(BehaviorState.Playing);
    }

    /// <summary>
    /// 지정된 행동 상태로 전환: NavMeshAgent 속성 설정, 애니메이션 전환, 목적지 설정 등 처리
    /// </summary>
   private void SetBehavior(BehaviorState state)
{
    // NavMeshAgent 상태 확인
    if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
    {
        Debug.LogWarning($"[PetMovementController] {petController.petName}: NavMeshAgent가 준비되지 않아 행동 설정을 건너뜁니다.");
        return;
    }

    // ★ 모이기 중일 때는 행동 변경하지 않음
    if (petController.isGathering)
    {
        return;
    }
    
    currentBehaviorState=state;
    nextBehaviorChange=pb.behaviorDuration+Random.Range(-1f,1f);
    var anim=petController.GetComponent<PetAnimationController>();
    
    // 안전하게 isStopped 설정
    try
    {
        petController.agent.isStopped=true;
    }
    catch (System.Exception e)
    {
        Debug.LogWarning($"[PetMovementController] {petController.petName}: isStopped 설정 실패 - {e.Message}");
        return;
    }
    
    switch(state)
    {
        case BehaviorState.Idle: 
            if (anim != null) anim.SetContinuousAnimation(0);
            break;
        case BehaviorState.Walking:
            SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier, false);
            SetRandomDestination();
            if (anim != null) anim.SetContinuousAnimation(1);
            break;
        case BehaviorState.Running:
            SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier * 1.5f, false);
            SetRandomDestination();
            if (anim != null) anim.SetContinuousAnimation(2);
            break;
        case BehaviorState.Jumping:
            StartCoroutine(PerformJump());
            break;
        case BehaviorState.Resting:
            if (anim != null) anim.SetContinuousAnimation(5);
            break;
        case BehaviorState.Looking:
            StartCoroutine(LookAround());
            break;
        case BehaviorState.Playing:
            StartCoroutine(PerformPlay());
            break;
    }
}

    /// <summary>
    /// NavMeshAgent 속도 및 isStopped 설정을 안전하게 수행
    /// </summary>
   // 안전한 NavMeshAgent 설정을 위한 헬퍼 메서드 수정
private void SafeSetAgentMovement(float speed, bool isStopped)
{
    if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
    {
        return;
    }

    // ★ 모이기 중일 때는 속도 변경하지 않음
    if (petController.isGathering)
    {
        return;
    }
    
    try
    {
        petController.agent.speed = speed;
        petController.agent.isStopped = isStopped;
    }
    catch (System.Exception e)
    {
        Debug.LogWarning($"[PetMovementController] {petController.petName}: Agent 설정 실패 - {e.Message}");
    }
}

    /// <summary>
    /// NavMeshAgent가 목적지에 도착했거나 경로 없음 상태일 때 무작위 목적지 재설정
    /// </summary>
    private void HandleMovement()
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        // 경로가 준비되지 않았거나 남은 거리가 1 이하이면 목적지 재설정
        if (!petController.agent.pathPending && petController.agent.remainingDistance < 1f)
            SetRandomDestination();
    }

    /// <summary>
    /// 점프 애니메이션 재생 코루틴
    /// </summary>
    private IEnumerator PerformJump()
    {
        yield return new WaitForSeconds(0.2f);
        var anim = petController.GetComponent<PetAnimationController>();
        if (anim != null)
            yield return StartCoroutine(anim.PlayAnimationWithCustomDuration(3, 1f, true, false));
    }

    /// <summary>
    /// 주변 둘러보기 애니메이션 및 회전 코루틴
    /// </summary>
    private IEnumerator LookAround()
    {
        var anim = petController.GetComponent<PetAnimationController>();
        if (anim != null) anim.SetContinuousAnimation(1); // walk during look around

        for (int i = 0; i < 2; i++)
        {
            if (petController.petModelTransform == null) break;
            float t = 0;
            var start = petController.petModelTransform.rotation;
            var end = start * Quaternion.Euler(0, 90, 0); // 90도 회전
            while (t < 1) { t += Time.deltaTime; petController.petModelTransform.rotation = Quaternion.Slerp(start, end, t); yield return null; }
            yield return new WaitForSeconds(0.5f);

            t = 0;
            start = petController.petModelTransform.rotation;
            end = start * Quaternion.Euler(0, -180, 0); // 반대쪽으로 180도 회전
            while (t < 1) { t += Time.deltaTime; petController.petModelTransform.rotation = Quaternion.Slerp(start, end, t); yield return null; }
            yield return new WaitForSeconds(0.5f);
        }

        if (anim != null) anim.StopContinuousAnimation();
    }

    /// <summary>
    /// 노는 동작 애니메이션 및 이동 코루틴
    /// </summary>
    private IEnumerator PerformPlay()
    {
        var anim = petController.GetComponent<PetAnimationController>();
        int type = Random.Range(0, 3);

        if (type == 0)
        {
            // 제자리에서 뛰기
            SafeSetAgentMovement(petController.baseSpeed, true);
            if (anim != null) anim.SetContinuousAnimation(2); // run
            yield return new WaitForSeconds(3f);
            if (anim != null) anim.StopContinuousAnimation();
        }
        else if (type == 1)
        {
            // 반복 점프 애니메이션
            if (anim != null)
                for (int i = 0; i < 3; i++)
                    yield return StartCoroutine(anim.PlayAnimationWithCustomDuration(3, 0.8f, true, false));
        }
        else
        {
            // 뛰면서 이동 후 멈춤
            SafeSetAgentMovement(petController.baseSpeed * 2f, false);
            if (anim != null) anim.SetContinuousAnimation(2);
            SetRandomDestination();
            yield return new WaitForSeconds(2f);
            SafeSetAgentMovement(petController.baseSpeed, true);
            if (anim != null) anim.StopContinuousAnimation();
            yield return new WaitForSeconds(0.5f);
        }
        // 놀기 후 기본 속도 및 이동 재설정
        SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier, false);
    }

    /// <summary>
    /// 랜덤 위치 샘플링하여 목적지 설정
    /// 물 속성 펫은 waterDestinationChance 확률로 물 영역, 그렇지 않으면 육지/전체 NavMesh
    /// </summary>
    public void SetRandomDestination()
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        int waterArea = NavMesh.GetAreaFromName("Water");
        int mask;

        // 물 속성 펫용 경로 마스크 결정
        if (petController.habitat == PetAIProperties.Habitat.Water && waterArea != -1)
        {
            mask = (Random.value < waterDestinationChance)
                ? (1 << waterArea)         // 오직 물 영역
                : NavMesh.AllAreas;        // 전체 NavMesh
        }
        else
        {
            // 육지 펫: 물 영역 제외 (유효할 때)
            mask = (waterArea != -1)
                ? (NavMesh.AllAreas & ~(1 << waterArea))
                : NavMesh.AllAreas;
        }

        // 반경 30 내 랜덤 위치 탐색
        Vector3 dir = Random.insideUnitSphere * 30f + transform.position;
        if (NavMesh.SamplePosition(dir, out var hit, 30f, mask))
        {
            try
            {
                petController.agent.SetDestination(hit.position);
                var anim = petController.GetComponent<PetAnimationController>();
                // 이동 시작 시 애니메이션 설정
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
