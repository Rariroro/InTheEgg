using UnityEngine;
using UnityEngine.AI;

namespace InTheEgg.Extensions
{
    /// <summary>
    /// NavMeshAgent 관련 확장 메서드
    /// 반복되는 null 체크와 상태 확인 로직을 간소화
    /// </summary>
    public static class NavMeshAgentExtensions
    {
        /// <summary>
        /// NavMeshAgent가 사용 가능한 상태인지 확인
        /// </summary>
        /// <param name="agent">확인할 NavMeshAgent</param>
        /// <returns>사용 가능하면 true, 아니면 false</returns>
        public static bool IsReady(this NavMeshAgent agent)
        {
            return agent != null && agent.enabled && agent.isOnNavMesh;
        }

        /// <summary>
        /// NavMeshAgent가 활성화되어 있는지 확인 (NavMesh 위치 확인 없이)
        /// </summary>
        /// <param name="agent">확인할 NavMeshAgent</param>
        /// <returns>활성화되어 있으면 true, 아니면 false</returns>
        public static bool IsActive(this NavMeshAgent agent)
        {
            return agent != null && agent.enabled;
        }

        /// <summary>
        /// 안전하게 목적지 설정 시도
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <param name="destination">목적지 위치</param>
        /// <returns>설정 성공 여부</returns>
        public static bool TrySetDestination(this NavMeshAgent agent, Vector3 destination)
        {
            if (!agent.IsReady())
            {
                return false;
            }

            return agent.SetDestination(destination);
        }

        /// <summary>
        /// 안전하게 이동 중지
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <returns>중지 성공 여부</returns>
        public static bool TryStop(this NavMeshAgent agent)
        {
            if (!agent.IsReady())
            {
                return false;
            }

            agent.isStopped = true;
            agent.ResetPath();
            return true;
        }

        /// <summary>
        /// 안전하게 이동 재개
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <returns>재개 성공 여부</returns>
        public static bool TryResume(this NavMeshAgent agent)
        {
            if (!agent.IsReady())
            {
                return false;
            }

            agent.isStopped = false;
            return true;
        }

        /// <summary>
        /// 목적지에 도착했는지 확인
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <param name="threshold">도착 판정 거리 (기본: 0.5f)</param>
        /// <returns>도착했으면 true, 아니면 false</returns>
        public static bool HasReachedDestination(this NavMeshAgent agent, float threshold = 0.5f)
        {
            if (!agent.IsReady())
            {
                return false;
            }

            if (agent.pathPending)
            {
                return false;
            }

            if (agent.hasPath && agent.remainingDistance > threshold)
            {
                return false;
            }

            // velocity가 거의 0인지 확인 (완전히 멈춰있는 상태)
            return agent.velocity.sqrMagnitude < 0.01f;
        }

        /// <summary>
        /// 경로 계산 중인지 확인
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <returns>경로 계산 중이면 true, 아니면 false</returns>
        public static bool IsCalculatingPath(this NavMeshAgent agent)
        {
            return agent != null && agent.enabled && agent.pathPending;
        }

        /// <summary>
        /// 유효한 경로를 가지고 있는지 확인
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <returns>유효한 경로가 있으면 true, 아니면 false</returns>
        public static bool HasValidPath(this NavMeshAgent agent)
        {
            if (!agent.IsReady())
            {
                return false;
            }

            return agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete;
        }

        /// <summary>
        /// 안전하게 속도 설정
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <param name="speed">설정할 속도</param>
        /// <param name="angularSpeed">설정할 회전 속도 (선택)</param>
        /// <param name="acceleration">설정할 가속도 (선택)</param>
        /// <returns>설정 성공 여부</returns>
        public static bool TrySetSpeed(this NavMeshAgent agent, float speed,
            float? angularSpeed = null, float? acceleration = null)
        {
            if (!agent.IsActive())
            {
                return false;
            }

            agent.speed = speed;

            if (angularSpeed.HasValue)
            {
                agent.angularSpeed = angularSpeed.Value;
            }

            if (acceleration.HasValue)
            {
                agent.acceleration = acceleration.Value;
            }

            return true;
        }

