// PetFeedingController.cs

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using PetAIProperties = PetTraits;
public class PetFeedingController : PetControllerBase
{
    private GameObject targetFood;
    private GameObject targetFeedingArea;

    // 탐색 및 상태 관련 변수
    private float detectionRadius = 100f;
    private float eatingDistance = 4f;
    private float feedingAreaDistance = 3.5f;  // 2m → 3.5m로 증가 (병목 상황에서 먹기 가능하도록)
    private bool isEating = false;

    // 타이머 및 간격 변수
    private float lastDetectionTime = 0f;
    private float detectionInterval = 1.5f;
    private float hungerIncreaseRate = 0.2f;

    // 애니메이션 인덱스
    private int eatAnimationIndex = 4;

    // 레이어 마스크 변수
    private int foodItemLayer;
    private int feedingAreaLayer;

    // ★★★ 추가: 움직이는 목표 추적을 위한 변수들 ★★★
    private float _chaseUpdateTimer = 0f;
    private Vector3 _lastTargetPosition;
    private const float CHASE_UPDATE_INTERVAL = 0.25f; // 0.25초마다 목표 위치 갱신
    // ★★★ 여기까지 추가 ★★★

    // FeedingArea 병목 해결을 위한 변수들
    private float feedingAreaWaitTimer = 0f;
    private const float FEEDING_AREA_WAIT_TIMEOUT = 5f; // 5초 대기 후 타겟 포기
    private const float MOVE_AWAY_DISTANCE = 20f; // 먹은 후 이동 거리

    // 블랙리스트: 바로 직전에 실패한 FeedingArea만 제외
    private GameObject lastFailedFeedingArea = null;

    // 마지막 FeedingArea 위치 (먹은 후 이동에 사용)
    private Vector3 lastFeedingAreaPosition;

    // NavMeshAgent 회피 우선순위 설정
    private const int SEEKING_FOOD_PRIORITY = 15;  // 먹으러 가는 중
    private const int EATING_PRIORITY = 5;          // 먹고 있는 중 / 나가는 중
    private int originalAvoidancePriority;
    private bool hasModifiedPriority = false;

    protected override void OnInitialize()
    {
        foodItemLayer = LayerMask.GetMask("FoodItem");
        feedingAreaLayer = LayerMask.GetMask("FeedingArea");
    }

    /// <summary>
    /// ★★★ 수정된 메서드: 먹을 것을 찾아 이동을 시작하고, 탐색 성공 여부를 반환합니다.
    /// </summary>
    public bool TryStartFeedingSequence(float customRadius = -1f)
    {
        if (petController.State.IsInteracting || petController.State.IsGathering || isEating || petController.State.IsHolding ||
            petController.State.IsSelected || // 터치된 상태에서는 먹이 찾기 중단
            (petController.GetComponent<PetSleepingController>() != null && petController.GetComponent<PetSleepingController>().IsSleepingOrSeeking()) ||
            petController.State.IsClimbingTree)
        {
            return false;
        }
        
        if (targetFood != null || targetFeedingArea != null)
        {
            return true;
        }
        
        // 커스텀 반경이 제공되면 사용, 아니면 기본값 사용
        float searchRadius = customRadius > 0 ? customRadius : detectionRadius;
        DetectNearbyFeedingSources(searchRadius);

        // ★★★ 추가: 추적 관련 변수 초기화 ★★★
        if (targetFood != null)
        {
            _chaseUpdateTimer = 0f;
            _lastTargetPosition = targetFood.transform.position;
        }
        // ★★★ 여기까지 추가 ★★★

        return (targetFood != null || targetFeedingArea != null);
    }

