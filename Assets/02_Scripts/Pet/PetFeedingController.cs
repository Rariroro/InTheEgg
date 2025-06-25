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



/// <summary>
    /// ★★★ 추가: EatAction의 OnEnter에서 호출될 메서드입니다.
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

        // 음식을 탐색합니다.
        DetectNearbyFeedingSources();

        // 탐색에 성공했다면 true를 반환합니다.
        if (targetFood != null || targetFeedingArea != null)
        {
            ResetPetStateForSeeking();
            Vector3 destination = (targetFood != null) ? targetFood.transform.position : targetFeedingArea.transform.position;
            petController.agent.SetDestination(destination);
            petController.ResumeMovement();
            return true;
        }

        return false;
    }

    /// <summary>
    /// ★★★ 추가: EatAction의 OnUpdate에서 호출될 메서드입니다.
    /// 목표에 도착했는지 확인하고 먹기 코루틴을 시작합니다.
    /// </summary>
    public void UpdateMovementToFood()
    {
        if (isEating || petController.agent == null || !petController.agent.enabled) return;
        
        // HandleMovementToTarget의 로직을 그대로 사용합니다.
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
        // (내용 변경 없음)
        if (isEating || petController.agent == null || !petController.agent.enabled) return;

        if (targetFood != null)
        {
            if (petController.agent.remainingDistance < eatingDistance && !petController.agent.pathPending)
            {
                StartCoroutine(EatFoodCoroutine());
            }
        }
        else if (targetFeedingArea != null)
        {
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
        // ★★★ 추가된 부분 시작 ★★★
        if (targetFood != null)
        {
            yield return StartCoroutine(LookAtTarget(targetFood.transform));
        }
        // ★★★ 추가된 부분 끝 ★★★

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
        // ★★★ 추가된 부분 시작 ★★★
        // 먹기 전에 목표물을 부드럽게 바라보도록 회전시킵니다.
        yield return StartCoroutine(LookAtTarget(targetFeedingArea.transform));
        // ★★★ 추가된 부분 끝 ★★★
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
    /// <summary>
    /// ★★★ 새로 추가된 코루틴 ★★★
    /// 지정된 목표물을 부드럽게 바라보게 하는 코루틴입니다.
    /// </summary>
    /// <param name="target">바라볼 대상의 Transform</param>
    private IEnumerator LookAtTarget(Transform target)
    {
        if (target == null) yield break;

        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0; // 펫이 위아래로 기울어지는 것을 방지

        // 바라볼 방향이 거의 0벡터에 가까우면 (너무 가까이 붙어있으면) 실행하지 않음
        if (direction.sqrMagnitude < 0.01f) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        float timer = 0f;
        float duration = 0.5f; // 0.5초 동안 회전
        Quaternion startRotation = transform.rotation;

        while (timer < duration)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation; // 정확하게 목표 방향으로 설정
    }
     public bool IsEatingOrSeeking()
    {
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