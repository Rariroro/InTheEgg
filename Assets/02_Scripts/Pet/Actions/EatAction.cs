// EatAction.cs

using UnityEngine;

public class EatAction : IPetAction
{
    private PetController _pet;
    private PetFeedingController _feedingController;
    private bool _hasFoundFood;

    // ▼▼▼ 음식을 찾지 못했을 때의 광역 배회 로직을 위한 변수 추가 ▼▼▼
    private float _searchTimer = 0f;
    private const float WIDE_WANDER_INTERVAL = 5.0f; // 5초마다 새로운 목적지 탐색
    private const float FOOD_SEARCH_RADIUS = 150f;   // 음식 탐색 시 배회 반경
    // ▲▲▲ 여기까지 추가 ▲▲▲

    public EatAction(PetController pet, PetFeedingController feedingController)
    {
        _pet = pet;
        _feedingController = feedingController;
    }

    public float GetPriority()
    {
        // ... (기존 GetPriority 로직은 변경 없음) ...
        if (_feedingController.IsEatingOrSeeking())
        {
            return 2.0f;
        }
        if (_pet.isClimbingTree) return 0f;
        if (_pet.hunger >= 70f)
        {
            return (_pet.hunger - 70f) / 30f;
        }
        return 0f;
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 식사 행동 시작.");
        _hasFoundFood = _feedingController.TryStartFeedingSequence();
        _searchTimer = 0f; // 탐색 타이머 초기화
    }

    public void OnUpdate()
    {
        if (_hasFoundFood)
        {
            _feedingController.UpdateMovementToFood();
        }
        // ▼▼▼ 음식을 찾지 못했을 때의 로직 추가 ▼▼▼
        else
        {
            // 음식을 찾을 때까지 또는 배고픔이 해결될 때까지 계속 주변을 배회합니다.
            _searchTimer += Time.deltaTime;
            if (_searchTimer >= WIDE_WANDER_INTERVAL)
            {
                Debug.Log($"{_pet.petName}이(가) 음식을 찾기 위해 주변을 넓게 탐색합니다.");
                _searchTimer = 0f;

                // PetMovementController를 통해 더 넓은 반경으로 목적지를 설정합니다.
                _pet.GetComponent<PetMovementController>()?.SetRandomDestination(FOOD_SEARCH_RADIUS);
            }
             _pet.HandleRotation();
        }
        // ▲▲▲ 여기까지 추가 ▲▲▲
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 식사 행동 종료.");
        _feedingController.CancelFeeding();
    }
}