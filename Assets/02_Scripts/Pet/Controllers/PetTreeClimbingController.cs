// PetTreeClimbingController.cs - 리팩토링 및 확장 버전
using System.Collections;
using System;
using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;
public class PetTreeClimbingController : PetControllerBase
{

    // 나무 탐지 설정
    private float treeDetectionRadius = 50f;
    // private float treeClimbChance = 0.1f; // 일반적인 상황에서 나무에 오를 확률
    private bool isSearchingForTree = false;
    private float lastTreeSearchTime = 0f;
    private float treeSearchCooldown = 10f; // 일반 탐색 쿨다운은 길게

    protected override void OnInitialize()
    {
        // 추가 초기화 로직 필요시 여기에 작성
    }
    
    // 외부에서 나무 찾기 상태를 확인할 수 있는 메서드
    public bool IsSearchingForTree()
    {
        return isSearchingForTree;
    }

    /// <summary>
    /// 일반적인 상황에서 확률적으로 나무에 오를지 검사합니다.
    /// </summary>
    public void CheckForTreeClimbing()
    {

        // ★★★ 추가: 배고플 때는 나무에 오르지 않음
        if (petController.Needs.Hunger > 70f)
        {
            return;
        }
        // ★★★ 잠을 자려 하거나, 특정 상태일 때는 일반 등반 로직 실행 안 함
        if (petController.habitat != PetAIProperties.Habitat.Tree ||
            petController.State.IsClimbingTree || petController.State.IsInWater || isSearchingForTree ||
            petController.GetComponent<PetSleepingController>().IsSleepingOrSeeking())
        {
            return;
        }

        if (Time.time - lastTreeSearchTime < treeSearchCooldown) return;
        lastTreeSearchTime = Time.time;

        if (UnityEngine.Random.value < petController.treeClimbChance)
        {
            StartCoroutine(SearchAndClimbTreeRegularly());
        }
    }

    /// <summary>
    /// ★★★ 새로 추가된 공개 메서드: 나무에 올라가 특정 행동을 수행하고 내려오는 전체 과정을 실행합니다.
    /// </summary>
    /// <param name="actionOnTree">나무 위에서 수행할 행동(코루틴)</param>
    public IEnumerator ClimbAndExecuteAction(IEnumerator actionOnTree)
    {
        isSearchingForTree = true;

        Transform nearestTree = TreeManager.Instance.FindNearestAvailableTree(transform.position, treeDetectionRadius);

        if (nearestTree != null && TreeManager.Instance.OccupyTree(nearestTree, petController))
        {
            // ★ [Phase 4] PetState를 통한 상태 업데이트
            petController.State.UpdateTreeClimbingState(true, nearestTree);

            // 1. 나무 오르기
            yield return StartCoroutine(ClimbUpPhase(nearestTree));

            // 2. 주어진 행동 수행 (예: 잠자기)
            if (actionOnTree != null)
            {
                yield return StartCoroutine(actionOnTree);
            }

            // 3. 나무 내려오기
            yield return StartCoroutine(ClimbDownTree());

            // 4. 모든 상태 초기화
            // ★ [Phase 4] PetState를 통한 상태 업데이트
            petController.State.UpdateTreeClimbingState(false);
        }
        else
        {
            Debug.LogWarning($"{petController.petName}: 행동을 수행할 나무를 찾지 못했습니다.");
        }

        isSearchingForTree = false;
    }

    /// <summary>
    /// 일반적인 주기에 따라 나무에 오르고, 잠시 쉬다 내려옵니다.
    /// </summary>
    // PetTreeClimbingController.cs의 SearchAndClimbTreeRegularly 메서드 수정

    // PetTreeClimbingController.cs

