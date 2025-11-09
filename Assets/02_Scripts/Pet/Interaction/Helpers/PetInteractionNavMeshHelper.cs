using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 펫 상호작용에서 NavMesh 관련 기능을 제공하는 헬퍼 클래스
/// BasePetInteraction에서 분리하여 코드 재사용성을 높입니다
/// </summary>
public static class PetInteractionNavMeshHelper
{
    /// <summary>
    /// NavMesh 위의 유효한 위치를 찾습니다
    /// </summary>
    /// <param name="targetPosition">찾고자 하는 대상 위치</param>
    /// <param name="maxDistance">검색할 최대 거리</param>
    /// <returns>유효한 NavMesh 위치</returns>
    public static Vector3 FindValidPositionOnNavMesh(Vector3 targetPosition, float maxDistance = 10f)
    {
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(targetPosition, out navHit, maxDistance, NavMesh.AllAreas))
        {
            return navHit.position;
        }
        return targetPosition; // 유효한 위치를 찾지 못하면 원래 위치 반환
    }

    /// <summary>
    /// 펫의 NavMeshAgent가 준비되었는지 확인하고 필요시 수정합니다
    /// </summary>
    public static bool EnsureAgentReady(PetController pet)
    {
        if (pet == null || pet.agent == null)
            return false;

        // 에이전트가 활성화되지 않았다면 활성화 시도
        if (!pet.agent.enabled)
        {
            pet.agent.enabled = true;
            // 활성화 후에도 확인
            if (!pet.agent.enabled)
                return false;
        }

        // NavMesh 위에 있는지 확인
        if (!pet.agent.isOnNavMesh)
        {
            // NavMesh 위치 찾기 시도
            NavMeshHit hit;
            if (NavMesh.SamplePosition(pet.transform.position, out hit, 10f, NavMesh.AllAreas))
            {
                // 위치 조정
                pet.transform.position = hit.position;

                // 에이전트 재활성화
                pet.agent.enabled = false;
                pet.agent.enabled = true;

                // 다시 확인
                if (!pet.agent.isOnNavMesh)
                    return false;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 펫의 NavMeshAgent가 안전하게 준비되었는지 확인합니다
    /// </summary>
    public static bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null &&
               pet.agent != null &&
               pet.agent.enabled &&
               pet.agent.isOnNavMesh;
    }

    /// <summary>
    /// 두 펫이 NavMesh 위에 있는지 확인하고 필요시 보정합니다
    /// </summary>
    public static IEnumerator EnsurePetsOnNavMesh(PetController pet1, PetController pet2, string interactionName)
    {
        // Debug.Log($"[{interactionName}] 펫 NavMesh 위치 확인 중...");

        // 첫 번째 펫이 NavMesh 위에 없으면 위치 조정
        if (pet1.agent != null && !pet1.agent.isOnNavMesh)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(pet1.transform.position, out navHit, 10f, NavMesh.AllAreas))
            {
                pet1.transform.position = navHit.position;
                // Debug.Log($"[{interactionName}] {pet1.petName}의 위치가 NavMesh로 조정됨");

                // NavMeshAgent 재활성화 (필요 시)
                pet1.agent.enabled = false;
                yield return new WaitForSeconds(0.1f);
                pet1.agent.enabled = true;

                // 안정화 대기
                yield return new WaitForSeconds(0.5f);
            }
        }

        // 두 번째 펫도 동일한 처리
        if (pet2.agent != null && !pet2.agent.isOnNavMesh)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(pet2.transform.position, out navHit, 10f, NavMesh.AllAreas))
            {
                pet2.transform.position = navHit.position;
                // Debug.Log($"[{interactionName}] {pet2.petName}의 위치가 NavMesh로 조정됨");

                // NavMeshAgent 재활성화 (필요 시)
                pet2.agent.enabled = false;
                yield return new WaitForSeconds(0.1f);
                pet2.agent.enabled = true;

                // 안정화 대기
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Debug.Log($"[{interactionName}] 펫 NavMesh 위치 확인 완료");
    }

    /// <summary>
    /// 지정된 펫의 NavMeshAgent가 활성화되고 NavMesh 위에 준비될 때까지 대기합니다
    /// </summary>
    public static IEnumerator WaitUntilAgentIsReady(PetController pet, string interactionName, float timeout = 2.0f)
    {
        float timer = 0f;
        int retryCount = 0;
        const int maxRetries = 2;

        while (timer < timeout)
        {
            // NavMeshAgent가 유효하고, 활성화되어 있으며, NavMesh 위에 있는지 확인
            if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
            {
                // 안정성을 위해 한 프레임 더 대기 후 종료
                yield return null;
                yield break; // 코루틴 정상 종료
            }

            // 일정 시간마다 agent 재활성화 시도
            if (timer > timeout / 2 && retryCount < maxRetries && pet.agent != null)
            {
                retryCount++;
                Debug.LogWarning($"[{interactionName}] {pet.petName}의 NavMeshAgent 재활성화 시도 {retryCount}/{maxRetries}");

                // Agent 재활성화
                pet.agent.enabled = false;
                yield return new WaitForSeconds(0.1f);
                pet.agent.enabled = true;

                // 위치 재설정
                if (pet.agent.enabled)
                {
                    pet.agent.Warp(pet.transform.position);
                }
            }

            timer += Time.deltaTime;
            yield return null; // 다음 프레임까지 대기
        }
        Debug.LogWarning($"[{interactionName}] {pet.petName}의 NavMeshAgent가 {timeout}초 내에 준비되지 않았습니다.");
    }

    /// <summary>
    /// 상호작용 위치를 찾습니다 (두 펫 사이의 적절한 위치)
    /// </summary>
    public static Vector3 FindInteractionSpot(PetController pet1, PetController pet2, float randomRadius = 2f)
    {
        // 두 펫 사이의 중간점 계산
        Vector3 midPoint = (pet1.transform.position + pet2.transform.position) / 2;

        // 약간의 무작위성 추가
        Vector3 randomOffset = new Vector3(
            Random.Range(-randomRadius, randomRadius),
            0,
            Random.Range(-randomRadius, randomRadius)
        );
        Vector3 interactionSpot = midPoint + randomOffset;

        // NavMesh 위의 유효한 위치 찾기
        return FindValidPositionOnNavMesh(interactionSpot, randomRadius * 2.5f);
    }

    /// <summary>
    /// 펫이 목적지에 도착했는지 확인합니다
    /// </summary>
    public static bool HasReachedDestination(PetController pet, float threshold = 0.5f)
    {
        if (!IsAgentSafelyReady(pet))
            return false;

        return !pet.agent.pathPending && pet.agent.remainingDistance < threshold;
    }

    /// <summary>
    /// 안전하게 목적지를 설정합니다
    /// </summary>
    public static bool TrySetDestination(PetController pet, Vector3 destination)
    {
        if (!IsAgentSafelyReady(pet))
            return false;

        Vector3 validPosition = FindValidPositionOnNavMesh(destination);
        return pet.agent.SetDestination(validPosition);
    }

    /// <summary>
    /// 펫의 NavMeshAgent를 안전하게 정지시킵니다
    /// </summary>
    public static void SafeStopAgent(PetController pet)
    {
        if (IsAgentSafelyReady(pet))
        {
            pet.agent.isStopped = true;
            pet.agent.ResetPath();
        }
    }

    /// <summary>
    /// 펫의 NavMeshAgent를 안전하게 재개합니다
    /// </summary>
    public static void SafeResumeAgent(PetController pet)
    {
        if (IsAgentSafelyReady(pet))
        {
            pet.agent.isStopped = false;
        }
    }
}