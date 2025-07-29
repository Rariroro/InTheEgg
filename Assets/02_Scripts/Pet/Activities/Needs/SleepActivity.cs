using UnityEngine;

/// <summary>
/// 펫의 수면 활동을 담당하는 클래스
/// </summary>
public class SleepActivity : PetActivityAdapter
{
    private readonly PetSleepingController sleepingController;
    private bool isPreparingToSleep;
    
    // 잠잘 곳을 찾지 못했을 때의 광역 배회 로직을 위한 변수
    private float searchTimer = 0f;
    private const float WIDE_WANDER_INTERVAL = 5.0f; // 5초마다 새로운 목적지 탐색
    private const float SLEEP_SEARCH_RADIUS = 150f;  // 잠잘 곳 탐색 시 배회 반경
    
    public override string Name => "Sleep";
    public override bool IsInterruptible => true; // 수면은 중단 가능
    
    public SleepActivity(PetController petController, PetSleepingController sleeping) : base(petController)
    {
        sleepingController = sleeping;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 기본 상태 체크
        if (state.CurrentStatus != PetStatus.Idle)
            return false;
            
        // 터치/홀드 상태에서는 잠자기 중단
        if (pet.isHolding || pet.isSelected)
            return false;
            
        // 나무 위에 있어도 잠들 수 있음
        
        // 이미 잠을 자거나 잠잘 곳을 찾는 중이라면 계속
        if (sleepingController.IsSleepingOrSeeking())
            return true;
            
        // 졸림 확인 (70 이상)
        return needs != null ? needs.IsSleepy : pet.Needs.Sleepiness >= 70f;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 이미 잠을 자거나 잠잘 곳을 찾는 중이라면 높은 우선순위
        if (sleepingController.IsSleepingOrSeeking())
            return 2.0f;
            
        // 졸림 수치에 비례하여 우선순위 증가
        float sleepiness = needs != null ? needs.Sleepiness : pet.Needs.Sleepiness;
        // 기본 우선순위 0.2f를 부여하고, 졸음 수치에 따라 점진적으로 증가
        return 0.2f + ((sleepiness - 70f) / 30f); // 0.2 ~ 1.2
    }
    
    // 기존 IPetAction 메서드 구현 (호환성)
    public override float GetPriority()
    {
        // 터치/홀드 상태에서는 잠자기도 중단
        if (pet.isHolding || pet.isSelected)
            return 0f;
            
        // 이미 잠을 자거나 잠잘 곳을 찾는 중이라면, 높은 우선순위를 유지
        if (sleepingController.IsSleepingOrSeeking())
            return 2.0f;
            
        float currentSleepiness = pet.Needs != null ? pet.Needs.Sleepiness : 30f;
        if (currentSleepiness >= 70f)
            return 0.2f + ((currentSleepiness - 70f) / 30f);
            
        return 0f;
    }
    
    public override void OnEnter()
    {
        Debug.Log($"[SleepActivity] {pet.petName}: 수면 활동 시작 (졸림: {pet.Needs.Sleepiness:F1})");
        isPreparingToSleep = sleepingController.TryStartSleepingSequence();
        searchTimer = 0f;
    }
    
    public override void OnUpdate()
    {
        if (isPreparingToSleep)
        {
            sleepingController.UpdateMovementToSleep();
        }
        else
        {
            // 잠잘 곳을 찾지 못했을 때의 로직
            searchTimer += Time.deltaTime;
            if (searchTimer >= WIDE_WANDER_INTERVAL)
            {
                Debug.Log($"[SleepActivity] {pet.petName}: 잠잘 곳을 찾기 위해 주변을 넓게 탐색합니다.");
                searchTimer = 0f;
                
                // PetMovementController를 통해 더 넓은 반경으로 목적지를 설정
                pet.GetComponent<PetMovementController>()?.SetRandomDestination(SLEEP_SEARCH_RADIUS);
                
                // 졸리다는 감정 표현을 주기적으로 표시
                pet.ShowEmotion(EmotionType.Sleepy, WIDE_WANDER_INTERVAL);
                
                // 다시 잠잘 곳 탐색을 시도
                isPreparingToSleep = sleepingController.TryStartSleepingSequence();
            }
            
            pet.HandleRotation();
        }
    }
    
    public override void OnExit()
    {
        Debug.Log($"[SleepActivity] {pet.petName}: 수면 활동 종료");
        sleepingController.InterruptSleep();
    }
}