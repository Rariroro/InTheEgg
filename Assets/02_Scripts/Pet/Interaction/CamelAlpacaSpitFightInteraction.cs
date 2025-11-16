// Pet.zip/Interaction/CamelAlpacaSpitFightInteraction.cs

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 낙타-알파카, 코끼리-하마의 침/물 뿜기 대결 상호작용을 처리합니다.
/// - Camel ↔ Alpaca: 침 뱉기 대결
/// - Elephant ↔ Hippo: 물 뿜기 대결
/// RaceInteraction, ChameleonCamouflageInteraction 등의 구조를 참고하여 최적화되었습니다.
/// </summary>
public class CamelAlpacaSpitFightInteraction : BasePetInteraction
{
    public override string InteractionName => "CamelAlpacaSpitFight";

    [Header("Fine-Tuning Settings")]
    [Tooltip("침/물 발사 위치를 펫의 앞쪽으로 미세 조정합니다. (단위: 미터)")]
    public float spitForwardOffset = 0.5f;

    [Tooltip("침/물 발사 위치를 위아래로 미세 조정합니다. (단위: 미터)")]
    public float spitUpwardOffset = 0.0f;

    [Header("Visual Effects")]
    [Tooltip("침/물을 뿜을 때 입에서 발사되는 효과 프리팹입니다.")]
    public GameObject spitEmissionPrefab;
    [Tooltip("침/물에 맞았을 때 몸에서 나타나는 피격 효과 프리팹입니다.")]
    public GameObject spitHitPrefab;


    [Header("Fight Settings")]
    [Tooltip("싸움을 시작할 때 두 펫 사이의 거리입니다.")]
    public float fightDistance = 7f;
    [Tooltip("침이 날아가는 데 걸리는 시간입니다.")]
    public float spitTravelDuration = 0.7f;
    [Tooltip("공격을 주고받는 횟수입니다. (각 펫이 공격하는 횟수)")]
    public int spitRounds = 5;
    [Tooltip("공격을 회피할 확률입니다. (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float dodgeChance = 0.5f;

    [Header("Animation Timings")]
    [Tooltip("공격(침 뱉기) 애니메이션의 지속 시간입니다.")]
    public float attackAnimationDuration = 1.0f;
    [Tooltip("피격 애니메이션의 지속 시간입니다.")]
    public float damageAnimationDuration = 1.0f;
    [Tooltip("회피 애니메이션의 지속 시간입니다.")]
    public float dodgeAnimationDuration = 0.8f;

    [Header("Safety Settings")]
    [Tooltip("NavMeshAgent가 준비될 때까지 기다리는 최대 시간입니다.")]
    public float agentSafetyTimeout = 3f;

    // 상호작용 타입을 Fight로 결정합니다.
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.Fight;
    }

    // 상호작용이 가능한 조합인지 확인합니다. (낙타-알파카, 코끼리-하마)
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // Camel ↔ Alpaca
        bool isCamelAlpaca = (pet1.PetType == PetType.Camel && pet2.PetType == PetType.Alpaca) ||
                             (pet1.PetType == PetType.Alpaca && pet2.PetType == PetType.Camel);

        // Elephant ↔ Hippo
        bool isElephantHippo = (pet1.PetType == PetType.Elephant && pet2.PetType == PetType.Hippo) ||
                               (pet1.PetType == PetType.Hippo && pet2.PetType == PetType.Elephant);

        return isCamelAlpaca || isElephantHippo;
    }

    /// <summary>
    /// 상호작용의 전체 흐름을 관리하는 메인 코루틴입니다.
    /// </summary>
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        string combinationType = GetCombinationTypeName(pet1, pet2);
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 {combinationType} 대결 시작!");

        // 역할 식별
        PetController fighter1 = pet1;
        PetController fighter2 = pet2;

        // NavMeshAgent 준비 상태 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(fighter1, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(fighter2, agentSafetyTimeout));

