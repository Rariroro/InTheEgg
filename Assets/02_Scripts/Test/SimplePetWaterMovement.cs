using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 테스트용 간단한 펫 이동 스크립트
/// 물 지역에서 펫 모델이 잠긴 듯한 효과를 구현
/// </summary>
public class SimplePetWaterMovement : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 3.5f;
    public float destinationChangeInterval = 5f; // 목적지 변경 간격
    public float moveRadius = 20f; // 이동 반경
    
    [Header("물 잠김 효과 설정")]
    public float waterSinkDepth = 0.3f; // 물에서 잠길 깊이
    public float transitionSpeed = 2f; // 잠김/복원 전환 속도
    [Range(0.1f, 1f)]
    public float waterSpeedMultiplier = 0.5f; // 물에서 속도 배율 (0.5 = 50% 속도)
    
    // 컴포넌트 참조
    private NavMeshAgent agent;
    private Transform petModel; // 자식 펫 모델
    
    // 상태 변수
    private bool isInWater = false;
    private Vector3 originalModelPosition; // 펫 모델의 원래 로컬 위치
    private float targetYOffset = 0f; // 목표 Y 오프셋
    private int waterAreaMask; // 물 지역 마스크
    
    // 타이머
    private float destinationTimer = 0f;

    void Start()
    {
        // NavMeshAgent 초기화
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("[SimplePetWaterMovement] NavMeshAgent가 없습니다!");
            return;
        }
        
        agent.speed = moveSpeed;
        agent.updateRotation = true; // 자동 회전 활성화
        
        // 펫 모델 찾기 (첫 번째 자식)
        if (transform.childCount > 0)
        {
            petModel = transform.GetChild(0);
            originalModelPosition = petModel.localPosition;
        }
        else
        {
            Debug.LogError("[SimplePetWaterMovement] 펫 모델(자식 오브젝트)이 없습니다!");
            return;
        }
        
        // 물 지역 마스크 설정
        int waterAreaIndex = NavMesh.GetAreaFromName("Water");
        if (waterAreaIndex != -1)
        {
            waterAreaMask = 1 << waterAreaIndex;
            Debug.Log($"[SimplePetWaterMovement] 물 지역 인덱스: {waterAreaIndex}, 마스크: {waterAreaMask}");
        }
        else
        {
            Debug.LogWarning("[SimplePetWaterMovement] 'Water' 지역을 찾을 수 없습니다!");
        }
        
        // 첫 번째 목적지 설정
        SetRandomDestination();
    }

    void Update()
    {
        if (agent == null || petModel == null) return;
        
        // 목적지 변경 타이머
        destinationTimer += Time.deltaTime;
        if (destinationTimer >= destinationChangeInterval)
        {
            SetRandomDestination();
            destinationTimer = 0f;
        }
        
        // 목적지 도착 시 새 목적지 설정
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            SetRandomDestination();
        }
        
        // 현재 위치가 물 지역인지 확인
        CheckWaterArea();
        
        // 펫 모델 위치 업데이트 (잠김 효과)
        UpdatePetModelPosition();
    }

    /// <summary>
    /// 현재 위치가 물 지역인지 확인하고 속도 조정
    /// </summary>
    void CheckWaterArea()
    {
        if (waterAreaMask == 0) return;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 0.1f, NavMesh.AllAreas))
        {
            // 현재 위치의 지역 마스크와 물 지역 마스크 비교
            bool wasInWater = isInWater;
            isInWater = (hit.mask & waterAreaMask) != 0;
            
            // 물 지역 진입/탈출 시 처리
            if (wasInWater != isInWater)
            {
                Debug.Log($"[SimplePetWaterMovement] {gameObject.name} - 물 지역 {(isInWater ? "진입" : "탈출")}");
                
                // 목표 Y 오프셋 설정
                targetYOffset = isInWater ? -waterSinkDepth : 0f;
                
                // 속도 조정
                if (isInWater)
                {
                    // 물에 들어갔을 때 - 속도 감소
                    agent.speed = moveSpeed * waterSpeedMultiplier;
                    Debug.Log($"[SimplePetWaterMovement] 물에서 속도: {agent.speed}");
                }
                else
                {
                    // 물에서 나왔을 때 - 원래 속도로 복원
                    agent.speed = moveSpeed;
                    Debug.Log($"[SimplePetWaterMovement] 육지에서 속도: {agent.speed}");
                }
            }
        }
    }

    /// <summary>
    /// 펫 모델 위치 업데이트 (잠김 효과 적용)
    /// </summary>
    void UpdatePetModelPosition()
    {
        if (petModel == null) return;
        
        // 현재 로컬 위치
        Vector3 currentLocalPos = petModel.localPosition;
        
        // 목표 Y 위치 계산
        float targetY = originalModelPosition.y + targetYOffset;
        
        // 부드럽게 Y 위치 전환
        float newY = Mathf.Lerp(currentLocalPos.y, targetY, transitionSpeed * Time.deltaTime);
        
        // 새 위치 적용
        petModel.localPosition = new Vector3(
            originalModelPosition.x,
            newY,
            originalModelPosition.z
        );
    }

    /// <summary>
    /// 랜덤한 목적지 설정
    /// </summary>
    void SetRandomDestination()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        
        Vector3 randomDirection = Random.insideUnitSphere * moveRadius;
        randomDirection += transform.position;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, moveRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            Debug.Log($"[SimplePetWaterMovement] 새 목적지 설정: {hit.position}");
        }
    }

    /// <summary>
    /// 특정 위치로 이동 (외부에서 호출 가능)
    /// </summary>
    public void MoveTo(Vector3 destination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        
        agent.SetDestination(destination);
        destinationTimer = 0f; // 타이머 리셋
    }

    /// <summary>
    /// 이동 속도 설정 (현재 환경에 맞게 자동 조정)
    /// </summary>
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
        
        // 현재 환경에 맞는 속도 적용
        if (agent != null)
        {
            agent.speed = isInWater ? (moveSpeed * waterSpeedMultiplier) : moveSpeed;
        }
    }

    /// <summary>
    /// 이동 정지
    /// </summary>
    public void StopMovement()
    {
        if (agent == null) return;
        agent.isStopped = true;
    }

    /// <summary>
    /// 이동 재개
    /// </summary>
    public void ResumeMovement()
    {
        if (agent == null) return;
        agent.isStopped = false;
    }

    // 디버그용 기즈모 그리기
    void OnDrawGizmosSelected()
    {
        // 이동 반경 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, moveRadius);
        
        // 물 지역에 있을 때 표시
        if (isInWater)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);
            
            // 물에서 느려진 상태 표시
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 3f, Vector3.one * 1f);
        }
        
        // 목적지 표시
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(agent.destination, 0.5f);
            
            // 경로 표시
            Gizmos.color = Color.green;
            Vector3[] path = agent.path.corners;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Gizmos.DrawLine(path[i], path[i + 1]);
            }
        }
    }
}