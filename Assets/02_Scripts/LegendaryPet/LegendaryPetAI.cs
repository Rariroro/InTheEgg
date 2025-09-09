using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace LegendaryPet
{
    /// <summary>
    /// 레전드 펫의 AI 시스템
    /// 단순한 배회 동작만 구현
    /// </summary>
    [RequireComponent(typeof(LegendaryPetController))]
    public class LegendaryPetAI : MonoBehaviour
    {
        [Header("AI 설정")]
        [SerializeField] private float wanderRadius = 100f;          // 배회 반경
        [SerializeField] private float minWanderDistance = 20f;      // 최소 이동 거리
        [SerializeField] private float idleTimeMin = 3f;            // 최소 대기 시간
        [SerializeField] private float idleTimeMax = 8f;            // 최대 대기 시간
        [SerializeField] private float specialAnimationChance = 0.1f; // 특별 애니메이션 확률
        
        [Header("전역 탐험 설정")]
        [SerializeField] private float explorationRadius = 200f;     // 전체 탐험 범위
        [SerializeField] private bool useGlobalExploration = true;   // 전역 탐험 활성화
        [SerializeField] private float globalExplorationChance = 0.3f; // 먼 곳 탐험 확률
        
        [Header("비행 설정")]
        [SerializeField] [Range(0f, 1f)] private float flyingChance = 0.7f; // 비행 확률 (0~1)
        
        [Header("움직임 패턴")]
        [SerializeField] private MovementPattern currentPattern = MovementPattern.Elegant;
        
        private LegendaryPetController controller;
        private NavMeshAgent agent;
        private Coroutine aiCoroutine;
        private Vector3 spawnPosition;
        private float nextActionTime;
        
        public enum MovementPattern
        {
            Elegant,    // 우아한 움직임 (긴 직선, 부드러운 곡선)
            Majestic,   // 장엄한 움직임 (느리고 위엄있게)
            Playful,    // 장난스러운 움직임 (짧고 빠른 이동)
            Patrol,     // 순찰 패턴 (일정한 경로)
            Random      // 무작위 패턴
        }
        
        private void Awake()
        {
            controller = GetComponent<LegendaryPetController>();
            agent = GetComponent<NavMeshAgent>();
            spawnPosition = transform.position;
        }
        
        private void OnEnable()
        {
            if (aiCoroutine != null)
            {
                StopCoroutine(aiCoroutine);
            }
            aiCoroutine = StartCoroutine(AIBehaviorLoop());
        }
        
        private void OnDisable()
        {
            if (aiCoroutine != null)
            {
                StopCoroutine(aiCoroutine);
                aiCoroutine = null;
            }
            controller?.StopMoving();
        }
        
        private IEnumerator AIBehaviorLoop()
        {
            // 초기 대기
            yield return new WaitForSeconds(2f);
            
            while (enabled)
            {
                if (!controller.IsActive)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                
                // 다음 행동 결정
                yield return StartCoroutine(DecideNextAction());
                
                // 행동 완료 대기
                while (Time.time < nextActionTime)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }
        
        private IEnumerator DecideNextAction()
        {
            // 유니콘과 드래곤의 비행 체크
            if ((controller.PetType == LegendaryPetType.Dragon || controller.PetType == LegendaryPetType.Unicorn) 
                && controller.Traits.canFly 
                && !controller.IsFlying 
                && Random.value < flyingChance)
            {
                yield return StartCoroutine(PerformFlight());
            }
            // 특별 애니메이션 확률 체크
            else if (Random.value < specialAnimationChance)
            {
                yield return StartCoroutine(PerformSpecialAction());
            }
            else
            {
                // 배회 또는 대기
                if (Random.value < 0.7f) // 70% 확률로 이동
                {
                    yield return StartCoroutine(WanderToNewPosition());
                }
                else // 30% 확률로 제자리 대기
                {
                    yield return StartCoroutine(IdleInPlace());
                }
            }
        }
        
        private IEnumerator WanderToNewPosition()
        {
            Vector3 destination = GetWanderDestination();
            
            if (destination != Vector3.zero)
            {
                controller.MoveTo(destination);
                
                // 목적지 도달까지 대기
                while (agent.pathPending || 
                       (agent.hasPath && agent.remainingDistance > agent.stoppingDistance))
                {
                    yield return new WaitForSeconds(0.5f);
                    
                    // 막힌 경우 새로운 목적지 설정
                    if (agent.pathStatus == NavMeshPathStatus.PathPartial || 
                        agent.pathStatus == NavMeshPathStatus.PathInvalid)
                    {
                        destination = GetWanderDestination();
                        if (destination != Vector3.zero)
                        {
                            controller.MoveTo(destination);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                
                // 속도가 충분히 낮아질 때까지 대기
                float waitTime = 0f;
                while (agent.velocity.magnitude > 0.1f && waitTime < 1f)
                {
                    yield return new WaitForSeconds(0.1f);
                    waitTime += 0.1f;
                }
                
                controller.StopMoving();
                
                // 애니메이션 전환을 위한 짧은 딜레이
                yield return new WaitForSeconds(0.1f);
            }
            
            // 도착 후 잠시 대기
            float idleTime = Random.Range(idleTimeMin * 0.5f, idleTimeMax * 0.5f);
            nextActionTime = Time.time + idleTime;
            yield return new WaitForSeconds(idleTime);
        }
        
        private IEnumerator IdleInPlace()
        {
            controller.StopMoving();
            
            // 대기 중 가끔 주변을 둘러봄
            if (Random.value < 0.5f)
            {
                yield return StartCoroutine(LookAround());
            }
            
            float idleTime = Random.Range(idleTimeMin, idleTimeMax);
            nextActionTime = Time.time + idleTime;
            yield return new WaitForSeconds(idleTime);
        }
        
        private IEnumerator PerformSpecialAction()
        {
            controller.StopMoving();
            
            // 특별 애니메이션 재생
            controller.PlaySpecialAnimation();
            
            // 애니메이션 시간 대기
            yield return new WaitForSeconds(3f);
            
            nextActionTime = Time.time + 2f;
        }
        
        private IEnumerator LookAround()
        {
            // 좌우로 천천히 회전
            Quaternion startRotation = transform.rotation;
            
            // 오른쪽으로 회전
            float rotationTime = 2f;
            float elapsedTime = 0f;
            Quaternion rightRotation = startRotation * Quaternion.Euler(0, 90, 0);
            
            while (elapsedTime < rotationTime)
            {
                transform.rotation = Quaternion.Slerp(startRotation, rightRotation, elapsedTime / rotationTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            yield return new WaitForSeconds(0.5f);
            
            // 왼쪽으로 회전
            elapsedTime = 0f;
            Quaternion leftRotation = startRotation * Quaternion.Euler(0, -90, 0);
            
            while (elapsedTime < rotationTime * 2f)
            {
                transform.rotation = Quaternion.Slerp(rightRotation, leftRotation, elapsedTime / (rotationTime * 2f));
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            yield return new WaitForSeconds(0.5f);
            
            // 원래 방향으로 복귀
            elapsedTime = 0f;
            while (elapsedTime < rotationTime)
            {
                transform.rotation = Quaternion.Slerp(leftRotation, startRotation, elapsedTime / rotationTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }
        
        private Vector3 GetWanderDestination()
        {
            Vector3 destination = Vector3.zero;
            
            // 전역 탐험 모드 - 설정된 확률로 먼 곳 탐험
            if (useGlobalExploration && Random.value < globalExplorationChance)
            {
                destination = GetGlobalExplorationPoint();
            }
            else
            {
                switch (currentPattern)
                {
                    case MovementPattern.Elegant:
                        destination = GetElegantDestination();
                        break;
                        
                    case MovementPattern.Majestic:
                        destination = GetMajesticDestination();
                        break;
                        
                    case MovementPattern.Playful:
                        destination = GetPlayfulDestination();
                        break;
                        
                    case MovementPattern.Patrol:
                        destination = GetPatrolDestination();
                        break;
                        
                    case MovementPattern.Random:
                    default:
                        destination = GetRandomDestination();
                        break;
                }
            }
            
            // NavMesh 상의 유효한 위치 찾기
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                // 높이 차이 검증 - 현재 위치와 5미터 이상 차이나면 거부
                float heightDifference = Mathf.Abs(hit.position.y - transform.position.y);
                if (heightDifference > 5f)
                {
                    // 지면 Raycast로 올바른 높이 찾기
                    if (Physics.Raycast(destination + Vector3.up * 10f, Vector3.down, out RaycastHit groundHit, 20f))
                    {
                        destination.y = groundHit.point.y;
                        
                        // 다시 NavMesh 샘플링 시도
                        if (NavMesh.SamplePosition(destination, out NavMeshHit newHit, 5f, NavMesh.AllAreas))
                        {
                            // 새로운 위치도 높이 차이 검증
                            if (Mathf.Abs(newHit.position.y - transform.position.y) <= 5f)
                            {
                                return newHit.position;
                            }
                        }
                    }
                    
                    Debug.LogWarning($"[LegendaryPetAI] {controller.PetName}: 목적지 높이 차이가 너무 큼 ({heightDifference:F1}m)");
                    return Vector3.zero;
                }
                
                return hit.position;
            }
            
            return Vector3.zero;
        }
        
        private Vector3 GetElegantDestination()
        {
            // 우아한 움직임: 긴 직선 경로 선호
            Vector3 direction = transform.forward;
            
            // 약간의 각도 변화
            float angle = Random.Range(-30f, 30f);
            direction = Quaternion.Euler(0, angle, 0) * direction;
            
            // 더 먼 거리로 이동 가능
            float distance = useGlobalExploration ? 
                Random.Range(wanderRadius, wanderRadius * 2f) : 
                Random.Range(wanderRadius * 0.6f, wanderRadius);
            
            return transform.position + direction * distance;
        }
        
        private Vector3 GetMajesticDestination()
        {
            // 장엄한 움직임: 느리고 짧은 거리
            Vector3 randomDirection = Random.insideUnitSphere * (wanderRadius * 0.3f);
            randomDirection.y = 0;
            return transform.position + randomDirection;
        }
        
        private Vector3 GetPlayfulDestination()
        {
            // 장난스러운 움직임: 빠르고 예측 불가능
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection.y = 0;
            
            // 최소 거리 보장
            if (randomDirection.magnitude < minWanderDistance)
            {
                randomDirection = randomDirection.normalized * minWanderDistance;
            }
            
            return transform.position + randomDirection;
        }
        
        private Vector3 GetPatrolDestination()
        {
            if (useGlobalExploration)
            {
                // 전역 순찰: 맵의 주요 지점들을 순회
                Vector3[] patrolPoints = new Vector3[]
                {
                    new Vector3(100, 0, 100),
                    new Vector3(-100, 0, 100),
                    new Vector3(-100, 0, -100),
                    new Vector3(100, 0, -100),
                    new Vector3(0, 0, 0)
                };
                
                int index = Mathf.FloorToInt(Time.time / 30f) % patrolPoints.Length;
                return patrolPoints[index];
            }
            else
            {
                // 순찰 패턴: 스폰 지점 주변을 원형으로 순찰
                float angle = Time.time * 10f; // 시간에 따라 각도 변화
                float radius = wanderRadius * 0.7f;
                
                Vector3 offset = new Vector3(
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
                    0,
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius
                );
                
                return spawnPosition + offset;
            }
        }
        
        private Vector3 GetRandomDestination()
        {
            if (useGlobalExploration)
            {
                // 맵 중심 기준 전역 랜덤
                Vector3 mapCenter = Vector3.zero;
                float radius = Random.Range(wanderRadius, explorationRadius);
                
                Vector3 randomDirection = Random.insideUnitSphere * radius;
                randomDirection.y = 0;
                
                return mapCenter + randomDirection;
            }
            else
            {
                // 무작위 패턴: 현재 위치 기준 랜덤
                Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
                randomDirection.y = 0;
                return transform.position + randomDirection;
            }
        }
        
        // 전역 탐험 포인트 생성
        private Vector3 GetGlobalExplorationPoint()
        {
            // 맵 전체에서 랜덤 위치 선택
            float x = Random.Range(-explorationRadius, explorationRadius);
            float z = Random.Range(-explorationRadius, explorationRadius);
            return new Vector3(x, 0, z);
        }
        
        // 비행 수행
        private IEnumerator PerformFlight()
        {
            Debug.Log($"[LegendaryPetAI] {controller.PetName}이(가) 비행을 시작합니다!");
            
            // 비행 목적지 설정 (현재 위치에서 랜덤한 방향)
            Vector3 flyDestination = GetRandomFlightDestination();
            
            // 비행 시작
            if (controller.StartFlying(flyDestination))
            {
                // 비행이 완료될 때까지 대기
                while (controller.IsFlying)
                {
                    yield return new WaitForSeconds(0.5f);
                }
                
                // 비행 완료 후 잠시 대기
                nextActionTime = Time.time + Random.Range(2f, 4f);
            }
            else
            {
                // 비행 실패 시 일반 행동으로 전환
                yield return StartCoroutine(WanderToNewPosition());
            }
        }
        
        // 비행 목적지 생성
        private Vector3 GetRandomFlightDestination()
        {
            Vector3 destination = Vector3.zero;
            int maxAttempts = 10; // 최대 시도 횟수
            bool foundValidDestination = false;
            
            for (int i = 0; i < maxAttempts; i++)
            {
                // 현재 위치에서 랜덤한 방향으로 비행 목적지 설정
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(wanderRadius * 0.5f, wanderRadius);
                
                Vector3 candidateDestination = spawnPosition + new Vector3(
                    Mathf.Cos(angle) * distance,
                    0f,
                    Mathf.Sin(angle) * distance
                );
                
                // 맵 경계 내인지 확인 (예: -200 ~ 200 범위)
                float mapBoundary = 150f; // 안전한 범위로 설정
                if (Mathf.Abs(candidateDestination.x) > mapBoundary || 
                    Mathf.Abs(candidateDestination.z) > mapBoundary)
                {
                    continue; // 범위 밖이면 다시 시도
                }
                
                // NavMesh 상에 유효한 위치인지 확인
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(candidateDestination, out navHit, 30f, NavMesh.AllAreas))
                {
                    destination = navHit.position;
                    foundValidDestination = true;
                    Debug.Log($"[LegendaryPetAI] {controller.PetName}: 유효한 비행 목적지 생성 - {destination}");
                    break;
                }
            }
            
            // 유효한 목적지를 찾지 못한 경우, 스폰 위치 근처로 설정
            if (!foundValidDestination)
            {
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(spawnPosition, out navHit, 50f, NavMesh.AllAreas))
                {
                    destination = navHit.position;
                    Debug.LogWarning($"[LegendaryPetAI] {controller.PetName}: 기본 비행 목적지 (스폰 위치) 사용");
                }
                else
                {
                    // 최후의 수단: 현재 위치 사용
                    destination = transform.position;
                    Debug.LogError($"[LegendaryPetAI] {controller.PetName}: 비행 목적지 생성 실패, 현재 위치 사용");
                }
            }
            
            return destination;
        }
        
        // 패턴 변경 메서드 (외부에서 호출 가능)
        public void SetMovementPattern(MovementPattern pattern)
        {
            currentPattern = pattern;
            Debug.Log($"[LegendaryPetAI] {controller.PetName} 움직임 패턴 변경: {pattern}");
        }
        
        // 디버그용
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                // 배회 반경 표시
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(spawnPosition, wanderRadius);
                
                // 현재 목적지 표시
                if (agent != null && agent.hasPath)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(agent.destination, 0.5f);
                }
            }
            else
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, wanderRadius);
            }
        }
    }
}