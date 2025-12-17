using System.Collections;
using System.Collections.Generic;
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
    
    // 개선된 막힘 해결 시스템
    private List<Vector3> stuckPositions = new List<Vector3>(); // 최근 막힌 위치들
    private int stuckResolveAttempts = 0; // 현재 막힘 해결 시도 횟수
    private const int MAX_STUCK_POSITIONS = 5; // 기억할 막힌 위치 최대 개수
    private const float STUCK_POSITION_AVOID_RADIUS = 3f; // 막힌 위치 회피 반경

    // NavMesh Area 캐싱 - JobTempAlloc 방지
    private static int cachedWaterArea = -2; // -2 = 미초기화, -1 = 없음

    // agent.updateRotation 캐싱
    private bool cachedUpdateRotation = true;

    // CheckIfStuck 주기 제어
    private float stuckCheckTimer = 0f;
    private const float STUCK_CHECK_INTERVAL = 0.5f;

    /// <summary>
    /// Water NavMesh Area 인덱스를 캐시에서 가져옵니다.
    /// </summary>
    public static int GetWaterAreaCached()
    {
        if (cachedWaterArea == -2)
        {
            cachedWaterArea = NavMesh.GetAreaFromName("Water");
        }
        return cachedWaterArea;
    }

    // === 초기화 ===
    protected override void OnInitialize()
    {
        lastPosition = transform.position;
    }
    
    // Unity Update - 회전 처리 및 막힘 감지
    private void Update()
    {
        HandleRotation();

        // 막힘 감지는 0.5초 주기로 실행 (JobTempAlloc 방지)
        stuckCheckTimer += Time.deltaTime;
        if (stuckCheckTimer >= STUCK_CHECK_INTERVAL)
        {
            stuckCheckTimer = 0f;
            CheckIfStuck();
        }
    }

    // === 공용 유틸리티 메서드들 (다른 곳에서 호출됨) ===
    
    /// <summary>
    /// 펫의 회전을 관리합니다.
    /// 단순화된 로직: 선택/홀딩 상태에서만 수동 제어, 나머지는 NavMeshAgent 자동 회전
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

        // 단순화된 회전 로직
        bool shouldDisableAutoRotation = petState.IsSelected || petState.IsHolding || petState.IsGathered;
        
        // 특수 애니메이션 처리 중에는 회전 설정 변경하지 않음
        if (shouldDisableAutoRotation)
        {
            var inputController = petController.GetComponent<PetInputController>();
            if (inputController != null && inputController.IsProcessingSpecialAnimation)
            {
                return;
            }
        }
        
        // NavMeshAgent 자동 회전 설정 - 캐싱으로 중복 쓰기 방지 (JobTempAlloc 방지)
        if (shouldDisableAutoRotation && cachedUpdateRotation)
        {
            // 선택/홀딩/모이기 상태에서는 수동 제어
            agent.updateRotation = false;
            cachedUpdateRotation = false;
        }
        else if (!shouldDisableAutoRotation && !cachedUpdateRotation)
        {
            // 그 외 모든 상태에서는 NavMeshAgent가 자동 회전
            agent.updateRotation = true;
            cachedUpdateRotation = true;
        }
        
        // 펫 모델이 본체와 동기화되도록 보장 (항상 수행)
        if (petModelTransform != null && petModelTransform.rotation != transform.rotation)
        {
            petModelTransform.rotation = Quaternion.Slerp(
                petModelTransform.rotation, 
                transform.rotation, 
                Time.deltaTime * 10f
            );
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
            Debug.LogWarning($"[Pet Warning] {petController.petName}: StopMovement 실패 - {e.Message}", petController);
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
            Debug.LogWarning($"[Pet Warning] {petController.petName}: ResumeMovement 실패 - {e.Message}", petController);
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
        // Debug.Log($"[PetMovementController] {petController.petName}: 다음 행동 결정 요청 → PetAI에 위임");
    }

    /// <summary>
    /// 지정된 탐색 반경 내에서 무작위 목적지를 설정합니다.
    /// </summary>
    /// <param name="searchRadius">목적지를 찾을 반경</param>
    public void SetRandomDestination(float searchRadius)
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        int waterArea = GetWaterAreaCached(); // 캐시된 값 사용 (JobTempAlloc 방지)
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
                    // Debug.Log($"[PetMovementController] {petController.petName}: 막힘 감지! 새로운 경로 찾기");
                    ResolveStuck();
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
                lastPosition = transform.position;
                // 정상적으로 이동 중이면 해결 시도 횟수 리셋
                if (movedDistance > MIN_MOVE_DISTANCE * 2)
                {
                    stuckResolveAttempts = 0;
                }
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
        
        // 현재 위치를 막힌 위치로 기록
        RecordStuckPosition(transform.position);
        
        // 현재 경로 취소
        petController.agent.ResetPath();
        
        Vector3 escapeDirection = Vector3.zero;
        float escapeDistance = 0f;
        
        // 점진적 회피 전략
        stuckResolveAttempts++;
        
        switch (stuckResolveAttempts)
        {
            case 1: // 1차 시도: 좌우로 이동
                escapeDirection = Random.value > 0.5f ? transform.right : -transform.right;
                escapeDistance = 2f;
                // Debug.Log($"[PetMovementController] {petController.petName}: 막힘 해결 1차 - 옆으로 이동");
                break;
                
            case 2: // 2차 시도: 뒤로 + 옆으로 이동
                escapeDirection = -transform.forward + (Random.value > 0.5f ? transform.right : -transform.right) * 0.5f;
                escapeDirection.Normalize();
                escapeDistance = 3f;
                // Debug.Log($"[PetMovementController] {petController.petName}: 막힘 해결 2차 - 뒤+옆으로 이동");
                break;
                
            default: // 3차 이상: 큰 우회
                // 막힌 위치들과 반대 방향으로 이동
                escapeDirection = GetAwayFromStuckPositions();
                escapeDistance = 5f;
                stuckResolveAttempts = 0; // 리셋
                // Debug.Log($"[PetMovementController] {petController.petName}: 막힘 해결 3차 - 큰 우회");
                break;
        }
        
        // 회피 위치 계산 및 이동
        Vector3 escapePosition = transform.position + escapeDirection * escapeDistance;
        
        // NavMesh 위의 유효한 위치 찾기
        if (NavMesh.SamplePosition(escapePosition, out NavMeshHit hit, escapeDistance * 1.5f, NavMesh.AllAreas))
        {
            // 막힌 위치에서 충분히 떨어진 곳인지 확인
            if (IsPositionAwayFromStuckAreas(hit.position))
            {
                petController.agent.SetDestination(hit.position);
            }
            else
            {
                // 막힌 위치 근처면 다른 랜덤 위치로
                SetRandomDestinationAvoidingStuck(10f);
            }
        }
        else
        {
            // 유효한 위치를 찾지 못하면 랜덤 이동
            SetRandomDestinationAvoidingStuck(15f);
        }
    }
    
    /// <summary>
    /// 막힌 위치를 기록합니다.
    /// </summary>
    private void RecordStuckPosition(Vector3 position)
    {
        // 이미 비슷한 위치가 기록되어 있는지 확인
        foreach (var stuckPos in stuckPositions)
        {
            if (Vector3.Distance(position, stuckPos) < 1f)
                return; // 이미 기록된 위치 근처면 추가하지 않음
        }
        
        stuckPositions.Add(position);
        
        // 최대 개수 유지
        if (stuckPositions.Count > MAX_STUCK_POSITIONS)
        {
            stuckPositions.RemoveAt(0); // 가장 오래된 위치 제거
        }
    }
    
    /// <summary>
    /// 막힌 위치들로부터 멀어지는 방향을 계산합니다.
    /// </summary>
    private Vector3 GetAwayFromStuckPositions()
    {
        if (stuckPositions.Count == 0)
            return Random.insideUnitSphere.normalized;
        
        Vector3 awayDirection = Vector3.zero;
        
        foreach (var stuckPos in stuckPositions)
        {
            Vector3 directionAway = (transform.position - stuckPos).normalized;
            float distance = Vector3.Distance(transform.position, stuckPos);
            float weight = 1f / (distance + 1f); // 가까운 막힌 위치일수록 더 강하게 회피
            awayDirection += directionAway * weight;
        }
        
        if (awayDirection.magnitude < 0.1f)
            return Random.insideUnitSphere.normalized;
        
        return awayDirection.normalized;
    }
    
    /// <summary>
    /// 위치가 막힌 지역들로부터 충분히 떨어져 있는지 확인합니다.
    /// </summary>
    private bool IsPositionAwayFromStuckAreas(Vector3 position)
    {
        foreach (var stuckPos in stuckPositions)
        {
            if (Vector3.Distance(position, stuckPos) < STUCK_POSITION_AVOID_RADIUS)
                return false;
        }
        return true;
    }
    
    /// <summary>
    /// 막힌 위치를 피해서 랜덤 목적지를 설정합니다.
    /// </summary>
    private void SetRandomDestinationAvoidingStuck(float searchRadius)
    {
        if (!IsNavMeshAgentValid()) return;
        
        int maxAttempts = 5;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * searchRadius;
            randomDirection.y = 0;
            Vector3 targetPosition = transform.position + randomDirection;
            
            if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            {
                if (IsPositionAwayFromStuckAreas(hit.position))
                {
                    petController.agent.SetDestination(hit.position);
                    petController.ResumeMovement();
                    
                    var anim = petController.GetComponent<PetAnimationController>();
                    anim?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
                    return;
                }
            }
        }
        
        // 모든 시도가 실패하면 기본 랜덤 이동
        SetRandomDestination(searchRadius);
    }
    
}