// 모든 펫 상호작용의 기본 클래스 - 공통 행동 추가 버전
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 기본 상호작용 추상 클래스
public abstract class BasePetInteraction : MonoBehaviour
{
    // 상호작용 이름 프로퍼티
    public abstract string InteractionName { get; }
    // ▼▼▼ [수정] 인스펙터에서 상호작용 시작 거리를 조절할 수 있는 변수 추가 ▼▼▼
    [Header("Common Interaction Settings")]
    [Tooltip("상호작용 시작 시 펫들이 유지할 기본 거리입니다.")]
    public float interactionStartDistance = 5f;
    // ▲▲▲ [여기까지 수정] ▲▲▲
    // 해당 펫들이 이 상호작용을 할 수 있는지 확인
    public abstract bool CanInteract(PetController pet1, PetController pet2);
    // ★★★ 핵심 변경: 상호작용의 시작을 책임지는 새로운 public 메서드 ★★★
    public void StartInteraction(PetController pet1, PetController pet2)
    {
        // Debug.Log("StartInteraction");
        // 모이기 상태 체크 - 모이기 중이면 상호작용 시작하지 않음
        if ((pet1 != null && (pet1.State.CurrentStatus == PetStatus.GatheringInProgress ||
                              pet1.State.CurrentStatus == PetStatus.GatheredWaiting)) ||
            (pet2 != null && (pet2.State.CurrentStatus == PetStatus.GatheringInProgress ||
                              pet2.State.CurrentStatus == PetStatus.GatheredWaiting)))
        {
            // Debug.Log($"[{InteractionName}] 모이기 상태로 인해 상호작용 시작이 차단됨");
            return;
        }
        // Debug.Log("StartInteraction2");

        // ✅ 토스트 알림을 준비 완료 후로 이동 (InteractionLifecycle에서 발생)

        // PetInteractionManager에서 직접 이 코루틴을 시작합니다.
        StartCoroutine(InteractionLifecycle(pet1, pet2));
    }

