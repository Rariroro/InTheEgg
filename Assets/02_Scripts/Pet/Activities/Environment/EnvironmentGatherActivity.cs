using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫이 환경에 이끌려 모이는 활동 (환경 스폰 시 축하 행동)
/// </summary>
public class EnvironmentGatherActivity : PetActivityAdapter
{
    private readonly NavMeshAgent agent;
    private bool isGathering; // 행동이 진행 중인지 추적
    
    // 환경 모임 시 적용할 속도 배율
    private const float SPEED_MULTIPLIER = 3f;
    private const float CELEBRATION_DURATION = 5f; // 축하 시간
    
    public override string Name => "EnvironmentGather";
    public override bool IsInterruptible => true; // 환경 모이기는 중단 가능
    
    public EnvironmentGatherActivity(PetController petController) : base(petController)
    {
        agent = pet.agent;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 환경에 이끌리는 상태일 때만 시작 가능
        return pet.State.IsAttractedToEnvironment;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 환경 모이기는 높은 우선순위 (일반 모이기보다는 낮음)
        return 15.0f;
    }
    
    
    public override void Start()
    {
        isGathering = true;
        pet.StartCoroutine(EnterSequence());
    }
    
    private IEnumerator EnterSequence()
    {
        // Debug.Log($"[EnvironmentGatherActivity] {pet.petName}: 환경에 이끌려 모이기 시작");
        
        // 나무에서 내려오기
        if (pet.State.IsClimbingTree)
        {
            var treeClimber = pet.GetComponent<PetTreeClimbingController>();
            if (treeClimber != null)
            {
                treeClimber.ForceCancelClimbing();
                yield return new WaitForSeconds(0.5f); // NavMeshAgent 안정화 대기
            }
        }
        
        if (agent != null && agent.enabled)
        {
            agent.speed = pet.baseSpeed * SPEED_MULTIPLIER;
            agent.acceleration = pet.baseAcceleration * SPEED_MULTIPLIER;
            agent.SetDestination(pet.State.EnvironmentTargetPosition);
            agent.isStopped = false;
            
            if (pet.animator) 
                pet.animator.SetInteger("animation", 2); // 달리기 애니메이션
        }
    }
    
    public override void Update()
    {
        if (!isGathering) return;
        
        if (pet.movementController != null)
        {
            pet.movementController.HandleRotation();
        }
        
        // 도착 체크 (거리를 넉넉하게 2.5f로 설정)
        if (agent != null && agent.enabled && !agent.pathPending && agent.remainingDistance <= 2.5f)
        {
            isGathering = false; // 더 이상 이동 업데이트는 하지 않음
            pet.StartCoroutine(CelebrateArrivalCoroutine());
        }
    }
    
    public override void Stop()
    {
        // Debug.Log($"[EnvironmentGatherActivity] {pet.petName}: 환경 모이기 중단");
        
        // 다른 고순위 행동에 의해 중단될 경우
        isGathering = false;
        // ★ [Phase 4] PetState를 통한 상태 업데이트
        pet.State.SetEnvironmentAttraction(false);
        
        // 펫의 이동 속도를 원래대로 복구
        if (agent != null && agent.enabled)
        {
            agent.speed = pet.baseSpeed;
            agent.acceleration = pet.baseAcceleration;
            agent.isStopped = false;
        }
        
        pet.StopAllCoroutines(); // 이 Activity에서 실행한 모든 코루틴 중지
    }
    
    /// <summary>
    /// 환경에 도착한 후 축하 행동
    /// </summary>
    private IEnumerator CelebrateArrivalCoroutine()
    {
        // 1. 도착 후 정지 및 방향 전환
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            // 환경의 중심을 바라보게 함
            Vector3 lookDirection = (pet.State.EnvironmentTargetPosition - pet.transform.position).normalized;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                pet.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
        
        // 2. 축하 애니메이션 및 이모티콘 표시
        if (pet.emotionController != null)
        {
            pet.emotionController.ShowEmotion(EmotionType.Happy, CELEBRATION_DURATION);
        }
        float celebrationTime = 0f;
        
        while (celebrationTime < CELEBRATION_DURATION)
        {
            if (pet == null) yield break;
            
            if (pet.animator != null)
            {
                int randomAnimation = Random.Range(0, 2); // 2가지 행동만 사용
                switch (randomAnimation)
                {
                    case 0:
                        pet.animator.SetInteger("animation", 3); // 점프
                        yield return new WaitForSeconds(1f);
                        break;
                    case 1:
                        pet.animator.SetInteger("animation", 2); // 달리기 제스처
                        yield return new WaitForSeconds(0.5f);
                        if (pet.animator != null) 
                            pet.animator.SetInteger("animation", 0);
                        yield return new WaitForSeconds(0.3f);
                        break;
                }
            }
            celebrationTime += 1.5f;
            yield return new WaitForSeconds(0.5f);
        }
        
        // 3. 모든 행동 종료 후 상태 초기화
        if (pet == null) yield break;
        
        if (pet.emotionController != null)
        {
            pet.emotionController.HideEmotion();
        }
        
        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", 0);
        }
        
        // 중요: 모든 행동이 끝났으므로, 펫이 다른 행동을 할 수 있도록 상태를 해제
        // ★ [Phase 4] PetState를 통한 상태 업데이트
        pet.State.SetEnvironmentAttraction(false);
    }
}