    /// <summary>
    /// ★★★ 핵심 수정: 움직이는 음식을 실시간으로 추적하도록 로직 변경 ★★★
    /// </summary>
    public void UpdateMovementToFood()
    {
        // 타임아웃 체크는 isEating 여부와 관계없이 실행 (접근 못하고 대기 중인 경우 처리)
        CheckFeedingAreaTimeout();

        if (isEating || petController.agent == null || !petController.agent.enabled) return;

        // 목표가 '음식 아이템'일 경우에만 추적 로직을 실행합니다.
        if (targetFood != null)
        {
            _chaseUpdateTimer += Time.deltaTime;
            
            // 지정된 간격마다 목표의 위치를 갱신합니다.
            if (_chaseUpdateTimer >= CHASE_UPDATE_INTERVAL)
            {
                _chaseUpdateTimer = 0f;
                
                // 목표물이 실제로 움직였을 때만 경로를 재계산하여 성능을 아낍니다.
                if (Vector3.Distance(targetFood.transform.position, _lastTargetPosition) > 0.1f)
                {
                    if (petController.agent.isOnNavMesh)
                    {
                        petController.agent.SetDestination(targetFood.transform.position);
                        _lastTargetPosition = targetFood.transform.position;
                    }
                }
            }
        }
        
        // 회전 및 도착 처리 로직은 그대로 유지합니다.
        if (petController.movementController != null)
        {
            petController.movementController.HandleRotation();
        }
        HandleMovementToTarget();
    }
  
    // ... (이하 다른 메서드들은 수정할 필요 없음) ...
    // DetectNearbyFeedingSources, FindClosestMatchingFood, ValidateCurrentTargets, 
    // HandleMovementToTarget, EatFoodCoroutine 등은 그대로 유지합니다.
    private void DetectNearbyFeedingSources(float searchRadius = -1f)
    {
        // NavMeshAgent 문제 해결 시도
        if (petController.agent == null)
        {
            Debug.LogWarning($"[Feeding] {petController.petName}: NavMeshAgent가 없어서 먹이 탐색 불가");
            return;
        }
        
        // agent가 비활성화되어 있으면 활성화 시도
        if (!petController.agent.enabled)
        {
            petController.agent.enabled = true;
        // Debug.Log($"[Feeding] {petController.petName}: NavMeshAgent 재활성화 시도");
        }
        
        // NavMesh에 없으면 재배치 시도
        if (!petController.agent.isOnNavMesh)
        {
            Profiler.BeginSample("PetFeeding.NavMesh.SamplePosition");
            UnityEngine.AI.NavMeshHit hit;
            bool found = UnityEngine.AI.NavMesh.SamplePosition(petController.transform.position, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas);
            Profiler.EndSample();
            if (found)
            {
                petController.agent.Warp(hit.position);
        // Debug.Log($"[Feeding] {petController.petName}: NavMesh로 재배치 성공");
            }
            else
            {
                Debug.LogWarning($"[Feeding] {petController.petName}: NavMesh 재배치 실패 - 먹이 탐색 불가");
                return;
            }
        }

        // 탐색 반경 결정
        float radius = searchRadius > 0 ? searchRadius : detectionRadius;

        Profiler.BeginSample("PetFeeding.OverlapSphere.FoodItem");
        Collider[] foodColliders = Physics.OverlapSphere(transform.position, radius, foodItemLayer);
        Profiler.EndSample();
        // Debug.Log($"[Feeding] {petController.petName}: {foodColliders.Length}개의 음식 아이템 발견 (반경 {radius}m)");

        Profiler.BeginSample("PetFeeding.FindClosestMatchingFood");
        GameObject nearestFood = FindClosestMatchingFood(foodColliders);
        Profiler.EndSample();

        if (nearestFood != null)
        {
            ResetPetStateForSeeking();
            targetFood = nearestFood;
            petController.agent.SetDestination(targetFood.transform.position);
            petController.ResumeMovement();
            SetSeekingPriority();
        // Debug.Log($"[Feeding] {petController.petName}: 음식 {nearestFood.name}을(를) 향해 이동 시작");
            return;
        }

        Profiler.BeginSample("PetFeeding.OverlapSphere.FeedingArea");
        Collider[] areaColliders = Physics.OverlapSphere(transform.position, radius, feedingAreaLayer);
        Profiler.EndSample();
        // Debug.Log($"[Feeding] {petController.petName}: {areaColliders.Length}개의 피딩 에어리어 발견 (반경 {radius}m)");
        GameObject nearestArea = FindClosestMatchingFood(areaColliders);

        if (nearestArea != null)
        {
            ResetPetStateForSeeking();
            targetFeedingArea = nearestArea;
            petController.agent.SetDestination(nearestArea.transform.position);
            petController.ResumeMovement();
            SetSeekingPriority();
        // Debug.Log($"[Feeding] {petController.petName}: 피딩 에어리어 {nearestArea.name}을(를) 향해 이동 시작");
        }
        else
        {
        // Debug.Log($"[Feeding] {petController.petName}: 먹을 수 있는 음식을 찾지 못함");
        }
    }
    
