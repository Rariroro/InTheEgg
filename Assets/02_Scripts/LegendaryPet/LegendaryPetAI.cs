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
        [SerializeField] private float wanderRadius = 200f;          // 탐색 반경 (일반 펫보다 넓게)
        [SerializeField] private float idleTimeMin = 3f;            // 최소 대기 시간
        [SerializeField] private float idleTimeMax = 8f;            // 최대 대기 시간
        
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
            // spawnPosition은 Start에서 설정
        }

        private void Start()
        {
            // 지면 위치 찾기 - 공중에서 스폰된 경우를 대비
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, 1000f))
            {
                spawnPosition = hit.point;
                Debug.Log($"[LegendaryPetAI] {controller.PetName}: 지면 spawnPosition 설정 - {spawnPosition}");
            }
            else
            {
                // Raycast 실패 시 현재 위치 사용
                spawnPosition = transform.position;
                Debug.LogWarning($"[LegendaryPetAI] {controller.PetName}: 지면을 찾을 수 없음, 현재 위치 사용 - {spawnPosition}");
            }
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
                // 날아다니는 상태나 NavMeshAgent가 비활성화된 상태에서는 AI 동작 중지
                if (controller.IsFlying || (agent != null && !agent.enabled))
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }

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
            // NavMeshAgent 체크
            if (agent == null || !agent.enabled)
            {
                yield break;
            }

            Vector3 destination = GetWanderDestination();

            if (destination != Vector3.zero)
            {
                controller.MoveTo(destination);

                // 목적지 도달까지 대기
                while (agent.enabled && agent.pathPending ||
                       (agent.enabled && agent.hasPath && agent.remainingDistance > agent.stoppingDistance))
                {
                    yield return new WaitForSeconds(0.5f);

                    // NavMeshAgent가 비활성화되면 중단
                    if (!agent.enabled)
                    {
                        yield break;
                    }

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
            int maxRetries = 3; // 재시도 횟수
            
            for (int retry = 0; retry < maxRetries; retry++)
            {
                // 전역 맵에서 목적지 선택
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
                
                // NavMesh 상의 유효한 위치 찾기
                if (NavMesh.SamplePosition(destination, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                {
                    // 펫 타입별 높이 제한 설정
                    float maxHeightDifference = 25f; // 기본값 15m (이전 5m에서 완화)
                    
                    // 날 수 있는 펫은 더 큰 높이 차이 허용
                    if (controller.Traits.canFly)
                    {
                        maxHeightDifference = 20f;
                    }
                    
                    // 높이 차이 검증
                    float heightDifference = Mathf.Abs(hit.position.y - transform.position.y);
                    if (heightDifference > maxHeightDifference)
                    {
                        // 지면 Raycast로 올바른 높이 찾기
                        if (Physics.Raycast(destination + Vector3.up * 10f, Vector3.down, out RaycastHit groundHit, 30f))
                        {
                            destination.y = groundHit.point.y;
                            
                            // 다시 NavMesh 샘플링 시도
                            if (NavMesh.SamplePosition(destination, out NavMeshHit newHit, 10f, NavMesh.AllAreas))
                            {
                                // 새로운 위치도 높이 차이 검증
                                if (Mathf.Abs(newHit.position.y - transform.position.y) <= maxHeightDifference)
                                {
        // Debug.Log($"[LegendaryPetAI] {controller.PetName}: 높이 조정 후 유효한 목적지 찾음");
                                    return newHit.position;
                                }
                            }
                        }
                        
                        // 재시도할 경우 경고는 마지막에만 출력
                        if (retry == maxRetries - 1)
                        {
                            Debug.LogWarning($"[LegendaryPetAI] {controller.PetName}: 목적지 높이 차이가 너무 큼 ({heightDifference:F1}m > {maxHeightDifference}m) - {retry + 1}번 시도 실패");
                        }
                        else
                        {
        // Debug.Log($"[LegendaryPetAI] {controller.PetName}: 높이 차이 {heightDifference:F1}m - 재시도 {retry + 1}/{maxRetries}");
                        }
                        continue; // 다음 재시도
                    }
                    
                    // 유효한 목적지 찾음
                    if (heightDifference > 5f)
                    {
        // Debug.Log($"[LegendaryPetAI] {controller.PetName}: 높이 차이 {heightDifference:F1}m의 목적지로 이동");
                    }
                    return hit.position;
                }
            }
            
            Debug.LogWarning($"[LegendaryPetAI] {controller.PetName}: {maxRetries}번 시도 후 유효한 목적지를 찾지 못함");
            return Vector3.zero;
        }
        
        private Vector3 GetElegantDestination()
        {
            // 우아한 움직임: 현재 위치에서 먼 거리로 직선 이동
            Vector3 direction = Random.insideUnitSphere;
            direction.y = 0;
            direction = direction.normalized;
            
            // 현재 위치 기준 랜덤 거리
            float distance = Random.Range(wanderRadius * 0.5f, wanderRadius);
            
            return transform.position + direction * distance;
        }
        
        private Vector3 GetMajesticDestination()
        {
            // 장엄한 움직임: 현재 위치에서 짧은 거리로 천천히
            Vector3 randomDirection = Random.insideUnitSphere * (wanderRadius * 0.3f);
            randomDirection.y = 0;
            return transform.position + randomDirection;
        }
        
        private Vector3 GetPlayfulDestination()
        {
            // 장난스러운 움직임: 현재 위치에서 랜덤
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection.y = 0;
            return transform.position + randomDirection;
        }
        
        private Vector3 GetPatrolDestination()
        {
            // 순찰 패턴: 현재 위치에서 원형으로 순찰
            float angle = Time.time * 10f; // 시간에 따라 각도 변화
            float radius = wanderRadius * 0.7f;
            
            Vector3 offset = new Vector3(
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
                0,
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius
            );
            
            return transform.position + offset;
        }
        
        private Vector3 GetRandomDestination()
        {
            // 현재 위치에서 랜덤 위치
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection.y = 0;
            return transform.position + randomDirection;
        }
        
        // 비행 수행
        private IEnumerator PerformFlight()
        {
        // Debug.Log($"[LegendaryPetAI] {controller.PetName}이(가) 비행을 시작합니다!");
            
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
                // 비행 목적지 설정 (현재 위치에서 랜덤)
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(wanderRadius * 0.5f, wanderRadius);
                
                Vector3 candidateDestination = transform.position + new Vector3(
                    Mathf.Cos(angle) * distance,
                    0f,
                    Mathf.Sin(angle) * distance
                );
                
                // 너무 멀리 가지 않도록 거리 체크 (선택적)
                if (Vector3.Distance(transform.position, candidateDestination) > wanderRadius * 1.5f)
                {
                    continue; // 너무 멀면 다시 시도
                }
                
                // NavMesh 상에 유효한 위치인지 확인 - 검색 범위 확대 (30f → 100f)
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(candidateDestination, out navHit, 100f, NavMesh.AllAreas))
                {
                    destination = navHit.position;
                    foundValidDestination = true;
        // Debug.Log($"[LegendaryPetAI] {controller.PetName}: 유효한 비행 목적지 생성 - {destination}");
                    break;
                }
            }
            
            // 유효한 목적지를 찾지 못한 경우, 스폰 위치 근처로 설정
            if (!foundValidDestination)
            {
                NavMeshHit navHit;
                // 검색 범위를 더 크게 확대 (50f → 200f)
                if (NavMesh.SamplePosition(spawnPosition, out navHit, 200f, NavMesh.AllAreas))
                {
                    destination = navHit.position;
                    Debug.LogWarning($"[LegendaryPetAI] {controller.PetName}: 기본 비행 목적지 (스폰 위치) 사용");
                }
                else
                {
                    // 최후의 수단: 현재 위치에서 아래로 레이캐스트하여 지면 찾기
                    RaycastHit groundHit;
                    if (Physics.Raycast(transform.position, Vector3.down, out groundHit, 1000f))
                    {
                        // 지면에서 NavMesh 위치 찾기
                        if (NavMesh.SamplePosition(groundHit.point, out navHit, 100f, NavMesh.AllAreas))
                        {
                            destination = navHit.position;
                            Debug.LogWarning($"[LegendaryPetAI] {controller.PetName}: 지면 기반 비행 목적지 사용");
                        }
                        else
                        {
                            destination = transform.position;
                            Debug.LogError($"[LegendaryPetAI] {controller.PetName}: 비행 목적지 생성 실패, 현재 위치 사용");
                        }
                    }
                    else
                    {
                        destination = transform.position;
                        Debug.LogError($"[LegendaryPetAI] {controller.PetName}: 지면을 찾을 수 없음, 현재 위치 사용");
                    }
                }
            }
            
            return destination;
        }

        // 착륙 완료 시 spawnPosition 업데이트 (외부에서 호출)
        public void UpdateSpawnPosition(Vector3 groundPosition)
        {
            spawnPosition = groundPosition;
            Debug.Log($"[LegendaryPetAI] {controller.PetName}: spawnPosition 업데이트 - {spawnPosition}");
        }

        // 패턴 변경 메서드 (외부에서 호출 가능)
        public void SetMovementPattern(MovementPattern pattern)
        {
            currentPattern = pattern;
        // Debug.Log($"[LegendaryPetAI] {controller.PetName} 움직임 패턴 변경: {pattern}");
        }
        
    }
}