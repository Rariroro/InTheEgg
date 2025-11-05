using UnityEngine;
using System.Collections;
using InTheEgg.Constants;

/// <summary>
/// 펫의 식사 활동을 담당하는 클래스
/// 감정 단계: 1) 음식 생각 감정 (탐색 중) → 2) Hungry 감정 (먹는 중) → 3) Happy 감정 (먹은 후)
/// </summary>
public class EatActivity : PetActivityAdapter
{
    private readonly PetFeedingController feedingController;

    // 음식을 찾지 못했을 때의 광역 배회 로직을 위한 변수
    private float searchTimer = 0f;
    private const float WIDE_WANDER_INTERVAL = 5.0f; // 5초마다 새로운 목적지 탐색

    // 점진적 탐색 범위 시스템
    private int searchFailureCount = 0; // 탐색 실패 횟수
    private const float BASE_SEARCH_RADIUS = 50f;    // 기본 탐색 반경
    private const float RADIUS_INCREMENT = 25f;      // 실패 시 증가량
    private const float MAX_SEARCH_RADIUS = 200f;    // 최대 탐색 반경
    private const float HUNGRY_BONUS_RADIUS = 50f;   // 매우 배고플 때 추가 반경

    // 배회 상태 관리
    private bool isWandering = false;
    private Vector3 currentWanderTarget;
    private const float ARRIVAL_DISTANCE = 3f; // 목적지 도착 판정 거리

    // 꿀 먹기 관련 추가 변수
    private bool isEatingHoney = false;

    // 감정 시스템 관련 변수
    private EmotionType currentFoodEmotion; // 현재 표시 중인 음식 감정
    private float foodEmotionTimer = 0f; // 음식 감정 변경 타이머
    private float foodEmotionChangeInterval; // 음식 감정 변경 주기
    private bool wasEating = false; // 이전 프레임에 먹고 있었는지 (먹기 시작 감지용)
    private Coroutine happyEmotionCoroutine; // Happy 감정 코루틴
    
    public override string Name => "Eat";
    public override bool IsInterruptible => !isEatingHoney; // 꿀을 먹는 중에만 중단 불가
    
    public EatActivity(PetController petController, PetFeedingController feeding) : base(petController)
    {
        feedingController = feeding;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 기본 상태 체크 - Idle 상태이거나, Environmental 상태에서 물 속에만 있을 때도 먹기 가능
        if (state.CurrentStatus != PetStatus.Idle && 
            !(state.CurrentStatus == PetStatus.Environmental && state.IsInWater && !state.IsClimbingTree))
            return false;
            
        // 터치/홀드 상태에서는 먹이 찾기 중단
        if (pet.State.IsHolding || pet.State.IsSelected)
            return false;
            
        // 나무 위에 있다면 식사 불가
        if (pet.State.IsClimbingTree)
            return false;
            
        // 이미 먹고 있거나 찾으러 가는 중이라면 계속
        if (feedingController.IsEatingOrSeeking())
            return true;
            
        // 배고픔 확인 (70 이상)
        return needs != null ? needs.IsHungry : pet.Needs.Hunger >= 70f;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;

        // 이미 먹고 있거나 찾으러 가는 중이라면 매우 높은 우선순위 (중단 방지)
        if (feedingController.IsEatingOrSeeking())
            return 40.0f; // 진행 중 보호 (35→40)

        // 배고픔 수치에 비례하여 우선순위 증가
        float hunger = needs != null ? needs.Hunger : pet.Needs.Hunger;

        // 생존 본능을 반영한 우선순위
        if (hunger >= 90f)
        {
            // 극도로 배고픔 - 긴급 (탈진 직전)
            return 60.0f;
        }
        else if (hunger >= 85f)
        {
            // 매우 배고픔 - 높은 우선순위 (선형 증가)
            return 25.0f + ((hunger - 85f) / 5f * 10.0f); // 25.0 ~ 35.0
        }
        else if (hunger >= 70f)
        {
            // 배고픔 시작 - 중간 우선순위
            return 20.0f;
        }

        return 0f;
    }
    
    public override void Start()
    {
        // Debug.Log($"[EatActivity] {pet.petName}: 식사 활동 시작 (배고픔: {pet.Needs.Hunger:F1})");
        searchTimer = 0f;
        isWandering = false;
        searchFailureCount = 0; // 탐색 실패 횟수 초기화
        wasEating = false;
        happyEmotionCoroutine = null;

        // 감정 1단계: 음식 생각 감정 시작
        StartFoodThoughtEmotion();

        // 초기 탐색 범위로 음식 탐색 시작
        float initialRadius = CalculateSearchRadius();
        if (!feedingController.TryStartFeedingSequence(initialRadius))
        {
            // Debug.Log($"[EatActivity] {pet.petName}: 주변 {initialRadius:F0}m에 음식이 없습니다. 광역 탐색을 시작합니다.");
            searchFailureCount++;
            WanderToFindFood();
        }
        else
        {
            searchFailureCount = 0;
        }
    }
    