    public IEnumerator SearchAndClimbTreeRegularly()
    {
        isSearchingForTree = true;
        ResetPetStateForSeeking();

        // ★★★ 행동 잠금 시작 ★★★
        petController.State.SetActionLocked(true);

        // 나무로 이동 시작할 때 Thought_ClimbingTree 감정 표시
        petController.ShowEmotion(EmotionType.Thought_ClimbingTree, 5f);

        Transform nearestTree = TreeManager.Instance.FindNearestAvailableTree(transform.position, treeDetectionRadius);

        if (nearestTree != null && TreeManager.Instance.OccupyTree(nearestTree, petController))
        {
            try
            {
                // ★ [Phase 4] PetState를 통한 상태 업데이트
                petController.State.UpdateTreeClimbingState(true, nearestTree);

                // 1. 나무 오르기
                // ClimbUpPhase 내부에서는 isActionLocked를 제어할 필요가 없어졌습니다.
                yield return StartCoroutine(ClimbUpPhase(nearestTree));

                // 나무에 올라간 후 Thought_ClimbingTree 감정 제거 (이미 표시 중인 감정이 자동으로 사라짐)
                // 새로운 감정(Happy)은 RestOnTree에서 표시됨

                // 2. 나무 위에서 휴식
                yield return StartCoroutine(RestOnTree());

                // 3. 나무 내려오기
                // ClimbDownTree 내부에서도 isActionLocked를 제어할 필요가 없어졌습니다.
                yield return StartCoroutine(ClimbDownTree());
            }
            finally
            {
                // 4. 모든 상태 최종 정리 및 ★★★ 행동 잠금 해제 ★★★
                ReleaseTreeAndResetState();
                petController.State.SetActionLocked(false);
                isSearchingForTree = false;
            }
        }
        else
        {
            // 나무를 못 찾았을 경우에도 잠금 해제
            petController.State.SetActionLocked(false);
            isSearchingForTree = false;
        }
    }
   // PetTreeClimbingController.cs의 RestOnTree 메서드

private IEnumerator RestOnTree()
{
    var animController = petController.GetComponent<PetAnimationController>();
    
    // 성격별 휴식 시간 설정
    float waitTime = GetPersonalityRestTime();
    float waited = 0f;
    
    // 행동 전환을 위한 변수
    float behaviorChangeInterval = UnityEngine.Random.Range(5f, 10f);
    float behaviorTimer = 0f;
    
    // 초기 감정 표현
    ShowTreeEmotionByPersonality();

    while (waited < waitTime)
    {
        // ★★★ 수정: 펫이 선택되면 휴식을 잠시 멈춥니다. ★★★
        if (petController.State.IsSelected)
        {
            // ClimbTreeAction의 OnUpdate가 카메라 보기와 Idle 애니메이션을 처리하므로,
            // 여기서는 선택이 해제될 때까지 그냥 기다리기만 하면 됩니다.
            yield return null;
            continue; // 아래 로직(시간 증가, 휴식 중단 조건 체크)을 건너뜁니다.
        }
        
        // 일정 시간마다 행동 변경
        behaviorTimer += Time.deltaTime;
        if (behaviorTimer >= behaviorChangeInterval)
        {
            behaviorTimer = 0f;
            behaviorChangeInterval = UnityEngine.Random.Range(5f, 10f);
            PerformTreeBehavior(animController);
        }

        // 다른 긴급한 행동이 필요한지 체크
        if (ShouldInterruptTreeRest())
        {
            // Debug.Log($"{petController.petName}이(가) 다른 할 일이 생겨 나무에서 내려옵니다.");
            yield break; // 휴식 종료하고 즉시 내려가기
        }

        yield return null;
        waited += Time.deltaTime;
    }
}

/// <summary>
/// 성격별 나무 위 휴식 시간 반환
/// </summary>
private float GetPersonalityRestTime()
{
    switch (petController.personality)
    {
        case PetAIProperties.Personality.Lazy:
            return UnityEngine.Random.Range(30f, 50f); // 더 오래 쉼
        case PetAIProperties.Personality.Playful:
            return UnityEngine.Random.Range(10f, 20f); // 빨리 지루해함
        case PetAIProperties.Personality.Brave:
            return UnityEngine.Random.Range(15f, 30f); // 주변 탐색
        case PetAIProperties.Personality.Shy:
            return UnityEngine.Random.Range(20f, 35f); // 조용히 쉼
        default:
            return UnityEngine.Random.Range(20f, 40f);
    }
}

/// <summary>
/// 나무 위에서 다양한 행동 수행
/// </summary>
private void PerformTreeBehavior(PetAnimationController animController)
{
    if (animController == null) return;
    
    int behavior = UnityEngine.Random.Range(0, 4);
    
    switch (behavior)
    {
        case 0: // 휴식
            animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
            break;
        case 1: // 주변 관찰
            animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            // 주변을 둘러보는 회전
            petController.StartCoroutine(LookAroundOnTree());
            break;
        case 2: // 놀기
            if (petController.personality == PetAIProperties.Personality.Playful)
            {
                petController.StartCoroutine(animController.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1f, false, false));
                petController.ShowEmotion(EmotionType.Happy, 2f);
            }
            else
            {
                animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
            }
            break;
        case 3: // 털 고르기 또는 기지개
            animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            break;
    }
}

