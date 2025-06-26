// SleepAction.cs

using UnityEngine;

public class SleepAction : IPetAction
{
    private PetController _pet;
    private PetSleepingController _sleepingController;
    private bool _isPreparingToSleep; // ★★★ 추가: 잠잘 준비를 시작했는지 여부

    public SleepAction(PetController pet, PetSleepingController sleepingController)
    {
        _pet = pet;
        _sleepingController = sleepingController;
    }

  // Pet.zip/SleepAction.cs

public float GetPriority()
{
    // 1. 이미 잠을 자거나 잠잘 곳을 찾는 중이라면, 높은 우선순위를 유지하여 행동을 중단시키지 않습니다.
    if (_sleepingController.IsSleepingOrSeeking())
    {
        return 2.0f; // 식사 행동과 동일한 우선순위 레벨을 부여하여 안정성을 확보합니다.
    }

    // 2. 졸음 수치가 70 이상일 때만 새로운 수면 행동을 시작할 수 있습니다.
    if (_pet.sleepiness >= 70f)
    {
        // 욕구에 따라 우선순위가 증가합니다.
        return (_pet.sleepiness - 70f) / 30f; // 0.0 ~ 1.0 사이의 값
    }

    // 그 외의 경우는 우선순위가 없습니다.
    return 0f;
}
    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 수면 행동 시작.");
        // ★★★ 수정: 행동이 시작되면 잠잘 곳을 찾고 이동을 시작합니다.
        _isPreparingToSleep = _sleepingController.TryStartSleepingSequence();
    }

    public void OnUpdate()
    {
        // ★★★ 수정: 잠잘 준비를 시작했다면, 목표에 도착할 때까지 계속 상태를 업데이트합니다.
        if (_isPreparingToSleep)
        {
            // 나무 펫의 경우, TryStartSleepingSequence에서 시작된 코루틴이 모든 것을 처리합니다.
            // 지상 펫의 경우, 도착 여부를 계속 체크해야 합니다.
            _sleepingController.UpdateMovementToSleep();
        }
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 수면 행동 종료.");
        // ★★★ 추가: 잠자는 도중에 방해받을 경우를 대비해 InterruptSleep을 호출하는 것이 안전합니다.
        _sleepingController.InterruptSleep();
    }
}