    private GameObject FindClosestMatchingFood(Collider[] colliders)
    {
        GameObject nearestSource = null;
        float nearestDistSqr = float.MaxValue;
        Vector3 myPos = transform.position;

        foreach (var col in colliders)
        {
            // 블랙리스트 체크: 바로 직전에 실패한 FeedingArea는 제외
            if (col.gameObject == lastFailedFeedingArea)
                continue;

            PetAIProperties.DietaryFlags foodType = PetAIProperties.DietaryFlags.None;
            FoodItem foodItem = col.GetComponent<FoodItem>();
            if (foodItem != null) foodType = foodItem.foodType;
            else
            {
                FeedingArea feedingArea = col.GetComponent<FeedingArea>();
                if (feedingArea != null) foodType = feedingArea.foodType;
            }

            if ((petController.diet & foodType) != 0)
            {
                float distSqr = (col.transform.position - myPos).sqrMagnitude;
                if (distSqr < nearestDistSqr)
                {
                    nearestSource = col.gameObject;
                    nearestDistSqr = distSqr;
                }
            }
        }

        // 새로운 타겟을 찾았으면 블랙리스트 해제 (다음번에 다시 선택 가능)
        if (nearestSource != null && nearestSource != lastFailedFeedingArea)
        {
            lastFailedFeedingArea = null;
        }

        return nearestSource;
    }

    public void ValidateCurrentTargets()
    {
        // 너무 멀리 있는 타겟만 검증 (300m 이상)
        if (targetFood != null)
        {
            float distance = Vector3.Distance(petController.transform.position, targetFood.transform.position);
            if (distance > 300f)
            {
                targetFood = null;
                // Debug.Log($"[Feeding] {petController.petName}: 타겟 음식이 너무 멀어서 검증 실패 (거리: {distance:F0}m)");
            }
        }
    
        if (targetFeedingArea != null)
        {
            float distance = Vector3.Distance(petController.transform.position, targetFeedingArea.transform.position);
            if (distance > 300f)
            {
                targetFeedingArea = null;
                // Debug.Log($"[Feeding] {petController.petName}: 타겟 피딩 에어리어가 너무 멀어서 검증 실패 (거리: {distance:F0}m)");
            }
        }
    }

    private void HandleMovementToTarget()
    {
        if (isEating || petController.agent == null || !petController.agent.enabled) return;

        if (targetFood != null)
        {
            float actualDistance = Vector3.Distance(petController.transform.position, targetFood.transform.position);

            if (actualDistance < eatingDistance && !petController.agent.pathPending)
            {
                StartCoroutine(EatFoodCoroutine());
            }
            // 거리 체크 제거 - 탐색 시 이미 범위 내 음식만 찾으므로 불필요
            // 너무 멀리 있는 경우만 체크 (300m 이상)
            else if (actualDistance > 300f)
            {
                targetFood = null;
                // Debug.Log($"[Feeding] {petController.petName}: 목표 음식이 너무 멀어서 취소 (거리: {actualDistance:F0}m)");
            }
        }
        else if (targetFeedingArea != null)
        {
            float actualDistance = Vector3.Distance(petController.transform.position, targetFeedingArea.transform.position);

            if (actualDistance < feedingAreaDistance && !petController.agent.pathPending)
            {
                feedingAreaWaitTimer = 0f; // 타이머 리셋
                StartCoroutine(EatAtAreaCoroutine());
            }
            // 너무 멀리 있는 경우 (300m 이상)
            else if (actualDistance > 300f)
            {
                targetFeedingArea = null;
                feedingAreaWaitTimer = 0f;
                // Debug.Log($"[Feeding] {petController.petName}: 목표 피딩 에어리어가 너무 멀어서 취소 (거리: {actualDistance:F0}m)");
            }
            // 타임아웃 로직은 CheckFeedingAreaTimeout()에서 처리
        }
    }

