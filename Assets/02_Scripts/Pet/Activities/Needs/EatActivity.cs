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
    private const float FOOD_SEARCH_RADIUS = 150f;   // 음식 탐색 시 배회 반경
    
    public override string Name => "Eat";
    public override bool IsInterruptible => false; // 식사 중에는 중단 불가
    
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
            
        // 이미 먹고 있거나 찾으러 가는 중이라면 높은 우선순위
        if (feedingController.IsEatingOrSeeking())
            return 2.0f;
            
        // 배고픔 수치에 비례하여 우선순위 증가
        float hunger = needs != null ? needs.Hunger : pet.Needs.Hunger;
        
        // 배고픔 70 이상이면 기본 0.5 + 추가 우선순위
        // 70일 때: 0.5, 85일 때: 1.0, 100일 때: 1.5
        return 0.5f + ((hunger - 70f) / 30f); // 0.5 ~ 1.5
    }
    
    public override void Start()
    {
        // Debug.Log($"[EatActivity] {pet.petName}: 식사 활동 시작 (배고픔: {pet.Needs.Hunger:F1})");
        searchTimer = 0f;
        
        // 즉시 음식 탐색 시작
        if (!feedingController.TryStartFeedingSequence())
        {
            // Debug.Log($"[EatActivity] {pet.petName}: 주변에 음식이 없습니다. 광역 탐색을 시작합니다.");
        }
    }
    
    public override void Update()
    {
        // 이미 먹고 있거나 음식을 향해 이동 중이라면 업데이트
        if (feedingController.IsEatingOrSeeking())
        {
            feedingController.UpdateMovementToFood();
            return;
        }
            
        // 광역 탐색 타이머 업데이트
        searchTimer += Time.deltaTime;
        
        // 일정 시간마다 음식 재탐색
        if (searchTimer >= WIDE_WANDER_INTERVAL)
        {
            searchTimer = 0f;
            
            if (!feedingController.TryStartFeedingSequence())
            {
                // 여전히 음식을 찾지 못했다면 광역 배회
                WanderToFindFood();
            }
        }
    }
    
    public override void Stop()
    {
        // Debug.Log($"[EatActivity] {pet.petName}: 식사 활동 종료");
        // 먹이 찾기 중단
        feedingController.CancelFeeding();
    }
    
    /// <summary>
    /// 음식을 찾기 위해 광역으로 배회합니다.
    /// </summary>
    private void WanderToFindFood()
    {
        if (pet.agent == null || !pet.agent.enabled || !pet.agent.isOnNavMesh)
            return;
            
        // 더 넓은 범위로 랜덤 목적지 설정
        Vector3 randomDirection = Random.insideUnitSphere * FOOD_SEARCH_RADIUS;
        randomDirection += pet.transform.position;
        
        if (UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out UnityEngine.AI.NavMeshHit hit, FOOD_SEARCH_RADIUS, UnityEngine.AI.NavMesh.AllAreas))
        {
            pet.agent.SetDestination(hit.position);
            // Debug.Log($"[EatActivity] {pet.petName}: 음식을 찾기 위해 새로운 위치로 이동합니다.");
        }
    }
}