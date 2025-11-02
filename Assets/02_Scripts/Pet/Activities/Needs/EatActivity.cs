using UnityEngine;

/// <summary>
/// 펫의 식사 활동을 담당하는 클래스
/// 기존 EatAction을 개선하여 더 명확한 구조를 제공합니다.
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

        // 이미 먹고 있거나 찾으러 가는 중이라면 매우 높은 우선순위
        if (feedingController.IsEatingOrSeeking())
            return 35.0f; // 먹는 중에는 거의 중단되지 않도록

        // 배고픔 수치에 비례하여 우선순위 증가
        float hunger = needs != null ? needs.Hunger : pet.Needs.Hunger;

        // 생존 본능을 반영한 우선순위
        // 70일 때: 12.0, 85일 때: 20.0, 100일 때: 30.0
        if (hunger >= 85f)
        {
            // 매우 배고픔 - 긴급 수준
            return 20.0f + ((hunger - 85f) / 15f * 10.0f); // 20.0 ~ 30.0
        }
        else if (hunger >= 70f)
        {
            // 배고픔 시작 - 중간 수준
            return 12.0f + ((hunger - 70f) / 15f * 8.0f); // 12.0 ~ 20.0
        }

        return 0f;
    }
    
    public override void Start()
    {
        // Debug.Log($"[EatActivity] {pet.petName}: 식사 활동 시작 (배고픔: {pet.Needs.Hunger:F1})");
        searchTimer = 0f;
        isWandering = false;
        searchFailureCount = 0; // 탐색 실패 횟수 초기화

        // 초기 탐색 범위로 음식 탐색 시작
        float initialRadius = CalculateSearchRadius();
        if (!feedingController.TryStartFeedingSequence(initialRadius))
        {
            // Debug.Log($"[EatActivity] {pet.petName}: 주변 {initialRadius:F0}m에 음식이 없습니다. 광역 탐색을 시작합니다.");
            searchFailureCount++;
            // 음식을 못 찾으면 PetEmotionController가 식성에 따른 음식 감정 자동 표시
            WanderToFindFood();
        }
        else
        {
            // 음식을 찾으면 Hungry 감정으로 변경
            pet.ShowEmotion(EmotionType.Hungry, 999f);
            searchFailureCount = 0;
        }
    }
    
    public override void Update()
    {
        // 이미 먹고 있거나 음식을 향해 이동 중이라면 업데이트
        if (feedingController.IsEatingOrSeeking())
        {
            // 벌 공격을 받고 있고 꿀을 먹는 중인지 확인
            if (pet.State.IsBeingAttacked && !isEatingHoney)
            {
                // 꿀을 먹기 시작했다면 중단 불가능 상태로 설정
                isEatingHoney = true;
        // Debug.Log($"[EatActivity] {pet.petName}: 꿀을 먹는 중이므로 중단 불가능 상태로 설정");
            }
            
            // 배고픔이 해소되었는지 확인 (꿀 먹기 완료)
            if (isEatingHoney && pet.Needs.Hunger < 70f)
            {
                isEatingHoney = false;
        // Debug.Log($"[EatActivity] {pet.petName}: 꿀 먹기 완료, 이제 도망갈 수 있음");
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
                // 음식을 찾으면 Hungry 감정으로 변경
                pet.ShowEmotion(EmotionType.Hungry, 999f);
                searchFailureCount = 0;
            }
        }
    }
    
    public override void Stop()
    {
        // Debug.Log($"[EatActivity] {pet.petName}: 식사 활동 종료");

        // 배고픔 감정 제거
        pet.HideEmotion();

        // 먹이 찾기 중단
        feedingController.CancelFeeding();
        isWandering = false;
        isEatingHoney = false; // 꿀 먹기 상태 초기화
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
}