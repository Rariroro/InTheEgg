// 이동과 랜덤 목적지 설정 전담
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class PetMovementController : MonoBehaviour
{
    private PetController petController; // PetController 스크립트에 대한 참조
    private float wanderTimer = 0f;     // 랜덤한 위치로 이동하는 주기를 결정하는 타이머

    // PetController 초기화 함수
    public void Init(PetController controller)
    {
        petController = controller;
        // 초기화 시점에 바로 SetRandomDestination을 호출하지 않고
        // 지연 호출하도록 변경
        StartCoroutine(DelayedSetDestination());
    }
    private IEnumerator DelayedSetDestination()
    {
        // NavMeshAgent가 준비될 때까지 대기
        yield return new WaitForSeconds(0.5f); // 잠시 대기

        // NavMeshAgent가 활성화되어 있고 NavMesh에 있는지 확인 후 목적지 설정
        if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
        {
            SetRandomDestination();
        }
    }
    // 펫의 움직임을 업데이트하는 함수
    public void UpdateMovement()
    {
        // 펫이 모인 상태라면 움직임 업데이트를 하지 않습니다.
        if (petController.isGathered)
        {
            if (petController.petModelTransform != null && Camera.main != null)
            {
                // 카메라와 펫 사이의 수평 방향 벡터 계산 (y축은 무시하여 수평 회전만 고려)
                Vector3 direction = Camera.main.transform.position - petController.petModelTransform.position;
                direction.y = 0f;
                // 벡터의 크기가 너무 작으면(펫과 카메라가 매우 가까우면) 회전하지 않음.
                if (direction.magnitude > 0.1f)
                {
                    // 목표 회전 방향 계산
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    // 현재 회전에서 목표 회전으로 부드럽게 회전 (Slerp 사용, rotationSpeed에 따라 속도 조절)
                    petController.petModelTransform.rotation = Quaternion.Slerp(
                        petController.petModelTransform.rotation,
                        targetRotation,
                        petController.rotationSpeed * Time.deltaTime
                    );
                }
            }
            return; // 모인 상태에서는 추가적인 이동 로직을 실행하지 않고 함수 종료
        }

        // 펫이 모이지 않은 상태라면 기존 이동 로직 실행
        // NavMeshAgent가 비활성화되었거나 NavMesh 위에 있지 않으면 이동 업데이트를 건너뜁니다.
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        // NavMeshAgent가 정지된 상태라면 이동 업데이트를 건너뜁니다.
        if (petController.agent.isStopped)
            return;

        // 목적지에 거의 도착했는지 확인 (pathPending: 경로 계산 중인지, remainingDistance: 남은 거리)
        // 목적지 근처에 도달했거나, 속도가 매우 낮은 상태로 일정 시간이 지나면 새 목적지 설정
        if ((!petController.agent.pathPending && petController.agent.remainingDistance < 1.0f) ||
            (petController.agent.velocity.magnitude < 0.2f && wanderTimer > 2.0f))
        {
            wanderTimer += Time.deltaTime;
            if (wanderTimer >= GetWanderInterval())
            {
                SetRandomDestination();
                wanderTimer = 0f;
            }
        }
        else
        {
            // 이동 중일 때도 wanderTimer를 천천히 증가시켜, 장기간 목적지에 도달하지 못하는 경우에도 대응
            wanderTimer += Time.deltaTime * 0.1f;
        }

        // petModelTransform을 NavMeshAgent 위치에 동기화 (모델의 위치를 에이전트 위치와 일치)
        if (petController.petModelTransform != null)
        {
            petController.petModelTransform.position = transform.position;
        }
    }


    // 랜덤 목적지 설정 (NavMesh 사용)
    public void SetRandomDestination()
    {
        // NavMeshAgent 상태 확인 추가
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
        {
            Debug.LogWarning($"펫 '{petController.petName}'의 NavMeshAgent가 준비되지 않았습니다. 랜덤 목적지 설정을 건너뜁니다.");
            return; // 조건을 만족하지 않으면 함수 종료
        }

        // 기존 코드는 그대로 유지
        Vector3 randomDirection = Random.insideUnitSphere * 30f;
        randomDirection += transform.position;

        NavMeshHit hit;
        Vector3 finalPosition = transform.position;

        if (NavMesh.SamplePosition(randomDirection, out hit, 10f, NavMesh.AllAreas))
        {
            finalPosition = hit.position;
        }
        petController.agent.SetDestination(finalPosition);
    }
    // 펫의 성격에 따라 랜덤 이동 간격(시간)을 반환하는 함수
    private float GetWanderInterval()
    {
        switch (petController.personality)
        {
            // case PetAIProperties.Personality.Lazy: return Random.Range(5f, 10f); // 게으른 성격: 5~10초
            // case PetAIProperties.Personality.Shy: return Random.Range(3f, 7f);  // 수줍은 성격: 3~7초
            // case PetAIProperties.Personality.Brave: return Random.Range(2f, 5f); // 용감한 성격: 2~5초
            // case PetAIProperties.Personality.Playful: return Random.Range(1f, 3f); // 활발한 성격: 1~3초
            case PetAIProperties.Personality.Lazy: return Random.Range(0f, 0f); // 게으른 성격: 5~10초
            case PetAIProperties.Personality.Shy: return Random.Range(0f, 0f);  // 수줍은 성격: 3~7초
            case PetAIProperties.Personality.Brave: return Random.Range(0f, 0f); // 용감한 성격: 2~5초
            case PetAIProperties.Personality.Playful: return Random.Range(0f, 0f); // 활발한 성격: 1~3초
            default: return 3f; // 기본값: 3초
        }
    }
}