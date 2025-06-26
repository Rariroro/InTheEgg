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

  // EatAction.cs

public float GetPriority()
{
    // ★★★ 수정: 탈진 관련 특별 우선순위 로직을 제거합니다. (ExhaustedAction이 전담) ★★★
    
    // 이미 음식을 먹고 있거나 찾으러 가는 중이라면, 행동을 계속 유지합니다.
    if (_feedingController.IsEatingOrSeeking())
    {
        return 2.0f;
    }

    // 나무 위에 있다면 식사 행동을 시작하지 않습니다.
    if (_pet.isClimbingTree) return 0f;

    // 배고픔 수치가 70 이상일 때만 새로운 식사 행동을 시작할 수 있습니다.
    if (_pet.hunger >= 70f)
    {
        // 배고픔 수치에 비례하여 우선순위가 증가합니다 (0.0 ~ 1.0).
        return (_pet.hunger - 70f) / 30f;
    }

    // 그 외의 경우는 우선순위가 없습니다.
    return 0f;
}

   public void OnEnter()
{
    // Debug.Log($"{_pet.petName}: 식사 행동 시작.");
    
    // ★★★ 행동 시작 전에 음식 타겟 유효성 재확인 ★★★
    _feedingController.ValidateCurrentTargets();
    
    _hasFoundFood = _feedingController.TryStartFeedingSequence();
    _searchTimer = 0f;
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