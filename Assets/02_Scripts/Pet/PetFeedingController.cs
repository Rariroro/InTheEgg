// PetFeedingController.cs - 레이어 기반으로 수정된 버전
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

    // ★★★ 수정: 레이어 마스크 변수 추가 ★★★
    private int foodItemLayer;
    private int feedingAreaLayer;


    public void Init(PetController controller)
    {
        petController = controller;
        // ★★★ 레이어 마스크 초기화 ★★★
        foodItemLayer = LayerMask.GetMask("FoodItem"); // "FoodItem"이라는 이름의 레이어를 사용한다고 가정
        feedingAreaLayer = LayerMask.GetMask("FeedingArea");
    }
    

   

  public void UpdateFeeding()
    {
        if (petController.isInteracting || petController.isGathering || isEating || petController.isHolding ||
            (petController.GetComponent<PetSleepingController>() != null && petController.GetComponent<PetSleepingController>().IsSleepingOrSeeking()))
        {
            return;
        }

        petController.hunger = Mathf.Clamp(petController.hunger + Time.deltaTime * hungerIncreaseRate, 0, 100);

        if (petController.hunger > 70f && Time.time - lastDetectionTime > detectionInterval)
        {
            lastDetectionTime = Time.time;

            if (targetFood == null && targetFeedingArea == null)
            {
                // ★★★ 수정: 레이어 기반 탐색 메서드 호출 ★★★
                DetectNearbyFeedingSources();
            }
        }

        HandleMovementToTarget();
    }

    /// <summary>
    /// 캐시된 목록에서 자신의 식성에 맞는 가장 가까운 '음식 아이템'을 찾습니다.
    /// </summary>
    /// <summary>
    /// ★★★ 새로운 통합 탐지 메서드 ★★★
    /// 레이어 마스크를 사용하여 주변의 음식 아이템과 먹이 장소를 한 번에 탐색합니다.
    /// </summary>
    private void DetectNearbyFeedingSources()
    {
        // 1순위: 소모성 음식 아이템 탐색 (FoodItem 레이어)
        Collider[] foodColliders = Physics.OverlapSphere(transform.position, detectionRadius, foodItemLayer);
        GameObject nearestFood = FindClosestMatchingFood(foodColliders);

        if (nearestFood != null)
        {
            ResetPetStateForSeeking();
            targetFood = nearestFood;
            petController.agent.SetDestination(targetFood.transform.position);
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
            petController.agent.SetDestination(targetFeedingArea.transform.position);
            petController.ResumeMovement();
        }
    }

    /// <summary>
    /// 콜라이더 배열에서 펫의 식성에 맞는 가장 가까운 오브젝트를 찾는 헬퍼 함수
    /// </summary>
    private GameObject FindClosestMatchingFood(Collider[] colliders)
    {
        GameObject nearestSource = null;
        float nearestDistSqr = float.MaxValue;
        Vector3 myPos = transform.position;

        foreach (var col in colliders)
        {
            PetAIProperties.DietaryFlags foodType = PetAIProperties.DietaryFlags.None;
            
            // FoodItem 또는 FeedingArea 컴포넌트에서 음식 타입 가져오기
            FoodItem foodItem = col.GetComponent<FoodItem>();
            if (foodItem != null) foodType = foodItem.foodType;
            else
            {
                FeedingArea feedingArea = col.GetComponent<FeedingArea>();
                if (feedingArea != null) foodType = feedingArea.foodType;
            }

            // 식성에 맞는지 확인
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


    private void HandleMovementToTarget()
    {
        if (isEating || petController.agent == null || !petController.agent.enabled) return;

        // 목표가 '음식 아이템'인 경우
        if (targetFood != null)
        {
            // 목표에 충분히 가까워졌으면 먹기 시작
            if (petController.agent.remainingDistance < eatingDistance && !petController.agent.pathPending)
            {
                StartCoroutine(EatFoodCoroutine());
            }
        }
        // 목표가 '먹이 장소'인 경우
        else if (targetFeedingArea != null)
        {
            // 목표에 충분히 가까워졌으면 먹기 시작
            if (petController.agent.remainingDistance < feedingAreaDistance && !petController.agent.pathPending)
            {
                StartCoroutine(EatAtAreaCoroutine());
            }
        }
    }

    /// <summary>
    /// '음식 아이템'을 먹는 코루틴. 먹고 나면 아이템을 파괴합니다.
    /// </summary>
    private IEnumerator EatFoodCoroutine()
    {
        isEating = true;
        petController.StopMovement();

        // 먹기 애니메이션 재생 (PlaySpecialAnimation 사용)
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(eatAnimationIndex));

        // 배고픔 해소
        petController.hunger = 0f;
        petController.ShowEmotion(EmotionType.Happy, 3f);

        // 먹은 음식 파괴
        if (targetFood != null)
        {
            Destroy(targetFood); // 이 때 FoodItem의 OnDestroy가 호출되어 캐시에서 자동 제거됨
        }

        // 상태 초기화 및 다시 움직임 시작
        targetFood = null;
        isEating = false;
        petController.ResumeMovement();
        petController.SetRandomDestination();
    }

    /// <summary>
    /// '먹이 장소'에서 먹는 코루틴. 환경은 파괴하지 않습니다.
    /// </summary>
    private IEnumerator EatAtAreaCoroutine()
    {
        isEating = true;
        petController.StopMovement();

        // 먹기 애니메이션 재생 (PlayAnimationWithCustomDuration으로 일정 시간동안 재생)
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(eatAnimationIndex, 5f, true, true));

        // 배고픔 해소
        petController.hunger = 0f;
        petController.ShowEmotion(EmotionType.Happy, 3f);

        // 상태 초기화 및 다시 움직임 시작
        targetFeedingArea = null;
        isEating = false;
        // PlayAnimationWithCustomDuration에서 움직임 재개를 처리하므로 여기서는 호출하지 않음
    }
    public bool IsEatingOrSeeking()
    {
        // 현재 밥을 먹고 있거나, 음식을 찾아가는 중이면 true를 반환
        return isEating || (targetFood != null) || (targetFeedingArea != null);
    }
       /// <summary>
    /// 펫이 음식을 찾아가기 직전에 현재 행동과 애니메이션을 강제로 초기화합니다.
    /// </summary>
    private void ResetPetStateForSeeking()
    {
        // 현재 진행 중인 행동(휴식, 둘러보기 등) 코루틴을 강제로 중단시킵니다.
        var moveController = petController.GetComponent<PetMovementController>();
        moveController?.ForceStopCurrentBehavior();

        // 재생 중인 연속 애니메이션(휴식 등)을 중단하고 Idle 상태로 되돌립니다.
        var animController = petController.GetComponent<PetAnimationController>();
        animController?.StopContinuousAnimation();

        // 휴식 등으로 인해 멈춰있었을 수 있으므로, NavMeshAgent의 이동을 다시 활성화합니다.
        petController.ResumeMovement();
    }
}