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


    public void Init(PetController controller)
    {
        petController = controller;
        foodItemLayer = LayerMask.GetMask("FoodItem");
        feedingAreaLayer = LayerMask.GetMask("FeedingArea");
    }

    /// <summary>
    /// ★★★ 수정된 메서드: EatAction의 OnEnter에서 호출됩니다.
    /// 먹을 것을 찾아 이동을 시작하고, 탐색 성공 여부를 반환합니다.
    /// </summary>
    public bool TryStartFeedingSequence()
    {
        // 다른 중요 행동 중이면 시작하지 않음
        if (petController.isInteracting || petController.isGathering || isEating || petController.isHolding ||
            (petController.GetComponent<PetSleepingController>() != null && petController.GetComponent<PetSleepingController>().IsSleepingOrSeeking()) ||
            petController.isClimbingTree)
        {
            return false;
        }

        // 이미 목표가 있다면 성공으로 간주
        if (targetFood != null || targetFeedingArea != null)
        {
            return true;
        }

        // 음식을 탐색하고, 성공하면 이 메서드가 목적지까지 설정해줍니다.
        DetectNearbyFeedingSources();

        // DetectNearbyFeedingSources가 성공적으로 타겟을 설정했는지 여부만 반환합니다.
        // 여기서 SetDestination을 중복 호출하던 로직을 삭제했습니다.
        return (targetFood != null || targetFeedingArea != null);
    }

    /// <summary>
    /// UpdateMovementToFood는 그대로 유지합니다.
    /// </summary>
    public void UpdateMovementToFood()
    {
        if (isEating || petController.agent == null || !petController.agent.enabled) return;
        
        petController.HandleRotation();
        HandleMovementToTarget();
    }
  

    /// <summary>
    /// ★★★ 수정된 메서드: NavMeshAgent 준비 상태를 먼저 확인합니다.
    /// 레이어 마스크를 사용하여 주변의 음식 아이템과 먹이 장소를 한 번에 탐색합니다.
    /// </summary>
    private void DetectNearbyFeedingSources()
    {
        // ★★★ 핵심 수정: NavMeshAgent가 준비되지 않았다면, 탐색을 시도하지 않고 즉시 종료합니다. ★★★
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            // 에이전트가 준비되지 않아 먹이 탐색을 시작할 수 없습니다. 다음 AI 업데이트 시 다시 시도됩니다.
            return;
        }

        // 1순위: 소모성 음식 아이템 탐색 (FoodItem 레이어)
        Collider[] foodColliders = Physics.OverlapSphere(transform.position, detectionRadius, foodItemLayer);
        GameObject nearestFood = FindClosestMatchingFood(foodColliders);

        if (nearestFood != null)
        {
            ResetPetStateForSeeking();
            targetFood = nearestFood;
            petController.agent.SetDestination(targetFood.transform.position); // 이 호출은 이제 안전합니다.
            petController.ResumeMovement();
            return; // 음식 아이템을 찾았으면 즉시 종료
        }

        // 2순위: 음식 아이템을 못 찾았으면 환경 먹이 장소 탐색 (FeedingArea 레이어)
        Collider[] areaColliders = Physics.OverlapSphere(transform.position, detectionRadius, feedingAreaLayer);
        GameObject nearestArea = FindClosestMatchingFood(areaColliders);

        if (nearestArea != null)
        {
            ResetPetStateForSeeking();
            targetFeedingArea = nearestArea;
            petController.agent.SetDestination(nearestArea.transform.position); // 이 호출도 이제 안전합니다.
            petController.ResumeMovement();
        }
    }
    
    // (이하 다른 메서드들은 변경 없습니다)
    // FindClosestMatchingFood, ValidateCurrentTargets, HandleMovementToTarget,
    // EatFoodCoroutine, EatAtAreaCoroutine, LookAtTarget, IsEatingOrSeeking,
    // ResetPetStateForSeeking, CancelFeeding 메서드는 그대로 유지합니다.
    
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
/// <summary>
/// ★★★ 새로 추가된 메서드 ★★★
/// 지정된 반경 내에 펫이 먹을 수 있는 음식이 있는지 간단히 확인만 합니다.
/// GetPriority() 와 같이 자주 호출되는 곳에서 사용하기에 안전합니다.
/// </summary>
/// <param name="radius">탐색할 반경</param>
/// <returns>먹을 수 있는 음식이 있으면 true, 없으면 false</returns>
public bool IsFoodInRange(float radius)
{
    // 1순위: 소모성 음식 아이템 탐색
    Collider[] foodColliders = Physics.OverlapSphere(transform.position, radius, foodItemLayer);
    if (FindClosestMatchingFood(foodColliders) != null)
    {
        return true; // 먹을 수 있는 아이템을 찾으면 즉시 true 반환
    }

    // 2순위: 환경 먹이 장소 탐색
    Collider[] areaColliders = Physics.OverlapSphere(transform.position, radius, feedingAreaLayer);
    if (FindClosestMatchingFood(areaColliders) != null)
    {
        return true; // 먹을 수 있는 장소를 찾으면 즉시 true 반환
    }

    // 탐색 반경 내에 먹을 것이 없음
    return false;
}
   public void CancelFeeding()
{
    StopAllCoroutines();
    isEating = false;
    targetFood = null;
    targetFeedingArea = null;

    // ★★★ 수정: NavMeshAgent가 활성화되어 있고, NavMesh 위에 있을 때만 isStopped를 체크하도록 조건을 추가합니다. ★★★
    if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
    {
        // 에이전트가 멈춰있었다면 다시 움직이게 합니다.
        if (petController.agent.isStopped)
        {
            petController.ResumeMovement();
        }
    }
}
}