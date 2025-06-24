// EatAction.cs
using UnityEngine;

public class EatAction : IPetAction
{
    private PetController _pet;
    private PetFeedingController _feedingController;

    public EatAction(PetController pet, PetFeedingController feedingController)
    {
        _pet = pet;
        _feedingController = feedingController;
    }

    public float GetPriority()
    {
        // 배고픔 수치가 높을수록 우선순위가 높아집니다.
        // 70 이상일 때 행동을 고려하기 시작하고, 100에 가까울수록 1에 가까운 우선순위를 가집니다.
        if (_pet.hunger < 70f) return 0f;
        return (_pet.hunger - 70f) / 30f; // 70일때 0, 100일때 1
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 식사 행동 시작. (우선순위: {GetPriority()})");
        // PetFeedingController의 업데이트 로직을 한 번 호출하여 음식 탐색을 시작하게 합니다.
        _feedingController.UpdateFeeding();
    }

    public void OnUpdate()
    {
        // PetFeedingController의 업데이트 로직을 계속 호출합니다.
        _feedingController.UpdateFeeding();
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 식사 행동 종료.");
        // 식사 행동이 중단될 경우를 대비해 관련 상태를 초기화하는 로직이 필요하다면 여기에 추가합니다.
        // (예: targetFood = null)
    }
}