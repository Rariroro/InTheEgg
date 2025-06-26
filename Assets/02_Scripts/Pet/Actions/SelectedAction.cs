using UnityEngine;

/// <summary>
/// 펫이 플레이어에 의해 선택되었을 때의 행동을 정의합니다.
/// 이 상태에서는 움직임을 멈추고 플레이어(카메라)를 부드럽게 바라봅니다.
/// </summary>
public class SelectedAction : IPetAction
{
    private readonly PetController _pet;
    private readonly PetAnimationController _animController;

    public SelectedAction(PetController pet)
    {
        _pet = pet;
        _animController = pet.GetComponent<PetAnimationController>();
    }

    public float GetPriority()
    {
        // 펫이 '선택'되었고, '들려있지' 않을 때 높은 우선순위를 가집니다. (5.0f)
        // 이 우선순위는 일반 배회(0.1)보다는 높고, 상호작용(10.0)이나 모이기(20.0)보다는 낮습니다.
        return (_pet.isSelected && !_pet.isHolding) ? 5.0f : 0f;
    }

    public void OnEnter()
    {
        // 행동이 시작되면, 현재 진행 중인 모든 움직임 관련 코루틴을 중지합니다.
        var moveController = _pet.GetComponent<PetMovementController>();
        moveController?.ForceStopCurrentBehavior();

        // 나무에 오르지 않았을 때만 움직임을 멈춥니다.
        if (!_pet.isClimbingTree)
        {
            _pet.StopMovement();
        }

        // 애니메이션을 즉시 Idle(0) 상태로 전환합니다.
        _animController?.SetContinuousAnimation(0);
    }

    public void OnUpdate()
    {
        // 행동이 활성화된 동안, 매 프레임 카메라를 부드럽게 바라봅니다.
        // 나무 위에 있을 때는 몸을 돌리지 않습니다.
        if (Camera.main != null && !_pet.isClimbingTree)
        {
            Vector3 directionToCamera = Camera.main.transform.position - _pet.transform.position;
            directionToCamera.y = 0; // 펫이 위아래로 기울지 않도록 Y축 고정

            if (directionToCamera != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                // 펫의 부모 오브젝트를 회전시켜 모델과 NavMeshAgent의 방향을 일치시킵니다.
                _pet.transform.rotation = Quaternion.Slerp(
                    _pet.transform.rotation,
                    targetRotation,
                    _pet.rotationSpeed * 2f * Time.deltaTime // 조금 더 빠른 속도로 회전
                );
            }
        }
    }

    public void OnExit()
    {
        // 이 행동이 끝날 때 특별히 정리할 내용은 없습니다.
        // 새로운 행동의 OnEnter()가 다음 상태를 설정할 것입니다.
    }
}