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
        // 모이기 명령이 활성화되어 있을 때 시작 가능 (GatheringInProgress 또는 GatheredWaiting 상태)
        return state.CurrentStatus == PetStatus.GatheringInProgress || 
               state.CurrentStatus == PetStatus.GatheredWaiting;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 플레이어 명령(모이기)은 높은 우선순위
        return 25.0f; // 플레이어 명령 우선 (20→25)
    }
    
    
    public override void Start()
    {
        // 상호작용 중이라면 강제로 중단
        if (pet.State.IsInteracting && pet.State.InteractionLogic != null)
        {
            var interactionLogic = pet.State.InteractionLogic;
            interactionLogic.StopAllCoroutines();
            
            // 상호작용 파트너가 있다면 그 쪽도 중단
            if (pet.State.InteractionPartner != null)
            {
                var partner = pet.State.InteractionPartner;
                
                // 파트너의 상호작용 로직도 중단
                if (partner.State.InteractionLogic != null)
                {
                    partner.State.InteractionLogic.StopAllCoroutines();
                }
                
                // 매니저에 종료 알림
                if (PetInteractionManager.Instance != null)
                {
                    PetInteractionManager.Instance.NotifyInteractionEnded(pet, partner);
                }
            }
            
            // Debug.Log($"{pet.petName}의 상호작용이 모이기 명령으로 인해 강제 중단되었습니다.");
        }
        
        // 코루틴 시작
        pet.StartCoroutine(EnterSequence());
    }
    
    private IEnumerator EnterSequence()
    {
        // Debug.Log($"[GatherActivity] {pet.petName}: 모이기 활동 시작");
        hasArrived = false;
        
        // 나무에 올라가고 있었다면, 강제로 내려오게 함
        if (pet.State.IsClimbingTree)
        {
            var treeClimber = pet.GetComponent<PetTreeClimbingController>();
            treeClimber?.ForceCancelClimbing();
            
            // NavMeshAgent가 다시 활성화되고 안정될 시간을 제공
            yield return null; 
        }
        
        // NavMeshAgent 안전성 검사 및 복구
        if (agent != null)
        {
            // 1. Agent가 비활성화되어 있다면 활성화
            if (!agent.enabled)
            {
                agent.enabled = true;
                yield return null; // 활성화 후 한 프레임 대기
            }
            
            // 2. Agent가 NavMesh 위에 없다면 가장 가까운 유효한 위치로 이동
            if (!agent.isOnNavMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(pet.transform.position, out hit, 5f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    // Debug.Log($"{pet.petName}: NavMesh 위로 재배치됨");
                }
                else
                {
                    // Debug.LogWarning($"{pet.petName}: NavMesh 위치를 찾을 수 없음");
                    yield break;
                }
            }
            
            // 3. Agent가 정지 상태라면 해제
            if (agent.isStopped)
            {
                agent.isStopped = false;
            }
            
            // 모이기 속도 설정
            agent.speed = pet.Movement.walkSpeed * SPEED_MULTIPLIER;
            agent.angularSpeed = pet.Movement.angularSpeed * ANGULAR_SPEED_MULTIPLIER;
            agent.acceleration = pet.Movement.acceleration * ACCELERATION_MULTIPLIER;
            agent.stoppingDistance = pet.Movement.stoppingDistance * STOPPING_DISTANCE_MULTIPLIER;
            
            agent.SetDestination(pet.State.GatherTargetPosition);
            agent.isStopped = false;
            
            // 뛰기 애니메이션
            if (pet.animator) 
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Run);
        }
    }
    
    public override void Update()
    {
        if (pet.movementController != null)
        {
            pet.movementController.HandleRotation();
        } 
        
        if (hasArrived || agent == null || !agent.enabled) 
            return;
        
        // 목표 지점 도착 체크
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            hasArrived = true;
            pet.State.SetGatheredState(); // 상태를 GatheredWaiting으로 전환
            agent.isStopped = true;
            
            // 정지 애니메이션
            if (pet.animator) 
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Idle);
            
            // 카메라 바라보기 로직 시작
            pet.StartCoroutine(LookAtCameraCoroutine());
        }
    }
    
    public override void Stop()
    {
        // Debug.Log($"[GatherActivity] {pet.petName}: 모이기 활동 종료");
        
        if (agent != null && agent.enabled)
        {
            // 속도 원래대로 복구
            agent.speed = pet.Movement.walkSpeed;
            agent.angularSpeed = pet.Movement.angularSpeed;
            agent.acceleration = pet.Movement.acceleration;
            agent.stoppingDistance = pet.Movement.stoppingDistance;
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
        
        // 펫이 모인 상태(GatheredWaiting)일 때만 카메라를 바라봄
        while (pet.State.CurrentStatus == PetStatus.GatheredWaiting && Quaternion.Angle(pet.transform.rotation, targetRotation) > 1.0f)
        {
            pet.transform.rotation = Quaternion.Slerp(pet.transform.rotation, targetRotation, pet.Movement.rotationSmoothness * Time.deltaTime);
            yield return null;
        }
    }
}