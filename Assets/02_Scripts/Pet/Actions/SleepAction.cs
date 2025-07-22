// Actions/SleepAction.cs

using UnityEngine;

public class SleepAction : IPetAction
{
    private PetController _pet;
    private PetSleepingController _sleepingController;
    private bool _isPreparingToSleep;

    // ★★★ 추가: 잠잘 곳을 찾지 못했을 때의 광역 배회 로직을 위한 변수 추가 ★★★
    private float _searchTimer = 0f;
    private const float WIDE_WANDER_INTERVAL = 5.0f; // 5초마다 새로운 목적지 탐색
    private const float SLEEP_SEARCH_RADIUS = 150f;  // 잠잘 곳 탐색 시 배회 반경

    public SleepAction(PetController pet, PetSleepingController sleepingController)
    {
        _pet = pet;
        _sleepingController = sleepingController;
    }

    public float GetPriority()
    {
        // 터치/홀드 상태에서는 잠자기도 중단
        if (_pet.isHolding || _pet.isSelected)
            return 0f;
            
        // 이미 잠을 자거나 잠잘 곳을 찾는 중이라면, 높은 우선순위를 유지합니다.
        if (_sleepingController.IsSleepingOrSeeking())
        {
            return 2.0f;
        }

        // ★★★ 수정: 졸음 수치가 70 이상일 때, WanderAction(0.1f)보다 즉시 높은 우선순위를 갖도록 변경 ★★★
        if (_pet.sleepiness >= 70f)
        {
            // 기본 우선순위 0.2f를 부여하고, 졸음 수치에 따라 점진적으로 증가시킵니다.
            return 0.2f + ((_pet.sleepiness - 70f) / 30f); // 0.2 ~ 1.2 사이의 값
        }

        return 0f;
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 수면 행동 시작.");
        _isPreparingToSleep = _sleepingController.TryStartSleepingSequence();
        
        // ★★★ 추가: 탐색 타이머 초기화 ★★★
        _searchTimer = 0f;
    }

    public void OnUpdate()
    {
        if (_isPreparingToSleep)
        {
            _sleepingController.UpdateMovementToSleep();
        }
        // ★★★ 추가: 잠잘 곳을 찾지 못했을 때의 로직 (EatAction 패턴 적용) ★★★
        else
        {
            // 잠잘 곳을 찾을 때까지 또는 졸음이 해결될 때까지 계속 주변을 배회합니다.
            _searchTimer += Time.deltaTime;
            if (_searchTimer >= WIDE_WANDER_INTERVAL)
            {
                Debug.Log($"{_pet.petName}이(가) 잠잘 곳을 찾기 위해 주변을 넓게 탐색합니다.");
                _searchTimer = 0f;

                // PetMovementController를 통해 더 넓은 반경으로 목적지를 설정합니다.
                _pet.GetComponent<PetMovementController>()?.SetRandomDestination(SLEEP_SEARCH_RADIUS);
                
                // 졸리다는 감정 표현을 주기적으로 보여줍니다.
                _pet.ShowEmotion(EmotionType.Sleepy, WIDE_WANDER_INTERVAL);

                // ★★★ 중요: 다시 잠잘 곳 탐색을 시도합니다. ★★★
                _isPreparingToSleep = _sleepingController.TryStartSleepingSequence();
            }
             _pet.HandleRotation();
        }
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 수면 행동 종료.");
        _sleepingController.InterruptSleep();
    }
}