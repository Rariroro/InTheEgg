// SleepAction.cs
using UnityEngine;

public class SleepAction : IPetAction
{
    private PetController _pet;
    private PetSleepingController _sleepingController;

    public SleepAction(PetController pet, PetSleepingController sleepingController)
    {
        _pet = pet;
        _sleepingController = sleepingController;
    }

    public float GetPriority()
    {
        // 졸음 수치가 높을수록 우선순위가 높아집니다.
        if (_pet.sleepiness < 70f) return 0f;
        return (_pet.sleepiness - 70f) / 30f;
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 수면 행동 시작. (우선순위: {GetPriority()})");
        _sleepingController.UpdateSleeping();
    }

    public void OnUpdate()
    {
        _sleepingController.UpdateSleeping();
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 수면 행동 종료.");
        // 잠이 깰 때를 대비한 로직 (이미 InterruptSleep에 구현되어 있음)
    }
}