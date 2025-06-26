// Pet.zip/ClimbTreeAction.cs

using UnityEngine;

public class ClimbTreeAction : IPetAction
{
    private readonly PetController _pet;
    private readonly PetTreeClimbingController _climbingController;

    public ClimbTreeAction(PetController pet, PetTreeClimbingController climbingController)
    {
        _pet = pet;
        _climbingController = climbingController;
    }

    public float GetPriority()
    {
        // 1. 이미 나무에 오르기 시작했거나, 나무 위에 있다면 높은 우선순위를 유지하여 행동이 중단되지 않도록 함
        if (_pet.isClimbingTree || _climbingController.IsSearchingForTree())
        {
            // 플레이어의 직접적인 명령(모이기, 들기)보다는 낮고, 일반 욕구(식사, 수면)보다는 높은 우선순위(예: 4.0f)를 반환
            return 4.0f;
        }

        // 2. 'Tree' 서식지 펫이 아니면 실행하지 않음
        if (_pet.habitat != PetAIProperties.Habitat.Tree) return 0f;

        // 3. 다른 중요한 행동(식사, 수면 등)을 하면 실행하지 않음
        if (_pet.hunger > 70f || _pet.sleepiness > 70f)
        {
            return 0f;
        }

        // 4. 설정된 확률(treeClimbChance)에 따라 우선순위를 가끔씩 높게 줌 (새로운 행동 시작 조건)
        if (Random.value < _pet.treeClimbChance * 0.1f)
        {
            // 나무에 오르는 것은 일반 배회(0.1)보다는 높은 우선순위를 가짐
            return 0.3f;
        }

        return 0f;
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 나무 오르기 행동 시작.");
        // PetTreeClimbingController의 핵심 로직(나무 찾고 오르기)을 코루틴으로 실행
        _pet.StartCoroutine(_climbingController.SearchAndClimbTreeRegularly());
    }

    public void OnUpdate()
    {
        // OnEnter에서 시작된 코루틴이 모든 로직을 처리하므로, 여기서는 할 일이 없습니다.
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 나무 오르기 행동 중단.");
        // 다른 고순위 행동(예: 모이기 명령)에 의해 중단될 경우,
        // 나무타기 상태를 강제로 취소합니다.
        _climbingController.ForceCancelClimbing();
    }
}