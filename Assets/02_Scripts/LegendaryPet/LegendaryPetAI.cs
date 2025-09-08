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
        [SerializeField] private float wanderRadius = 30f;          // 배회 반경
        [SerializeField] private float minWanderDistance = 10f;     // 최소 이동 거리
        [SerializeField] private float idleTimeMin = 3f;           // 최소 대기 시간
        [SerializeField] private float idleTimeMax = 8f;           // 최대 대기 시간
        [SerializeField] private float specialAnimationChance = 0.1f; // 특별 애니메이션 확률
        
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
            // 특별 애니메이션 확률 체크
            if (Random.value < specialAnimationChance)
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
                
                controller.StopMoving();
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
            
            float distance = Random.Range(wanderRadius * 0.6f, wanderRadius);
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
        
        private Vector3 GetRandomDestination()
        {
            // 무작위 패턴: 완전히 랜덤한 위치
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection.y = 0;
            return transform.position + randomDirection;
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