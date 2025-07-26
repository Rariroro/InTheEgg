// WanderAction.cs
// 리팩토링: PetMovementController의 배회 로직을 WanderAction으로 통합

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class WanderAction : IPetAction
{
    private readonly PetController _pet;
    private PetMovementController _moveController; // 점진적 마이그레이션을 위해 유지
    
    // 배회 행동 관련 변수들 (PetMovementController에서 이동)
    private float behaviorTimer = 0f;
    private float nextBehaviorChange = 0f;
    private BehaviorState currentBehaviorState = BehaviorState.Walking;
    private Coroutine currentBehaviorCoroutine = null;
    
    // 성향별 행동 가중치 저장
    private PersonalityBehavior pb;
    
    /// <summary>펫이 수행 가능한 행동 목록</summary>
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
    
    /// <summary>성향별 행동 가중치, 지속시간, 속도 배율 저장 클래스</summary>
    private class PersonalityBehavior
    {
        public float idleWeight, walkWeight, runWeight, jumpWeight;
        public float restWeight, lookWeight, playWeight;
        public float behaviorDuration;   // 행동 지속 기본 시간
        public float speedMultiplier;    // 기본 속도 배율
    }

    public WanderAction(PetController pet, PetMovementController moveController)
    {
        _pet = pet;
        _moveController = moveController;
        InitializePersonalityBehavior();
    }

    public float GetPriority()
    {
        // 다른 중요한 행동 중일 때는 배회하지 않음
        if (_pet.isInteracting || _pet.isSelected || _pet.isHolding || _pet.isClimbingTree || _pet.isGathering)
        {
            return 0f;
        }
        
        // 기본 배회 우선순위
        return 0.1f;
    }

    public void OnEnter()
    {
        // Debug.Log($"{_pet.petName}: 배회 행동 시작.");
        DecideNextBehavior();
    }

    public void OnUpdate()
    {
        // NavMeshAgent 준비 여부 체크
        if (!IsAgentReady()) return;
        
        // 행동 전환 타이머 업데이트
        behaviorTimer += Time.deltaTime;
        
        // 현재 이동 중이라면 목표 지점 도착 여부 체크
        if (!_pet.agent.isStopped &&
            (currentBehaviorState == BehaviorState.Walking || currentBehaviorState == BehaviorState.Running))
        {
            HandleMovement();
        }
        
        // 다음 행동을 결정할 시간이 되었는지 체크
        if (behaviorTimer >= nextBehaviorChange)
        {
            DecideNextBehavior();
        }
        
        _pet.HandleRotation();
    }

    public void OnExit()
    {
        // Debug.Log($"{_pet.petName}: 배회 행동 종료.");
        ForceStopCurrentBehavior();
    }
    
    // === 이하 PetMovementController에서 이동한 메서드들 ===
    
    private void InitializePersonalityBehavior()
    {
        pb = new PersonalityBehavior();
        switch (_pet.personality)
        {
            case PetAIProperties.Personality.Lazy:
                pb.idleWeight = 3; pb.walkWeight = 2; pb.runWeight = 0.1f; pb.jumpWeight = 0.1f;
                pb.restWeight = 10; pb.lookWeight = 2; pb.playWeight = 0.1f;
                pb.behaviorDuration = 5; pb.speedMultiplier = 0.7f;
                break;
            case PetAIProperties.Personality.Shy:
                pb.idleWeight = 2; pb.walkWeight = 3; pb.runWeight = 0.5f; pb.jumpWeight = 0.5f;
                pb.restWeight = 2; pb.lookWeight = 4; pb.playWeight = 0.5f;
                pb.behaviorDuration = 6; pb.speedMultiplier = 0.8f;
                break;
            case PetAIProperties.Personality.Brave:
                pb.idleWeight = 1; pb.walkWeight = 2; pb.runWeight = 4; pb.jumpWeight = 3;
                pb.restWeight = 1; pb.lookWeight = 1; pb.playWeight = 2;
                pb.behaviorDuration = 8; pb.speedMultiplier = 1.2f;
                break;
            default: // Playful
                pb.idleWeight = 0.5f; pb.walkWeight = 2; pb.runWeight = 3; pb.jumpWeight = 4;
                pb.restWeight = 0.5f; pb.lookWeight = 1; pb.playWeight = 5;
                pb.behaviorDuration = 4; pb.speedMultiplier = 1.1f;
                break;
        }
    }
    
    private void DecideNextBehavior()
    {
        if (!IsAgentReady()) return;
        
        behaviorTimer = 0f;
        float total = pb.idleWeight + pb.walkWeight + pb.runWeight +
                      pb.jumpWeight + pb.restWeight + pb.lookWeight + pb.playWeight;
        float r = Random.Range(0, total), sum = 0;
        
        if ((sum += pb.idleWeight) >= r) { SetBehavior(BehaviorState.Idle); return; }
        if ((sum += pb.walkWeight) >= r) { SetBehavior(BehaviorState.Walking); return; }
        if ((sum += pb.runWeight) >= r) { SetBehavior(BehaviorState.Running); return; }
        if ((sum += pb.jumpWeight) >= r) { SetBehavior(BehaviorState.Jumping); return; }
        if ((sum += pb.restWeight) >= r) { SetBehavior(BehaviorState.Resting); return; }
        if ((sum += pb.lookWeight) >= r) { SetBehavior(BehaviorState.Looking); return; }
        SetBehavior(BehaviorState.Playing);
    }
    
    private void SetBehavior(BehaviorState state)
    {
        if (currentBehaviorCoroutine != null)
        {
            _pet.StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }
        
        if (!IsAgentReady()) return;
        
        currentBehaviorState = state;
        nextBehaviorChange = pb.behaviorDuration + Random.Range(-1f, 1f);
        
        try { _pet.agent.isStopped = true; }
        catch { /* 예외 무시 */ }
        
        var anim = _pet.GetComponent<PetAnimationController>();
        if (anim != null)
        {
            anim.StopContinuousAnimation();
        }
        
        switch (state)
        {
            case BehaviorState.Idle:
                // Idle 상태에서는 이동을 완전히 정지
                if (_pet.agent != null && _pet.agent.enabled)
                {
                    _pet.agent.ResetPath();
                    _pet.agent.velocity = Vector3.zero;
                }
                anim?.SetContinuousAnimation(0);
                break;
                
            case BehaviorState.Walking:
                SafeSetAgentMovement(_pet.baseSpeed * pb.speedMultiplier, false);
                SetRandomDestination();
                break;
                
            case BehaviorState.Running:
                SafeSetAgentMovement(_pet.baseSpeed * pb.speedMultiplier * 1.5f, false);
                SetRandomDestination();
                break;
                
            case BehaviorState.Jumping:
                currentBehaviorCoroutine = _pet.StartCoroutine(PerformJump());
                break;
                
            case BehaviorState.Resting:
                anim?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
                break;
                
            case BehaviorState.Looking:
                currentBehaviorCoroutine = _pet.StartCoroutine(LookAround());
                break;
                
            case BehaviorState.Playing:
                currentBehaviorCoroutine = _pet.StartCoroutine(PerformPlay());
                break;
        }
        
        // 물에 있으면 속도 재조정
        _pet.AdjustSpeedForWater();
    }
    
    private void ForceStopCurrentBehavior()
    {
        if (currentBehaviorCoroutine != null)
        {
            _pet.StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }
        
        var animController = _pet.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.StopContinuousAnimation();
        }
        
        currentBehaviorState = BehaviorState.Idle;
        behaviorTimer = 0f;
    }
    
    // === 헬퍼 메서드들 ===
    
    private bool IsAgentReady()
    {
        return _pet.agent != null &&
               _pet.agent.enabled &&
               _pet.agent.isOnNavMesh;
    }
    
    private void HandleMovement()
    {
        if (!_pet.agent.pathPending && _pet.agent.remainingDistance < 1f)
            SetRandomDestination();
    }
    
    private void SafeSetAgentMovement(float speed, bool isStopped)
    {
        if (!IsAgentReady() || _pet.isGathering) return;
        
        try
        {
            _pet.agent.speed = speed;
            _pet.agent.isStopped = isStopped;
        }
        catch { /* 예외 무시 */ }
    }
    
    private void SetRandomDestination()
    {
        if (!IsAgentReady()) return;
        
        // PetMovementController의 공용 메서드 호출 (호환성 유지)
        _moveController?.SetRandomDestination();
    }
    
    // === 코루틴들 ===
    
    private IEnumerator PerformJump()
    {
        yield return new WaitForSeconds(0.2f);
        var anim = _pet.GetComponent<PetAnimationController>();
        if (anim != null)
            yield return _pet.StartCoroutine(anim.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1f, true, false));
    }
    
    private IEnumerator LookAround()
    {
        var anim = _pet.GetComponent<PetAnimationController>();
        anim?.SetContinuousAnimation((int)PetAnimationController.PetAnimationType.Idle);
        
        for (int i = 0; i < 2; i++)
        {
            float t = 0f;
            Quaternion start = _pet.transform.rotation;
            Quaternion end = start * Quaternion.Euler(0, 45, 0);
            
            while (t < 1f)
            {
                t += Time.deltaTime;
                _pet.transform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            
            t = 0f;
            start = _pet.transform.rotation;
            end = start * Quaternion.Euler(0, -90, 0);
            
            while (t < 1f)
            {
                t += Time.deltaTime;
                _pet.transform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
        }
        
        anim?.StopContinuousAnimation();
    }
    
    private IEnumerator PerformPlay()
    {
        var anim = _pet.GetComponent<PetAnimationController>();
        int type = Random.Range(0, 3);
        
        if (type == 0)
        {
            SafeSetAgentMovement(_pet.baseSpeed, true);
            anim?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            yield return new WaitForSeconds(3f);
            anim?.StopContinuousAnimation();
        }
        else if (type == 1)
        {
            if (anim != null)
                for (int i = 0; i < 3; i++)
                    yield return _pet.StartCoroutine(anim.PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 0.8f, true, false));
        }
        else
        {
            SafeSetAgentMovement(_pet.baseSpeed * 2f, false);
            anim?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
            SetRandomDestination();
            yield return new WaitForSeconds(2f);
            SafeSetAgentMovement(_pet.baseSpeed, true);
            anim?.StopContinuousAnimation();
            yield return new WaitForSeconds(0.5f);
        }
        SafeSetAgentMovement(_pet.baseSpeed * pb.speedMultiplier, false);
    }
}