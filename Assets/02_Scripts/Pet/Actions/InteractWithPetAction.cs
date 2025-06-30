// Actions/InteractWithPetAction.cs (수정된 버전)

public class InteractWithPetAction : IPetAction
{
    private PetController _pet;

    public InteractWithPetAction(PetController pet)
    {
        _pet = pet;
    }

    public float GetPriority()
    {
        // 펫이 상호작용 중일 때, 다른 저순위 행동(배회 등)을 막기 위해 중간 정도의 우선순위를 유지합니다.
        // Eat, Sleep(우선순위 ~2.0) 보다는 낮고 Wander(0.1)보다는 높게 설정합니다.
        return _pet.isInteracting ? 1.5f : 0f;
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 상호작용 상태 유지 중. 다른 행동을 하지 않습니다.");
        // BasePetInteraction이 이미 이동을 제어하므로 여기서는 아무것도 할 필요가 없습니다.
        _pet.StopMovement(); 
    }

    public void OnUpdate()
    {
        // 모든 실제 행동은 BasePetInteraction 코루틴에서 일어나므로 여기서는 할 일이 없습니다.
    }

    public void OnExit()
    {
        // OnExit도 BasePetInteraction의 finally 블록에서 모든 정리를 하므로,
        // 여기서 특별히 정리할 내용은 없습니다.
        // isInteracting 플래그는 BasePetInteraction에서 해제됩니다.
    }
}