    public override void Update()
    {
        // 음식 생각 감정 변경 타이머 업데이트 (음식 찾는 중에만)
        if (!feedingController.IsEatingOrSeeking())
        {
            foodEmotionTimer += Time.deltaTime;
            if (foodEmotionTimer >= foodEmotionChangeInterval)
            {
                ChangeFoodThoughtEmotion();
            }
        }

        // 이미 먹고 있거나 음식을 향해 이동 중이라면 업데이트
        if (feedingController.IsEatingOrSeeking())
        {
            // 감정 2단계: 먹기 시작할 때 Hungry 감정으로 변경
            bool isEating = IsPlayingEatingAnimation();
            if (isEating && !wasEating)
            {
                // 먹기 시작
                pet.ShowEmotion(EmotionType.Hungry, EmotionConstants.DURATION_PERSISTENT);
            }
            wasEating = isEating;

            // 벌 공격을 받고 있고 꿀을 먹는 중인지 확인
            if (pet.State.IsBeingAttacked && !isEatingHoney)
            {
                // 꿀을 먹기 시작했다면 중단 불가능 상태로 설정
                isEatingHoney = true;
                // Debug.Log($"[EatActivity] {pet.petName}: 꿀을 먹는 중이므로 중단 불가능 상태로 설정");
            }

            // 감정 3단계: 배고픔이 해소되었는지 확인 (먹기 완료)
            if (wasEating && pet.Needs.Hunger < 70f)
            {
                // 먹기 완료 - Happy 감정 표시
                if (happyEmotionCoroutine != null)
                {
                    pet.StopCoroutine(happyEmotionCoroutine);
                }
                happyEmotionCoroutine = pet.StartCoroutine(ShowHappyEmotionAfterEating());

                if (isEatingHoney)
                {
                    isEatingHoney = false;
                    // Debug.Log($"[EatActivity] {pet.petName}: 꿀 먹기 완료, 이제 도망갈 수 있음");
                }
            }

            feedingController.UpdateMovementToFood();
            isWandering = false; // 음식을 찾았으므로 배회 중단
            return;
        }

        // 배회 중인 경우 목적지 도착 체크
        if (isWandering)
        {
            // 목적지에 도착했는지 확인
            if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
            {
                float distanceToTarget = Vector3.Distance(pet.transform.position, currentWanderTarget);

                // 목적지 도착 또는 경로 없음
                if (distanceToTarget < ARRIVAL_DISTANCE || !pet.agent.hasPath || pet.agent.remainingDistance < ARRIVAL_DISTANCE)
                {
                    // 새로운 목적지 설정
                    WanderToFindFood();
                }
            }
        }

        // 광역 탐색 타이머 업데이트
        searchTimer += Time.deltaTime;

        // 일정 시간마다 음식 재탐색
        if (searchTimer >= WIDE_WANDER_INTERVAL)
        {
            searchTimer = 0f;

            // 점진적으로 늘어난 범위로 재탐색
            float currentRadius = CalculateSearchRadius();
            if (!feedingController.TryStartFeedingSequence(currentRadius))
            {
                searchFailureCount++; // 실패 횟수 증가
                // Debug.Log($"[EatActivity] {pet.petName}: 탐색 실패 {searchFailureCount}회, 현재 범위: {currentRadius:F0}m");

                // 여전히 음식을 찾지 못했다면 계속 배회
                if (!isWandering)
                {
                    WanderToFindFood();
                }
            }
            else
            {
                searchFailureCount = 0;
            }
        }
    }
    
    public override void Stop()
    {
        // Debug.Log($"[EatActivity] {pet.petName}: 식사 활동 종료");

        // Happy 감정 코루틴 정리
        if (happyEmotionCoroutine != null)
        {
            pet.StopCoroutine(happyEmotionCoroutine);
            happyEmotionCoroutine = null;
        }

        // 감정 제거
        pet.HideEmotion();

        // 먹이 찾기 중단
        feedingController.CancelFeeding();
        isWandering = false;
        isEatingHoney = false; // 꿀 먹기 상태 초기화
        wasEating = false;
    }
    
