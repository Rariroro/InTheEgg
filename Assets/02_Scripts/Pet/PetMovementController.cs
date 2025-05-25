using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫의 기본 움직임(Idle, Walk, Run, Jump, Rest, LookAround, Play)을
/// 성향(personality)별 가중치에 따라 결정하고 NavMesh를 이용해 이동합니다.
/// PetAnimationController를 이용해 애니메이션을 전환합니다.
/// 물 속성 펫의 물/육지 이동 비율을 조정할 수 있습니다.
/// </summary>
public class PetMovementController : MonoBehaviour
{
    // 외부 PetController 참조 (NavMeshAgent, baseSpeed, personality 등 포함)
    private PetController petController;

    // 행동 전환 타이밍 제어
    private float behaviorTimer = 0f;
    private float nextBehaviorChange = 0f;
    private BehaviorState currentBehaviorState = BehaviorState.Walking;

    /// <summary>펫이 수행할 수 있는 행동 목록</summary>
    private enum BehaviorState
    {
        Idle,
        Walking,
        Running,
        Jumping,
        Resting,
        Looking,
        Playing
    }

    /// <summary>
    /// 성향별 행동 가중치, 지속시간, 속도 배율 저장용
    /// </summary>
    private class PersonalityBehavior
    {
        public float idleWeight, walkWeight, runWeight, jumpWeight;
        public float restWeight, lookWeight, playWeight;
        public float behaviorDuration;
        public float speedMultiplier;
    }

    private PersonalityBehavior pb;

    /// <summary>
    /// 물 속성 펫이 물 vs 육지 목적지를 골랐을 때의 확률(0~1)
    /// 예: 0.8이면 80% 확률로 물 위 목적지
    /// </summary>
    [Range(0f,1f)] public float waterDestinationChance = 0.8f;

    /// <summary>
    /// 초기화: PetController 세팅, 물 영역 이동 비용 설정, 성향별 행동 설정
    /// </summary>
    public void Init(PetController controller)
    {
        petController = controller;
        
        // NavMeshAgent가 활성화되고 NavMesh 위에 있을 때만 SetAreaCost 호출
        if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (waterArea != -1) // 유효한 영역인지 확인
            {
                petController.agent.SetAreaCost(
                    waterArea,
                    petController.habitat == PetAIProperties.Habitat.Water ? 0.5f : 10f
                );
            }
        }
        
