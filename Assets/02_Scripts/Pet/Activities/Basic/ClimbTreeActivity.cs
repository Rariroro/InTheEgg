using UnityEngine;
using PetAIProperties = PetTraits;
/// <summary>
/// 펫의 나무 오르기 활동을 담당하는 클래스
/// </summary>
public class ClimbTreeActivity : PetActivityAdapter
{
    private readonly PetTreeClimbingController climbingController;
    private readonly PetAnimationController animController;
    
    private bool wasSelectedLastFrame = false;
    
    public override string Name => "ClimbTree";
    public override bool IsInterruptible => true; // 나무 오르기는 중단 가능
    
    public ClimbTreeActivity(PetController petController, PetTreeClimbingController climbing) : base(petController)
    {
        climbingController = climbing;
        animController = pet.GetComponent<PetAnimationController>();
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 터치/홀드 상태에서는 나무 오르기 중단
        if (pet.State.IsHolding || (pet.State.IsSelected && !pet.State.IsClimbingTree))
            return false;
            
        // 이미 나무에 오르기 시작했거나, 나무 위에 있다면 계속
        if (pet.State.IsClimbingTree || climbingController.IsSearchingForTree())
            return true;
            
        // 'Tree' 서식지 펫이 아니면 실행하지 않음
        if (pet.habitat != PetAIProperties.Habitat.Tree)
            return false;
            
        // 다른 중요한 행동(식사, 수면 등)을 하면 실행하지 않음
        float hunger = needs != null ? needs.Hunger : pet.Needs.Hunger;
        float sleepiness = needs != null ? needs.Sleepiness : pet.Needs.Sleepiness;
        if (hunger > 70f || sleepiness > 70f)
            return false;
            
        // 설정된 확률에 따라 가끔씩 시작
        return Random.value < pet.treeClimbChance * 0.1f;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 이미 나무에 오르기 시작했거나, 나무 위에 있다면 높은 우선순위
        if (pet.State.IsClimbingTree || climbingController.IsSearchingForTree())
        {
            // SelectedAction(5.5f)보다 높은 우선순위로 설정
            return 6.0f;
        }
        
        // 나무 오르기 시작 확률
        return 0.3f;
    }
    
    // 기존 IPetAction 메서드 구현 (호환성)
    public override float GetPriority()
    {
        // 터치/홀드 상태에서는 나무 오르기도 중단
        if (pet.State.IsHolding || (pet.State.IsSelected && !pet.State.IsClimbingTree))
            return 0f;
            
        // 이미 나무에 오르기 시작했거나, 나무 위에 있다면 최상위 우선순위
        if (pet.State.IsClimbingTree || climbingController.IsSearchingForTree())
        {
            return 6.0f;
        }
        
        // 'Tree' 서식지 펫이 아니면 실행하지 않음
        if (pet.habitat != PetAIProperties.Habitat.Tree) 
            return 0f;
        
        // 다른 중요한 행동(식사, 수면 등)을 하면 실행하지 않음
        if (pet.Needs.Hunger > 70f || pet.Needs.Sleepiness > 70f)
        {
            return 0f;
        }
        
        // 설정된 확률에 따라 우선순위를 가끔씩 높게 줌
        if (Random.value < pet.treeClimbChance * 0.1f)
        {
            return 0.3f;
        }
        
        return 0f;
    }
    
    public override void OnEnter()
    {
        Debug.Log($"[ClimbTreeActivity] {pet.petName}: 나무 오르기 활동 시작");
        
        // 이미 나무 위에 있는 상태에서 이 액션이 다시 시작될 수 있으므로,
        // isClimbingTree가 false일 때만 새로 나무를 찾도록 함
        if (!pet.State.IsClimbingTree)
        {
            pet.StartCoroutine(climbingController.SearchAndClimbTreeRegularly());
        }
    }
    
    public override void OnUpdate()
    {
        // 나무 위에 있을 때 펫이 선택되었다면, 카메라를 바라보도록 처리
        if (pet.State.IsClimbingTree && pet.State.IsSelected)
        {
            // 1. 카메라 바라보기
            if (Camera.main != null)
            {
                Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
                directionToCamera.y = 0; // Y축 고정
                
                if (directionToCamera != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                    // 나무 위에서는 NavMeshAgent가 비활성화되어 있으므로 transform 직접 회전
                    pet.transform.rotation = Quaternion.Slerp(
                        pet.transform.rotation,
                        targetRotation,
                        pet.Movement.rotationSmoothness * 2f * Time.deltaTime
                    );
                }
            }
            
            // 2. 애니메이션 처리
            // 선택 상태가 바뀔 때만 애니메이션 설정
            if (!wasSelectedLastFrame && !pet.isAnimationLocked)
            {
                // 나무 위에서 선택되었을 때는 Idle 애니메이션
                animController?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
                wasSelectedLastFrame = true;
            }
        }
        else
        {
            wasSelectedLastFrame = false;
        }
    }
    
    public override void OnExit()
    {
        Debug.Log($"[ClimbTreeActivity] {pet.petName}: 나무 오르기 활동 중단");
        
        // 다른 고순위 행동에 의해 중단될 경우, 나무타기 상태를 강제로 취소
        climbingController.ForceCancelClimbing();
    }
}