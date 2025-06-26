// EatAction.cs

using UnityEngine;

public class EatAction : IPetAction
{
    private PetController _pet;
    private PetFeedingController _feedingController;
    private bool _hasFoundFood; // ★★★ 추가: 음식을 찾았는지 여부를 저장하는 상태 변수

    public EatAction(PetController pet, PetFeedingController feedingController)
    {
        _pet = pet;
        _feedingController = feedingController;
    }

   // Pet.zip/EatAction.cs

public float GetPriority()
{
    // 1. 이미 식사/탐색 중이라면, 다른 행동으로 전환되지 않도록 높은 우선순위를 유지합니다.
    if (_feedingController.IsEatingOrSeeking())
    {
        // 이 값은 배회(0.1)나 나무타기(0.3) 등 자율 행동보다는 높고,
        // 플레이어 선택(5.0), 상호작용(10.0), 모이기(20.0)보다는 낮아야 합니다.
        return 2.0f; // 예: 2.0의 고정 우선순위
    }

    // 2. 나무 위에서는 먹을 수 없습니다.
    if (_pet.isClimbingTree) return 0f;

    // 3. 배고픔 수치가 70 이상일 때만 새로운 식사 행동을 시작할 수 있습니다.
    if (_pet.hunger >= 70f)
    {
        // 욕구 기반 우선순위 계산 (기존 로직 유지)
        return (_pet.hunger - 70f) / 30f; // 0.0 ~ 1.0 사이의 값
    }

    // 그 외의 경우는 우선순위가 없습니다.
    return 0f;
}

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 식사 행동 시작.");
        // ★★★ 수정: 행동이 시작되면, 음식을 찾고 이동을 시작합니다.
        _hasFoundFood = _feedingController.TryStartFeedingSequence();
    }

    public void OnUpdate()
    {
        // ★★★ 수정: 음식을 성공적으로 찾았다면, 목표에 도착할 때까지 계속 상태를 업데이트합니다.
        if (_hasFoundFood)
        {
            _feedingController.UpdateMovementToFood();
        }
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 식사 행동 종료.");
        // ★★★ 추가: 행동이 중단될 경우, 진행중인 먹기 코루틴을 중지하고 목표를 초기화하는 것이 안전합니다.
        // 이 로직은 PetFeedingController에 추가하는 것이 좋습니다.
        // 예: _feedingController.CancelFeeding();
            _feedingController.CancelFeeding();

    }
}