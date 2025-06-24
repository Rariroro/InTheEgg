// WanderAction.cs
using UnityEngine;

public class WanderAction : IPetAction
{
    private PetController _pet;
    private PetMovementController _moveController; // 기존 MovementController의 기능 활용

    // 생성자: 필요한 컴포넌트를 미리 받아옴
    public WanderAction(PetController pet, PetMovementController moveController)
    {
        _pet = pet;
        _moveController = moveController;
    }

    public float GetPriority()
    {
        // 배회는 다른 모든 행동이 없을 때 수행하는 가장 낮은 우선순위를 가집니다.
        // 단, 펫이 선택되거나 들고 있는 등 특정 상태에서는 실행되지 않도록 0을 반환합니다.
        if (_pet.isSelected || _pet.isHolding || _pet.isGathering)
        {
            return 0f;
        }
        return 0.1f; // 매우 낮은 기본 우선순위
    }

    public void OnEnter()
    {
        // 배회 상태에 진입하면, PetMovementController의 행동 결정 로직을 시작시킵니다.
        // Debug.Log($"{_pet.petName}: 배회 행동 시작.");
        _moveController.DecideNextBehavior();
    }

    public void OnUpdate()
    {
        // PetMovementController의 업데이트 로직을 그대로 사용합니다.
        _moveController.UpdateMovement();
    }

    public void OnExit()
    {
        // 배회 상태를 벗어날 때, 현재 진행 중인 모든 움직임 코루틴을 멈춥니다.
        // Debug.Log($"{_pet.petName}: 배회 행동 종료.");
        _moveController.ForceStopCurrentBehavior();
    }
}