        if (!IsAgentSafelyReady(fighter1) || !IsAgentSafelyReady(fighter2))
        {
            Debug.LogError($"[{InteractionName}] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(fighter1, fighter2);
            yield break;
        }

        // 펫들의 원래 상태 저장
        PetOriginalState fighter1State = new PetOriginalState(fighter1);
        PetOriginalState fighter2State = new PetOriginalState(fighter2);

        try
        {
            // 각 단계별 코루틴 순차 실행
            yield return StartCoroutine(MeetAndConfrontPhase(fighter1, fighter2));
            yield return StartCoroutine(SpitExchangePhase(fighter1, fighter2));
            yield return StartCoroutine(DetermineWinnerPhase(fighter1, fighter2));
        }
        finally
        {
            // 상호작용이 어떤 이유로든 종료될 때 항상 정리 작업을 수행합니다.
            Debug.Log($"[{InteractionName}] 상호작용 정리 시작.");
            EndInteraction(fighter1, fighter2);
            Debug.Log($"[{InteractionName}] 상호작용 정리 완료.");
        }
    }

    #region Interaction Phases

    /// <summary>
    /// 1. 두 펫이 만나서 대치하는 단계
    /// </summary>
    private IEnumerator MeetAndConfrontPhase(PetController fighter1, PetController fighter2)
    {
        string combinationType = GetCombinationTypeName(fighter1, fighter2);
        Debug.Log($"[{InteractionName}] 1단계: {combinationType} 대치");

        // 감정 표현 (화난 표정)
        fighter1.ShowEmotion(EmotionType.Angry, 30f);
        fighter2.ShowEmotion(EmotionType.Angry, 30f);

        // 서로 마주볼 위치 계산
        Vector3 direction = (fighter2.transform.position - fighter1.transform.position).normalized;
        if (direction == Vector3.zero) direction = fighter1.transform.forward;
        Vector3 midpoint = (fighter1.transform.position + fighter2.transform.position) / 2f;

        // 펫 크기에 따라 적절한 거리 계산
        float adjustedDistance = CalculateFightDistance(fighter1, fighter2);

        Vector3 fighter1TargetPos = midpoint - direction * (adjustedDistance / 2f);
        Vector3 fighter2TargetPos = midpoint + direction * (adjustedDistance / 2f);

        // NavMesh 위의 유효한 위치로 보정
        fighter1TargetPos = FindValidPositionOnNavMesh(fighter1TargetPos, 5f);
        fighter2TargetPos = FindValidPositionOnNavMesh(fighter2TargetPos, 5f);

        Debug.Log($"[{InteractionName}] 목표 거리: {adjustedDistance:F1}m, 실제 목표 위치 간 거리: {Vector3.Distance(fighter1TargetPos, fighter2TargetPos):F1}m");

        // 계산된 위치로 정밀하게 이동
        yield return StartCoroutine(MoveToPositionsPrecise(fighter1, fighter2, fighter1TargetPos, fighter2TargetPos, 15f));

        // 이동 완료 후 Agent 정지 (위치 고정)
        if (fighter1.agent != null) fighter1.agent.isStopped = true;
        if (fighter2.agent != null) fighter2.agent.isStopped = true;

        Debug.Log($"[{InteractionName}] 이동 완료 후 실제 거리: {Vector3.Distance(fighter1.transform.position, fighter2.transform.position):F1}m");

        // 서로 부드럽게 마주보게 회전 (충분한 시간 확보)
        yield return StartCoroutine(SmoothlyLookAtEachOther(fighter1, fighter2, 1.5f));

        // 정확히 마주보는지 재확인 및 보정
        yield return StartCoroutine(EnsureFacingEachOther(fighter1, fighter2));

        // 긴장감 조성을 위한 대치 시간
        yield return new WaitForSeconds(2.0f);
    }


