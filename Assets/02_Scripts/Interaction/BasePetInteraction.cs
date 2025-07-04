// 모든 펫 상호작용의 기본 클래스 - 공통 행동 추가 버전
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 기본 상호작용 추상 클래스
public abstract class BasePetInteraction : MonoBehaviour
{
    // 상호작용 이름 프로퍼티
    public abstract string InteractionName { get; }

    // 해당 펫들이 이 상호작용을 할 수 있는지 확인
    public abstract bool CanInteract(PetController pet1, PetController pet2);

    // 상호작용 수행 코루틴
    public abstract IEnumerator PerformInteraction(PetController pet1, PetController pet2);

    // 두 펫의 상호작용을 시작하는 공통 메서드
    public void StartInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 상호작용 시작");
        StartCoroutine(InteractionSequence(pet1, pet2));
    }
    

    // 상호작용 시퀀스 관리
    private IEnumerator InteractionSequence(PetController pet1, PetController pet2)
    {
        // 상호작용 상태 설정
        pet1.isInteracting = true;
        pet2.isInteracting = true;
        pet1.interactionPartner = pet2;
        pet2.interactionPartner = pet1;

        // 이동 중지
        pet1.StopMovement();
        pet2.StopMovement();
        // 펫이 NavMesh 위에 있는지 확인하고 필요시 보정
        yield return StartCoroutine(EnsurePetsOnNavMesh(pet1, pet2));
        // 상호작용 유형 결정 (이 부분은 각 상호작용 구현체에서 제공해야 함)
        InteractionType interactionType = DetermineInteractionType();

        // // 감정 표현 추가 (양쪽 펫에 감정 말풍선 표시)
        // ShowInteractionEmotions(pet1, pet2, interactionType);

        // 실제 상호작용 실행
        yield return StartCoroutine(PerformInteraction(pet1, pet2));

        // 상호작용 종료
        EndInteraction(pet1, pet2);
    }

    // 새로운 메서드 추가 - 펫이 NavMesh 위에 있는지 확인하고 보정
    private IEnumerator EnsurePetsOnNavMesh(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] 펫 NavMesh 위치 확인 중...");

        // 첫 번째 펫이 NavMesh 위에 없으면 위치 조정
        if (pet1.agent != null && !pet1.agent.isOnNavMesh)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(pet1.transform.position, out navHit, 10f, NavMesh.AllAreas))
            {
                pet1.transform.position = navHit.position;
                Debug.Log($"[{InteractionName}] {pet1.petName}의 위치가 NavMesh로 조정됨");

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
                Debug.Log($"[{InteractionName}] {pet2.petName}의 위치가 NavMesh로 조정됨");

                // NavMeshAgent 재활성화 (필요 시)
                pet2.agent.enabled = false;
                yield return new WaitForSeconds(0.1f);
                pet2.agent.enabled = true;

                // 안정화 대기
                yield return new WaitForSeconds(0.5f);
            }
        }

        Debug.Log($"[{InteractionName}] 펫 NavMesh 위치 확인 완료");
    }

    // 상호작용 종료 처리 수정
    protected void EndInteraction(PetController pet1, PetController pet2)
    {
        // // 감정 말풍선 숨기기
        // if (pet1 != null) pet1.HideEmotion();
        // if (pet2 != null) pet2.HideEmotion();

        // 상호작용 상태 제거
        pet1.isInteracting = false;
        pet2.isInteracting = false;
        pet1.interactionPartner = null;
        pet2.interactionPartner = null;

        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 상호작용 종료");

        // 이동 재개 (NavMeshAgent가 활성화되어 있고 NavMesh 위에 있는 경우에만)
        if (pet1.agent != null && pet1.agent.enabled && pet1.agent.isOnNavMesh)
        {
            pet1.ResumeMovement();
            pet1.GetComponent<PetMovementController>().SetRandomDestination();
        }

        if (pet2.agent != null && pet2.agent.enabled && pet2.agent.isOnNavMesh)
        {
            pet2.ResumeMovement();
            pet2.GetComponent<PetMovementController>().SetRandomDestination();
        }

        // 상호작용 매니저에 종료 알림 추가
        if (PetInteractionManager.Instance != null)
        {
            PetInteractionManager.Instance.NotifyInteractionEnded(pet1, pet2);
        }
    }

    // 상호작용 유형 결정 메서드 (하위 클래스에서 구현)
    protected virtual InteractionType DetermineInteractionType()
    {
        // 기본 구현은 InteractionType.WalkTogether 반환
        return InteractionType.WalkTogether;
    }
    
    // 유틸리티 메서드들
    // 두 펫이 서로 마주보게 하는 함수
    protected void LookAtEachOther(PetController pet1, PetController pet2)
    {
        if (pet1.petModelTransform != null && pet2.petModelTransform != null)
        {
            // 첫 번째 펫이 두 번째 펫을 바라보도록
            Vector3 direction1 = pet2.transform.position - pet1.transform.position;
            direction1.y = 0; // 높이 무시
            if (direction1.sqrMagnitude > 0.01f)
            {
                pet1.transform.rotation = Quaternion.LookRotation(direction1);
                if (pet1.petModelTransform != null)
                {
                    pet1.petModelTransform.rotation = pet1.transform.rotation;
                }
            }

            // 두 번째 펫이 첫 번째 펫을 바라보도록
            Vector3 direction2 = pet1.transform.position - pet2.transform.position;
            direction2.y = 0; // 높이 무시
            if (direction2.sqrMagnitude > 0.01f)
            {
                pet2.transform.rotation = Quaternion.LookRotation(direction2);
                if (pet2.petModelTransform != null)
                {
                    pet2.petModelTransform.rotation = pet2.transform.rotation;
                }
            }

            Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}이(가) 서로 마주보도록 설정됨");
        }
    }

    // 한 펫이 다른 펫을 바라보게 하는 함수
    protected void LookAtOther(PetController looker, PetController target)
    {
        Vector3 direction = target.transform.position - looker.transform.position;
        direction.y = 0; // 높이 무시
        if (direction.sqrMagnitude > 0.01f)
        {
            looker.transform.rotation = Quaternion.LookRotation(direction);
            if (looker.petModelTransform != null)
            {
                looker.petModelTransform.rotation = looker.transform.rotation;
            }
        }
    }

    // NavMesh 위의 유효한 위치 찾기
    protected Vector3 FindValidPositionOnNavMesh(Vector3 targetPosition, float maxDistance = 10f)
    {
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(targetPosition, out navHit, maxDistance, NavMesh.AllAreas))
        {
            return navHit.position;
        }
        return targetPosition; // 유효한 위치를 찾지 못하면 원래 위치 반환
    }

    // ====== 추가된 공통 행동 메서드들 ======

    // 기존 MoveToPositions 메서드 개선
    protected IEnumerator MoveToPositions(PetController pet1, PetController pet2, Vector3 pos1, Vector3 pos2, float timeout = 10f)
    {
        // NavMeshAgent 상태 확인 및 설정 (개선된 부분)
        bool pet1Ready = EnsureAgentReady(pet1);
        bool pet2Ready = EnsureAgentReady(pet2);

        if (!pet1Ready || !pet2Ready)
        {
            Debug.LogWarning($"[{InteractionName}] 하나 이상의 펫 NavMeshAgent가 준비되지 않았습니다. 상호작용을 진행할 수 없습니다.");
            yield break;
        }

        // 펫들의 목적지 설정
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.SetDestination(pos1);
        pet2.agent.SetDestination(pos2);

        // 걷기 애니메이션 활성화
        pet1.GetComponent<PetAnimationController>().SetContinuousAnimation(1);
        pet2.GetComponent<PetAnimationController>().SetContinuousAnimation(1);

        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}가 목적지로 이동 중");

        // 두 펫이 목적지에 도착할 때까지 대기
        float startTime = Time.time;

        while (Time.time - startTime < timeout)
        {
            bool pet1Arrived = !pet1.agent.pathPending && pet1.agent.remainingDistance < 0.5f;
            bool pet2Arrived = !pet2.agent.pathPending && pet2.agent.remainingDistance < 0.5f;

            if (pet1Arrived && pet2Arrived)
            {
                Debug.Log($"[{InteractionName}] 두 펫이 목적지에 도착");
                break;
            }

            // 한 펫만 도착한 경우 상대를 기다림
            if (pet1Arrived && !pet2Arrived)
            {
                // 1번 펫이 2번 펫 쪽을 바라봄
                LookAtOther(pet1, pet2);
                // 도착한 펫은 걸음을 멈춤
                if (pet1.animator != null) pet1.animator.SetInteger("animation", 0);
            }

            if (pet2Arrived && !pet1Arrived)
            {
                // 2번 펫이 1번 펫 쪽을 바라봄
                LookAtOther(pet2, pet1);
                // 도착한 펫은 걸음을 멈춤
                if (pet2.animator != null) pet2.animator.SetInteger("animation", 0);
            }

            yield return null;
        }

        // 이동 완료 후 정지
        pet1.agent.isStopped = true;
        pet2.agent.isStopped = true;

        // 애니메이션 정지
        pet1.GetComponent<PetAnimationController>().StopContinuousAnimation();
        pet2.GetComponent<PetAnimationController>().StopContinuousAnimation();
    }

    // 새로 추가된 메서드 - NavMeshAgent가 준비되었는지 확인
    private bool EnsureAgentReady(PetController pet)
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

    // 상호작용 위치 찾기 (두 펫 사이의 적절한 위치)
    protected Vector3 FindInteractionSpot(PetController pet1, PetController pet2, float randomRadius = 2f)
    {
        // 두 펫 사이의 중간점 계산
        Vector3 midPoint = (pet1.transform.position + pet2.transform.position) / 2;

        // 약간의 무작위성 추가
        Vector3 randomOffset = new Vector3(Random.Range(-randomRadius, randomRadius), 0, Random.Range(-randomRadius, randomRadius));
        Vector3 interactionSpot = midPoint + randomOffset;

        // NavMesh 위의 유효한 위치 찾기
        return FindValidPositionOnNavMesh(interactionSpot, randomRadius * 2.5f);
    }

    // 상호작용 실행 전 원래 상태 저장 헬퍼 클래스
    protected class PetOriginalState
    {
        public bool agentWasStopped;
        public float originalSpeed;
        public float originalAcceleration;
        public bool agentUpdateRotation;

        public PetOriginalState(PetController pet)
        {
            if (pet.agent != null)
            {
                agentWasStopped = pet.agent.isStopped;
                originalSpeed = pet.agent.speed;
                originalAcceleration = pet.agent.acceleration;
                agentUpdateRotation = pet.agent.updateRotation;
            }
        }

        // 원래 상태 복원
        public void Restore(PetController pet)
        {
            if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
            {
                pet.agent.isStopped = agentWasStopped;
                pet.agent.speed = originalSpeed;
                pet.agent.acceleration = originalAcceleration;
                pet.agent.updateRotation = agentUpdateRotation;
            }
        }
    }

    // 사용법: 
    // PetOriginalState pet1State = new PetOriginalState(pet1);
    // try { ... } finally { pet1State.Restore(pet1); }

    // 동시에 특별 애니메이션 재생
    protected IEnumerator PlaySimultaneousAnimations(
        PetController pet1, PetController pet2,
        int anim1, int anim2,
        float duration = 2.0f)
    {
        // 두 펫의 애니메이션 컨트롤러
        PetAnimationController anim1Controller = pet1.GetComponent<PetAnimationController>();
        PetAnimationController anim2Controller = pet2.GetComponent<PetAnimationController>();

        // 동시에 애니메이션 시작
        StartCoroutine(anim1Controller.PlayAnimationWithCustomDuration(anim1, duration, false, false));
        yield return StartCoroutine(anim2Controller.PlayAnimationWithCustomDuration(anim2, duration, false, false));
    }

    // BasePetInteraction에 추가할 메서드들
    // 승자 결정 메서드
    protected PetController DetermineWinner(PetController pet1, PetController pet2, float probability = 0.5f)
    {
        bool pet1IsWinner = Random.value > probability;
        return pet1IsWinner ? pet1 : pet2;
    }

    // 승자 패자 애니메이션 재생 메서드
    protected IEnumerator PlayWinnerLoserAnimations(
        PetController winner, PetController loser,
        int winnerAnim = 3, int loserAnim = 4)
    {
        Debug.Log($"[{InteractionName}] 결과: {winner.petName}이(가) 승리!");

        // 승자와 패자 애니메이션 동시 재생
        PetAnimationController winnerAnimController = winner.GetComponent<PetAnimationController>();
        PetAnimationController loserAnimController = loser.GetComponent<PetAnimationController>();

        StartCoroutine(winnerAnimController.PlayAnimationWithCustomDuration(winnerAnim, 2.0f, false, false));
        yield return StartCoroutine(loserAnimController.PlayAnimationWithCustomDuration(loserAnim, 2.0f, false, false));
    }

    // 상호작용에 대한 시작 위치 계산 (나란히)
    protected void CalculateStartPositions(
        PetController pet1, PetController pet2,
        out Vector3 pet1Pos, out Vector3 pet2Pos,
        float spacing = 3f)
    {
        // 두 펫의 중간 지점
        Vector3 midPoint = (pet1.transform.position + pet2.transform.position) / 2;

        // 랜덤 방향 설정
        Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        Vector3 perpDirection = Vector3.Cross(Vector3.up, randomDirection).normalized;

        // 각 펫의 위치 계산
        pet1Pos = midPoint - perpDirection * (spacing / 2);
        pet2Pos = midPoint + perpDirection * (spacing / 2);

        // NavMesh 보정
        pet1Pos = FindValidPositionOnNavMesh(pet1Pos, spacing * 2);
        pet2Pos = FindValidPositionOnNavMesh(pet2Pos, spacing * 2);
    }
}