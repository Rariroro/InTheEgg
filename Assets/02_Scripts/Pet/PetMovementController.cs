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
    // 클래스 상단에 필드 추가
    private bool isInWater = false;
    private float waterSpeedMultiplier = 0.3f;
    private float currentDepth = 0f;
    private float depthTransitionSpeed = 2f;


    private float treeDetectionRadius = 50f;
    private float treeClimbChance = 0.9f; // 30% 확률로 나무에 올라감
    private bool isSearchingForTree = false;
    // ✅ 새로 추가할 변수들
    [SerializeField] private LayerMask treeLayerMask = -1; // 나무 레이어 마스크
    private float lastTreeSearchTime = 0f;                // 마지막 탐색 시간
    private float treeSearchCooldown = 2f;                // 탐색 쿨다운 (초)

    private Coroutine currentBehaviorCoroutine = null;

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
       // ★ 들고 있는 상태면 즉시 리턴 (나무 체크보다 우선)
    if (petController.isHolding)
    {
        return;
    }
    
    // 물 영역 체크
    CheckWaterArea();
    
    // ★ 나무 체크도 isHolding 상태가 아닐 때만
    if (!petController.isHolding)
    {
        CheckTreeArea();
    }
    
    // 나무에 올라가 있으면 다른 움직임 처리 스킵
    if (petController.isClimbingTree)
    {
        return;
    }
    
        // 모으기 모드면 즉시 리턴
        if (petController.isGathering)
        {
            // 카메라 회전 로직만 처리
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

        // NavMeshAgent 체크를 캐싱
        if (!IsAgentReady()) return;

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


        }
    }
    // 헬퍼 메서드 추가
    private bool IsAgentReady()
    {
        return petController.agent != null &&
               petController.agent.enabled &&
               petController.agent.isOnNavMesh;
    }
    private void CheckTreeArea()
    {
        // Tree habitat이 아니면 즉시 리턴
        if (petController.habitat != PetAIProperties.Habitat.Tree) return;

        // 이미 상태가 정해진 경우 즉시 리턴
        if (petController.isClimbingTree || isInWater || isSearchingForTree) return;

        // 쿨다운 체크를 먼저 (불필요한 Random.value 호출 방지)
        if (Time.time - lastTreeSearchTime < treeSearchCooldown) return;

        // 확률 체크
        if (Random.value < 0.1f)
        {
            lastTreeSearchTime = Time.time;
            StartCoroutine(SearchAndClimbTree());
        }
    }
    private Coroutine climbTreeCoroutine = null;

    // 나무 찾기 및 올라가기 코루틴
    private IEnumerator SearchAndClimbTree()
    {
        isSearchingForTree = true;

        Transform nearestTree = TreeManager.Instance.FindNearestTree(transform.position);

        // 가장 가까운 나무를 찾았고, 일정 확률을 통과하면 올라가기를 시도합니다.
        if (nearestTree != null && Random.value < treeClimbChance)
        {
            // 찾은 나무가 너무 멀리 있으면 포기 (선택 사항)
            if ((nearestTree.position - transform.position).sqrMagnitude > treeDetectionRadius * treeDetectionRadius)
            {
                // 너무 멀어서 포기
            }
            else
            {
                climbTreeCoroutine = StartCoroutine(ClimbTree(nearestTree));
                yield return climbTreeCoroutine;
                climbTreeCoroutine = null;
            }
        }

        isSearchingForTree = false;
    }
    // 나무 올라가기 중단 메서드 추가
   public void StopTreeClimbing()
{
    // 모든 진행 중인 코루틴 중단
    StopAllCoroutines();
    
    if (climbTreeCoroutine != null)
    {
        StopCoroutine(climbTreeCoroutine);
        climbTreeCoroutine = null;
    }

    isSearchingForTree = false;
    petController.isClimbingTree = false;

    // 애니메이션 완전 초기화
    var anim = GetComponent<PetAnimationController>();
    if (anim != null)
    {
        anim.StopContinuousAnimation();
        anim.ForceStopAllAnimations(); // 강제 중단 메서드 호출
    }
    
    // 애니메이터 상태 초기화
    if (petController.animator != null)
    {
        petController.animator.SetInteger("animation", 0);
        petController.animator.speed = 1.0f;
    }
}
    // 나무 올라가기 코루틴
   // PetMovementController.cs