    /// <summary>
    /// 2. 침/물 뿜기 공방 단계
    /// </summary>
    private IEnumerator SpitExchangePhase(PetController fighter1, PetController fighter2)
    {
        string combinationType = GetCombinationTypeName(fighter1, fighter2);
        Debug.Log($"[{InteractionName}] 2단계: {combinationType} 공방 (각 펫이 {spitRounds}번씩 공격)");

        // 총 라운드 수는 spitRounds * 2 (각 펫이 spitRounds번씩)
        int totalRounds = spitRounds * 2;

        for (int i = 0; i < totalRounds; i++)
        {
            PetController attacker = (i % 2 == 0) ? fighter1 : fighter2;
            PetController target = (attacker == fighter1) ? fighter2 : fighter1;

            bool isCamelAlpaca = (fighter1.PetType == PetType.Camel || fighter1.PetType == PetType.Alpaca);
            string attackType = isCamelAlpaca ? "침 발사" : "물 발사";

            // 각 펫별 공격 횟수 표시
            int attackerRound = (i / 2) + 1;
            string attackerName = (i % 2 == 0) ? fighter1.petName : fighter2.petName;
            Debug.Log($"[{InteractionName}] {attackerName}의 {attackerRound}번째 {attackType}! (전체 라운드 {i + 1}/{totalRounds})");

            yield return StartCoroutine(attacker.GetComponent<PetAnimationController>()
                .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, attackAnimationDuration, true, false));

            // 침/물 뿜기 효과
            StartCoroutine(SpitEffectCoroutine(attacker, target));

            yield return new WaitForSeconds(spitTravelDuration);

            if (Random.value < dodgeChance)
            {
                Debug.Log($"[{InteractionName}] {target.petName}이(가) {attackType}을(를) 회피했습니다!");
                target.ShowEmotion(EmotionType.Happy, 2f);

                // 옆으로 점프하면서 회피
                yield return StartCoroutine(DodgeSideways(target, attacker));
            }
            else
            {
                Debug.Log($"[{InteractionName}] {target.petName}이(가) {attackType}에 맞았습니다!");

                // 피격 시 짜증 감정 표현
                target.ShowEmotion(EmotionType.Annoyed, 2f);

                // 피격 효과 생성은 SpitEffectCoroutine이 담당
                yield return StartCoroutine(target.GetComponent<PetAnimationController>()
                    .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Damage, damageAnimationDuration, true, false));
            }

            // 회피 후에도 서로 마주보도록 재조정
            yield return StartCoroutine(EnsureFacingEachOther(fighter1, fighter2));