    /// <summary>
    /// FeedingArea 접근 타임아웃 체크 (다른 펫에게 막혀서 접근 못하는 경우)
    /// </summary>
    private void CheckFeedingAreaTimeout()
    {
        if (targetFeedingArea == null || isEating) return;
        if (petController.agent == null || !petController.agent.enabled) return;

        float actualDistance = Vector3.Distance(petController.transform.position, targetFeedingArea.transform.position);
        bool pathPending = petController.agent.pathPending;
        float remainingDist = petController.agent.remainingDistance;
        float velocity = petController.agent.velocity.magnitude;

        // NavMeshAgent가 멈춘 상태인지 체크 (경로 문제 포함)
        bool hasPathProblem = float.IsInfinity(remainingDist) || float.IsNaN(remainingDist);
        bool agentStopped = !pathPending && (remainingDist < 0.5f || velocity < 0.1f || hasPathProblem);

        // 타임아웃 조건: Agent가 멈춘 상태에서만 체크
        // - 거리 상관없이 Agent가 멈추면 막힌 것으로 판단
        // - 걸어오는 중인 펫은 타임아웃 대상 아님
        bool shouldCheckTimeout = agentStopped;

        if (shouldCheckTimeout)
        {
            feedingAreaWaitTimer += Time.deltaTime;
            if (feedingAreaWaitTimer >= FEEDING_AREA_WAIT_TIMEOUT)
            {
                // 타임아웃: 현재 FeedingArea를 블랙리스트에 추가하고 포기
                lastFailedFeedingArea = targetFeedingArea;
                targetFeedingArea = null;
                feedingAreaWaitTimer = 0f;
            }
        }
        else
        {
            // 먹기 거리 이내이고 아직 움직이는 중이면 타이머 리셋
            feedingAreaWaitTimer = 0f;
        }
    }

    private IEnumerator EatFoodCoroutine()
    {
        isEating = true;
        petController.StopMovement();
        SetEatingPriority();  // 먹는 중 우선순위 설정

        // 터치/홀드 상태가 되면 즉시 중단
        if (petController.State.IsHolding || petController.State.IsSelected)
        {
            CancelFeeding();
            yield break;
        }
        
        if (targetFood != null)
        {
            yield return StartCoroutine(LookAtTarget(targetFood.transform));
        }

        // 애니메이션 재생 중에도 터치/홀드 체크
        if (petController.State.IsHolding || petController.State.IsSelected)
        {
            CancelFeeding();
            yield break;
        }

        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(PetAnimationController.PetAnimationType.Eat));
        
        // ★ [Phase 2] PetNeeds를 통해 배고픔 감소
        if (petController.Needs != null)
        {
            petController.Needs.ReduceHunger(100f); // 배고픔 완전 해소
        }
        else
        {
            petController.Needs.SetHunger(0f); // 폴백
        }
        
        // 음식 섭취시 친밀도 증가
        float affectionIncrease = UnityEngine.Random.Range(petController.GetDroppedFoodAffectionMin(), petController.GetDroppedFoodAffectionMax());
        
        // ★ [Phase 2] PetNeeds를 통해 친밀도 증가
        if (petController.Needs != null)
        {
            petController.Needs.IncreaseAffection(affectionIncrease);
        }
        else
        {
            petController.Needs.SetAffection(Mathf.Clamp(petController.Needs.Affection + affectionIncrease, 0f, 100f)); // 폴백
            // Debug.Log($"[Affection] {petController.petName}이(가) 음식을 먹고 친밀도가 {affectionIncrease:F1} 증가: {petController.Needs.Affection:F1}");
        }
        
