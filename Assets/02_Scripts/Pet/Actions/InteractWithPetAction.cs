// Actions/InteractWithPetAction.cs

using UnityEngine;

public class InteractWithPetAction : IPetAction
{
    private PetController _pet;

    public InteractWithPetAction(PetController pet)
    {
        _pet = pet;
    }

    public float GetPriority()
    {
        // PetController의 isInteracting 플래그를 사용하여 우선순위를 결정합니다.
        return _pet.isInteracting ? 10.0f : 0f;
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 펫 간 상호작용 상태 진입.");
        _pet.StopMovement();

        // ★★★ 핵심 변경 ★★★
        // 이 Action 상태에 진입했을 때, 비로소 실제 상호작용 로직을 시작합니다.
        if (_pet.currentInteractionLogic != null && _pet.interactionPartner != null)
        {
            // 상호작용 로직을 PetController의 코루틴으로 실행합니다.
            _pet.StartCoroutine(
                _pet.currentInteractionLogic.PerformInteraction(_pet, _pet.interactionPartner)
            );
        }
        else
        {
            // 예외 처리: 상호작용 로직이나 파트너가 없으면 상태를 즉시 종료
            Debug.LogWarning($"{_pet.petName}의 상호작용 정보가 없어 즉시 종료합니다.");
            _pet.isInteracting = false; // 플래그를 내려서 다른 행동으로 전환 유도
        }
    }

    public void OnUpdate()
{
   
}

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 펫 간 상호작용 상태 종료.");
        
        // ★★★ 추가: 행동이 종료될 때, 진행 중이던 상호작용 코루틴을 확실히 중지시킵니다.
        _pet.StopAllCoroutines();
        
        // NavMeshAgent가 비활성화 되었을 수 있으므로 복구 로직이 필요할 수 있습니다.
        if (_pet.agent != null && !_pet.agent.enabled)
        {
            _pet.agent.enabled = true;
        }

        // isInteracting 플래그를 false로 설정합니다.
        _pet.isInteracting = false; 

        // 파트너 정보도 초기화
        _pet.interactionPartner = null;
        _pet.currentInteractionLogic = null;

        // 다른 펫에게도 상호작용 종료를 알릴 수 있습니다.
        if(PetInteractionManager.Instance != null)
        {
             PetInteractionManager.Instance.NotifyInteractionEnded(_pet, _pet.interactionPartner);
        }
    }
}