        /// <summary>
        /// 속도 배수 설정 (기본 속도에 대한 배수)
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <param name="baseSpeed">기본 속도</param>
        /// <param name="baseAngularSpeed">기본 회전 속도</param>
        /// <param name="baseAcceleration">기본 가속도</param>
        /// <param name="speedMultiplier">속도 배수</param>
        /// <param name="angularMultiplier">회전 속도 배수</param>
        /// <param name="accelerationMultiplier">가속도 배수</param>
        /// <returns>설정 성공 여부</returns>
        public static bool TrySetSpeedMultipliers(this NavMeshAgent agent,
            float baseSpeed, float baseAngularSpeed, float baseAcceleration,
            float speedMultiplier = 1f, float angularMultiplier = 1f, float accelerationMultiplier = 1f)
        {
            if (!agent.IsActive())
            {
                return false;
            }

            agent.speed = baseSpeed * speedMultiplier;
            agent.angularSpeed = baseAngularSpeed * angularMultiplier;
            agent.acceleration = baseAcceleration * accelerationMultiplier;

            return true;
        }

        /// <summary>
        /// NavMesh 위로 안전하게 이동 (워프)
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <param name="position">이동할 위치</param>
        /// <param name="maxDistance">NavMesh 검색 최대 거리 (기본: 10f)</param>
        /// <returns>이동 성공 여부</returns>
        public static bool TryWarp(this NavMeshAgent agent, Vector3 position, float maxDistance = 10f)
        {
            if (agent == null || !agent.enabled)
            {
                return false;
            }

            // NavMesh 위의 가장 가까운 점 찾기
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                return agent.Warp(hit.position);
            }

            return false;
        }

        /// <summary>
        /// 막힌 상태인지 확인 (이동 중이지만 실제로 움직이지 않는 경우)
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <param name="velocityThreshold">속도 임계값 (기본: 0.1f)</param>
        /// <returns>막혀있으면 true, 아니면 false</returns>
        public static bool IsStuck(this NavMeshAgent agent, float velocityThreshold = 0.1f)
        {
            if (!agent.IsReady())
            {
                return false;
            }

            // 경로가 있고 멈춤 상태가 아닌데 속도가 매우 낮은 경우
            return agent.hasPath &&
                   !agent.isStopped &&
                   agent.velocity.magnitude < velocityThreshold &&
                   agent.remainingDistance > agent.stoppingDistance;
        }

        /// <summary>
        /// 남은 거리 안전하게 가져오기
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <returns>남은 거리, 유효하지 않으면 float.MaxValue</returns>
        public static float GetRemainingDistance(this NavMeshAgent agent)
        {
            if (!agent.IsReady() || !agent.hasPath)
            {
                return float.MaxValue;
            }

            return agent.remainingDistance;
        }

        /// <summary>
        /// 디버그용 상태 문자열 반환
        /// </summary>
        /// <param name="agent">NavMeshAgent</param>
        /// <returns>현재 상태를 설명하는 문자열</returns>
        public static string GetDebugStatus(this NavMeshAgent agent)
        {
            if (agent == null)
                return "NavMeshAgent: null";

            if (!agent.enabled)
                return "NavMeshAgent: disabled";

            if (!agent.isOnNavMesh)
                return "NavMeshAgent: not on NavMesh";

            if (agent.pathPending)
                return "NavMeshAgent: calculating path";

            if (!agent.hasPath)
                return "NavMeshAgent: no path";

            if (agent.isStopped)
                return $"NavMeshAgent: stopped (distance: {agent.remainingDistance:F2})";

            return $"NavMeshAgent: moving (distance: {agent.remainingDistance:F2}, speed: {agent.velocity.magnitude:F2})";
        }
    }
}