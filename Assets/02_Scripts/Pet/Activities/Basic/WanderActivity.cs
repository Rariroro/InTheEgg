using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;
/// <summary>
/// 펫의 배회 활동을 담당하는 클래스
/// 기존 WanderAction을 개선하여 더 명확한 구조를 제공합니다.
/// </summary>
public class WanderActivity : PetActivityAdapter
{
    private PetMovementController moveController;
    
    // 배회 행동 관련 변수들
    private float behaviorTimer = 0f;
    private float nextBehaviorChange = 0f;
    private BehaviorState currentBehaviorState = BehaviorState.Walking;
    private Coroutine currentBehaviorCoroutine = null;
    
    // 성향별 행동 가중치 저장
    private PersonalityBehavior personalityBehavior;
    
    private enum BehaviorState
    {
        Idle,    // 가만히 대기
        Walking, // 느리게 걷기
        Running, // 빠르게 달리기
        Jumping, // 점프
        Resting, // 쉬기(앉기 등)
        Looking, // 주변 둘러보기
        Playing  // 놀기(제자리 뱅글뱅글, 연속 점프 등)
    }
    
    private class PersonalityBehavior
    {
        public float idleWeight, walkWeight, runWeight, jumpWeight;
        public float restWeight, lookWeight, playWeight;
        public float behaviorDuration;   // 행동 지속 기본 시간
        public float speedMultiplier;    // 기본 속도 배율
    }
    
    public override string Name => "Wander";
    public override bool IsInterruptible => true;
    
    public WanderActivity(PetController petController, PetMovementController movementController) : base(petController)
    {
        moveController = movementController;
        InitializePersonalityBehavior();
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // Idle 상태이고 특별한 활동이 없을 때만 배회 가능
        return state.CurrentStatus == PetStatus.Idle && 
               !pet.isInteracting && 
               !pet.isSelected && 
               !pet.isHolding && 
               !pet.isClimbingTree && 
               !pet.isGathering;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        // 배회는 기본 활동이므로 낮은 우선순위
        return CanStart(state, needs) ? 0.1f : 0f;
    }
    
    
    // 기존 IPetAction 메서드 구현
    public override float GetPriority()
    {
        // 호환성을 위해 기존 로직 유지
        if (pet.isInteracting || pet.isSelected || pet.isHolding || pet.isClimbingTree || pet.isGathering)
        {
            return 0f;
        }
        return 0.1f;
    }
    
    public override void OnEnter()
    {
        Debug.Log($"[WanderActivity] {pet.petName}: 배회 활동 시작");
        DecideNextBehavior();
    }
    
    public override void OnUpdate()
    {
        if (!IsAgentReady()) return;
        
        behaviorTimer += Time.deltaTime;
        
        // 현재 이동 중이라면 목표 지점 도착 여부 체크
        if (!pet.agent.isStopped &&
            (currentBehaviorState == BehaviorState.Walking || currentBehaviorState == BehaviorState.Running))
        {
            HandleMovement();
        }
        
        // 다음 행동을 결정할 시간이 되었는지 체크
        if (behaviorTimer >= nextBehaviorChange)
        {
            DecideNextBehavior();
        }
        
        // 선택 상탌가 아닐 때만 회전 처리
        if (!pet.isSelected)
        {
            pet.HandleRotation();
        }
    }
    
    public override void OnExit()
    {
        Debug.Log($"[WanderActivity] {pet.petName}: 배회 활동 종료");
        ForceStopCurrentBehavior();
    }
    
    // === 헬퍼 메서드들 ===
    