        InitializePersonalityBehavior();
        StartCoroutine(DelayedStart());
    }

    private void InitializePersonalityBehavior()
    {
        pb = new PersonalityBehavior();
        switch (petController.personality)
        {
            case PetAIProperties.Personality.Lazy:
                pb.idleWeight=3;pb.walkWeight=2;pb.runWeight=0.1f;pb.jumpWeight=0.1f;
                pb.restWeight=5;pb.lookWeight=2;pb.playWeight=0.1f;
                pb.behaviorDuration=5;pb.speedMultiplier=0.7f;
                break;
            case PetAIProperties.Personality.Shy:
                pb.idleWeight=2;pb.walkWeight=3;pb.runWeight=0.5f;pb.jumpWeight=0.5f;
                pb.restWeight=2;pb.lookWeight=4;pb.playWeight=0.5f;
                pb.behaviorDuration=3;pb.speedMultiplier=0.8f;
                break;
            case PetAIProperties.Personality.Brave:
                pb.idleWeight=1;pb.walkWeight=2;pb.runWeight=4;pb.jumpWeight=3;
                pb.restWeight=1;pb.lookWeight=1;pb.playWeight=2;
                pb.behaviorDuration=4;pb.speedMultiplier=1.2f;
                break;
            default:
                // Playful
                pb.idleWeight=0.5f;pb.walkWeight=2;pb.runWeight=3;pb.jumpWeight=4;
                pb.restWeight=0.5f;pb.lookWeight=1;pb.playWeight=5;
                pb.behaviorDuration=2;pb.speedMultiplier=1.1f;
                break;
        }
    }

    private IEnumerator DelayedStart()
    {
        // NavMeshAgent가 준비될 때까지 대기
        float maxWaitTime = 5f; // 최대 5초 대기
        float waitTime = 0f;
        
        while (waitTime < maxWaitTime)
        {
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                break;
            }
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }
        
        // 여전히 준비되지 않았다면 경고 출력하고 계속 진행
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[PetMovementController] {petController.petName}: NavMeshAgent가 준비되지 않았습니다.");
            yield break;
        }
        
        // 물 영역 비용 설정 (여기서 다시 시도)
        try
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (waterArea != -1)
            {
                petController.agent.SetAreaCost(
                    waterArea,
                    petController.habitat == PetAIProperties.Habitat.Water ? 0.5f : 10f
                );
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PetMovementController] {petController.petName}: SetAreaCost 실패 - {e.Message}");
        }
        
        DecideNextBehavior();
    }

    public void UpdateMovement()
    {
        if (petController.isGathered) return;
        if (petController.agent==null||!petController.agent.isOnNavMesh||!petController.agent.enabled) return;

        behaviorTimer+=Time.deltaTime;
        if(behaviorTimer>=nextBehaviorChange) DecideNextBehavior();

        if(!petController.agent.isStopped)
        {
            if(currentBehaviorState==BehaviorState.Walking) HandleMovement();
            else if(currentBehaviorState==BehaviorState.Running) HandleMovement();
        }
        if(petController.petModelTransform!=null)
            petController.petModelTransform.position=transform.position;
    }

    private void DecideNextBehavior()
    {
        // NavMeshAgent 상태 확인
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            return;
        }
        
        behaviorTimer=0;
        float total=pb.idleWeight+pb.walkWeight+pb.runWeight+pb.jumpWeight+pb.restWeight+pb.lookWeight+pb.playWeight;
        float r=Random.Range(0,total), sum=0;
        if((sum+=pb.idleWeight)>=r){SetBehavior(BehaviorState.Idle);return;}
        if((sum+=pb.walkWeight)>=r){SetBehavior(BehaviorState.Walking);return;}
        if((sum+=pb.runWeight)>=r){SetBehavior(BehaviorState.Running);return;}
        if((sum+=pb.jumpWeight)>=r){SetBehavior(BehaviorState.Jumping);return;}
        if((sum+=pb.restWeight)>=r){SetBehavior(BehaviorState.Resting);return;}
        if((sum+=pb.lookWeight)>=r){SetBehavior(BehaviorState.Looking);return;}
        SetBehavior(BehaviorState.Playing);
    }

    private void SetBehavior(BehaviorState state)
    {
        // NavMeshAgent 상태 확인
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[PetMovementController] {petController.petName}: NavMeshAgent가 준비되지 않아 행동 설정을 건너뜁니다.");
            return;
        }
        
        currentBehaviorState=state;
        nextBehaviorChange=pb.behaviorDuration+Random.Range(-1f,1f);
        var anim=petController.GetComponent<PetAnimationController>();
        
        // 안전하게 isStopped 설정
        try
        {
            petController.agent.isStopped=true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PetMovementController] {petController.petName}: isStopped 설정 실패 - {e.Message}");
            return;
        }
        
        switch(state)
        {
            case BehaviorState.Idle: 
                if (anim != null) anim.SetContinuousAnimation(0);
                break;
            case BehaviorState.Walking:
                SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier, false);
                SetRandomDestination();
                if (anim != null) anim.SetContinuousAnimation(1);
                break;
            case BehaviorState.Running:
                SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier * 1.5f, false);
                SetRandomDestination();
                if (anim != null) anim.SetContinuousAnimation(2);
                break;
            case BehaviorState.Jumping:
                StartCoroutine(PerformJump());
                break;
            case BehaviorState.Resting:
                if (anim != null) anim.SetContinuousAnimation(5);
                break;
            case BehaviorState.Looking:
                StartCoroutine(LookAround());
                break;
            case BehaviorState.Playing:
                StartCoroutine(PerformPlay());
                break;
        }
    }

    // 안전한 NavMeshAgent 설정을 위한 헬퍼 메서드
    private void SafeSetAgentMovement(float speed, bool isStopped)
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            return;
        }
        
        try
        {
            petController.agent.speed = speed;
            petController.agent.isStopped = isStopped;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PetMovementController] {petController.petName}: Agent 설정 실패 - {e.Message}");
        }
    }

    private void HandleMovement()
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            return;
        }
        
        if(!petController.agent.pathPending&&petController.agent.remainingDistance<1f)
            SetRandomDestination();
    }

    private IEnumerator PerformJump()
    {
        yield return new WaitForSeconds(0.2f);
        var anim = petController.GetComponent<PetAnimationController>();
        if (anim != null)
        {
            yield return StartCoroutine(anim.PlayAnimationWithCustomDuration(3,1f,true,false));
        }
    }

    private IEnumerator LookAround()
    {
        var anim=petController.GetComponent<PetAnimationController>();
        if (anim != null) anim.SetContinuousAnimation(1);
        
        for(int i=0;i<2;i++)
        {
            if (petController.petModelTransform == null) break;
            
            float t=0;var start=petController.petModelTransform.rotation;
            var end=start*Quaternion.Euler(0,90,0);
            while(t<1){t+=Time.deltaTime;petController.petModelTransform.rotation=Quaternion.Slerp(start,end,t);yield return null;}
            yield return new WaitForSeconds(0.5f);
            t=0;start=petController.petModelTransform.rotation;
            end=start*Quaternion.Euler(0,-180,0);
            while(t<1){t+=Time.deltaTime;petController.petModelTransform.rotation=Quaternion.Slerp(start,end,t);yield return null;}
            yield return new WaitForSeconds(0.5f);
        }
        if (anim != null) anim.StopContinuousAnimation();
    }

    private IEnumerator PerformPlay()
    {
        var anim=petController.GetComponent<PetAnimationController>();
        int type=Random.Range(0,3);
        
        if(type==0)
        {
            SafeSetAgentMovement(petController.baseSpeed, true);
            if (anim != null) anim.SetContinuousAnimation(2);
            yield return new WaitForSeconds(3f);
            if (anim != null) anim.StopContinuousAnimation();
        }
        else if(type==1)
        {
            if (anim != null)
            {
                for(int i=0;i<3;i++)
                    yield return StartCoroutine(anim.PlayAnimationWithCustomDuration(3,0.8f,true,false));
            }
        }
        else 
        {
            SafeSetAgentMovement(petController.baseSpeed * 2f, false);
            if (anim != null) anim.SetContinuousAnimation(2);
            SetRandomDestination();
            yield return new WaitForSeconds(2f);
            SafeSetAgentMovement(petController.baseSpeed, true);
            if (anim != null) anim.StopContinuousAnimation();
            yield return new WaitForSeconds(0.5f);
        }        
        SafeSetAgentMovement(petController.baseSpeed * pb.speedMultiplier, false);
    }

    /// <summary>
    /// 물 속성 펫은 waterDestinationChance 확률로 물 위 목적지,
    /// 나머지 확률로 육지 또는 전체 NavMesh 목적지를 선택
    /// </summary>
    public void SetRandomDestination()
    {
        if(petController.agent==null||!petController.agent.isOnNavMesh||!petController.agent.enabled) return;

        int waterArea=NavMesh.GetAreaFromName("Water");
        int mask;
        if(petController.habitat==PetAIProperties.Habitat.Water && waterArea != -1)
        {
            // 무작위로 물 또는 전체 허용
            if(Random.value<waterDestinationChance)
                mask = 1<<waterArea;          // 오직 물 영역
            else
                mask = NavMesh.AllAreas;      // 전체 영역
        }
        else
        {
            // 물 비속성 펫: 물 제외 (waterArea가 유효할 때만)
            if (waterArea != -1)
                mask = NavMesh.AllAreas & ~(1<<waterArea);
            else
                mask = NavMesh.AllAreas;
        }

        Vector3 dir = Random.insideUnitSphere*30f + transform.position;
        if(NavMesh.SamplePosition(dir, out var hit, 30f, mask))
        {
            try
            {
                petController.agent.SetDestination(hit.position);
                var anim=petController.GetComponent<PetAnimationController>();
                if (anim != null)
                {
                    if(currentBehaviorState==BehaviorState.Walking) anim.SetContinuousAnimation(1);
                    else if(currentBehaviorState==BehaviorState.Running) anim.SetContinuousAnimation(2);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PetMovementController] {petController.petName}: SetDestination 실패 - {e.Message}");
            }
        }
    }
}