private IEnumerator ClimbTree(Transform tree)
{
    // ★ 시작할 때 isHolding 체크
    if (petController.isHolding)
    {
        yield break;
    }

    petController.isClimbingTree = true;
    petController.currentTree = tree;

    // NavMeshAgent 비활성화
    if (petController.agent != null)
    {
        petController.agent.enabled = false;
    }

    // ==================== 수정된 로직 시작 ====================

    // 목표 지점: 나무 바로 아래의 NavMesh 위의 지점
    Vector3 treeBaseTarget = tree.position;
    NavMeshHit hit;
    if (NavMesh.SamplePosition(treeBaseTarget, out hit, 5f, NavMesh.AllAreas))
    {
        treeBaseTarget = hit.position;
    }

    // 이동 속도: 펫의 기본 걷기 속도를 사용
    float moveSpeed = petController.baseSpeed; 

    // 걷기 애니메이션 설정
    var anim = petController.GetComponent<PetAnimationController>();
    anim?.SetContinuousAnimation(1); // 걷기(Walk) 애니메이션

    // 목표 지점에 도착할 때까지 프레임마다 이동
    while (Vector3.Distance(transform.position, treeBaseTarget) > 0.1f)
    {
        // 도중에 유저가 펫을 잡았는지 체크
        if (petController.isHolding)
        {
            // 잡혔으면 즉시 중단
            petController.isClimbingTree = false;
            if (petController.agent != null)
            {
                petController.agent.enabled = true;
            }
            yield break;
        }
        
        // Vector3.MoveTowards를 사용하여 일정한 속도로 이동
        transform.position = Vector3.MoveTowards(transform.position, treeBaseTarget, moveSpeed * Time.deltaTime);

        // 나무를 바라보도록 부드럽게 회전
        Vector3 lookDir = (treeBaseTarget - transform.position).normalized;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookDir), Time.deltaTime * 5f);
        }

        yield return null;
    }

    // 목표 지점에 정확히 위치시키기
    transform.position = treeBaseTarget;
    
    // ==================== 수정된 로직 끝 ====================

    // 올라가기 애니메이션
    anim?.SetContinuousAnimation(3);

    // 나무의 실제 높이 계산
    float treeHeight = CalculateTreeHeight(tree);
    float climbTargetHeight = CalculateClimbPosition(tree, treeHeight);

    // 계산된 높이로 올라가기
    Vector3 climbTarget = transform.position + Vector3.up * climbTargetHeight;
    float elapsed = 0f;
    float moveTime = 3f; // 올라가는 시간은 3초로 고정
    Vector3 startPos = transform.position;

    while (elapsed < moveTime)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / moveTime;
        transform.position = Vector3.Lerp(startPos, climbTarget, t);
        yield return null;
    }

    // 나무 위에서 쉬기
    anim?.SetContinuousAnimation(5);

    // 5-10초 동안 나무 위에서 쉬기
    yield return new WaitForSeconds(Random.Range(5f, 10f));

    // 내려오기
    yield return StartCoroutine(ClimbDownTree());
}

    // 나무의 실제 높이를 계산하는 메서드
    private float CalculateTreeHeight(Transform tree)
    {
        float height = 5f; // 기본 높이

        // 1. Collider bounds로 높이 계산
        Collider treeCollider = tree.GetComponent<Collider>();
        if (treeCollider != null)
        {
            height = treeCollider.bounds.size.y;
        }
        else
        {
            // 2. Collider가 없으면 MeshRenderer bounds 사용
            MeshRenderer meshRenderer = tree.GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                height = meshRenderer.bounds.size.y;
            }
            else
            {
                // 3. 둘 다 없으면 자식 오브젝트들의 bounds 계산
                Bounds combinedBounds = CalculateCombinedBounds(tree);
                if (combinedBounds.size != Vector3.zero)
                {
                    height = combinedBounds.size.y;
                }
            }
        }

        return height;
    }

    // 올라갈 위치 계산 (나무 높이의 60-80% 지점)
    private float CalculateClimbPosition(Transform tree, float treeHeight)
    {
        // 나무 높이에 따라 올라갈 비율 조정
        float climbRatio;


        climbRatio = Random.Range(2.3f, 2.5f); // 70-85%

        // 나무의 로컬 Y 위치도 고려
        float baseY = 0f;
        if (tree.GetComponent<Collider>() != null)
        {
            baseY = tree.GetComponent<Collider>().bounds.min.y - tree.position.y;
        }

        return baseY + (treeHeight * climbRatio);
    }

    // 자식 오브젝트들을 포함한 전체 bounds 계산
    private Bounds CalculateCombinedBounds(Transform parent)
    {
        Renderer[] renderers = parent.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return new Bounds(parent.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }


    // 나무에서 내려오기
    // 나무에서 내려오기
    private IEnumerator ClimbDownTree()
    {
        // ★ null 체크 추가
        if (petController.currentTree == null)
        {
            Debug.LogWarning($"{petController.petName}: currentTree가 null입니다. 내려오기 중단.");

            // NavMeshAgent 재활성화
            if (petController.agent != null)
            {
                petController.agent.enabled = true;
                petController.agent.Warp(transform.position);
            }

            petController.isClimbingTree = false;
            yield break;
        }

        var anim = petController.GetComponent<PetAnimationController>();

        // 내려가기 애니메이션
        anim?.SetContinuousAnimation(1);

        // 바닥으로 내려오기
        Vector3 groundPos = petController.currentTree.position; // ★ 이미 null 체크 완료
        NavMeshHit hit;
        if (NavMesh.SamplePosition(groundPos, out hit, 5f, NavMesh.AllAreas))
        {
            groundPos = hit.position;
        }

        float moveTime = 2f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;

        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveTime;
            transform.position = Vector3.Lerp(startPos, groundPos, t);
            yield return null;
        }

        // NavMeshAgent 재활성화
        if (petController.agent != null)
        {
            petController.agent.enabled = true;
            petController.agent.Warp(transform.position);
        }

        petController.isClimbingTree = false;
        petController.currentTree = null;

        // 일반 애니메이션으로 복귀
        anim?.StopContinuousAnimation();
    }
    // 새 메서드 추가
    private void CheckWaterArea()
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        // 현재 NavMesh 영역 확인
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 1f, NavMesh.AllAreas))
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (waterArea != -1)
            {
                // 현재 위치가 물 영역인지 확인
                bool currentlyInWater = (1 << waterArea) == hit.mask;

                if (currentlyInWater != isInWater)
                {
                    isInWater = currentlyInWater;
                    petController.isInWater = isInWater;

                    if (isInWater)
                    {
                        Debug.Log($"{petController.petName}: 물에 들어감");
                        OnEnterWater();
                    }
                    else
                    {
                        Debug.Log($"{petController.petName}: 물에서 나옴");
                        OnExitWater();
                    }
                }
            }
        }

        // 부드러운 깊이 전환
        float targetDepth = isInWater ? -petController.waterSinkDepth : 0f; // (변경 후)
        currentDepth = Mathf.Lerp(currentDepth, targetDepth, Time.deltaTime * depthTransitionSpeed);
        petController.waterDepthOffset = currentDepth;
    }

    private void OnEnterWater()
    {
        // 물 서식지 펫은 덜 느려짐
        float speedMult = (petController.habitat == PetAIProperties.Habitat.Water)
                          ? 0.7f : waterSpeedMultiplier;

        // 속도 감소 - SafeSetAgentMovement 사용하지 않고 직접 설정
        if (petController.agent != null && !petController.isGathering)
        {
            petController.agent.speed = petController.baseSpeed * speedMult;
            petController.agent.acceleration = petController.baseAcceleration * speedMult;
        }

        // 애니메이션 속도도 감소
        var anim = petController.GetComponent<PetAnimationController>();
        if (anim != null && petController.animator != null)
        {
            petController.animator.speed = speedMult;
        }
    }

    private void OnExitWater()
    {
        // 속도 복구
        if (petController.agent != null && !petController.isGathering)
        {
            petController.agent.speed = petController.baseSpeed * pb.speedMultiplier;
            petController.agent.acceleration = petController.baseAcceleration;
        }

        // 애니메이션 속도 복구
        if (petController.animator != null)
        {
            petController.animator.speed = 1f;
        }
    }


    // 행동 전환 시 호출: 가중치 기반 랜덤으로 다음 행동 선정
    private void DecideNextBehavior()
    {
        // 선택된 상태에서는 새로운 행동을 결정하지 않음
        if (petController.isSelected)
        {
            behaviorTimer = 0f;
            return;
        }
        // [수정] 새로운 행동을 결정하기 전, 더 중요한 행동(식사, 수면)을 하는지 확인
        var feedingController = petController.GetComponent<PetFeedingController>();
        var sleepingController = petController.GetComponent<PetSleepingController>();
        // 나무에 올라가 있으면 행동 변경하지 않음
        if (petController.isClimbingTree)
        {
            behaviorTimer = 0f;
            return;
        }
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

        if ((sum += pb.idleWeight) >= r) { SetBehavior(BehaviorState.Idle); return; }
        if ((sum += pb.walkWeight) >= r) { SetBehavior(BehaviorState.Walking); return; }
        if ((sum += pb.runWeight) >= r) { SetBehavior(BehaviorState.Running); return; }
        if ((sum += pb.jumpWeight) >= r) { SetBehavior(BehaviorState.Jumping); return; }
        if ((sum += pb.restWeight) >= r) { SetBehavior(BehaviorState.Resting); return; }
        if ((sum += pb.lookWeight) >= r) { SetBehavior(BehaviorState.Looking); return; }
        { SetBehavior(BehaviorState.Playing); }
    }

    // 행동 상태 전환: NavMeshAgent 속성·애니메이션 적용 :contentReference[oaicite:8]{index=8}
    // PetMovementController.cs

    private void SetBehavior(BehaviorState state)
    {  // ★ 이전 코루틴 중단
        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }
        // 모으기 상태거나 NavMeshAgent가 준비되지 않았으면 행동을 변경하지 않습니다.
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

        // ★★★ 중요: 새로운 행동을 시작하기 전에, 이전의 연속 애니메이션(예: 휴식) 상태를 해제합니다.
        // 이렇게 해야 '휴식' 상태에서 '걷기'로 전환될 때, 애니메이션이 '휴식'에 고정되지 않고
        // UpdateAnimation 로직에 의해 '걷기' 애니메이션으로 자연스럽게 변경됩니다.
        if (anim != null)
        {
            anim.StopContinuousAnimation();
        }

        switch (state)
        {
            case BehaviorState.Idle:
                // Idle 상태를 명시적으로 유지하고 싶을 때 사용합니다.
                anim?.SetContinuousAnimation(0);
                break;

            case BehaviorState.Walking:
                SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier, false);
                SetRandomDestination();
                // anim?.SetContinuousAnimation(1); // << 이 줄을 삭제! UpdateAnimation이 속도를 감지해 처리하도록 합니다.
                break;

            case BehaviorState.Running:
                SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier * 1.5f, false);
                SetRandomDestination();
                // anim?.SetContinuousAnimation(2); // << 이 줄을 삭제! UpdateAnimation이 속도를 감지해 처리하도록 합니다.
                break;

            case BehaviorState.Jumping:
                StartCoroutine(PerformJump());
                break;

            case BehaviorState.Resting:
                // '휴식'은 움직임이 없는 행동 애니메이션이므로, 이 호출은 유지합니다.
                anim?.SetContinuousAnimation(5);
                break;

            case BehaviorState.Looking:
                StartCoroutine(LookAround());
                break;

            case BehaviorState.Playing:
                StartCoroutine(PerformPlay());
                break;
        }

        // 행동 설정 후 물에 있으면 속도를 재조정합니다.
        if (isInWater && petController.agent != null)
        {
            float speedMult = (petController.habitat == PetAIProperties.Habitat.Water)
                              ? 0.7f : waterSpeedMultiplier;
            petController.agent.speed *= speedMult;
        }
    }

    // 새 메서드 추가
    public void ForceStopCurrentBehavior()
    {
        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }

        // 현재 행동 상태를 Idle로 리셋
        currentBehaviorState = BehaviorState.Idle;
        behaviorTimer = 0f;
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