    /// <summary>
    /// 음식을 찾기 위해 광역으로 배회합니다.
    /// </summary>
    private void WanderToFindFood()
    {
        if (pet.agent == null || !pet.agent.enabled || !pet.agent.isOnNavMesh)
            return;
            
        // 현재 탐색 범위에 맞춰 랜덤 목적지 설정
        float currentRadius = CalculateSearchRadius();
        Vector3 randomDirection = Random.insideUnitSphere * currentRadius;
        randomDirection += pet.transform.position;
        
        if (UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out UnityEngine.AI.NavMeshHit hit, currentRadius, UnityEngine.AI.NavMesh.AllAreas))
        {
            currentWanderTarget = hit.position;
            pet.agent.SetDestination(currentWanderTarget);
            isWandering = true;
            // Debug.Log($"[EatActivity] {pet.petName}: 음식을 찾기 위해 새로운 위치로 이동합니다. 범위: {currentRadius:F0}m");
        }
    }
    
    /// <summary>
    /// 현재 탐색 범위를 계산합니다.
    /// </summary>
    private float CalculateSearchRadius()
    {
        // 기본 범위 + (실패 횟수 * 증가량)
        float radius = BASE_SEARCH_RADIUS + (searchFailureCount * RADIUS_INCREMENT);

        // 매우 배고플 때 추가 범위
        if (pet.Needs.Hunger >= 80f)
        {
            radius += HUNGRY_BONUS_RADIUS;
        }

        // 최대 범위 제한
        return Mathf.Min(radius, MAX_SEARCH_RADIUS + (pet.Needs.Hunger >= 80f ? HUNGRY_BONUS_RADIUS : 0));
    }

    #region 감정 시스템 (3단계)

    /// <summary>
    /// 감정 1단계: 음식 생각 감정 시작 (탐색 중)
    /// </summary>
    private void StartFoodThoughtEmotion()
    {
        currentFoodEmotion = GetRandomFoodEmotionByDiet();
        foodEmotionTimer = 0f;
        foodEmotionChangeInterval = Random.Range(
            EmotionConstants.HUNGER_EMOTION_MIN_INTERVAL,
            EmotionConstants.HUNGER_EMOTION_MAX_INTERVAL
        );
        pet.ShowEmotion(currentFoodEmotion, EmotionConstants.DURATION_PERSISTENT);
    }

    /// <summary>
    /// 음식 생각 감정 변경 (7-25초마다)
    /// </summary>
    private void ChangeFoodThoughtEmotion()
    {
        currentFoodEmotion = GetRandomFoodEmotionByDiet();
        foodEmotionTimer = 0f;
        foodEmotionChangeInterval = Random.Range(
            EmotionConstants.HUNGER_EMOTION_MIN_INTERVAL,
            EmotionConstants.HUNGER_EMOTION_MAX_INTERVAL
        );
        pet.ShowEmotion(currentFoodEmotion, EmotionConstants.DURATION_PERSISTENT);
    }

    /// <summary>
    /// 펫의 식성에 따른 랜덤 음식 감정 선택
    /// </summary>
    private EmotionType GetRandomFoodEmotionByDiet()
    {
        var diet = pet.diet;
        var foodEmotions = new System.Collections.Generic.List<EmotionType>();

        // 식성에 따라 가능한 음식 감정 추가
        if ((diet & PetTraits.DietaryFlags.Meat) != 0)
            foodEmotions.Add(EmotionType.Thought_Food_Meat);

        if ((diet & PetTraits.DietaryFlags.Fish) != 0)
            foodEmotions.Add(EmotionType.Thought_Food_Fish);

        if ((diet & PetTraits.DietaryFlags.Grass) != 0)
            foodEmotions.Add(EmotionType.Thought_Food_Grass);

        if ((diet & PetTraits.DietaryFlags.SeedsAndGrains) != 0)
            foodEmotions.Add(EmotionType.Thought_Food_Grain);

        if ((diet & PetTraits.DietaryFlags.FruitsAndVegetables) != 0)
        {
            foodEmotions.Add(EmotionType.Thought_Food_Fruit);
            foodEmotions.Add(EmotionType.Thought_Food_Vegetable);
        }

        // 식성이 없거나 매칭되는 음식 감정이 없으면 기본 Hungry 반환
        if (foodEmotions.Count == 0)
            return EmotionType.Hungry;

        // 랜덤으로 하나 선택
        return foodEmotions[Random.Range(0, foodEmotions.Count)];
    }

    /// <summary>
    /// 먹기 애니메이션이 재생 중인지 확인
    /// </summary>
    private bool IsPlayingEatingAnimation()
    {
        if (pet.animator == null)
            return false;

        // Animator의 "animation" 파라미터를 통해 현재 애니메이션 확인
        int currentAnimation = pet.animator.GetInteger("animation");
        return currentAnimation == (int)PetAnimationController.PetAnimationType.Eat;
    }

    /// <summary>
    /// 감정 3단계: 먹은 후 Happy 감정 표시 (3초)
    /// </summary>
    private IEnumerator ShowHappyEmotionAfterEating()
    {
        pet.ShowEmotion(EmotionType.Happy, EmotionConstants.DURATION_SHORT);
        yield return new WaitForSeconds(EmotionConstants.DURATION_SHORT);

        // Happy 감정 종료 후 자동으로 새 Activity로 전환됨 (PetAI가 처리)
        // 음식 생각 감정은 새 EatActivity 시작 시 자동 표시됨
    }

    #endregion
}