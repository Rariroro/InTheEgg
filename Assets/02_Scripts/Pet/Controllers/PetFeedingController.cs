// PetFeedingController.cs

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PetFeedingController : MonoBehaviour
{
    private PetController petController;
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

    public void Init(PetController controller)
    {
        petController = controller;
        foodItemLayer = LayerMask.GetMask("FoodItem");
        feedingAreaLayer = LayerMask.GetMask("FeedingArea");
    }

    /// <summary>
    /// ★★★ 수정된 메서드: 먹을 것을 찾아 이동을 시작하고, 탐색 성공 여부를 반환합니다.
    /// </summary>
    public bool TryStartFeedingSequence()
    {
        if (petController.isInteracting || petController.isGathering || isEating || petController.isHolding ||
            (petController.GetComponent<PetSleepingController>() != null && petController.GetComponent<PetSleepingController>().IsSleepingOrSeeking()) ||
            petController.isClimbingTree)
        {
            return false;
        }
        
        if (targetFood != null || targetFeedingArea != null)
        {
            return true;
        }
        
        DetectNearbyFeedingSources();

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
        petController.HandleRotation();
        HandleMovementToTarget();
    }
  
    // ... (이하 다른 메서드들은 수정할 필요 없음) ...
    // DetectNearbyFeedingSources, FindClosestMatchingFood, ValidateCurrentTargets, 
    // HandleMovementToTarget, EatFoodCoroutine 등은 그대로 유지합니다.
    private void DetectNearbyFeedingSources()
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            return;
        }

        Collider[] foodColliders = Physics.OverlapSphere(transform.position, detectionRadius, foodItemLayer);
        GameObject nearestFood = FindClosestMatchingFood(foodColliders);

        if (nearestFood != null)
        {
            ResetPetStateForSeeking();
            targetFood = nearestFood;
            petController.agent.SetDestination(targetFood.transform.position); 
            petController.ResumeMovement();
            return;
        }
        
        Collider[] areaColliders = Physics.OverlapSphere(transform.position, detectionRadius, feedingAreaLayer);
        GameObject nearestArea = FindClosestMatchingFood(areaColliders);

        if (nearestArea != null)
        {
            ResetPetStateForSeeking();
            targetFeedingArea = nearestArea;
            petController.agent.SetDestination(nearestArea.transform.position);
            petController.ResumeMovement();
        }
    }
    
    private GameObject FindClosestMatchingFood(Collider[] colliders)
    {
        GameObject nearestSource = null;
        float nearestDistSqr = float.MaxValue;
        Vector3 myPos = transform.position;

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
        if (targetFood != null)
        {
            yield return StartCoroutine(LookAtTarget(targetFood.transform));
        }

        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(eatAnimationIndex));
        petController.hunger = 0f;
        petController.ShowEmotion(EmotionType.Happy, 3f);

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
        yield return StartCoroutine(LookAtTarget(targetFeedingArea.transform));
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(eatAnimationIndex, 5f, true, true));
        petController.hunger = 0f;
        petController.ShowEmotion(EmotionType.Happy, 3f);
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