// 최적화된 PetFeedingController
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PetFeedingController : MonoBehaviour
{
    private PetController petController;
    private GameObject targetFood;
    private GameObject targetFeedingArea;
    private float detectionRadius = 100f;
    private float eatingDistance = 4f;
    private float feedingAreaDistance = 2f;
    private bool isEating = false;
    private float hungerIncreaseRate = 0.1f;

    // 마지막으로 배고픔 상태를 표시한 시간
    private float lastHungryEmotionTime = 0f;
    private float hungryEmotionInterval = 35f;

    // 먹이 공간을 위한 레이어 마스크
    private int feedingAreaLayer;
    private const string FEEDING_AREA_TAG = "FeedingArea";

    // 성능 최적화를 위한 캐싱
    private static List<GameObject> allFoodItems = new List<GameObject>();
    private static List<GameObject> allFeedingAreas = new List<GameObject>();
    private static float lastFoodCacheUpdate = 0f;
    private static float foodCacheUpdateInterval = 2f; // 2초마다 캐시 업데이트

    // 거리 체크 최적화
    private float lastDetectionTime = 0f;
    private float detectionInterval = 1f; // 1초마다 탐지

    // 성격별 먹이 증가 배율
    private float lazyHungerModifier = 0.8f;
    private float playfulHungerModifier = 1.2f;
    private float braveHungerModifier = 1.0f;
    private float shyHungerModifier = 0.9f;

    // 배고픔에 따른 속도 감소 관련 변수
    private float minSpeedFactor = 0.3f;
    private float lastHungerCheck = 0f;
    private float hungerCheckInterval = 5f;

    // 배고픔 단계별 임계값
    private float veryHungryThreshold = 80f;
    private float extremeHungryThreshold = 95f;

    // 애정도 감소 관련
    private float affectionDecreaseRate = 0.05f;
    private float lastAffectionDecreaseTime = 0f;
    private float affectionDecreaseInterval = 10f;

    // 앉기 상태 관련
    private bool isSitting = false;
    private int sittingAnimationIndex = 5;

    // 애정도 증가량
    private float affectionIncreaseSmall = 2f;
    private float affectionIncreaseLarge = 5f;

    // 정적 초기화 블록
    static PetFeedingController()
    {
        // 씬 로드 시 캐시 초기화
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => {
            allFoodItems.Clear();
            allFeedingAreas.Clear();
            lastFoodCacheUpdate = 0f;
        };
    }

    public void Init(PetController controller)
    {
        petController = controller;

        feedingAreaLayer = LayerMask.GetMask("FeedingArea");
        if (feedingAreaLayer == 0)
        {
            feedingAreaLayer = Physics.DefaultRaycastLayers;
        }

        // 초기 캐시 구축
        UpdateFoodCache();
        UpdateFeedingAreaCache();
    }

    // 음식 캐시 업데이트 - 정적 메서드로 모든 펫이 공유
    private static void UpdateFoodCache()
    {
        if (Time.time - lastFoodCacheUpdate < foodCacheUpdateInterval)
            return;

        allFoodItems.Clear();
        try
        {
            GameObject[] foods = GameObject.FindGameObjectsWithTag("Food");
            allFoodItems.AddRange(foods);
        }
        catch (UnityException)
        {
            // Food 태그가 없는 경우 무시
        }
        
        lastFoodCacheUpdate = Time.time;
    }

    // 먹이 구역 캐시 업데이트
    private void UpdateFeedingAreaCache()
    {
        if (allFeedingAreas.Count > 0)
            return; // 이미 캐시가 있으면 재사용

        GameObject[] areas = GameObject.FindGameObjectsWithTag(FEEDING_AREA_TAG);
        allFeedingAreas.Clear();
        allFeedingAreas.AddRange(areas);
    }

    public void UpdateFeeding()
    {
        // 모이기 중이거나 이미 모였을 때는 먹이 행동을 하지 않음
        if (petController.isGathering || petController.isGathered) return;
        
        if (!isEating)
        {
            // 배고픔 증가
            float personalityHungerModifier = GetPersonalityHungerModifier();
            petController.hunger += Time.deltaTime * hungerIncreaseRate * personalityHungerModifier;
            petController.hunger = Mathf.Clamp(petController.hunger, 0f, 100f);

            // 감정 표시 (간격 체크)
            if (petController.hunger > 60f &&
                Time.time - lastHungryEmotionTime > hungryEmotionInterval)
            {
                petController.ShowEmotion(EmotionType.Hungry, 5f);
                lastHungryEmotionTime = Time.time;
            }

            // 속도 조절 (간격 체크)
            if (Time.time - lastHungerCheck > hungerCheckInterval)
            {
                UpdateBehaviorBasedOnHunger();
                lastHungerCheck = Time.time;
            }

            // 애정도 감소 (100% 배고픔 시)
            if (petController.hunger >= 100f &&
                Time.time - lastAffectionDecreaseTime > affectionDecreaseInterval)
            {
                petController.affection = Mathf.Max(0f, petController.affection - affectionDecreaseRate);
                lastAffectionDecreaseTime = Time.time;
                Debug.Log($"{petController.petName} 애정도 감소: {petController.affection:F1}");
            }

            // 탐지 간격 체크 - 매 프레임이 아닌 주기적으로
            if (petController.hunger > 70f && 
                targetFood == null && 
                targetFeedingArea == null && 
                !isEating &&
                Time.time - lastDetectionTime > detectionInterval)
            {
                lastDetectionTime = Time.time;
                
                // 먹이 구역 우선 탐지
                DetectFeedingAreaOptimized();

                // 못 찾았으면 일반 음식 탐지
                if (targetFeedingArea == null)
                {
                    DetectFoodOptimized();
                }

                // 찾았고 앉아있다면 일어나기
                if ((targetFood != null || targetFeedingArea != null) && isSitting)
                {
                    StopSitting();
                }
            }
        }

        // 목표물로 이동 처리
        HandleMovementToTarget();
    }

    // 최적화된 먹이 구역 탐지
    private void DetectFeedingAreaOptimized()
    {
        GameObject nearestArea = null;
        float nearestDistSqr = float.MaxValue;
        Vector3 myPos = transform.position;

        // 캐시된 먹이 구역 사용
        for (int i = allFeedingAreas.Count - 1; i >= 0; i--)
        {
            GameObject area = allFeedingAreas[i];
            if (area == null)
            {
                allFeedingAreas.RemoveAt(i);
                continue;
            }

            // 제곱 거리로 비교 (sqrt 연산 제거)
            float distSqr = (area.transform.position - myPos).sqrMagnitude;
            if (distSqr < detectionRadius * detectionRadius && distSqr < nearestDistSqr)
            {
                nearestArea = area;
                nearestDistSqr = distSqr;
            }
        }

        if (nearestArea != null)
        {
            targetFeedingArea = nearestArea;
            petController.ShowEmotion(EmotionType.Hungry, 5f);
            
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                Vector3 feedingPosition = GetPositionInFeedingArea(nearestArea);
                petController.agent.SetDestination(feedingPosition);
            }
            Debug.Log($"{petController.petName} 먹이 구역 발견: 거리 {Mathf.Sqrt(nearestDistSqr):F1}m");
        }
    }

    // 최적화된 음식 탐지
    private void DetectFoodOptimized()
    {
        // 캐시 업데이트 (모든 펫이 공유)
        UpdateFoodCache();

        GameObject nearestFood = null;
        float nearestDistSqr = float.MaxValue;
        Vector3 myPos = transform.position;
        float detectionRadiusSqr = detectionRadius * detectionRadius;

        // 캐시된 음식 아이템 검색
        for (int i = allFoodItems.Count - 1; i >= 0; i--)
        {
            GameObject food = allFoodItems[i];
            if (food == null)
            {
                allFoodItems.RemoveAt(i);
                continue;
            }

            float distSqr = (food.transform.position - myPos).sqrMagnitude;
            if (distSqr < detectionRadiusSqr && distSqr < nearestDistSqr)
            {
                nearestFood = food;
                nearestDistSqr = distSqr;
            }
        }

        if (nearestFood != null)
        {
            targetFood = nearestFood;
            petController.ShowEmotion(EmotionType.Hungry, 5f);
            
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                petController.agent.SetDestination(targetFood.transform.position);
            }
            Debug.Log($"{petController.petName} 음식 발견: 거리 {Mathf.Sqrt(nearestDistSqr):F1}m");
        }
    }

    // 목표물로의 이동 처리를 별도 메서드로 분리
    private void HandleMovementToTarget()
    {
        if (isEating || isSitting)
            return;

        // NavMeshAgent 체크
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        // 먹이 구역 처리
        if (targetFeedingArea != null)
        {
            // 목표물이 삭제되었는지 체크
            if (targetFeedingArea == null)
            {
                targetFeedingArea = null;
                return;
            }

            float distSqr = (targetFeedingArea.transform.position - transform.position).sqrMagnitude;
            if (distSqr <= feedingAreaDistance * feedingAreaDistance ||
                (petController.agent.remainingDistance <= feedingAreaDistance && !petController.agent.pathPending))
            {
                StartCoroutine(EatAtFeedingArea());
            }
            else if (petController.agent.destination != targetFeedingArea.transform.position)
            {
                petController.agent.SetDestination(targetFeedingArea.transform.position);
            }
        }
        // 일반 음식 처리
        else if (targetFood != null)
        {
            // 목표물이 삭제되었는지 체크
            if (targetFood == null)
            {
                targetFood = null;
                return;
            }

            float distSqr = (targetFood.transform.position - transform.position).sqrMagnitude;
            if (distSqr <= eatingDistance * eatingDistance)
            {
                StartCoroutine(EatFood());
            }
            else if (petController.agent.destination != targetFood.transform.position)
            {
                petController.agent.SetDestination(targetFood.transform.position);
            }
        }
    }

    // 나머지 메서드들은 동일...
    private float GetPersonalityHungerModifier()
    {
        switch (petController.personality)
        {
            case PetAIProperties.Personality.Lazy:
                return lazyHungerModifier;
            case PetAIProperties.Personality.Playful:
                return playfulHungerModifier;
            case PetAIProperties.Personality.Brave:
                return braveHungerModifier;
            case PetAIProperties.Personality.Shy:
                return shyHungerModifier;
            default:
                return 1.0f;
        }
    }

    private void UpdateBehaviorBasedOnHunger()
    {
        // 모이기 중일 때는 배고픔 행동을 하지 않음
        if (petController.isGathering || petController.isGathered) return;
        
        if (petController.agent != null && petController.agent.enabled)
        {
            if (petController.hunger >= extremeHungryThreshold && !isSitting)
            {
                StartCoroutine(SitDueToHunger());
                return;
            }
            else if (petController.hunger >= veryHungryThreshold)
            {
                float hungerFactor = 1f - ((petController.hunger / 100f) * (1f - minSpeedFactor));
                petController.agent.speed = petController.baseSpeed * hungerFactor;
            }
            else
            {
                if (isSitting && petController.hunger < extremeHungryThreshold)
                {
                    StopSitting();
                }
                petController.agent.speed = petController.baseSpeed;
            }
        }
    }

    // 음식 아이템 등록/제거를 위한 정적 메서드 추가
    public static void RegisterFoodItem(GameObject food)
    {
        if (food != null && !allFoodItems.Contains(food))
        {
            allFoodItems.Add(food);
        }
    }

    public static void UnregisterFoodItem(GameObject food)
    {
        if (food != null)
        {
            allFoodItems.Remove(food);
        }
    }

    // 나머지 코드는 기존과 동일...
    private IEnumerator SitDueToHunger()
    {
        if (isSitting)
            yield break;

        isSitting = true;
        petController.StopMovement();
        petController.ShowEmotion(EmotionType.Hungry, 5f);

        PetAnimationController animController = petController.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            yield return StartCoroutine(animController.PlayAnimationWithCustomDuration(
                sittingAnimationIndex, 1f, false, false));
        }

        Debug.Log($"{petController.petName} 너무 배고파서 앉음. 배고픔: {petController.hunger:F0}");

        float lastFoodSearchTime = 0f;
        float foodSearchInterval = 5f;

        while (petController.hunger >= extremeHungryThreshold)
        {
            // 모이기 명령이 오면 앉기 중단
            if (petController.isGathering)
            {
                StopSitting();
                yield break;
            }
            
            if (Time.time - lastHungryEmotionTime > hungryEmotionInterval * 0.5f)
            {
                petController.ShowEmotion(EmotionType.Hungry, 5f);
                lastHungryEmotionTime = Time.time;
            }

            if (Time.time - lastFoodSearchTime > foodSearchInterval)
            {
                DetectFeedingAreaOptimized();
                if (targetFeedingArea == null)
                {
                    DetectFoodOptimized();
                }

                if (targetFood != null || targetFeedingArea != null)
                {
                    StopSitting();
                    break;
                }
                lastFoodSearchTime = Time.time;
            }

            if (petController.animator != null &&
                petController.animator.GetInteger("animation") != sittingAnimationIndex)
            {
                petController.animator.SetInteger("animation", sittingAnimationIndex);
            }

            yield return new WaitForSeconds(1f);
        }

        StopSitting();
    }

    private void StopSitting()
    {
        if (!isSitting)
            return;

        isSitting = false;
        PetAnimationController animController = petController.GetComponent<PetAnimationController>();

        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", 0);
        }

        if (animController != null)
        {
            animController.StopContinuousAnimation();
        }

        petController.ResumeMovement();
        petController.GetComponent<PetMovementController>().SetRandomDestination();
    }

    private Vector3 GetPositionInFeedingArea(GameObject feedingArea)
    {
        Collider feedingAreaCollider = feedingArea.GetComponent<Collider>();
        if (feedingAreaCollider == null)
        {
            return feedingArea.transform.position;
        }

        Vector3 colliderCenter;
        if (feedingAreaCollider is BoxCollider)
        {
            BoxCollider boxCollider = feedingAreaCollider as BoxCollider;
            colliderCenter = feedingArea.transform.TransformPoint(boxCollider.center);
        }
        else if (feedingAreaCollider is SphereCollider)
        {
            SphereCollider sphereCollider = feedingAreaCollider as SphereCollider;
            colliderCenter = feedingArea.transform.TransformPoint(sphereCollider.center);
        }
        else
        {
            colliderCenter = feedingAreaCollider.bounds.center;
        }

        RaycastHit rayHit;
        if (Physics.Raycast(colliderCenter, Vector3.down, out rayHit, 10f))
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(rayHit.point, out navHit, 2f, NavMesh.AllAreas))
            {
                return navHit.position;
            }
        }

        NavMeshHit directHit;
        if (NavMesh.SamplePosition(colliderCenter, out directHit, 5f, NavMesh.AllAreas))
        {
            return directHit.position;
        }

        return new Vector3(colliderCenter.x, petController.transform.position.y, colliderCenter.z);
    }

    private IEnumerator EatAtFeedingArea()
    {
        isEating = true;
        petController.StopMovement();

        if (petController.petModelTransform != null && targetFeedingArea != null)
        {
            Vector3 lookDirection = targetFeedingArea.transform.position - petController.transform.position;
            lookDirection.y = 0;

            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                float rotationTime = 0f;
                Quaternion startRotation = petController.petModelTransform.rotation;

                while (rotationTime < 1f)
                {
                    rotationTime += Time.deltaTime * 2f;
                    petController.petModelTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, rotationTime);
                    yield return null;
                }
            }
        }

        petController.hunger = Mathf.Max(0f, petController.hunger - 60f);
        petController.affection = Mathf.Min(100f, petController.affection + affectionIncreaseLarge);

        Debug.Log($"{petController.petName} 먹이 구역 식사 완료. 배고픔: {petController.hunger:F0}, 애정도: {petController.affection:F0}");

        petController.HideEmotion();

        yield return StartCoroutine(petController.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(4, 5f, true, false));

        petController.ShowEmotion(EmotionType.Happy, 3f);

        targetFeedingArea = null;
        isEating = false;

        if (isSitting)
        {
            StopSitting();
        }
        else
        {
            petController.ResumeMovement();
            petController.GetComponent<PetMovementController>().SetRandomDestination();
        }
    }

    private IEnumerator EatFood()
    {
        isEating = true;
        petController.StopMovement();

        if (petController.petModelTransform != null && targetFood != null)
        {
            Vector3 lookDirection = targetFood.transform.position - petController.transform.position;
            lookDirection.y = 0;

            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                float rotationTime = 0f;
                Quaternion startRotation = petController.petModelTransform.rotation;

                while (rotationTime < 1f)
                {
                    rotationTime += Time.deltaTime * 2f;
                    petController.petModelTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, rotationTime);
                    yield return null;
                }
            }
        }

        petController.hunger = Mathf.Max(0f, petController.hunger - 30f);
        petController.affection = Mathf.Min(100f, petController.affection + affectionIncreaseSmall);

        Debug.Log($"{petController.petName} 음식 섭취. 배고픔: {petController.hunger:F0}, 애정도: {petController.affection:F0}");

        petController.HideEmotion();

        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(4));

        if (targetFood != null)
        {
            UnregisterFoodItem(targetFood); // 캐시에서 제거
            Destroy(targetFood);
            targetFood = null;
        }

        petController.ShowEmotion(EmotionType.Happy, 3f);

        if (isSitting)
        {
            StopSitting();
        }
        else
        {
            petController.GetComponent<PetMovementController>().SetRandomDestination();
        }

        isEating = false;
    }

    public void FeedAtArea(GameObject feedingArea)
    {
        if (!isEating && feedingArea != null)
        {
            targetFeedingArea = feedingArea;
            if (isSitting) StopSitting();

            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                Vector3 feedingPosition = GetPositionInFeedingArea(feedingArea);
                petController.agent.SetDestination(feedingPosition);
                petController.ShowEmotion(EmotionType.Hungry, 3f);
            }
        }
    }

    public void ForceFeed(float amount)
    {
        petController.hunger = Mathf.Max(0f, petController.hunger - amount);
        petController.affection = Mathf.Min(100f, petController.affection + affectionIncreaseSmall);
        
        StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(4));
        petController.ShowEmotion(EmotionType.Happy, 3f);
        
        if (isSitting) StopSitting();
    }
}