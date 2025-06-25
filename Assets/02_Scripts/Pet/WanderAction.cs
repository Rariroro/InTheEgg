// WanderAction.cs

using UnityEngine;

public class WanderAction : IPetAction
{
    private PetController _pet;
    private PetMovementController _moveController;

    public WanderAction(PetController pet, PetMovementController moveController)
    {
        _pet = pet;
        _moveController = moveController;
    }

    // ★★★ 수정: isInteracting 체크를 추가하여 우선순위를 더 명확하게 합니다. ★★★
    public float GetPriority()
    {
        // 배회는 다른 모든 행동이 없을 때 수행하는 가장 낮은 우선순위를 가집니다.
        // isInteracting과 isGathering은 PetController의 AI 루프에서 더 높은 우선순위의 Action으로 처리되므로,
        // 여기서는 isSelected나 isHolding 같은 직접적인 상태만 체크해도 충분합니다.
        if (_pet.isSelected || _pet.isHolding)
        {
            return 0f;
        }
        // 기본 행동이므로 항상 낮은 우선순위를 가짐
        return 0.1f;
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 배회 행동 시작.");
        // ★★★ 수정: 배회 상태에 진입하면, 즉시 다음 배회 행동(예: 걷기, 쉬기)을 결정하도록 합니다. ★★★
        _moveController.DecideNextBehavior();
    }

    public void OnUpdate()
    {
        // ★★★ 수정: PetMovementController의 새로운 핵심 메서드를 호출합니다. ★★★
        _moveController.ExecuteWanderBehavior();
        _pet.HandleRotation();
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 배회 행동 종료.");
        // ★★★ 변경 없음: 배회 상태를 벗어날 때, 현재 진행 중인 모든 움직임 코루틴을 멈추는 것은 유효합니다. ★★★
        _moveController.ForceStopCurrentBehavior();
    }
}