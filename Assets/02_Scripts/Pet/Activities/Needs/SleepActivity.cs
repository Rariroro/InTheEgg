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
        if (pet.State.IsHolding || pet.State.IsSelected)
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

        // 이미 잠을 자거나 잠잘 곳을 찾는 중이라면 매우 높은 우선순위
        if (sleepingController.IsSleepingOrSeeking())
            return 30.0f; // 수면 중에는 거의 중단되지 않도록

        // 졸림 수치에 비례하여 우선순위 증가
        float sleepiness = needs != null ? needs.Sleepiness : pet.Needs.Sleepiness;

        // 생존 본능을 반영한 우선순위
        // 70일 때: 8.0, 85일 때: 15.0, 100일 때: 25.0
        if (sleepiness >= 85f)
        {
            // 매우 졸림 - 긴급 수준
            return 15.0f + ((sleepiness - 85f) / 15f * 10.0f); // 15.0 ~ 25.0
        }
        else if (sleepiness >= 70f)
        {
            // 졸림 시작 - 중간 수준
            return 8.0f + ((sleepiness - 70f) / 15f * 7.0f); // 8.0 ~ 15.0
        }

        return 0f;
    }
    
    
    public override void Start()
    {
        // Debug.Log($"[SleepActivity] {pet.petName}: 수면 활동 시작 (졸림: {pet.Needs.Sleepiness:F1})");
        isPreparingToSleep = sleepingController.TryStartSleepingSequence();
        searchTimer = 0f;
    }
    
    public override void Update()
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
                // Debug.Log($"[SleepActivity] {pet.petName}: 잠잘 곳을 찾기 위해 주변을 넓게 탐색합니다.");
                searchTimer = 0f;
                
                // PetMovementController를 통해 더 넓은 반경으로 목적지를 설정
                pet.GetComponent<PetMovementController>()?.SetRandomDestination(SLEEP_SEARCH_RADIUS);
                
                // 졸리다는 감정 표현을 주기적으로 표시
                if (pet.emotionController != null)
                {
                    pet.emotionController.ShowEmotion(EmotionType.Sleepy, WIDE_WANDER_INTERVAL);
                }
                
                // 다시 잠잘 곳 탐색을 시도
                isPreparingToSleep = sleepingController.TryStartSleepingSequence();
            }
            
            if (pet.movementController != null)
            {
                pet.movementController.HandleRotation();
            }
        }
    }
    
    public override void Stop()
    {
        // Debug.Log($"[SleepActivity] {pet.petName}: 수면 활동 종료");
        sleepingController.InterruptSleep();
    }
}