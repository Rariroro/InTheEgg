using UnityEngine;

/// <summary>
/// 펫이 플레이어에 의해 선택되었을 때의 활동
/// 움직임을 멈추고 플레이어(카메라)를 부드럽게 바라봅니다.
/// </summary>
public class SelectedActivity : PetActivityAdapter
{
    private readonly PetAnimationController animController;
    
    public override string Name => "Selected";
    public override bool IsInterruptible => true; // 선택 상태는 중단 가능
    
    public SelectedActivity(PetController petController) : base(petController)
    {
        animController = pet.GetComponent<PetAnimationController>();
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 펫이 선택되었고 들려있지 않을 때만 시작 가능
        return pet.State.IsSelected && !pet.State.IsHolding;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 나무 위에 있을 때는 약간 낮은 우선순위
        // 일반 상태에서는 높은 우선순위 (긴급 상황 제외 모든 행동보다 높음)
        return pet.State.IsClimbingTree ? 5.5f : 30.0f;
    }
    
    
    public override void Start()
    {
        Debug.Log($"[SelectedActivity] {pet.petName}: 선택된 상태 시작");
        
        // 현재 진행 중인 모든 움직임 관련 코루틴을 중지
        var moveController = pet.GetComponent<PetMovementController>();
        moveController?.ForceStopCurrentBehavior();
        
        // 나무에 오르지 않았을 때만 움직임을 멈춤
        if (!pet.State.IsClimbingTree)
        {
            if (pet.movementController != null)
            {
                pet.movementController.StopMovement();
            }
        }
        
        // 애니메이션을 즉시 Idle 상태로 전환
        animController?.SetContinuousAnimation((int)PetAnimationController.PetAnimationType.Idle);
    }
    
    public override void Update()
    {
        // 카메라를 부드럽게 바라봄 (나무 위에 있을 때는 제외)
        if (Camera.main != null && !pet.State.IsClimbingTree)
        {
            Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
            directionToCamera.y = 0; // Y축 고정
            
            if (directionToCamera != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                // 부드러운 회전
                pet.transform.rotation = Quaternion.Slerp(
                    pet.transform.rotation,
                    targetRotation,
                    pet.Movement.rotationSmoothness * 2f * Time.deltaTime // 조금 더 빠른 회전
                );
            }
        }
        else if (pet.State.IsClimbingTree)
        {
            Debug.Log($"[SelectedActivity] {pet.petName}: 나무 위에 있어서 회전하지 않음");
        }
    }
    
    public override void Stop()
    {
        Debug.Log($"[SelectedActivity] {pet.petName}: 선택된 상태 종료");
        // 특별한 정리 작업 없음
        // 다음 활동이 필요한 설정을 수행할 것임
    }
}