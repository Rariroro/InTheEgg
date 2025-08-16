// PetFeedingController.cs

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PetAIProperties = PetTraits;
public class PetFeedingController : PetControllerBase
{
    private GameObject targetFood;
    private GameObject targetFeedingArea;

    // 탐색 및 상태 관련 변수
    private float detectionRadius = 100f;
    private float eatingDistance = 4f;
    private float feedingAreaDistance = 2f;
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
            Debug.Log($"[Feeding] {petController.petName}: NavMeshAgent 재활성화 시도");
        }
        
        // NavMesh에 없으면 재배치 시도
        if (!petController.agent.isOnNavMesh)
        {
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(petController.transform.position, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                petController.agent.Warp(hit.position);
                Debug.Log($"[Feeding] {petController.petName}: NavMesh로 재배치 성공");
            }
            else
            {
                Debug.LogWarning($"[Feeding] {petController.petName}: NavMesh 재배치 실패 - 먹이 탐색 불가");
                return;
            }
        }

        // 탐색 반경 결정
        float radius = searchRadius > 0 ? searchRadius : detectionRadius;
        
        Collider[] foodColliders = Physics.OverlapSphere(transform.position, radius, foodItemLayer);
        Debug.Log($"[Feeding] {petController.petName}: {foodColliders.Length}개의 음식 아이템 발견 (반경 {radius}m)");
        GameObject nearestFood = FindClosestMatchingFood(foodColliders);

        if (nearestFood != null)
        {
            ResetPetStateForSeeking();
            targetFood = nearestFood;
            petController.agent.SetDestination(targetFood.transform.position); 
            petController.ResumeMovement();
            Debug.Log($"[Feeding] {petController.petName}: 음식 {nearestFood.name}을(를) 향해 이동 시작");
            return;
        }
        
        Collider[] areaColliders = Physics.OverlapSphere(transform.position, radius, feedingAreaLayer);
        Debug.Log($"[Feeding] {petController.petName}: {areaColliders.Length}개의 피딩 에어리어 발견 (반경 {radius}m)");
        GameObject nearestArea = FindClosestMatchingFood(areaColliders);

        if (nearestArea != null)
        {
            ResetPetStateForSeeking();
            targetFeedingArea = nearestArea;
            petController.agent.SetDestination(nearestArea.transform.position);
            petController.ResumeMovement();
            Debug.Log($"[Feeding] {petController.petName}: 피딩 에어리어 {nearestArea.name}을(를) 향해 이동 시작");
        }
        else
        {
            Debug.Log($"[Feeding] {petController.petName}: 먹을 수 있는 음식을 찾지 못함");
        }
    }
    
    private GameObject FindClosestMatchingFood(Collider[] colliders)
    {
        GameObject nearestSource = null;
        float nearestDistSqr = float.MaxValue;
        Vector3 myPos = transform.position;

        // 디버그: 펫의 현재 식성 출력
        if (colliders.Length > 0)
        {
            Debug.Log($"[Feeding] {petController.petName}의 식성: {petController.diet} ({PetTraits.GetDietaryDescription(petController.diet)})");
        }

        foreach (var col in colliders)
        {
            PetAIProperties.DietaryFlags foodType = PetAIProperties.DietaryFlags.None;
            FoodItem foodItem = col.GetComponent<FoodItem>();
            if (foodItem != null) foodType = foodItem.foodType;
            else
            {
                FeedingArea feedingArea = col.GetComponent<FeedingArea>();
                if (feedingArea != null) foodType = feedingArea.foodType;
            }

            // 디버그: 발견된 음식 타입 출력
            Debug.Log($"[Feeding] 발견된 음식: {col.name}, 타입: {foodType} ({PetTraits.GetDietaryDescription(foodType)})");

            if ((petController.diet & foodType) != 0)
            {
                float distSqr = (col.transform.position - myPos).sqrMagnitude;
                if (distSqr < nearestDistSqr)
                {
                    nearestSource = col.gameObject;
                    nearestDistSqr = distSqr;
                    Debug.Log($"[Feeding] {petController.petName}이(가) 먹을 수 있는 음식 발견: {col.name}");
                }
            }
            else
            {
                Debug.Log($"[Feeding] {petController.petName}은(는) {col.name}을(를) 먹을 수 없음 (식성 불일치)");
            }
        }
        return nearestSource;
    }

    public void ValidateCurrentTargets()
    {
        if (targetFood != null)
        {
            float distance = Vector3.Distance(petController.transform.position, targetFood.transform.position);
            if (distance > detectionRadius * 0.5f)
            {
                targetFood = null;
            }
        }
    
        if (targetFeedingArea != null)
        {
            float distance = Vector3.Distance(petController.transform.position, targetFeedingArea.transform.position);
            if (distance > detectionRadius * 0.5f)
            {
                targetFeedingArea = null;
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
            else if (actualDistance > detectionRadius)
            {
                targetFood = null;
                DetectNearbyFeedingSources();
            }
        }
        else if (targetFeedingArea != null)
        {
            float actualDistance = Vector3.Distance(petController.transform.position, targetFeedingArea.transform.position);
        
            if (actualDistance < feedingAreaDistance && !petController.agent.pathPending)
            {
                StartCoroutine(EatAtAreaCoroutine());
            }
            else if (actualDistance > detectionRadius)
            {
                targetFeedingArea = null;
                DetectNearbyFeedingSources();
            }
        }
    }

    private IEnumerator EatFoodCoroutine()
    {
        isEating = true;
        petController.StopMovement();
        
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
        petController.ResumeMovement();
        petController.SetRandomDestination();
    }

    private IEnumerator EatAtAreaCoroutine()
    {
        isEating = true;
        petController.StopMovement();
        
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
        isEating = false;
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
    Collider[] foodColliders = Physics.OverlapSphere(transform.position, radius, foodItemLayer);
    if (FindClosestMatchingFood(foodColliders) != null)
    {
        return true; 
    }
    
    Collider[] areaColliders = Physics.OverlapSphere(transform.position, radius, feedingAreaLayer);
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
    
    if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
    {
        if (petController.agent.isStopped)
        {
            petController.ResumeMovement();
        }
    }
}
}