    /// <summary>
    /// 상호작용의 전체 생명주기(준비, 실행, 정리)를 관리하는 코루틴입니다.
    /// </summary>
    private IEnumerator InteractionLifecycle(PetController pet1, PetController pet2)
    {
        // Debug.Log("InteractionLifecycle");

        // 1. 사전 준비 단계
        // Debug.Log($"[{InteractionName}] 상호작용 준비: {pet1.petName} & {pet2.petName}");

        // ★★★ Activity 시스템에서 자동으로 처리됨 ★★★

        // ★ [Phase 4] PetState를 통한 상태 업데이트
        pet1.State.StartInteraction(pet2);
        pet1.State.SetInteractionLogic(this);
        // Debug.Log("InteractionLifecycle2");

        pet2.State.StartInteraction(pet1);
        pet2.State.SetInteractionLogic(this);
        // Debug.Log("InteractionLifecycle3");

        // 이동 중지
        pet1.StopMovement();
        pet2.StopMovement();

        // (선택사항) 펫이 NavMesh 위에 있는지 최종 확인
        yield return StartCoroutine(EnsurePetsOnNavMesh(pet1, pet2));

        // NavMesh 확인 실패 시 조용히 종료
        if (pet1.agent == null || !pet1.agent.isOnNavMesh || pet2.agent == null || !pet2.agent.isOnNavMesh)
        {
            // Debug.LogWarning($"[{InteractionName}] NavMesh 준비 실패로 상호작용 취소");
            yield break;
        }

        // ▼▼▼ [수정] 상호작용 시작 시 펫들을 지정된 거리로 자연스럽게 이동시키는 로직 추가 ▼▼▼
        // Debug.Log($"[{InteractionName}] 상호작용 시작을 위해 펫들을 정렬합니다. 목표 거리: {interactionStartDistance}m");

        // 펫 크기에 따른 거리 조정
        float adjustedDistance = CalculateAdjustedDistance(pet1, pet2);

        // 펫들이 서로 마주볼 위치 계산
        Vector3 direction = (pet2.transform.position - pet1.transform.position).normalized;
        if (direction == Vector3.zero) direction = pet1.transform.forward; // 위치가 겹쳤을 경우를 대비
        Vector3 midpoint = (pet1.transform.position + pet2.transform.position) / 2f;

        Vector3 pet1TargetPos = midpoint - direction * (adjustedDistance / 2f);
        Vector3 pet2TargetPos = midpoint + direction * (adjustedDistance / 2f);

        pet1TargetPos = FindValidPositionOnNavMesh(pet1TargetPos);
        pet2TargetPos = FindValidPositionOnNavMesh(pet2TargetPos);

        // 계산된 위치로 이동
        yield return StartCoroutine(MoveToPositions(pet1, pet2, pet1TargetPos, pet2TargetPos, 10f));

        // 이동 중단 체크 (터치/홀드/모이기)
        if ((pet1 != null && (pet1.State.IsHolding || pet1.State.IsSelected)) ||
            (pet2 != null && (pet2.State.IsHolding || pet2.State.IsSelected)))
        {
            // Debug.Log($"[{InteractionName}] 이동 중 터치로 인해 상호작용 취소");
            yield break;
        }

        if ((pet1 != null && (pet1.State.CurrentStatus == PetStatus.GatheringInProgress ||
                              pet1.State.CurrentStatus == PetStatus.GatheredWaiting)) ||
            (pet2 != null && (pet2.State.CurrentStatus == PetStatus.GatheringInProgress ||
                              pet2.State.CurrentStatus == PetStatus.GatheredWaiting)))
        {
            // Debug.Log($"[{InteractionName}] 이동 중 모이기로 인해 상호작용 취소");
            yield break;
        }

        // ✅ 여기까지 성공적으로 준비 완료! 토스트 알림 발생
        NotifyInteractionStart(pet1, pet2);

        // 서로 마주보게 회전 - 각 상호작용이 자체적으로 처리하도록 제거
        // LookAtEachOther(pet1, pet2);  // 즉시 회전 제거 - 각 상호작용에서 부드럽게 처리
        // ▲▲▲ [여기까지 수정] ▲▲▲
        // 2. 실제 상호작용 실행 (try-finally로 안정성 확보)
        try
        {
            // 터치/홀드 체크를 포함한 상호작용 실행
            yield return StartCoroutine(PerformInteractionWithTouchCheck(pet1, pet2));
        }
        finally
        {
            // 3. 사후 정리 단계
            // 코루틴이 어떤 이유로든 종료될 때(성공, 실패, 중단), 반드시 정리를 수행합니다.
            // Debug.Log($"[{InteractionName}] 상호작용 종료 및 정리 시작.");
            EndInteraction(pet1, pet2);
        }
    }

