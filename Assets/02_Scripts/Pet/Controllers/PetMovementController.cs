using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;

/// <summary>
/// 펫의 이동 관련 공용 유틸리티 메서드들을 제공합니다.
/// 리팩토링: 배회 로직은 WanderAction으로 이동됨
/// </summary>
public class PetMovementController : PetControllerBase
{
    /// <summary>
    /// 물 속성 펫이 물 vs 육지 목적지를 고를 확률 (0~1).
    /// </summary>
    [Range(0f, 1f)] public float waterDestinationChance = 0.8f;
    
    // 막힘 감지 관련 변수
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private const float STUCK_THRESHOLD = 3f; // 3초 동안 움직이지 않으면 막힌 것으로 판단
    private const float MIN_MOVE_DISTANCE = 0.5f; // 최소 이동 거리

    // === 초기화 ===
    protected override void OnInitialize()
    {
        lastPosition = transform.position;
    }
    
    // Unity Update - 회전 처리 및 막힘 감지
    private void Update()
    {
        HandleRotation();
        CheckIfStuck();
    }

    // === 공용 유틸리티 메서드들 (다른 곳에서 호출됨) ===
    
    /// <summary>
    /// 펫의 회전을 관리합니다.
    /// 특별한 상태(선택, 모이기 등)에서만 수동 회전을 처리합니다.
    /// </summary>
    public void HandleRotation()
    {
        var agent = petController.agent;
        var petState = petController.State;
        var petModelTransform = petController.petModelTransform;
        
        // NavMeshAgent가 없거나 비활성화된 경우 처리하지 않음
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        // 선택된 상태나 모인 상태에서는 수동 회전 제어
        if (petState.IsSelected || petState.IsGathered)
        {
            // NavMeshAgent의 자동 회전 비활성화
            if (agent.updateRotation)
            {
                agent.updateRotation = false;
            }
            
            // 선택된 상태에서는 회전하지 않음 (플레이어가 제어)
            return;
        }
        
        // 상호작용 중이지만 이동하지 않는 경우
        if (petState.IsInteracting && agent.velocity.magnitude < 0.1f)
        {
            // NavMeshAgent의 자동 회전 비활성화
            if (agent.updateRotation)
            {
                agent.updateRotation = false;
            }
            return;
        }
        
        // 일반적인 이동 상태에서는 NavMeshAgent가 자동으로 회전 처리
        if (!agent.updateRotation)
        {
            agent.updateRotation = true;
        }
        
        // 펫 모델이 본체와 동기화되도록 보장
        if (petModelTransform != null && petModelTransform.rotation != transform.rotation)
        {
            petModelTransform.rotation = transform.rotation;
        }
    }
    
    /// <summary>
    /// 행동을 강제로 중단합니다. (SelectedAction, ExhaustedAction 등에서 호출)
    /// </summary>
    public void ForceStopCurrentBehavior()
    {
        // 애니메이션 정지
        var animController = petController.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.StopContinuousAnimation();
        }
        