    private void InitializePersonalityBehavior()
    {
        personalityBehavior = new PersonalityBehavior();
        switch (pet.personality)
        {
            case PetAIProperties.Personality.Lazy:
                personalityBehavior.idleWeight = 3;
                personalityBehavior.walkWeight = 2;
                personalityBehavior.runWeight = 0.1f;
                personalityBehavior.jumpWeight = 0.1f;
                personalityBehavior.restWeight = 10;
                personalityBehavior.lookWeight = 2;
                personalityBehavior.playWeight = 0.1f;
                personalityBehavior.behaviorDuration = 5;
                personalityBehavior.speedMultiplier = 0.7f;
                break;
                
            case PetAIProperties.Personality.Shy:
                personalityBehavior.idleWeight = 2;
                personalityBehavior.walkWeight = 3;
                personalityBehavior.runWeight = 0.5f;
                personalityBehavior.jumpWeight = 0.5f;
                personalityBehavior.restWeight = 2;
                personalityBehavior.lookWeight = 4;
                personalityBehavior.playWeight = 0.5f;
                personalityBehavior.behaviorDuration = 6;
                personalityBehavior.speedMultiplier = 0.8f;
                break;
                
            case PetAIProperties.Personality.Brave:
                personalityBehavior.idleWeight = 1;
                personalityBehavior.walkWeight = 2;
                personalityBehavior.runWeight = 4;
                personalityBehavior.jumpWeight = 3;
                personalityBehavior.restWeight = 1;
                personalityBehavior.lookWeight = 1;
                personalityBehavior.playWeight = 2;
                personalityBehavior.behaviorDuration = 8;
                personalityBehavior.speedMultiplier = 1.2f;
                break;
                
            default: // Playful
                personalityBehavior.idleWeight = 0.5f;
                personalityBehavior.walkWeight = 2;
                personalityBehavior.runWeight = 3;
                personalityBehavior.jumpWeight = 4;
                personalityBehavior.restWeight = 0.5f;
                personalityBehavior.lookWeight = 1;
                personalityBehavior.playWeight = 5;
                personalityBehavior.behaviorDuration = 4;
                personalityBehavior.speedMultiplier = 1.1f;
                break;
        }
    }
    
    private void DecideNextBehavior()
    {
        if (!IsAgentReady()) return;
        
        behaviorTimer = 0f;
        float total = personalityBehavior.idleWeight + personalityBehavior.walkWeight + personalityBehavior.runWeight +
                      personalityBehavior.jumpWeight + personalityBehavior.restWeight + personalityBehavior.lookWeight + 
                      personalityBehavior.playWeight;
        float r = Random.Range(0, total);
        float sum = 0;
        
        if ((sum += personalityBehavior.idleWeight) >= r) { SetBehavior(BehaviorState.Idle); return; }
        if ((sum += personalityBehavior.walkWeight) >= r) { SetBehavior(BehaviorState.Walking); return; }
        if ((sum += personalityBehavior.runWeight) >= r) { SetBehavior(BehaviorState.Running); return; }
        if ((sum += personalityBehavior.jumpWeight) >= r) { SetBehavior(BehaviorState.Jumping); return; }
        if ((sum += personalityBehavior.restWeight) >= r) { SetBehavior(BehaviorState.Resting); return; }
        if ((sum += personalityBehavior.lookWeight) >= r) { SetBehavior(BehaviorState.Looking); return; }
        SetBehavior(BehaviorState.Playing);
    }
    
    private void SetBehavior(BehaviorState state)
    {
        if (currentBehaviorCoroutine != null)
        {
            pet.StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }
        
        if (!IsAgentReady()) return;
        
        currentBehaviorState = state;
        nextBehaviorChange = personalityBehavior.behaviorDuration + Random.Range(-1f, 1f);
        
        try { pet.agent.isStopped = true; }
        catch { /* 예외 무시 */ }
        
        var anim = pet.GetComponent<PetAnimationController>();
        if (anim != null)
        {
            anim.StopContinuousAnimation();
        }
        
        switch (state)
        {
            case BehaviorState.Idle:
                if (pet.agent != null && pet.agent.enabled)
                {
                    pet.agent.ResetPath();
                    pet.agent.velocity = Vector3.zero;
                }
                anim?.SetContinuousAnimation(0);
                break;
                
            case BehaviorState.Walking:
                SafeSetAgentMovement(pet.baseSpeed * personalityBehavior.speedMultiplier, false);
                SetRandomDestination();
                break;
                
            case BehaviorState.Running:
                SafeSetAgentMovement(pet.baseSpeed * personalityBehavior.speedMultiplier * 1.5f, false);
                SetRandomDestination();
                break;
                
            case BehaviorState.Jumping:
                currentBehaviorCoroutine = pet.StartCoroutine(PerformJump());
                break;
                
            case BehaviorState.Resting:
                anim?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
                break;
                
            case BehaviorState.Looking:
                currentBehaviorCoroutine = pet.StartCoroutine(LookAround());
                break;
                
            case BehaviorState.Playing:
                currentBehaviorCoroutine = pet.StartCoroutine(PerformPlay());
                break;
        }
        
        // 물에 있으면 속도 재조정
        pet.AdjustSpeedForWater();
    }
    
