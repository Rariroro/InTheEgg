// InteractWithPetAction.cs
using UnityEngine;

public class InteractWithPetAction : IPetAction
{
    private PetController _pet;
    // 실제 상호작용 로직은 PetInteractionManager 또는 별도의 상호작용 핸들러가 담당
    // 이 액션은 상호작용 중 다른 행동을 하지 않도록 막는 '상태' 역할에 집중합니다.

    public InteractWithPetAction(PetController pet)
    {
        _pet = pet;
    }

    public float GetPriority()
    {
        // PetController의 isInteracting 플래그를 사용하여 우선순위를 결정합니다.
        // 이 플래그는 PetInteractionManager가 설정해줍니다.
        return _pet.isInteracting ? 10.0f : 0f; // 최상위 우선순위
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 펫 간 상호작용 상태 진입.");
        // PetInteractionManager가 이미 움직임을 제어하고 있으므로, 여기서는 특별한 로직이 필요 없을 수 있습니다.
        // 또는 상호작용 시작 애니메이션 등을 여기서 제어할 수 있습니다.
        _pet.StopMovement();
    }

    public void OnUpdate()
    {
        // PetInteractionManager가 펫들을 직접 제어하므로,
        // 이 액션의 Update는 비워두거나, 파트너를 계속 바라보게 하는 등의 보조 로직을 넣을 수 있습니다.
        if (_pet.interactionPartner != null)
        {
            Vector3 direction = _pet.interactionPartner.transform.position - _pet.transform.position;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                _pet.transform.rotation = Quaternion.Slerp(_pet.transform.rotation, targetRotation, Time.deltaTime * _pet.rotationSpeed);
            }
        }
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 펫 간 상호작용 상태 종료.");
        // PetInteractionManager에서 isInteracting을 false로 만들면 이 액션이 종료되고,
        // 다음 프레임에 WanderAction 같은 다른 행동으로 자연스럽게 전환됩니다.
    }
}