    // PerformInteraction은 이제 protected virtual로 변경하여 자식 클래스에서 재정의하도록 합니다.
    protected virtual IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        yield return new WaitForSeconds(1.0f); // 기본 구현
    }

    /// <summary>
    /// 터치/홀드 체크를 포함한 상호작용 실행 래퍼
    /// </summary>
    private IEnumerator PerformInteractionWithTouchCheck(PetController pet1, PetController pet2)
    {
        // 기존 PerformInteraction을 호출하면서 매 프레임 터치 체크
        IEnumerator interaction = PerformInteraction(pet1, pet2);
        
        while (interaction.MoveNext())
        {
            // 둘 중 하나라도 터치/홀드되면 즉시 중단
            if ((pet1 != null && (pet1.State.IsHolding || pet1.State.IsSelected)) || 
                (pet2 != null && (pet2.State.IsHolding || pet2.State.IsSelected)))
            {
                // Debug.Log($"[{InteractionName}] 터치로 인해 상호작용이 중단됨");
                yield break;
            }
            
            // 둘 중 하나라도 모이기 명령을 받으면 즉시 중단
            if ((pet1 != null && (pet1.State.CurrentStatus == PetStatus.GatheringInProgress || 
                                  pet1.State.CurrentStatus == PetStatus.GatheredWaiting)) ||
                (pet2 != null && (pet2.State.CurrentStatus == PetStatus.GatheringInProgress || 
                                  pet2.State.CurrentStatus == PetStatus.GatheredWaiting)))
            {
                // Debug.Log($"[{InteractionName}] 모이기 명령으로 인해 상호작용이 중단됨");
                yield break;
            }
            
            yield return interaction.Current;
        }
    }

    // EndInteraction은 private 또는 protected로 변경하여 외부 호출을 막습니다.
    protected void EndInteraction(PetController pet1, PetController pet2)
    {
        // Debug.Log($"[{InteractionName}] EndInteraction 호출: {pet1?.petName} & {pet2?.petName}");
        
        // 중복 호출 방지를 위한 체크
        bool pet1AlreadyClean = (pet1 == null || !pet1.State.IsInteracting);
        bool pet2AlreadyClean = (pet2 == null || !pet2.State.IsInteracting);
        
        if (pet1AlreadyClean && pet2AlreadyClean)
        {
        // Debug.Log($"[{InteractionName}] 이미 상호작용이 종료된 상태, 중복 호출 무시");
            return;
        }
        
        if (pet1 != null && pet1.State.IsInteracting) SafeResumePet(pet1);
        if (pet2 != null && pet2.State.IsInteracting) SafeResumePet(pet2);

        // ✅ 상호작용 종료 시 쿨타임 시작
        if (CooldownManager.Instance != null)
        {
            if (pet1 != null)
            {
                CooldownManager.Instance.StartCooldown(
                    CooldownManager.CooldownType.PetInteraction,
                    pet1.petName);
            }
            if (pet2 != null)
            {
                CooldownManager.Instance.StartCooldown(
                    CooldownManager.CooldownType.PetInteraction,
                    pet2.petName);
            }
        }

        // 상호작용 매니저에 종료 알림
        if (PetInteractionManager.Instance != null)
        {
            PetInteractionManager.Instance.NotifyInteractionEnded(pet1, pet2);
        }
        
        // Debug.Log($"[{InteractionName}] EndInteraction 완료");
    }
    // 상호작용 수행 코루틴



    // 상호작용 시퀀스 관리
    private IEnumerator InteractionSequence(PetController pet1, PetController pet2)
    {
        // ★ [Phase 4] PetState를 통한 상태 업데이트
        pet1.State.StartInteraction(pet2);
        pet2.State.StartInteraction(pet1);

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
        // Debug.Log($"[{InteractionName}] 펫 NavMesh 위치 확인 중...");

        // 첫 번째 펫이 NavMesh 위에 없으면 위치 조정
        if (pet1.agent != null && !pet1.agent.isOnNavMesh)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(pet1.transform.position, out navHit, 10f, NavMesh.AllAreas))
            {
                pet1.transform.position = navHit.position;
                // Debug.Log($"[{InteractionName}] {pet1.petName}의 위치가 NavMesh로 조정됨");

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
                // Debug.Log($"[{InteractionName}] {pet2.petName}의 위치가 NavMesh로 조정됨");

                // NavMeshAgent 재활성화 (필요 시)
                pet2.agent.enabled = false;
                yield return new WaitForSeconds(0.1f);
                pet2.agent.enabled = true;

                // 안정화 대기
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Debug.Log($"[{InteractionName}] 펫 NavMesh 위치 확인 완료");
    }



    /// <summary>
    /// 지연된 행동 결정을 위한 코루틴
    /// </summary>
    private IEnumerator DelayedBehaviorDecision(PetController pet)
    {
        yield return new WaitForSeconds(0.2f); // 애니메이션과 NavMeshAgent 동기화를 위한 짧은 대기
        pet.GetComponent<PetMovementController>()?.DecideNextBehavior();
    }
    
    /// <summary>
    /// 펫의 상태를 안전하게 복구하고 다음 행동을 준비시키는 헬퍼 메서드
    /// </summary>
    private void SafeResumePet(PetController pet)
    {
        if (pet == null) return;

        // Debug.Log($"[SafeResumePet] {pet.petName}: 상태 복구 시작");
        
        // ★ [Phase 4] PetState를 통한 상태 업데이트
        pet.State.EndInteraction();
        pet.State.SetInteractionLogic(null);

        // 감정 말풍선 숨기기
        pet.HideEmotion();
        
        // 애니메이션 상태 초기화 (agent 체크 전에 수행)
        var animController = pet.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.StopAllCoroutines();
            animController.StopContinuousAnimation();
            // Idle 상태로 명시적 전환
            animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
        }

        if (pet.agent == null) return;

        // 1. NavMeshAgent가 비활성화되었다면 다시 활성화
        if (!pet.agent.enabled)
        {
            pet.agent.enabled = true;
        }

        // 2. NavMesh 위에 있는지 확인하고, 없다면 위치 보정
        if (!pet.agent.isOnNavMesh)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(pet.transform.position, out navHit, 2f, NavMesh.AllAreas))
            {
                pet.agent.Warp(navHit.position);
            }
            else
            {
                Debug.LogError($"[SafeResumePet] {pet.petName}의 NavMesh 위치를 찾지 못해 이동을 재개할 수 없습니다.");
                return;
            }
        }

        // 3. NavMeshAgent 상태 완전 초기화
        pet.agent.isStopped = false;
        pet.agent.ResetPath();
        pet.agent.speed = pet.baseSpeed;
        pet.agent.acceleration = pet.baseAcceleration;
        pet.agent.angularSpeed = 120f; // 기본 회전 속도
        pet.agent.updateRotation = true;
        pet.agent.updatePosition = true;
        
        // 4. 이동 재개
        pet.ResumeMovement();
        
        // 5. AI를 즉시 재평가하여 다음 행동 결정
        if (pet.AI != null)
        {
        // Debug.Log($"[SafeResumePet] {pet.petName}: AI 즉시 재평가");
            pet.AI.InterruptAndResetAI();  // 현재 활동 중단하고 새로 평가
            
            // 0.1초 후 AI 활동 보장
            pet.StartCoroutine(EnsureAIActivity(pet, 0.1f));
        }
        
        // 추가 안전장치: 약간의 지연 후에도 다시 확인
        pet.StartCoroutine(DelayedBehaviorDecision(pet));
        
        // Debug.Log($"[SafeResumePet] {pet.petName}: 상태 복구 완료");
    }
    
    /// <summary>
    /// AI 활동이 없으면 강제로 Wander 시작
    /// </summary>
    private IEnumerator EnsureAIActivity(PetController pet, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (pet.AI != null && pet.AI.GetCurrentActivity() == null)
        {
            Debug.LogWarning($"[EnsureAIActivity] {pet.petName}: AI 활동이 없어서 강제로 배회 시작");
            
            // MovementController를 통해 배회 시작
            var movementController = pet.GetComponent<PetMovementController>();
            if (movementController != null)
            {
                movementController.DecideNextBehavior();
            }
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

            // Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}이(가) 서로 마주보도록 설정됨");
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
    
    /// <summary>
    /// 두 펫의 크기를 고려하여 조정된 상호작용 거리를 계산
    /// </summary>
    protected float CalculateAdjustedDistance(PetController pet1, PetController pet2)
    {
        // 각 펫의 크기 배율 가져오기
        float multiplier1 = pet1.Profile.GetInteractionDistanceMultiplier();
        float multiplier2 = pet2.Profile.GetInteractionDistanceMultiplier();
        
        // 두 펫의 평균 배율 계산
        float averageMultiplier = (multiplier1 + multiplier2) / 2f;
        
        // 기본 거리에 평균 배율 적용
        float adjustedDistance = interactionStartDistance * averageMultiplier;
        
        // Debug.Log($"[{InteractionName}] 크기 조정 거리: {pet1.petName}({pet1.Profile.size}) & {pet2.petName}({pet2.Profile.size}) = {adjustedDistance:F1}m (기본: {interactionStartDistance}m)");
        
        return adjustedDistance;
    }

    // ====== 추가된 공통 행동 메서드들 ======

    // 기존 MoveToPositions 메서드 개선
    protected IEnumerator MoveToPositions(PetController pet1, PetController pet2,
      Vector3 pos1, Vector3 pos2, float timeout = 10f)
    {
        // NavMeshAgent 준비 확인
        bool pet1Ready = EnsureAgentReady(pet1);
        bool pet2Ready = EnsureAgentReady(pet2);

        if (!pet1Ready || !pet2Ready)
        {
            Debug.LogWarning($"[{InteractionName}] NavMeshAgent가 준비되지 않았습니다.");
            yield break;
        }

        // ★★★ 수정: 더 정밀한 도착 판정을 위해 stoppingDistance 임시 조정 ★★★
        float originalStop1 = pet1.agent.stoppingDistance;
        float originalStop2 = pet2.agent.stoppingDistance;
        pet1.agent.stoppingDistance = 0.3f;
        pet2.agent.stoppingDistance = 0.3f;

        // 목적지 설정
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.SetDestination(pos1);
        pet2.agent.SetDestination(pos2);

        // 걷기 애니메이션
        pet1.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        pet2.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        float startTime = Time.time;
        while (Time.time - startTime < timeout)
        {
            // 터치/홀드 체크 - 이동 중에도 즉시 중단
            if ((pet1 != null && (pet1.State.IsHolding || pet1.State.IsSelected)) || 
                (pet2 != null && (pet2.State.IsHolding || pet2.State.IsSelected)))
            {
                // Debug.Log($"[{InteractionName}] 이동 중 터치로 인해 상호작용 중단");
                // 애니메이션 정지
                pet1.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
                pet2.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
                yield break;
            }
            
            // 모이기 체크 - 이동 중에도 즉시 중단
            if ((pet1 != null && (pet1.State.CurrentStatus == PetStatus.GatheringInProgress || 
                                  pet1.State.CurrentStatus == PetStatus.GatheredWaiting)) ||
                (pet2 != null && (pet2.State.CurrentStatus == PetStatus.GatheringInProgress || 
                                  pet2.State.CurrentStatus == PetStatus.GatheredWaiting)))
            {
                // Debug.Log($"[{InteractionName}] 이동 중 모이기 명령으로 인해 상호작용 중단");
                // 애니메이션 정지
                pet1.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
                pet2.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
                yield break;
            }
            
            // NavMeshAgent 상태를 먼저 확인
            bool pet1Arrived = false;
            bool pet2Arrived = false;

            if (pet1.agent != null && pet1.agent.enabled && pet1.agent.isOnNavMesh)
            {
                pet1Arrived = !pet1.agent.pathPending && pet1.agent.remainingDistance < 0.5f;
            }

            if (pet2.agent != null && pet2.agent.enabled && pet2.agent.isOnNavMesh)
            {
                pet2Arrived = !pet2.agent.pathPending && pet2.agent.remainingDistance < 0.5f;
            }

            if (pet1Arrived && pet2Arrived)
            {
                // Debug.Log($"[{InteractionName}] 두 펫이 목적지에 도착");
                break;
            }
            // ▼▼▼ [수정] 이동 중 펫이 자연스럽게 경로를 바라보도록 회전 처리 ▼▼▼
            if (!pet1Arrived) pet1.HandleRotation();
            if (!pet2Arrived) pet2.HandleRotation();
            // ▲▲▲ [여기까지 수정] ▲▲▲

            // 먼저 도착한 펫은 상대를 기다림
            if (pet1Arrived && !pet2Arrived)
            {
                pet1.agent.isStopped = true;
                LookAtOther(pet1, pet2);
                pet1.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            }
            if (pet2Arrived && !pet1Arrived)
            {
                pet2.agent.isStopped = true;
                LookAtOther(pet2, pet1);
                pet2.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            }

            yield return null;
        }

        // stoppingDistance 복원
        pet1.agent.stoppingDistance = originalStop1;
        pet2.agent.stoppingDistance = originalStop2;

        // 이동 정지
        if (pet1.agent != null && pet1.agent.enabled && pet1.agent.isOnNavMesh)
            pet1.agent.isStopped = true;
        if (pet2.agent != null && pet2.agent.enabled && pet2.agent.isOnNavMesh)
            pet2.agent.isStopped = true;

        // 애니메이션 정지
        pet1.GetComponent<PetAnimationController>().StopContinuousAnimation();
        pet2.GetComponent<PetAnimationController>().StopContinuousAnimation();
    }
    // ▼▼▼ [추가] 두 펫이 서로를 부드럽게 바라보게 하는 새로운 헬퍼 메서드 추가 ▼▼▼
   // BasePetInteraction.cs 클래스 내부에 아래 코루틴을 새로 추가합니다.

    /// <summary>
    /// 지정된 시간 동안 두 펫이 서로를 부드럽게 바라보도록 회전시킵니다.
    /// </summary>
    /// <param name="pet1">첫 번째 펫</param>
    /// <param name="pet2">두 번째 펫</param>
    /// <param name="duration">회전에 걸리는 시간 (초)</param>
    protected IEnumerator SmoothlyLookAtEachOther(PetController pet1, PetController pet2, float duration = 0.5f)
    {
        if (pet1 == null || pet2 == null) yield break;

        // 목표 회전값 계산
        Quaternion pet1TargetRotation = Quaternion.LookRotation(pet2.transform.position - pet1.transform.position);
        Quaternion pet2TargetRotation = Quaternion.LookRotation(pet1.transform.position - pet2.transform.position);

        // 현재 회전값 저장
        Quaternion pet1StartRotation = pet1.transform.rotation;
        Quaternion pet2StartRotation = pet2.transform.rotation;
        
        // 회전 각도가 충분히 큰 경우에만 걷기 애니메이션 재생
        float pet1Angle = Quaternion.Angle(pet1StartRotation, pet1TargetRotation);
        float pet2Angle = Quaternion.Angle(pet2StartRotation, pet2TargetRotation);
        
        // 회전 각도가 30도 이상인 경우 걷기 애니메이션 시작
        bool pet1NeedsWalkAnim = pet1Angle > 30f;
        bool pet2NeedsWalkAnim = pet2Angle > 30f;
        
        if (pet1NeedsWalkAnim)
        {
            pet1.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        }
        if (pet2NeedsWalkAnim)
        {
            pet2.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        }

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            // Slerp를 사용하여 부드럽게 회전
            pet1.transform.rotation = Quaternion.Slerp(pet1StartRotation, pet1TargetRotation, t);
            pet2.transform.rotation = Quaternion.Slerp(pet2StartRotation, pet2TargetRotation, t);
            
            // 모델도 함께 회전
            if (pet1.petModelTransform != null) pet1.petModelTransform.rotation = pet1.transform.rotation;
            if (pet2.petModelTransform != null) pet2.petModelTransform.rotation = pet2.transform.rotation;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 회전값으로 정확하게 설정
        pet1.transform.rotation = pet1TargetRotation;
        pet2.transform.rotation = pet2TargetRotation;
        
        if (pet1.petModelTransform != null) pet1.petModelTransform.rotation = pet1.transform.rotation;
        if (pet2.petModelTransform != null) pet2.petModelTransform.rotation = pet2.transform.rotation;
        
        // 회전이 끝난 후 걷기 애니메이션 중지 (필요한 경우만)
        if (pet1NeedsWalkAnim)
        {
            pet1.GetComponent<PetAnimationController>().StopContinuousAnimation();
        }
        if (pet2NeedsWalkAnim)
        {
            pet2.GetComponent<PetAnimationController>().StopContinuousAnimation();
        }
    }
    // ▲▲▲ [여기까지 추가] ▲▲▲
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
    public class PetOriginalState
    {
        public bool agentWasStopped;
        public float originalSpeed;
        public float originalAcceleration;
        public bool agentUpdateRotation;

        public PetOriginalState(PetController pet)
        {
            // ★★★ 수정 시작 ★★★
            // agent가 활성화되고 NavMesh 위에 있을 때만 상태를 읽어옵니다.
            if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
            {
                agentWasStopped = pet.agent.isStopped;
                originalSpeed = pet.agent.speed;
                originalAcceleration = pet.agent.acceleration;
                agentUpdateRotation = pet.agent.updateRotation;
            }
            else
            {
                // 에이전트가 준비되지 않은 경우, 상호작용이 끝난 후 펫이 멈추지 않도록 기본값을 설정합니다.
                agentWasStopped = true; // 기본적으로 멈춰있었다고 가정하여, Restore 시 isStopped = true가 되는 것을 방지
                originalSpeed = pet.baseSpeed; // PetController에 저장된 기본 속도 사용
                originalAcceleration = pet.baseAcceleration; // PetController에 저장된 기본 가속도 사용
                agentUpdateRotation = true;
                Debug.LogWarning($"[PetOriginalState] {pet.petName}의 NavMeshAgent가 준비되지 않아 기본값으로 상태를 초기화합니다.");
            }
            // ★★★ 수정 끝 ★★★
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
      PetAnimationController.PetAnimationType anim1Type, PetAnimationController.PetAnimationType anim2Type,
      float duration = 2.0f)
    {
        // 두 펫의 애니메이션 컨트롤러
        PetAnimationController anim1Controller = pet1.GetComponent<PetAnimationController>();
        PetAnimationController anim2Controller = pet2.GetComponent<PetAnimationController>();

        // 동시에 애니메이션 시작
        StartCoroutine(anim1Controller.PlayAnimationWithCustomDuration(anim1Type, duration, false, false));
        yield return StartCoroutine(anim2Controller.PlayAnimationWithCustomDuration(anim2Type, duration, false, false));
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
     PetAnimationController.PetAnimationType winnerAnimType = PetAnimationController.PetAnimationType.Jump,
     PetAnimationController.PetAnimationType loserAnimType = PetAnimationController.PetAnimationType.Eat)
    {
        // Debug.Log($"[{InteractionName}] 결과: {winner.petName}이(가) 승리!");

        PetAnimationController winnerAnimController = winner.GetComponent<PetAnimationController>();
        PetAnimationController loserAnimController = loser.GetComponent<PetAnimationController>();

        if (winnerAnimController != null)
        {
            StartCoroutine(winnerAnimController.PlayAnimationWithCustomDuration(winnerAnimType, 2.0f, false, false));
        }
        if (loserAnimController != null)
        {
            yield return StartCoroutine(loserAnimController.PlayAnimationWithCustomDuration(loserAnimType, 2.0f, false, false));
        }
    }
    // BasePetInteraction.cs 에 아래 메서드를 새로 추가합니다.

    /// <summary>
    /// 지정된 펫의 NavMeshAgent가 활성화되고 NavMesh 위에 준비될 때까지 대기합니다.
    /// </summary>
    /// <param name="pet">체크할 펫</param>
    /// <param name="timeout">최대 대기 시간</param>
    /// <returns></returns>
    protected IEnumerator WaitUntilAgentIsReady(PetController pet, float timeout = 2.0f)
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
                Debug.LogWarning($"[{InteractionName}] {pet.petName}의 NavMeshAgent 재활성화 시도 {retryCount}/{maxRetries}");
                
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
        Debug.LogWarning($"[{InteractionName}] {pet.petName}의 NavMeshAgent가 {timeout}초 내에 준비되지 않았습니다. 상호작용에 문제가 발생할 수 있습니다.");
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

    /// <summary>
    /// 토스트 알림을 표시할지 여부 결정 (서브클래스에서 오버라이드 가능)
    /// </summary>
    protected virtual bool ShouldShowToastNotification()
    {
        return true;
    }

    /// <summary>
    /// 상호작용 시작 알림
    /// </summary>
    private void NotifyInteractionStart(PetController pet1, PetController pet2)
    {
        // 토스트 알림 표시 여부 체크
        if (!ShouldShowToastNotification())
        {
            return;
        }

        // InteractionType 결정
        InteractionType interactionType = GetInteractionType();

        // 토스트 알림 핸들러에 알림
        if (InteractionNotificationHandler.Instance != null)
        {
            InteractionNotificationHandler.NotifyInteractionStarted(pet1, pet2, interactionType);
        }
    }

    /// <summary>
    /// 현재 상호작용의 타입 가져오기
    /// </summary>
    protected virtual InteractionType GetInteractionType()
    {
        // 각 상호작용 클래스에서 오버라이드 가능
        // 기본값 반환 (InteractionName 기반)
        switch (InteractionName)
        {
            case "Fight": return InteractionType.Fight;
            case "Headbutt": return InteractionType.Headbutt;
            case "CamelAlpacaSpitFight": return InteractionType.CamelAlpacaSpitFight;
            case "WalkTogether": return InteractionType.WalkTogether;
            case "RestAndSleepTogether": return InteractionType.RestTogether;
            case "RestTogether": return InteractionType.RestTogether;
            case "SleepTogether": return InteractionType.SleepTogether;
            case "Race": return InteractionType.Race;
            case "Chase": return InteractionType.ChaseAndRun;
            case "ChaseAndRun": return InteractionType.ChaseAndRun;
            case "RideAndWalk": return InteractionType.RideAndWalk;
            case "SlothKoalaRace": return InteractionType.SlothKoalaRace;
            case "PredatorMoleHunt": return InteractionType.PredatorMoleHunt;
            case "PredatorPossumPrank": return InteractionType.PredatorPossumPrank;
            case "ChameleonCamouflage": return InteractionType.ChameleonCamouflage;
            case "PersonalityReaction": return InteractionType.PersonalityReaction;
            case "SkunkDefense": return InteractionType.SkunkDefense;
            default: return InteractionType.WalkTogether;
        }
    }
}