/// <summary>
/// 나무 위에서 주변을 둘러보는 코루틴
/// </summary>
private IEnumerator LookAroundOnTree()
{
    float lookDuration = 3f;
    float elapsed = 0f;
    float rotationSpeed = 30f; // 초당 30도 회전
    
    while (elapsed < lookDuration)
    {
        petController.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        elapsed += Time.deltaTime;
        yield return null;
    }
}

/// <summary>
/// 나무 위 감정 표현 (모든 성격 동일하게 Happy)
/// </summary>
private void ShowTreeEmotionByPersonality()
{
    // 모든 성격의 펫이 나무에 올라가면 Happy 감정을 표시
    petController.ShowEmotion(EmotionType.Happy, 3f);
}

    /// <summary>
    /// ★★★ 새로 추가: 나무 위 휴식을 중단해야 할 조건들을 모아놓은 헬퍼 메서드
    /// </summary>
    private bool ShouldInterruptTreeRest()
    {
        return petController.Needs.Hunger > 70f ||
               petController.Needs.Sleepiness > 90f || // 나무에서 졸다가 바로 잠들지는 않도록
               petController.State.IsGathering ||
               petController.State.IsHolding;
    }

    /// <summary>
    /// ★★★ 새로 추가: 나무에서 내려온 후 상태를 확실하게 초기화하는 메서드
    /// </summary>
    private void ReleaseTreeAndResetState()
    {
        if (petController.currentTree != null)
        {
            TreeManager.Instance.ReleaseTree(petController.currentTree);
        }
        // ★ [Phase 4] PetState를 통한 상태 업데이트
        petController.State.UpdateTreeClimbingState(false);
        petController.State.SetActionLocked(false);
        // NavMeshAgent 재활성화는 ClimbDownTree에서 이미 처리됨
    }

    // ★★★ 추가: 탐색 시작 시 펫의 상태를 초기화하는 헬퍼 메서드
    private void ResetPetStateForSeeking()
    {
        // PetMovementController의 현재 행동(휴식, 점프 등) 코루틴 강제 종료
        petController.GetComponent<PetMovementController>()?.ForceStopCurrentBehavior();
        // PetAnimationController의 연속 애니메이션(휴식 등) 중지 후 Idle로
        petController.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
        // 멈춰있을 수 있으니 이동 재개
        petController.ResumeMovement();
    }
    /// <summary>
    /// ★★★ 리팩토링: 나무에 올라가는 단계만 수행하는 코루틴
    /// </summary>
    private IEnumerator ClimbUpPhase(Transform tree)
    {
        // ★★★ 행동 잠금 시작 ★★★
        petController.State.SetActionLocked(true);
        try
        {
            // ★ [Phase 4] 상태는 이미 UpdateTreeClimbingState에서 설정됨

            // 1단계: 나무 근처로 이동
            Vector3 treeBaseTarget = tree.position;
            if (NavMesh.SamplePosition(treeBaseTarget, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                treeBaseTarget = hit.position;
            }

            if (petController.agent != null && petController.agent.enabled)
            {
                petController.agent.SetDestination(treeBaseTarget);
                petController.agent.isStopped = false;
                petController.GetComponent<PetAnimationController>()?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

                while (!petController.State.IsHolding && petController.agent.enabled &&
                       (petController.agent.pathPending || petController.agent.remainingDistance > 0.5f))
                {
                    // 이동 중 애니메이션 동기화를 위해 HandleRotation 호출
                    petController.HandleRotation();
                    
                    // 애니메이션이 Idle로 바뀌면 다시 Walk로 설정
                    if (petController.animator != null && petController.animator.GetInteger("animation") == 0)
                    {
                        petController.GetComponent<PetAnimationController>()?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
                    }
                    
                    yield return null;
                }

                if (petController.State.IsHolding || !petController.agent.enabled)
                {
                    ForceCancelClimbing();
                    yield break;
                }

                petController.agent.isStopped = true;
                yield return new WaitForSeconds(0.1f);
            }

            // 2단계: NavMeshAgent 비활성화 후 나무 오르기
            if (petController.agent != null) petController.agent.enabled = false;

            petController.GetComponent<PetAnimationController>()?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Jump);

            // ... (나무 오르는 Lerp 로직) ...
            float treeHeight = CalculateTreeHeight(tree);
            float climbTargetHeight = CalculateClimbPosition(tree, treeHeight);
            Vector3 climbTarget = new Vector3(transform.position.x, transform.position.y + climbTargetHeight, transform.position.z);

            float elapsed = 0f;
            float moveTime = 3f;
            Vector3 startPos = transform.position;

            while (elapsed < moveTime)
            {
                if (petController.State.IsHolding)
                {
                    ForceCancelClimbing();
                    yield break;
                }
                transform.position = Vector3.Lerp(startPos, climbTarget, elapsed / moveTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = climbTarget;
        }
        finally
        {
            // ★★★ 나무 오르기 이동이 끝나면 잠금 해제 ★★★
            // (올라가서 쉬거나 자는 행동은 잠금 상태가 아님)
            petController.State.SetActionLocked(false);
        }
    }

    /// <summary>
    /// ★★★ 리팩토링: 나무에서 내려오는 단계만 수행하는 코루틴
    /// </summary>
    public IEnumerator ClimbDownTree()
    {
        if (petController.currentTree == null)
        {
            ForceCancelClimbing();
            yield break;
        }

        // ★★★ 행동 잠금 시작 ★★★
        petController.State.SetActionLocked(true);
        try
        {
            petController.GetComponent<PetAnimationController>()?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            Vector3 groundPos = petController.currentTree.position;
            if (NavMesh.SamplePosition(groundPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                groundPos = hit.position;
            }

            // ... (나무 내려오는 Lerp 로직) ...
            float moveTime = 2f;
            float elapsed = 0f;
            Vector3 startPos = transform.position;

            while (elapsed < moveTime)
            {
                if (petController.State.IsHolding)
                {
                    ForceCancelClimbing();
                    yield break;
                }
                transform.position = Vector3.Lerp(startPos, groundPos, elapsed / moveTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = groundPos;

            TreeManager.Instance.ReleaseTree(petController.currentTree);

            if (petController.agent != null && !petController.agent.enabled)
            {
                petController.agent.enabled = true;
                petController.agent.Warp(transform.position);
            }

            // NavMeshAgent가 완전히 준비될 때까지 대기
            float waitTime = 0f;
            while (waitTime < 2f && petController.agent != null)
            {
                if (petController.agent.enabled && petController.agent.isOnNavMesh)
                {
                    // 한 프레임 더 대기하여 안정화
                    yield return null;
                    break;
                }
                waitTime += Time.deltaTime;
                yield return null;
            }

            petController.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
        }
        finally
        {
            // ★★★ 내려오기 행동이 완전히 끝나면 잠금 해제 ★★★
            petController.State.SetActionLocked(false);
        }
    }

    public void ForceCancelClimbing()
    {
        StopAllCoroutines();

        isSearchingForTree = false;
        if (petController != null)
        {
            if (petController.currentTree != null)
            {
                TreeManager.Instance.ReleaseTree(petController.currentTree);
            }

            // ★ [Phase 4] PetState를 통한 상태 업데이트
            petController.State.UpdateTreeClimbingState(false);

        }

        if (petController != null && petController.agent != null && !petController.agent.enabled)
        {
            petController.agent.enabled = true;
            petController.agent.Warp(transform.position);
        }

        GetComponent<PetAnimationController>()?.ForceStopAllAnimations();

        if (petController != null) petController.State.SetActionLocked(false);

    }

    private float CalculateTreeHeight(Transform tree)
    {
        float height = 5f;

        Collider treeCollider = tree.GetComponent<Collider>();
        if (treeCollider != null)
        {
            height = treeCollider.bounds.size.y;
        }
        else
        {
            MeshRenderer meshRenderer = tree.GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                height = meshRenderer.bounds.size.y;
            }
        }
        return height;
    }

    private float CalculateClimbPosition(Transform tree, float treeHeight)
    {
        float climbRatio = UnityEngine.Random.Range(2.3f, 2.5f);
        return treeHeight * climbRatio;
    }
}