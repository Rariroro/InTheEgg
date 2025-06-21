// PetFeedingController.cs - 완성된 버전
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PetFeedingController : MonoBehaviour
{
    private PetController petController;
    private GameObject targetFood;            // 목표 음식 아이템
    private GameObject targetFeedingArea;     // 목표 먹이 장소

    // 탐색 및 상태 관련 변수
    private float detectionRadius = 100f;     // 먹이 탐색 반경
    private float eatingDistance = 4f;       // 아이템을 먹기 시작할 거리
    private float feedingAreaDistance = 2f;   // 먹이 장소에 도착했다고 판단할 거리
    private bool isEating = false;

    // 타이머 및 간격 변수
    private float lastDetectionTime = 0f;
    private float detectionInterval = 1.5f;   // 1.5초마다 먹이 탐색
    private float hungerIncreaseRate = 0.2f;  // 배고픔 증가율 (기존보다 약간 높여 테스트 용이하게)

    // 애니메이션 인덱스 (프로젝트에 맞게 조절)
    private int eatAnimationIndex = 4;        // 먹기 애니메이션

    // --- 정적 캐시: 모든 펫이 공유 ---
    private static List<GameObject> allFoodItems = new List<GameObject>();
    private static List<GameObject> allFeedingAreas = new List<GameObject>();
    private static float lastCacheUpdateTime = 0f;
    private static float cacheUpdateInterval = 5f; // 5초마다 씬의 먹이 장소 목록 갱신

    // 씬 로드 시 캐시 초기화
    static PetFeedingController()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            allFoodItems.Clear();
            allFeedingAreas.Clear();
            lastCacheUpdateTime = 0f;
        };
    }

    public void Init(PetController controller)
    {
        petController = controller;
        // 최초 실행 시 캐시를 즉시 업데이트
        UpdateFeedingAreaCache();
    }

    /// <summary>
    /// 외부(FoodItem 스크립트 등)에서 호출하여 캐시에 아이템을 등록
    /// </summary>
    public static void RegisterFoodItem(GameObject food)
    {
        if (food != null && !allFoodItems.Contains(food))
        {
            allFoodItems.Add(food);
        }
    }

    /// <summary>
    /// 외부(FoodItem 스크립트 등)에서 호출하여 캐시에서 아이템을 제거
    /// </summary>
    public static void UnregisterFoodItem(GameObject food)
    {
        if (food != null)
        {
            allFoodItems.Remove(food);
        }
    }

    // 주기적으로 먹이 장소(환경) 캐시 업데이트
    private static void UpdateFeedingAreaCache()
    {
        if (Time.time - lastCacheUpdateTime < cacheUpdateInterval) return;

        allFeedingAreas.Clear();
        GameObject[] areas = GameObject.FindGameObjectsWithTag("FeedingArea");
        allFeedingAreas.AddRange(areas);
        lastCacheUpdateTime = Time.time;
    }

    public void UpdateFeeding()
    {
        // 상호작용, 모이기, 먹는 중, 잠자는 중일 때는 로직 실행 안함
        if (petController.isInteracting || petController.isGathering || isEating || petController.isHolding ||
            (petController.GetComponent<PetSleepingController>() != null && petController.GetComponent<PetSleepingController>().IsSleepingOrSeeking()))
        {
            return;
        }

        // 배고픔 수치 증가
        petController.hunger = Mathf.Clamp(petController.hunger + Time.deltaTime * hungerIncreaseRate, 0, 100);

        // 배고픔이 70%를 넘고, 탐색 쿨타임이 지났으면 먹이 탐색 시작
        if (petController.hunger > 70f && Time.time - lastDetectionTime > detectionInterval)
        {
            lastDetectionTime = Time.time;

            // 목표가 없을 때만 새로운 목표 탐색
            if (targetFood == null && targetFeedingArea == null)
            {
                // 1순위: 소모성 음식 아이템 탐색
                DetectFoodOptimized();

                // 2순위: 음식 아이템을 못 찾았으면, 환경 먹이 장소 탐색
                if (targetFood == null)
                {
                    DetectFeedingAreaOptimized();
                }
            }
        }

        // 목표를 향해 이동 및 섭취 처리
        HandleMovementToTarget();
    }

    /// <summary>
    /// 캐시된 목록에서 자신의 식성에 맞는 가장 가까운 '음식 아이템'을 찾습니다.
    /// </summary>
    private void DetectFoodOptimized()
    {
        GameObject nearestFood = null;
        float nearestDistSqr = detectionRadius * detectionRadius; // 탐색 반경의 제곱
        Vector3 myPos = transform.position;

        for (int i = allFoodItems.Count - 1; i >= 0; i--)
        {
            GameObject food = allFoodItems[i];
            if (food == null) { allFoodItems.RemoveAt(i); continue; }

            FoodItem foodItem = food.GetComponent<FoodItem>();
            // [핵심] 식성 확인: FoodItem 컴포넌트가 있고, 펫의 식단(diet)에 해당 음식 타입이 포함되는지 비트 연산으로 확인
            if (foodItem != null && (petController.diet & foodItem.foodType) != 0)
            {
                float distSqr = (food.transform.position - myPos).sqrMagnitude;
                if (distSqr < nearestDistSqr)
                {
                    nearestFood = food;
                    nearestDistSqr = distSqr;
                }
            }
        }

        if (nearestFood != null)
        {
                // ★★★ 추가된 부분 ★★★
            // 음식으로 이동하기 전에 펫의 현재 상태를 리셋합니다.
            ResetPetStateForSeeking();
            targetFood = nearestFood;
            petController.agent.SetDestination(targetFood.transform.position);
            // ▼▼▼▼▼ [수정] 목표를 찾았으면 즉시 움직임을 재개하도록 명령합니다. ▼▼▼▼▼
            petController.ResumeMovement();
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        }
    }

    /// <summary>
    /// 캐시된 목록에서 가장 가까운 '먹이 장소'를 찾습니다. (식성 무관)
    /// </summary>
    private void DetectFeedingAreaOptimized()
    {
        UpdateFeedingAreaCache(); // 주기적으로 목록 갱신

        GameObject nearestArea = null;
        float nearestDistSqr = detectionRadius * detectionRadius;
        Vector3 myPos = transform.position;

        foreach (GameObject area in allFeedingAreas)
        {
            if (area == null) continue;
            // ▼▼▼▼▼ [수정된 부분] 먹이 장소의 음식 타입을 확인하는 로직 추가 ▼▼▼▼▼
            FeedingArea feedingAreaInfo = area.GetComponent<FeedingArea>();

            // FeedingArea 컴포넌트가 있고, 펫의 식성에 맞는 장소일 때만 목표로 삼습니다.
            if (feedingAreaInfo != null && (petController.diet & feedingAreaInfo.foodType) != 0)
            {
                float distSqr = (area.transform.position - myPos).sqrMagnitude;
                if (distSqr < nearestDistSqr)
                {
                    nearestArea = area;
                    nearestDistSqr = distSqr;
                }
            }
        }

        if (nearestArea != null)
        {
               // ★★★ 추가된 부분 ★★★
            // 먹이 장소로 이동하기 전에 펫의 현재 상태를 리셋합니다.
            ResetPetStateForSeeking();
            targetFeedingArea = nearestArea;
            petController.agent.SetDestination(targetFeedingArea.transform.position);

            // ▼▼▼▼▼ [수정] 목표를 찾았으면 즉시 움직임을 재개하도록 명령합니다. ▼▼▼▼▼
            petController.ResumeMovement();
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
        }
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