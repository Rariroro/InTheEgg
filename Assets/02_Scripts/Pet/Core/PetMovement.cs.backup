using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫의 이동 기능만을 담당하는 클래스
/// PetController에서 이동 관련 로직을 분리
/// </summary>
public class PetMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float wanderRadius = 30f;
    [SerializeField] private float stopDistance = 1f;
    
    private NavMeshAgent agent;
    private PetController petController;
    private bool isInitialized = false;
    
    // 프로퍼티
    public bool IsMoving => agent != null && agent.enabled && !agent.isStopped && agent.velocity.magnitude > 0.1f;
    public bool HasReachedDestination => agent != null && agent.enabled && !agent.pathPending && agent.remainingDistance <= stopDistance;
    public Vector3 CurrentDestination => agent != null ? agent.destination : transform.position;
    public float RemainingDistance => agent != null ? agent.remainingDistance : 0f;
    
    /// <summary>
    /// PetMovement 초기화
    /// </summary>
    public void Init(PetController controller, NavMeshAgent navAgent)
    {
        petController = controller;
        agent = navAgent;
        
        if (agent == null)
        {
            Debug.LogError($"[PetMovement] {petController.petName}: NavMeshAgent가 없습니다!");
            return;
        }
        
        // NavMeshAgent 기본 설정
        agent.stoppingDistance = stopDistance;
        
        isInitialized = true;
        Debug.Log($"[PetMovement] {petController.petName}: 이동 시스템 초기화 완료");
    }
    
    /// <summary>
    /// 특정 위치로 이동
    /// </summary>
    public bool MoveTo(Vector3 destination)
    {
        if (!CanMove())
            return false;
            
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            agent.isStopped = false;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 랜덤한 위치로 이동
    /// </summary>
    public bool MoveToRandomPosition(float radius = -1f)
    {
        if (!CanMove())
            return false;
            
        float searchRadius = radius > 0 ? radius : wanderRadius;
        Vector3 randomDirection = Random.insideUnitSphere * searchRadius;
        randomDirection += transform.position;
        
        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
        {
            return MoveTo(hit.position);
        }
        
        return false;
    }
    
    /// <summary>
    /// 즉시 정지
    /// </summary>
    public void Stop()
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }
    
    /// <summary>
    /// 일시 정지
    /// </summary>
    public void Pause()
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
        }
    }
    
    /// <summary>
    /// 이동 재개
    /// </summary>
    public void Resume()
    {
        if (agent != null && agent.enabled && agent.hasPath)
        {
            agent.isStopped = false;
        }
    }
    
    /// <summary>
    /// 이동 속도 설정
    /// </summary>
    public void SetSpeed(float speed)
    {
        if (agent != null)
        {
            agent.speed = speed;
        }
    }
    
    /// <summary>
    /// 회전 속도 설정
    /// </summary>
    public void SetRotationSpeed(float speed)
    {
        if (agent != null)
        {
            agent.angularSpeed = speed;
        }
    }
    
    /// <summary>
    /// 가속도 설정
    /// </summary>
    public void SetAcceleration(float acceleration)
    {
        if (agent != null)
        {
            agent.acceleration = acceleration;
        }
    }
    
    /// <summary>
    /// 자동 회전 설정
    /// </summary>
    public void SetAutoRotation(bool enable)
    {
        if (agent != null)
        {
            agent.updateRotation = enable;
        }
    }
    
    /// <summary>
    /// 특정 위치로 즉시 이동 (텔레포트)
    /// </summary>
    public bool Warp(Vector3 position)
    {
        if (agent != null && agent.enabled)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 이동 가능 여부 확인
    /// </summary>
    private bool CanMove()
    {
        if (!isInitialized || agent == null || !agent.enabled)
            return false;
            
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[PetMovement] {petController.petName}: NavMesh 위에 있지 않습니다!");
            return false;
        }
        
        // 플레이어가 제어 중이거나 특수 상태일 때는 이동 불가
        if (petController.isHolding || petController.isActionLocked)
            return false;
            
        return true;
    }
    
    /// <summary>
    /// 디버그 정보 그리기
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!isInitialized || agent == null)
            return;
            
        // 현재 목적지 표시
        if (agent.hasPath)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(agent.destination, 1f);
            
            // 경로 표시
            Gizmos.color = Color.cyan;
            Vector3 previousPoint = transform.position;
            foreach (var point in agent.path.corners)
            {
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
        }
        
        // 배회 반경 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, wanderRadius);
    }
}