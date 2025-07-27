using UnityEngine;

/// <summary>
/// 펫의 수면 활동
/// </summary>
public class SleepActivity : IPetActivity
{
    private readonly PetController _pet;
    private readonly PetSleepingController _sleepingController;
    private readonly PetMovementController _movementController;
    
    private bool _isPreparingToSleep;
    private float _searchTimer = 0f;
    
    private const float WIDE_WANDER_INTERVAL = 5.0f;
    private const float SLEEP_SEARCH_RADIUS = 150f;
    
    public string Name => "Sleep";
    public bool IsComplete => false; // 수면은 자동으로 완료되지 않음
    public bool IsInterruptible => true;
    
    public SleepActivity(PetController pet, PetSleepingController sleepingController, PetMovementController movementController)
    {
        _pet = pet;
        _sleepingController = sleepingController;
        _movementController = movementController;
    }
    
    public bool CanStart(PetState state, PetNeeds needs)
    {
        // 플레이어가 제어 중이면 불가
        if (state.CurrentStatus == PetStatus.PlayerControl)
            return false;
            
        // 이미 수면 중이거나 찾는 중이면 가능
        if (_sleepingController.IsSleepingOrSeeking())
            return true;
            
        // 졸림 수치가 70 이상이면 시작 가능
        float currentSleepiness = needs?.Sleepiness ?? _pet.sleepiness;
        return currentSleepiness >= 70f;
    }
    
    public float GetPriority(PetState state, PetNeeds needs)
    {
        // 터치/홀드 상태에서는 우선순위 0
        if (_pet.isHolding || _pet.isSelected)
            return 0f;
            
        // 이미 잠을 자거나 찾는 중이면 높은 우선순위
        if (_sleepingController.IsSleepingOrSeeking())
            return 2.0f;
            
        // 졸림 수치에 따라 우선순위 결정
        float currentSleepiness = needs?.Sleepiness ?? _pet.sleepiness;
        if (currentSleepiness >= 70f)
        {
            return 0.2f + ((currentSleepiness - 70f) / 30f); // 0.2 ~ 1.2
        }
        
        return 0f;
    }
    
    public void Start()
    {
        _isPreparingToSleep = _sleepingController.TryStartSleepingSequence();
        _searchTimer = 0f;
    }
    
    public void Update()
    {
        if (_isPreparingToSleep)
        {
            _sleepingController.UpdateMovementToSleep();
        }
        else
        {
            // 잠잘 곳을 찾을 때까지 배회
            _searchTimer += Time.deltaTime;
            if (_searchTimer >= WIDE_WANDER_INTERVAL)
            {
                Debug.Log($"{_pet.petName}이(가) 잠잘 곳을 찾기 위해 주변을 넓게 탐색합니다.");
                _searchTimer = 0f;

                // 더 넓은 반경으로 탐색
                _movementController?.SetRandomDestination(SLEEP_SEARCH_RADIUS);
                
                // 졸리다는 감정 표현
                _pet.ShowEmotion(EmotionType.Sleepy, WIDE_WANDER_INTERVAL);

                // 다시 잠잘 곳 탐색 시도
                _isPreparingToSleep = _sleepingController.TryStartSleepingSequence();
            }
            
            _pet.HandleRotation();
        }
    }
    
    public void Stop()
    {
        _sleepingController.InterruptSleep();
    }
}