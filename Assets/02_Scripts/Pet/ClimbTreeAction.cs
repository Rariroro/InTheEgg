// ClimbTreeAction.cs
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
        // 1. 'Tree' 서식지 펫이 아니면 실행하지 않음
        if (_pet.habitat != PetAIProperties.Habitat.Tree) return 0f;

        // 2. 이미 나무에 있거나, 다른 중요한 행동(식사, 수면 등)을 하면 실행하지 않음
        if (_pet.isClimbingTree || _pet.hunger > 70f || _pet.sleepiness > 70f || _climbingController.IsSearchingForTree())
        {
            return 0f;
        }

        // 3. 설정된 확률(treeClimbChance)에 따라 우선순위를 가끔씩 높게 줌
        //    (매번 높은 우선순위를 주면 나무만 타려고 할 수 있으므로 Random.value 사용)
        if (Random.value < _pet.treeClimbChance * 0.1f) // 확률을 조금 낮춰서 체크
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
        // 행동이 완료되거나 다른 행동에 의해 중단되면 OnExit가 호출됩니다.
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 나무 오르기 행동 중단.");
        // 다른 고순위 행동(예: 갑자기 배고파짐)에 의해 중단될 경우,
        // 나무타기 상태를 강제로 취소합니다.
        _climbingController.ForceCancelClimbing();
    }
}