            yield return new WaitForSeconds(1.0f);
        }
    }

    /// <summary>
    /// 3. 승패를 결정하고 마무리하는 단계
    /// </summary>
    private IEnumerator DetermineWinnerPhase(PetController fighter1, PetController fighter2)
    {
        string combinationType = GetCombinationTypeName(fighter1, fighter2);
        Debug.Log($"[{InteractionName}] 3단계: {combinationType} 승패 결정");

        // 랜덤으로 승자 결정
        PetController winner = DetermineWinner(fighter1, fighter2, 0.5f);
        PetController loser = (winner == fighter1) ? fighter2 : fighter1;

        // 감정 표현
        winner.ShowEmotion(EmotionType.Victory, 5f);
        loser.ShowEmotion(EmotionType.Defeat, 5f);

        // 승자와 패자 애니메이션 재생
        yield return StartCoroutine(PlayWinnerLoserAnimations(winner, loser));

        yield return new WaitForSeconds(2.0f); // 결과 감상 시간
    }

    #endregion

    #region Helper Coroutines & Methods

    /// <summary>
    /// 현재 조합이 어떤 타입인지 식별하여 디버그 메시지용 문자열을 반환합니다.
    /// </summary>
    private string GetCombinationTypeName(PetController pet1, PetController pet2)
    {
        bool isCamelAlpaca = (pet1.PetType == PetType.Camel && pet2.PetType == PetType.Alpaca) ||
                            (pet1.PetType == PetType.Alpaca && pet2.PetType == PetType.Camel);
        return isCamelAlpaca ? "낙타-알파카 침 뱉기" : "코끼리-하마 물 뿜기";
    }

    /// <summary>
    /// 두 펫이 정확히 마주보고 있는지 확인하고 필요시 보정합니다.
    /// </summary>
    private IEnumerator EnsureFacingEachOther(PetController fighter1, PetController fighter2)
    {
        const float ANGLE_THRESHOLD = 10f; // 허용 오차 10도

        // 서로를 향하는 방향 계산
        Vector3 dir1to2 = (fighter2.transform.position - fighter1.transform.position).normalized;
        Vector3 dir2to1 = (fighter1.transform.position - fighter2.transform.position).normalized;

        // 현재 각도 오차 확인
        float angle1 = Vector3.Angle(fighter1.transform.forward, dir1to2);
        float angle2 = Vector3.Angle(fighter2.transform.forward, dir2to1);

        // 오차가 크면 추가 보정
        if (angle1 > ANGLE_THRESHOLD || angle2 > ANGLE_THRESHOLD)
        {
            string combinationType = GetCombinationTypeName(fighter1, fighter2);
            Debug.Log($"[{InteractionName}] {combinationType} 각도 보정 필요 ({fighter1.petName}: {angle1:F1}°, {fighter2.petName}: {angle2:F1}°)");
            yield return StartCoroutine(SmoothlyLookAtEachOther(fighter1, fighter2, 0.5f));
        }
    }

    /// <summary>
    /// 펫이 옆으로 점프하여 회피하는 동작을 수행합니다.
    /// 회피 중에도 공격자를 계속 바라보도록 합니다.
    /// </summary>
    private IEnumerator DodgeSideways(PetController dodger, PetController attacker)
    {
        if (dodger.agent == null || !dodger.agent.enabled || !dodger.agent.isOnNavMesh)
        {
            // NavMeshAgent가 없으면 일반 점프만 수행
            yield return StartCoroutine(dodger.GetComponent<PetAnimationController>()
                .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, dodgeAnimationDuration, true, false));
            yield break;
        }

        // 회피 방향 결정 (좌 또는 우로 랜덤하게)
        bool dodgeLeft = Random.value < 0.5f;
        Vector3 dodgeDirection = dodgeLeft ? -dodger.transform.right : dodger.transform.right;

        // 회피 거리 계산 (펫 크기에 비례)
        float dodgeDistance = GetPetRadius(dodger) * 2f;

        // 목표 위치 계산
        Vector3 dodgeTarget = dodger.transform.position + (dodgeDirection * dodgeDistance);

        // NavMesh 위의 유효한 위치로 보정
        dodgeTarget = FindValidPositionOnNavMesh(dodgeTarget, 2f);

        // 원래 위치 저장
        Vector3 originalPosition = dodger.transform.position;

        // NavMeshAgent 일시 정지
        bool wasAgentStopped = dodger.agent.isStopped;
        dodger.agent.isStopped = true;

        // 점프 애니메이션 시작
        var animController = dodger.GetComponent<PetAnimationController>();
        animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Jump);

        // 옆으로 이동하면서 계속 공격자를 바라보기
        float elapsedTime = 0f;
        float jumpHeight = 1.5f; // 점프 높이

        while (elapsedTime < dodgeAnimationDuration)
        {
            float progress = elapsedTime / dodgeAnimationDuration;

            // 수평 이동 (Lerp)
            Vector3 currentPos = Vector3.Lerp(originalPosition, dodgeTarget, progress);

            // 포물선 점프 (위아래 움직임)
            float jumpProgress = Mathf.Sin(progress * Mathf.PI);
            currentPos.y = originalPosition.y + (jumpHeight * jumpProgress);

            // 위치 업데이트 (NavMeshAgent 우회)
            dodger.transform.position = currentPos;

            // 공격자를 계속 바라보도록 회전
            Vector3 lookDirection = (attacker.transform.position - dodger.transform.position).normalized;
            lookDirection.y = 0; // 수평 방향만 고려
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                dodger.transform.rotation = Quaternion.Slerp(dodger.transform.rotation, targetRotation, Time.deltaTime * 10f);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 위치 설정
        dodger.transform.position = dodgeTarget;

        // NavMeshAgent 재활성화 및 위치 동기화
        if (dodger.agent != null && dodger.agent.enabled && dodger.agent.isOnNavMesh)
        {
            dodger.agent.Warp(dodgeTarget);
            dodger.agent.isStopped = wasAgentStopped;
        }

        // 애니메이션 정지
        animController.StopContinuousAnimation();

        // 최종적으로 공격자를 정확히 바라보도록 조정
        Vector3 finalLookDirection = (attacker.transform.position - dodger.transform.position).normalized;
        finalLookDirection.y = 0;
        if (finalLookDirection != Vector3.zero)
        {
            dodger.transform.rotation = Quaternion.LookRotation(finalLookDirection);
        }
    }

    /// <summary>
    /// 두 펫의 크기에 따라 적절한 대결 거리를 계산합니다.
    /// </summary>
    private float CalculateFightDistance(PetController pet1, PetController pet2)
    {
        // 두 펫의 Collider radius 합산 기반 거리 계산
        float radius1 = GetPetRadius(pet1);
        float radius2 = GetPetRadius(pet2);

        // 기본 거리 + (반지름 합 * 배율)
        // 예: Camel(1.5) + Alpaca(1.5) = 3 * 1.5 = 4.5 → 7 + 4.5 = 11.5f
        // 예: Elephant(4) + Hippo(4) = 8 * 1.5 = 12 → 7 + 12 = 19f
        float sizeMultiplier = 1.5f;

        return fightDistance + (radius1 + radius2) * sizeMultiplier;
    }

    /// <summary>
    /// 펫의 Collider radius를 반환합니다.
    /// </summary>
    private float GetPetRadius(PetController pet)
    {
        if (pet == null) return 1f;

        var capsuleCollider = pet.GetComponent<CapsuleCollider>();
        if (capsuleCollider != null) return capsuleCollider.radius;

        var sphereCollider = pet.GetComponent<SphereCollider>();
        if (sphereCollider != null) return sphereCollider.radius;

        return 1f; // 기본값
    }

    /// <summary>
    /// 두 펫을 목표 위치로 정밀하게 이동시킵니다. (거리 확보를 위한 엄격한 도착 판정)
    /// </summary>
    private IEnumerator MoveToPositionsPrecise(PetController pet1, PetController pet2,
        Vector3 pos1, Vector3 pos2, float timeout = 15f)
    {
        // NavMeshAgent 준비 확인
        if (pet1.agent == null || !pet1.agent.enabled || !pet1.agent.isOnNavMesh ||
            pet2.agent == null || !pet2.agent.enabled || !pet2.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[{InteractionName}] NavMeshAgent가 준비되지 않았습니다.");
            yield break;
        }

        // stoppingDistance를 매우 작게 설정 (정밀한 위치 제어)
        float originalStop1 = pet1.agent.stoppingDistance;
        float originalStop2 = pet2.agent.stoppingDistance;
        pet1.agent.stoppingDistance = 0.1f;
        pet2.agent.stoppingDistance = 0.1f;

        // 목적지 설정
        pet1.agent.isStopped = false;
        pet2.agent.isStopped = false;
        pet1.agent.SetDestination(pos1);
        pet2.agent.SetDestination(pos2);

        // 걷기 애니메이션
        pet1.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        pet2.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        float startTime = Time.time;
        const float PRECISE_THRESHOLD = 0.3f; // 정밀한 도착 판정 거리

        while (Time.time - startTime < timeout)
        {
            // NavMeshAgent 상태 확인
            bool pet1Arrived = false;
            bool pet2Arrived = false;

            if (pet1.agent != null && pet1.agent.enabled && pet1.agent.isOnNavMesh)
            {
                // NavMesh 경로 기반 판정 + 실제 위치 거리 판정
                bool navMeshArrived = !pet1.agent.pathPending && pet1.agent.remainingDistance <= PRECISE_THRESHOLD;
                float actualDistance = Vector3.Distance(pet1.transform.position, pos1);
                pet1Arrived = navMeshArrived && actualDistance <= PRECISE_THRESHOLD;
            }

            if (pet2.agent != null && pet2.agent.enabled && pet2.agent.isOnNavMesh)
            {
                bool navMeshArrived = !pet2.agent.pathPending && pet2.agent.remainingDistance <= PRECISE_THRESHOLD;
                float actualDistance = Vector3.Distance(pet2.transform.position, pos2);
                pet2Arrived = navMeshArrived && actualDistance <= PRECISE_THRESHOLD;
            }

            if (pet1Arrived && pet2Arrived)
            {
                Debug.Log($"[{InteractionName}] 두 펫이 정밀하게 목적지에 도착");
                break;
            }

            // 먼저 도착한 펫은 상대를 기다림
            if (pet1Arrived && !pet2Arrived)
            {
                pet1.agent.isStopped = true;
                pet1.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            }
            if (pet2Arrived && !pet1Arrived)
            {
                pet2.agent.isStopped = true;
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

    /// <summary>
    /// 침 뱉기 효과 (발사 및 피격)를 순차적으로 재생하는 코루틴입니다.
    /// </summary>
    private IEnumerator SpitEffectCoroutine(PetController attacker, PetController target)
    {
        // 1. 발사 효과 재생
        if (spitEmissionPrefab != null)
        {
             // ▼▼▼ 이 부분을 수정합니다 ▼▼▼
        // 1순위: 'SpitOrigin' 오브젝트를 먼저 찾습니다.
        Transform spitOrigin = FindDeepChild(attacker.petModelTransform, "SpitOrigin");

        // 2순위: 'SpitOrigin'이 없으면 기존 방식대로 'Head'를 찾습니다. (하위 호환성)
        if (spitOrigin == null)
        {
            spitOrigin = FindDeepChild(attacker.petModelTransform, "Head", "Head_M");
        }
        // ▲▲▲ 여기까지 수정 ▲▲▲
   // ▼▼▼ 이 부분을 아래와 같이 수정합니다 ▼▼▼

            // 1. 기본 위치를 먼저 계산합니다.
            Vector3 basePosition = (spitOrigin != null) ? spitOrigin.position : GetApproximateHeadPosition(attacker);

            // 2. 펫의 앞쪽(forward)과 위쪽(up) 방향을 기준으로 오프셋을 적용한 최종 위치를 계산합니다.
            Vector3 finalEmissionPosition = basePosition
                                          + (attacker.transform.forward * spitForwardOffset)
                                          + (attacker.transform.up * spitUpwardOffset);

            // 3. 타겟 위치는 오프셋 없이 그대로 계산합니다. (타격 효과는 정확한 위치에 맞아야 하므로)
            Transform targetHead = FindDeepChild(target.petModelTransform, "SpitOrigin", "Head", "Head_M");
            Vector3 targetPosition = (targetHead != null) ? targetHead.position : GetApproximateHeadPosition(target);

            // 4. 최종 계산된 위치에서 프리팹을 생성합니다.
            Quaternion rotationTowardsTarget = Quaternion.LookRotation(targetPosition - finalEmissionPosition);
            Instantiate(spitEmissionPrefab, finalEmissionPosition, rotationTowardsTarget);

            // ▲▲▲ 여기까지 수정 ▲▲▲
        }

        // 2. 침이 날아가는 시간 동안 대기
        yield return new WaitForSeconds(spitTravelDuration);

        // 3. 피격 효과 재생
        if (spitHitPrefab != null)
        {
            // "Head" 또는 "Head_M" 이름의 오브젝트를 찾아 그 위치를 피격 지점으로 사용
            Transform targetHead = FindDeepChild(target.petModelTransform, "Head", "Head_M");
            Vector3 hitPosition = (targetHead != null) ? targetHead.position : GetApproximateHeadPosition(target);

            Instantiate(spitHitPrefab, hitPosition, Quaternion.identity);
        }
    }

    // ▼▼▼ 디버깅을 위해 이 헬퍼 함수를 클래스 내부에 추가해주세요 ▼▼▼
    /// <summary>
    /// 디버깅을 위해 오브젝트의 전체 경로를 반환하는 헬퍼 함수입니다.
    /// </summary>
    private string GetFullPath(Transform obj)
    {
        if (obj == null) return "null";
        string path = obj.name;
        while (obj.parent != null)
        {
            obj = obj.parent;
            path = obj.name + "/" + path;
        }
        return path;
    }

    // ▼▼▼ [수정] FindDeepChild 헬퍼 메서드를 아래 코드로 교체합니다. ▼▼▼
    /// <summary>
    /// 부모 Transform 아래에서 여러 후보 이름 중 하나와 일치하는 자식을 재귀적으로 탐색합니다.
    /// (수정: 여러 개의 이름을 대소문자 구분 없이 검색)
    /// </summary>
    /// <param name="parent">검색을 시작할 부모 Transform</param>
    /// <param name="childNames">찾고자 하는 자식의 이름들 (가변 인자)</param>
    /// <returns>가장 먼저 찾은 자식의 Transform. 없으면 null을 반환합니다.</returns>
    private Transform FindDeepChild(Transform parent, params string[] childNames)
    {
        if (parent == null || childNames == null || childNames.Length == 0) return null;

        foreach (Transform child in parent)
        {
            // 여러 후보 이름들과 대소문자 구분 없이 비교
            foreach (string name in childNames)
            {
                if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
            
            // 자식의 자식들을 계속해서 재귀적으로 탐색
            Transform result = FindDeepChild(child, childNames);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }


   // 'Head' 오브젝트를 찾지 못했을 경우, 콜라이더 기반으로 머리 위치를 추정하는 폴백(Fallback) 메서드입니다.
    // 기존 GetApproximateHeadPosition 메서드를 아래 코드로 교체하세요.
    private Vector3 GetApproximateHeadPosition(PetController pet)
    {
        Debug.LogWarning($"[{InteractionName}] {pet.petName}에게서 'Head' 오브젝트를 찾지 못해 위치를 추정합니다. 콜라이더 기준으로 위치를 계산합니다.");

        Collider petCollider = pet.GetComponent<Collider>();
        if (petCollider == null)
        {
            // 콜라이더도 없으면 기존 방식 사용
            float headHeight = 2.0f; // 기본 높이값
            return pet.transform.position + new Vector3(0, headHeight, 0);
        }

        // 콜라이더의 최상단 지점을 머리 위치로 추정합니다.
        // bounds.center는 월드 좌표 기준 중심점, bounds.extents는 중심점에서 각 축 방향으로의 거리입니다.
        Vector3 colliderTop = petCollider.bounds.center + new Vector3(0, petCollider.bounds.extents.y, 0);
        
        return colliderTop;
    }

    
    /// <summary>
    /// 펫의 머리 위치를 근사치로 계산하여 반환합니다.
    /// </summary>
    private Vector3 GetPetHeadPosition(PetController pet)
    {
        // 펫의 키(Collider의 높이)에 비례하여 머리 위치를 추정합니다.
        float headHeight = pet.GetComponent<Collider>().bounds.size.y * 0.8f;
        return pet.transform.position + new Vector3(0, headHeight, 0);
    }
    
    /// <summary>
    /// NavMeshAgent가 안전하게 준비되었는지 확인하는 헬퍼 메서드
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }
    
    #endregion
}