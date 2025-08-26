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
        if (pet.State.IsHolding || pet.State.IsSelected)
        {
            // 상호작용 중이었다면 즉시 false 반환하여 활동 전환 유도
            if (pet.State.IsInteracting)
            {
                Debug.Log($"[InteractWithPetActivity] {pet.petName}: 터치/홀드로 인해 상호작용 불가");
            }
            return false;
        }
            
        // 모이기 중이거나 모인 상태에서는 상호작용 불가
        if (pet.State.CurrentStatus == PetStatus.GatheringInProgress || 
            pet.State.CurrentStatus == PetStatus.GatheredWaiting)
            return false;
            
        // 상호작용 중일 때만 시작 가능
        return pet.State.IsInteracting;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 상호작용 중에는 중간 정도의 우선순위
        // Eat, Sleep(~2.0)보다는 낮고 Wander(0.1)보다는 높게
        return 1.5f;
    }
    
    
    public override void Start()
    {
        // Debug.Log($"[InteractWithPetActivity] {pet.petName}: 상호작용 상태 유지 중");
        // BasePetInteraction이 이미 이동을 제어하므로 여기서는 움직임만 중지
        if (pet.movementController != null)
        {
            pet.movementController.StopMovement();
        } 
    }
    
    public override void Update()
    {
        // 모든 실제 행동은 BasePetInteraction 코루틴에서 처리되므로
        // 여기서는 추가 작업 없음
    }
    
    public override void Stop()
    {
        Debug.Log($"[InteractWithPetActivity] {pet.petName}: 상호작용 활동 종료");
        
        // 상호작용이 아직 남아있다면 강제로 정리
        if (pet.State.IsInteracting)
        {
            Debug.LogWarning($"[InteractWithPetActivity] {pet.petName}: 상호작용 상태가 남아있어 강제 정리");
            pet.State.EndInteraction();
            pet.State.SetInteractionLogic(null);
        }
        
        // 애니메이션 정리
        var animController = pet.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.StopContinuousAnimation();
        }
        
        // AI 재평가 트리거
        if (pet.AI != null)
        {
            Debug.Log($"[InteractWithPetActivity] {pet.petName}: AI 재평가 요청");
            // 약간의 지연 후 AI 재평가 (즉시 하면 충돌 가능)
            pet.StartCoroutine(TriggerAIRestart(0.1f));
        }
    }
    
    private System.Collections.IEnumerator TriggerAIRestart(float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        
        if (pet.AI != null)
        {
            pet.AI.UpdateAI();
        }
    }
}