        // 친밀도에 따른 감정 표현
        if (petController.Needs.Affection >= petController.GetHighAffectionThreshold())
        {
            petController.ShowEmotion(EmotionType.Love, 3f);
        }
        else
        {
            petController.ShowEmotion(EmotionType.Happy, 3f);
        }

        if (targetFood != null)
        {
            Destroy(targetFood);
        }

        targetFood = null;
        isEating = false;
        RestoreOriginalPriority();  // 우선순위 복구
        petController.ResumeMovement();
        petController.SetRandomDestination();
    }

    private IEnumerator EatAtAreaCoroutine()
    {
        isEating = true;
        petController.StopMovement();
        SetEatingPriority();  // 먹는 중 우선순위 설정

        // FeedingArea 위치 저장 (나중에 이동할 때 사용)
        if (targetFeedingArea != null)
            lastFeedingAreaPosition = targetFeedingArea.transform.position;

        // 터치/홀드 상태가 되면 즉시 중단
        if (petController.State.IsHolding || petController.State.IsSelected)
        {
            CancelFeeding();
            yield break;
        }

        // 꿀 지역인지 확인하고 벌 공격 트리거
        if (targetFeedingArea != null)
        {
            FeedingArea feedingArea = targetFeedingArea.GetComponent<FeedingArea>();
            if (feedingArea != null && (feedingArea.foodType & PetAIProperties.DietaryFlags.Honey) != 0)
            {
                // 꿀을 먹기 시작했음을 BeeHazardZone에 직접 알림
                BeeHazardZone beeZone = targetFeedingArea.GetComponent<BeeHazardZone>();
                if (beeZone != null)
                {
                    beeZone.OnPetStartedEating(petController);
                }
            }
        }
        
        yield return StartCoroutine(LookAtTarget(targetFeedingArea.transform));
        
        // 애니메이션 재생 중에도 터치/홀드 체크
        if (petController.State.IsHolding || petController.State.IsSelected)
        {
            CancelFeeding();
            yield break;
        }
        
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 5f, true, true));
        
        // ★ [Phase 2] PetNeeds를 통해 배고픔 감소
        if (petController.Needs != null)
        {
            petController.Needs.ReduceHunger(100f); // 배고픔 완전 해소
        }
        else
        {
            petController.Needs.SetHunger(0f); // 폴백
        }
        
        // 환경 음식 섭취시 친밀도 증가
        float affectionIncrease = UnityEngine.Random.Range(petController.GetEnvironmentFoodAffectionMin(), petController.GetEnvironmentFoodAffectionMax());
        
        // ★ [Phase 2] PetNeeds를 통해 친밀도 증가
        if (petController.Needs != null)
        {
            petController.Needs.IncreaseAffection(affectionIncrease);
        }
        else
        {
            petController.Needs.SetAffection(Mathf.Clamp(petController.Needs.Affection + affectionIncrease, 0f, 100f)); // 폴백
            // Debug.Log($"[Affection] {petController.petName}이(가) 환경 음식을 먹고 친밀도가 {affectionIncrease:F1} 증가: {petController.Needs.Affection:F1}");
        }
        
        // 친밀도에 따른 감정 표현
        if (petController.Needs.Affection >= petController.GetHighAffectionThreshold())
        {
            petController.ShowEmotion(EmotionType.Love, 3f);
        }
        else
        {
            petController.ShowEmotion(EmotionType.Happy, 3f);
        }
        targetFeedingArea = null;
        // isEating = false; // 이동 완료 후에 설정 (MoveAwayAndFinish에서)

        // FeedingArea에서 벗어나도록 이동 (병목 방지)
        petController.ResumeMovement();
        yield return StartCoroutine(MoveAwayAndFinish());
    }

    /// <summary>
    /// FeedingArea에서 벗어난 후 isEating을 false로 설정합니다.
    /// 이동 중에 다른 Activity가 시작되지 않도록 보장합니다.
    /// </summary>
    private IEnumerator MoveAwayAndFinish()
    {
        MoveAwayFromFeedingArea();

        // 이동 완료까지 대기 (최대 3초)
        float timeout = 3f;
        while (timeout > 0f)
        {
            if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
                break;

            if (petController.agent.remainingDistance < 1f && !petController.agent.pathPending)
                break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        isEating = false; // 이제 Activity 전환 허용
        RestoreOriginalPriority();  // 우선순위 복구
    }

    /// <summary>
    /// FeedingArea에서 벗어나도록 랜덤 방향으로 이동합니다.
    /// 병목 현상을 방지하기 위해 먹은 후 호출됩니다.
    /// </summary>
    private void MoveAwayFromFeedingArea()
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        // 랜덤 방향 계산 (펫들이 다양한 방향으로 흩어지도록)
        Vector3 randomDirection = Random.insideUnitSphere;
        randomDirection.y = 0;
        randomDirection.Normalize();

        // 지정 거리만큼 떨어진 위치로 이동
        Vector3 targetPos = petController.transform.position + randomDirection * MOVE_AWAY_DISTANCE;
        if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
        {
            petController.agent.SetDestination(hit.position);
        }
        else
        {
            petController.SetRandomDestination();
        }
    }

    private IEnumerator LookAtTarget(Transform target)
    {
        if (target == null) yield break;
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.01f) yield break;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        float timer = 0f;
        float duration = 0.5f;
        Quaternion startRotation = transform.rotation;

        while (timer < duration)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRotation;
    }

    public bool IsEatingOrSeeking()
    {
        return isEating || (targetFood != null) || (targetFeedingArea != null);
    }

    private void ResetPetStateForSeeking()
    {
        var moveController = petController.GetComponent<PetMovementController>();
        moveController?.ForceStopCurrentBehavior();
        var animController = petController.GetComponent<PetAnimationController>();
        animController?.StopContinuousAnimation();
        petController.ResumeMovement();
    }

