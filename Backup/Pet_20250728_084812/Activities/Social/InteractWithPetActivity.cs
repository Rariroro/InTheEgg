using UnityEngine;

/// <summary>
/// 펫이 다른 펫과 상호작용 중일 때의 활동
/// BasePetInteraction이 실제 상호작용을 제어하고, 이 활동은 상태만 유지합니다.
/// </summary>
public class InteractWithPetActivity : PetActivityAdapter
{
    public override string Name => "InteractWithPet";
    public override bool IsInterruptible => false; // 펫 간 상호작용은 중단 불가
    
    public InteractWithPetActivity(PetController petController) : base(petController)
    {
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 터치/홀드 상태에서는 펫 간 상호작용 중단
        if (pet.isHolding || pet.isSelected)
            return false;
            
        // 상호작용 중일 때만 시작 가능
        return pet.isInteracting;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 상호작용 중에는 중간 정도의 우선순위
        // Eat, Sleep(~2.0)보다는 낮고 Wander(0.1)보다는 높게
        return 1.5f;
    }
    
    // 기존 IPetAction 메서드 구현 (호환성)
    public override float GetPriority()
    {
        // 터치/홀드 상태에서는 펫 간 상호작용도 중단
        if (pet.isHolding || pet.isSelected)
            return 0f;
            
        // 펫이 상호작용 중일 때, 다른 저순위 행동을 막기 위해 중간 정도의 우선순위 유지
        return pet.isInteracting ? 1.5f : 0f;
    }
    
    public override void OnEnter()
    {
        Debug.Log($"[InteractWithPetActivity] {pet.petName}: 상호작용 상태 유지 중");
        // BasePetInteraction이 이미 이동을 제어하므로 여기서는 움직임만 중지
        pet.StopMovement(); 
    }
    
    public override void OnUpdate()
    {
        // 모든 실제 행동은 BasePetInteraction 코루틴에서 처리되므로
        // 여기서는 추가 작업 없음
    }
    
    public override void OnExit()
    {
        Debug.Log($"[InteractWithPetActivity] {pet.petName}: 상호작용 종료");
        // BasePetInteraction의 finally 블록에서 모든 정리를 수행하므로
        // 여기서는 특별한 정리 작업 없음
        // isInteracting 플래그는 BasePetInteraction에서 해제됨
    }
}