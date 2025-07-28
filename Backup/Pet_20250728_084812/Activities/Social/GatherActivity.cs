using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 펫의 모이기 활동을 담당하는 클래스
/// </summary>
public class GatherActivity : PetActivityAdapter
{
    private readonly NavMeshAgent agent;
    private bool hasArrived = false;
    
    // 모이기 행동 시 적용할 속도 배율
    private const float SPEED_MULTIPLIER = 4f;
    private const float ANGULAR_SPEED_MULTIPLIER = 4f;
    private const float ACCELERATION_MULTIPLIER = 4f;
    private const float STOPPING_DISTANCE_MULTIPLIER = 3f;
    
    public override string Name => "Gather";
    public override bool IsInterruptible => false; // 모이기는 중단 불가
    
    public GatherActivity(PetController petController) : base(petController)
    {
        agent = pet.agent;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 모이기 명령이 활성화되어 있을 때만 시작 가능
        return pet.isGathering;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 모이기는 최상위 우선순위
        return 20.0f;
    }
    
    // 기존 IPetAction 메서드 구현 (호환성)
    public override float GetPriority()
    {
        return pet.isGathering ? 20.0f : 0f;
    }
    
    public override void OnEnter()
    {
        // 코루틴 시작
        pet.StartCoroutine(EnterSequence());
    }
    
    private IEnumerator EnterSequence()
    {
        Debug.Log($"[GatherActivity] {pet.petName}: 모이기 활동 시작");
        hasArrived = false;
        pet.isGathered = false; // 도착 상태 초기화
        
        // 나무에 올라가고 있었다면, 강제로 내려오게 함
        if (pet.isClimbingTree)
        {
            var treeClimber = pet.GetComponent<PetTreeClimbingController>();
            treeClimber?.ForceCancelClimbing();
            
            // NavMeshAgent가 다시 활성화되고 안정될 시간을 제공
            yield return null; 
        }
        
        if (agent != null && agent.enabled)
        {
            // 모이기 속도 설정
            agent.speed = pet.baseSpeed * SPEED_MULTIPLIER;
            agent.angularSpeed = pet.baseAngularSpeed * ANGULAR_SPEED_MULTIPLIER;
            agent.acceleration = pet.baseAcceleration * ACCELERATION_MULTIPLIER;
            agent.stoppingDistance = pet.baseStoppingDistance * STOPPING_DISTANCE_MULTIPLIER;
            
            agent.SetDestination(pet.gatherTargetPosition);
            agent.isStopped = false;
            
            // 뛰기 애니메이션
            if (pet.animator) 
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Run);
        }
    }
    
    public override void OnUpdate()
    {
        pet.HandleRotation(); 
        
        if (hasArrived || agent == null || !agent.enabled) 
            return;
        
        // 목표 지점 도착 체크
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            hasArrived = true;
            pet.isGathered = true; // 도착 완료 플래그 설정
            agent.isStopped = true;
            
            // 정지 애니메이션
            if (pet.animator) 
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Idle);
            
            // 카메라 바라보기 로직 시작
            pet.StartCoroutine(LookAtCameraCoroutine());
        }
    }
    
    public override void OnExit()
    {
        Debug.Log($"[GatherActivity] {pet.petName}: 모이기 활동 종료");
        pet.isGathered = false; 
        
        if (agent != null && agent.enabled)
        {
            // 속도 원래대로 복구
            agent.speed = pet.baseSpeed;
            agent.angularSpeed = pet.baseAngularSpeed;
            agent.acceleration = pet.baseAcceleration;
            agent.stoppingDistance = pet.baseStoppingDistance;
            agent.isStopped = false;
        }
        
        // 애니메이션 정상화
        pet.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
        
        // 실행 중인 코루틴 중지
        pet.StopCoroutine("EnterSequence");
        pet.StopCoroutine("LookAtCameraCoroutine");
    }
    
    private IEnumerator LookAtCameraCoroutine()
    {
        if (Camera.main == null) 
            yield break;
        
        // 현재 위치를 계속 유지하도록 isStopped를 true로 유지
        if (agent != null && agent.enabled) 
            agent.isStopped = true;
        
        Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
        directionToCamera.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
        
        // 펫이 여전히 모이기 상태일 때만 카메라를 바라봄
        while (pet.isGathering && Quaternion.Angle(pet.transform.rotation, targetRotation) > 1.0f)
        {
            pet.transform.rotation = Quaternion.Slerp(pet.transform.rotation, targetRotation, pet.rotationSpeed * Time.deltaTime);
            yield return null;
        }
    }
}