        // 이동 정지
        if (petController.agent != null && petController.agent.enabled)
        {
            try
            {
                petController.agent.isStopped = true;
                petController.agent.ResetPath();
                petController.agent.velocity = Vector3.zero;
            }
            catch { /* 예외 무시 */ }
        }
    }
    
    /// <summary>
    /// 펫의 이동을 정지합니다.
    /// </summary>
    public void StopMovement()
    {
        if (!IsNavMeshAgentValid()) return;
        
        try
        {
            petController.agent.isStopped = true;
            petController.agent.ResetPath();
            petController.agent.velocity = Vector3.zero;
        }
        catch (System.Exception e)
        {
            PetDebug.LogWarning($"{petController.petName}: StopMovement 실패 - {e.Message}", petController);
        }
    }

    /// <summary>
    /// 펫의 이동을 재개합니다.
    /// </summary>
    public void ResumeMovement()
    {
        if (petController.State.IsGathering || !IsNavMeshAgentValid()) return;

        try
        {
            petController.agent.isStopped = false;
        }
        catch (System.Exception e)
        {
            PetDebug.LogWarning($"{petController.petName}: ResumeMovement 실패 - {e.Message}", petController);
        }
    }
    
    /// <summary>
    /// NavMeshAgent 유효성을 검사합니다.
    /// </summary>
    private bool IsNavMeshAgentValid()
    {
        return petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh;
    }
    
    /// <summary>
    /// 다음 행동을 결정합니다. (상호작용 종료 후 호출됨)
    /// </summary>
    public void DecideNextBehavior()
    {
        // PetAI가 자동으로 다음 Activity를 결정함
        // 상호작용 종료 시 AI가 재평가되도록 요청
        if (petController.AI != null)
        {
            petController.AI.UpdateAI();
        }
        Debug.Log($"[PetMovementController] {petController.petName}: 다음 행동 결정 요청 → PetAI에 위임");
    }

    /// <summary>
    /// 지정된 탐색 반경 내에서 무작위 목적지를 설정합니다.
    /// </summary>
    /// <param name="searchRadius">목적지를 찾을 반경</param>
    public void SetRandomDestination(float searchRadius)
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        int waterArea = NavMesh.GetAreaFromName("Water");
        int mask;

        // 물/육지 선호도에 따른 영역 마스크 설정
        if (petController.habitat == PetAIProperties.Habitat.Water && waterArea != -1)
        {
            mask = (Random.value < waterDestinationChance) ? (1 << waterArea) : NavMesh.AllAreas;
        }
        else
        {
            mask = (waterArea != -1) ? (NavMesh.AllAreas & ~(1 << waterArea)) : NavMesh.AllAreas;
        }

        Vector3 dir = Random.insideUnitSphere * searchRadius + transform.position;
        if (NavMesh.SamplePosition(dir, out NavMeshHit hit, searchRadius, mask))
        {
            try
            {
                petController.agent.SetDestination(hit.position);
                petController.ResumeMovement();
                
                // 기본 걷기 애니메이션 설정
                var anim = petController.GetComponent<PetAnimationController>();
                anim?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PetMovementController] {petController.petName}: SetDestination 실패 - {e.Message}");
            }
        }
    }
    
    public void SetRandomDestination()
    {
        SetRandomDestination(50f); // 기본 반경 50f
    }
    
    /// <summary>
    /// 펫이 막혔는지 감지하고 해결합니다.
    /// </summary>
    private void CheckIfStuck()
    {
        if (!IsNavMeshAgentValid() || petController.State.IsHolding || petController.State.IsSelected)
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
            return;
        }
        
        // 이동 중이지만 실제로 움직이지 않는 경우
        if (petController.agent != null && !petController.agent.isStopped && petController.agent.hasPath)
        {
            float movedDistance = Vector3.Distance(transform.position, lastPosition);
            
            if (movedDistance < MIN_MOVE_DISTANCE)
            {
                stuckTimer += Time.deltaTime;
                
                if (stuckTimer >= STUCK_THRESHOLD)
                {
                    Debug.Log($"[PetMovementController] {petController.petName}: 막힘 감지! 새로운 경로 찾기");
                    ResolveStuck();
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
                lastPosition = transform.position;
            }
        }
        else
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
        }
    }
    
    /// <summary>
    /// 막힌 상태를 해결합니다.
    /// </summary>
    private void ResolveStuck()
    {
        if (!IsNavMeshAgentValid()) return;
        
        // 방법 1: 현재 경로 취소하고 새로운 목적지 설정
        petController.agent.ResetPath();
        
        // 방법 2: 약간 뒤로 이동
        Vector3 backDirection = -transform.forward;
        Vector3 newPosition = transform.position + backDirection * 1f;
        
        // NavMesh 위의 유효한 위치 찾기
        if (NavMesh.SamplePosition(newPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            petController.agent.Warp(hit.position);
        }
        
        // 방법 3: 새로운 랜덤 목적지 설정 (짧은 거리)
        SetRandomDestination(10f);
        
        Debug.Log($"[PetMovementController] {petController.petName}: 막힌 상태 해결 시도");
    }
    
}