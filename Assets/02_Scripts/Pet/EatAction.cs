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

    public float GetPriority()
    {
        // 배고픔 수치가 70 이상이고, 현재 먹고 있거나 찾는 중이 아닐 때 우선순위를 계산합니다.
        if (_pet.hunger < 70f || _feedingController.IsEatingOrSeeking()) return 0f;
        
        // 나무 위에서는 먹을 수 없으므로 우선순위 0
        if (_pet.isClimbingTree) return 0f;

        return (_pet.hunger - 70f) / 30f;
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