    private void ForceStopCurrentBehavior()
    {
        if (currentBehaviorCoroutine != null)
        {
            pet.StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }
        
        var animController = pet.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.StopContinuousAnimation();
        }
        
        currentBehaviorState = BehaviorState.Idle;
        behaviorTimer = 0f;
    }
    
    // === 유틸리티 메서드들 ===
    
    private bool IsAgentReady()
    {
        return pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }
    
    private void HandleMovement()
    {
        if (!pet.agent.pathPending && pet.agent.remainingDistance < 1f)
            SetRandomDestination();
    }
    
    private void SafeSetAgentMovement(float speed, bool isStopped)
    {
        if (!IsAgentReady() || pet.isGathering) return;
        
        try
        {
            pet.agent.speed = speed;
            pet.agent.isStopped = isStopped;
        }
        catch { /* 예외 무시 */ }
    }
    
    private void SetRandomDestination()
    {
        if (!IsAgentReady()) return;
        moveController?.SetRandomDestination();
    }
    
    // === 코루틴들 ===
    
    private IEnumerator PerformJump()
    {
        yield return new WaitForSeconds(0.2f);
        var anim = pet.GetComponent<PetAnimationController>();
        if (anim != null)
            yield return pet.StartCoroutine(anim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, 1f, true, false));
    }
    
    private IEnumerator LookAround()
    {
        var anim = pet.GetComponent<PetAnimationController>();
        anim?.SetContinuousAnimation((int)PetAnimationController.PetAnimationType.Idle);
        
        for (int i = 0; i < 2; i++)
        {
            float t = 0f;
            Quaternion start = pet.transform.rotation;
            Quaternion end = start * Quaternion.Euler(0, 45, 0);
            
            while (t < 1f)
            {
                t += Time.deltaTime;
                pet.transform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            
            t = 0f;
            start = pet.transform.rotation;
            end = start * Quaternion.Euler(0, -90, 0);
            
            while (t < 1f)
            {
                t += Time.deltaTime;
                pet.transform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
        }
        
        anim?.StopContinuousAnimation();
    }
    
    private IEnumerator PerformPlay()
    {
        var anim = pet.GetComponent<PetAnimationController>();
        int type = Random.Range(0, 3);
        
        if (type == 0)
        {
            SafeSetAgentMovement(pet.baseSpeed, true);
            anim?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            yield return new WaitForSeconds(3f);
            anim?.StopContinuousAnimation();
        }
        else if (type == 1)
        {
            if (anim != null)
                for (int i = 0; i < 3; i++)
                    yield return pet.StartCoroutine(anim.PlayAnimationWithCustomDuration(
                        PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        }
        else
        {
            SafeSetAgentMovement(pet.baseSpeed * 2f, false);
            anim?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            SetRandomDestination();
            yield return new WaitForSeconds(2f);
            SafeSetAgentMovement(pet.baseSpeed, true);
            anim?.StopContinuousAnimation();
            yield return new WaitForSeconds(0.5f);
        }
        SafeSetAgentMovement(pet.baseSpeed * personalityBehavior.speedMultiplier, false);
    }
}