public bool IsFoodInRange(float radius)
{
    Profiler.BeginSample("PetFeeding.IsFoodInRange.FoodItem");
    Collider[] foodColliders = Physics.OverlapSphere(transform.position, radius, foodItemLayer);
    Profiler.EndSample();
    if (FindClosestMatchingFood(foodColliders) != null)
    {
        return true;
    }

    Profiler.BeginSample("PetFeeding.IsFoodInRange.FeedingArea");
    Collider[] areaColliders = Physics.OverlapSphere(transform.position, radius, feedingAreaLayer);
    Profiler.EndSample();
    if (FindClosestMatchingFood(areaColliders) != null)
    {
        return true;
    }

    return false;
}
   public void CancelFeeding()
{
    StopAllCoroutines();
    isEating = false;
    targetFood = null;
    targetFeedingArea = null;
    RestoreOriginalPriority();  // 우선순위 복구

    if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
    {
        if (petController.agent.isStopped)
        {
            petController.ResumeMovement();
        }
    }
}

    #region NavMeshAgent Priority Management

    /// <summary>
    /// 먹으러 가는 중 우선순위 설정 (priority 15)
    /// </summary>
    private void SetSeekingPriority()
    {
        if (petController.agent == null || !petController.agent.enabled) return;
        if (!hasModifiedPriority)
        {
            originalAvoidancePriority = petController.agent.avoidancePriority;
            hasModifiedPriority = true;
        }
        petController.agent.avoidancePriority = SEEKING_FOOD_PRIORITY;
    }

    /// <summary>
    /// 먹고 있는 중 / 나가는 중 우선순위 설정 (priority 5)
    /// </summary>
    private void SetEatingPriority()
    {
        if (petController.agent == null || !petController.agent.enabled) return;
        if (!hasModifiedPriority)
        {
            originalAvoidancePriority = petController.agent.avoidancePriority;
            hasModifiedPriority = true;
        }
        petController.agent.avoidancePriority = EATING_PRIORITY;
    }

    /// <summary>
    /// 원래 우선순위로 복구
    /// </summary>
    private void RestoreOriginalPriority()
    {
        if (petController.agent == null || !petController.agent.enabled) return;
        if (hasModifiedPriority)
        {
            petController.agent.avoidancePriority = originalAvoidancePriority;
            hasModifiedPriority = false;
        }
    }

    #endregion
}