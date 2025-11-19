// RideAndWalkInteraction.cs (수정된 버전)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RideAndWalkInteraction : BasePetInteraction
{
    public override string InteractionName => "RideAndWalk";

    // 우선순위: 95 (2순위)
    public override int Priority => 95;

    [Header("Ride & Walk Settings")]
    [Tooltip("상호작용 시작 시 펫들이 만나는 거리입니다.")]
    public float meetingDistance = 5f;

    [Tooltip("기본 탑승 위치 오프셋 (폴백용)")]
    public Vector3 defaultRideOffset = new Vector3(0, 1.7f, 0.2f);

    [Tooltip("탑승하거나 내릴 때 걸리는 시간입니다.")]
    public float mountDuration = 1.0f;

    [Tooltip("함께 걷는 총 시간입니다.")]
    public float walkTogetherDuration = 20f;

    [Tooltip("함께 걷는 동안 새로운 목적지를 설정하는 주기입니다.")]
    public float pathUpdateInterval = 7f;

    [Tooltip("함께 걷는 동안의 이동 속도 배율입니다.")]
    public float walkingSpeedMultiplier = 0.9f;

    [Tooltip("내린 후 작별인사를 할 때 유지할 거리입니다.")]
    public float farewellDistance = 7f;

    [Header("Auto Height Calculation")]
    [Tooltip("자동 높이 계산 시 Collider 높이 배율")]
    [Range(0.7f, 1.0f)]
    public float autoHeightMultiplier = 0.85f;

    [Tooltip("자동 깊이 계산 시 Collider 깊이 배율")]
    [Range(0.0f, 0.5f)]
    public float autoDepthMultiplier = 0.2f;

    [Header("Safety Settings")]
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;

    // 탈 수 있는 펫 조합 정의 (rider, mount) - 10개 조합
    private readonly HashSet<(PetType rider, PetType mount)> validRideCombinations = new()
    {
        (PetType.Meerkat, PetType.Boar),
        (PetType.Chick, PetType.Pig),
        (PetType.Chicken, PetType.Cow),
        (PetType.Cat, PetType.Mule),
        (PetType.Squirrel, PetType.Bear),
        (PetType.Monkey, PetType.Elephant),
        (PetType.RedPanda, PetType.Panda),
        (PetType.Otter, PetType.Hippo),
        (PetType.Platypus, PetType.Crocodile),
        (PetType.Kangaroo, PetType.Horse)
    };

    // 탑승 위치 캐시 (성능 최적화)
    private Dictionary<PetController, Vector3> ridePositionCache = new Dictionary<PetController, Vector3>();

    // 라이더의 원래 회피 우선순위를 저장할 변수
    private int riderOriginalPriority;

    // 캐시 초기화 (씬 로드/재로드 시 이전 잘못된 값 제거)
    private void OnEnable()
    {
        ridePositionCache.Clear();
    }

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.RideAndWalk;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // HashSet을 사용한 유연한 조합 체크
        return validRideCombinations.Contains((pet1.PetType, pet2.PetType)) ||
               validRideCombinations.Contains((pet2.PetType, pet1.PetType));
    }

    /// <summary>
    /// 탑승 위치를 계산합니다 (Transform 마커 → Collider 자동 계산 → 기본값)
    /// </summary>
    private Vector3 GetRidePosition(PetController mount)
    {
        // 캐시 확인
        if (ridePositionCache.ContainsKey(mount))
        {
            return ridePositionCache[mount];
        }

        Vector3 ridePosition;

        // 1순위: PetController의 ridePoint Transform 확인
        if (mount.ridePoint != null)
        {
            // RidePoint의 로컬 좌표 직접 사용 (부모의 자식이므로 불변)
            ridePosition = mount.ridePoint.localPosition;
            Debug.Log($"[RideWalk] {mount.petName}의 RidePoint 사용 (local): {ridePosition}");
        }
        // 2순위: Collider 기반 자동 계산
        else
        {
            Collider mountCollider = mount.GetComponent<Collider>();
            if (mountCollider != null)
            {
                Bounds bounds = mountCollider.bounds;
                // 로컬 좌표로 변환
                Vector3 localTop = mount.transform.InverseTransformPoint(bounds.center);
                localTop.y += bounds.extents.y * autoHeightMultiplier;
                localTop.z = bounds.extents.z * autoDepthMultiplier;
                ridePosition = localTop;
                Debug.Log($"[RideWalk] {mount.petName}의 Collider 자동 계산: {ridePosition}");
            }
            // 3순위: 기본값 사용
            else
            {
                ridePosition = defaultRideOffset;
                Debug.LogWarning($"[RideWalk] {mount.petName}의 RidePoint와 Collider를 찾을 수 없어 기본값 사용");
            }
        }

        // 캐시에 저장
        ridePositionCache[mount] = ridePosition;
        return ridePosition;
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[RideAndWalk] {pet1.petName}와(과) {pet2.petName}의 타고 걷기 상호작용이 시작됩니다!");

        // 역할 동적 식별 (rider와 mount 결정)
        PetController rider = null;
        PetController mount = null;

        // validRideCombinations에서 역할 확인
        if (validRideCombinations.Contains((pet1.PetType, pet2.PetType)))
        {
            rider = pet1;
            mount = pet2;
        }
        else if (validRideCombinations.Contains((pet2.PetType, pet1.PetType)))
        {
            rider = pet2;
            mount = pet1;
        }
        else
        {
            Debug.LogError($"[RideAndWalk] 유효하지 않은 펫 조합: {pet1.PetType} & {pet2.PetType}");
            yield break;
        }

        Debug.Log($"[RideAndWalk] Rider: {rider.petName}({rider.PetType}), Mount: {mount.petName}({mount.PetType})");

        // NavMeshAgent 준비 상태 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(rider, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(mount, agentSafetyTimeout));

        if (!IsAgentSafelyReady(rider) || !IsAgentSafelyReady(mount))
        {
            Debug.LogError("[RideAndWalk] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(rider, mount);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState riderState = new PetOriginalState(rider);
        PetOriginalState mountState = new PetOriginalState(mount);

        // 라이더의 원래 부모와 스케일 정보를 정확히 저장합니다.
        Transform originalRiderParent = rider.transform.parent;
        Vector3 originalRiderLocalScale = rider.transform.localScale;
        Vector3 originalRiderWorldScale = rider.transform.lossyScale;

        riderOriginalPriority = rider.agent.avoidancePriority;

        try
        {
            // 감정 표현 시작
            rider.ShowEmotion(EmotionType.Love, walkTogetherDuration + 15f);
            mount.ShowEmotion(EmotionType.Love, walkTogetherDuration + 15f);

            // 1. 만나서 노는 단계
            yield return StartCoroutine(MeetAndPlay(rider, mount));

            // 2. 등에 올라타는 단계
            yield return StartCoroutine(MountPet(rider, mount));

            // 3. 함께 주변을 산책하는 단계
            yield return StartCoroutine(WalkTogether(rider, mount));

            // 4. 등에서 내리는 단계
            yield return StartCoroutine(DismountPet(rider, mount));

            // 5. 작별 인사를 하는 단계
            yield return StartCoroutine(SayFarewell(rider, mount));
        }
        finally
        {
            Debug.Log("[RideAndWalk] 상호작용 정리 시작.");

            // 부모 관계 복원
            if (rider.transform.parent == mount.transform)
            {
                rider.transform.SetParent(originalRiderParent, true);
            }

            // 원래 부모가 있었는지 여부에 따라 스케일을 정확하게 복원합니다.
            if (originalRiderParent == null)
            {
                // 원래 부모가 없었다면 월드 스케일 기준으로 복원
                rider.transform.localScale = originalRiderWorldScale;
            }
            else
            {
                // 원래 부모가 있었다면 로컬 스케일 기준으로 복원
                rider.transform.localScale = originalRiderLocalScale;
            }

            // 라이더의 회피 우선순위 복원
            if (IsAgentSafelyReady(rider))
            {
                rider.agent.avoidancePriority = riderOriginalPriority;
            }

            // 상태 복원
            riderState.Restore(rider);
            mountState.Restore(mount);

            // 상호작용 종료
            EndInteraction(rider, mount);
            Debug.Log("[RideAndWalk] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 두 펫이 만나서 노는 초기 단계를 처리합니다.
    /// </summary>
    private IEnumerator MeetAndPlay(PetController meerkat, PetController boar)
    {
        Debug.Log("[RideAndWalk] 1단계: 만나서 놀기");

        // BasePetInteraction이 이미 위치를 정렬했으므로 중복 이동 제거
        // 펫들이 이미 적절한 거리에서 마주보고 있는 상태

        // 안정성을 위해 잠시 대기
        yield return new WaitForSeconds(0.2f);

        // 서로 즐겁게 노는 애니메이션
        yield return StartCoroutine(PlaySimultaneousAnimations(
            meerkat, boar,
            PetAnimationController.PetAnimationType.Jump,
            PetAnimationController.PetAnimationType.Idle,
            1.5f));

        yield return StartCoroutine(PlaySimultaneousAnimations(
            boar, meerkat,
            PetAnimationController.PetAnimationType.Attack,
            PetAnimationController.PetAnimationType.Jump,
            2.0f));
    }


    /// <summary>
    /// 라이더가 마운트 펫의 등에 올라타는 단계를 처리합니다.
    /// </summary>
    private IEnumerator MountPet(PetController rider, PetController mount)
    {
        Debug.Log($"[RideAndWalk] 2단계: {rider.petName}이(가) {mount.petName}의 등에 올라탑니다.");

        // 라이더의 회피 우선순위를 낮춰서 마운트를 가로막지 않도록
        if (IsAgentSafelyReady(rider))
        {
            rider.agent.avoidancePriority = 99;
        }

        // 마운트가 앉아서 기다려주는 애니메이션
        if (IsAgentSafelyReady(mount))
        {
            mount.agent.isStopped = true;
        }

        yield return StartCoroutine(mount.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 2.0f, false, false));

        // 라이더가 점프해서 올라타는 애니메이션
        yield return StartCoroutine(rider.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, mountDuration, false, false));

        // 라이더의 NavMeshAgent를 비활성화
        if (rider.agent != null && rider.agent.enabled)
        {
            rider.agent.enabled = false;
        }

        // 라이더를 마운트의 자식으로 만들기
        rider.transform.SetParent(mount.transform, true);

        // 부드러운 위치 이동 (GetRidePosition 사용)
        Vector3 targetRidePosition = GetRidePosition(mount);
        yield return StartCoroutine(SmoothMountTransition(rider, targetRidePosition, mountDuration));
    }

   /// <summary>
/// 멧돼지가 미어캣을 태우고 함께 산책하는 단계를 처리합니다.
/// </summary>
private IEnumerator WalkTogether(PetController meerkat, PetController boar)
{
    Debug.Log($"[RideAndWalk] 3단계: 함께 산책하기");

    if (!IsAgentSafelyReady(boar))
    {
        Debug.LogWarning("[RideAndWalk] 멧돼지의 NavMeshAgent가 준비되지 않아 산책을 건너뜁니다.");
        yield break;
    }

    // 멧돼지 설정
    boar.agent.speed = boar.baseSpeed * walkingSpeedMultiplier;
    boar.agent.updateRotation = true;
    boar.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

    // 미어캣의 기본 자세를 Eat(앉기)으로 설정
    meerkat.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Eat);

    float walkStartTime = Time.time;
    float lastPathUpdateTime = 0f;

    while (Time.time - walkStartTime < walkTogetherDuration)
    {
        if (!IsAgentSafelyReady(boar))
        {
            Debug.LogWarning("[RideAndWalk] 산책 중 멧돼지의 NavMeshAgent 문제 발생");
            break;
        }

        // 일정 주기마다 새로운 목적지로 갱신 (기존 코드와 동일)
        if (Time.time - lastPathUpdateTime > pathUpdateInterval)
        {
            lastPathUpdateTime = Time.time;
            Vector3 randomDirection = Random.insideUnitSphere * 25f;
            randomDirection.y = 0;
            Vector3 newDestination = boar.transform.position + randomDirection;
            boar.agent.updateRotation = true;

            boar.agent.SetDestination(FindValidPositionOnNavMesh(newDestination, 30f));
            boar.agent.isStopped = false;
            Debug.Log($"[RideAndWalk] 새로운 목적지로 이동: {boar.agent.destination}");
        }

        // ▼▼▼ [수정] 미어캣이 등 위에서 더 다양한 행동을 하도록 로직 개선 ▼▼▼
        // 1. 행동 빈도를 조금 높이고 (1% -> 2%), 현재 다른 특별 행동 중이 아닐 때만 실행
        if (Random.value < 0.02f && !IsPetPlayingRidingAnimation(meerkat))
        {
            // 2. 여러 행동 중 하나를 무작위로 선택
            int randomAction = Random.Range(0, 4);

            switch (randomAction)
            {
                case 0: // 점프하며 즐거워하기 (기존 행동)
                    meerkat.ShowEmotion(EmotionType.Happy, 2f);
                    StartCoroutine(PlayRidingAnimation(meerkat, PetAnimationController.PetAnimationType.Jump, 1.0f));
                    break;
                
                case 1: // 주변을 두리번거리며 경계하기 (Idle 애니메이션 활용)
                    meerkat.ShowEmotion(EmotionType.Surprised, 2f); // 놀람/호기심 표현
                    StartCoroutine(PlayRidingAnimation(meerkat, PetAnimationController.PetAnimationType.Idle, 1.5f));
                    break;
                    
                case 2: // 신나게 소리치기 (Attack 애니메이션 활용)
                    meerkat.ShowEmotion(EmotionType.Love, 2f);
                    StartCoroutine(PlayRidingAnimation(meerkat, PetAnimationController.PetAnimationType.Attack, 1.2f));
                    break;

                case 3: // 잠시 편하게 눕기 (Rest 애니메이션 활용)
                    meerkat.ShowEmotion(EmotionType.Sleepy, 3f);
                    StartCoroutine(PlayRidingAnimation(meerkat, PetAnimationController.PetAnimationType.Rest, 2.0f));
                    break;
            }
        }
        // ▲▲▲ [여기까지 수정] ▲▲▲

        yield return null;
    }
}
// ▼▼▼ [추가] 멧돼지 등 위에서 미어캣의 짧은 행동을 재생하고 기본 자세로 돌리는 헬퍼 메서드 ▼▼▼
private IEnumerator PlayRidingAnimation(PetController meerkat, PetAnimationController.PetAnimationType animType, float duration)
{
    var animController = meerkat.GetComponent<PetAnimationController>();
    
    // 1. 일시적인 애니메이션 재생
    animController.SetContinuousAnimation(animType);
    yield return new WaitForSeconds(duration);

    // 2. 다시 기본 탑승 애니메이션(Eat)으로 복귀
    // 단, 코루틴 실행 중에 다른 애니메이션으로 바뀌지 않았을 경우에만 복귀
    if (animController != null && IsPetPlayingAnimation(meerkat, animType))
    {
        animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Eat);
    }
}

private bool IsPetPlayingAnimation(PetController pet, PetAnimationController.PetAnimationType animType)
{
    if (pet.animator == null) return false;
    return pet.animator.GetInteger("animation") == (int)animType;
}

private bool IsPetPlayingRidingAnimation(PetController meerkat)
{
    if (meerkat.animator == null) return true; // 애니메이터 없으면 실행 방지
    
    // 기본 자세(Eat)가 아닐 경우, 다른 행동 중인 것으로 간주
    return meerkat.animator.GetInteger("animation") != (int)PetAnimationController.PetAnimationType.Eat;
}
// ▲▲▲ [여기까지 추가] ▲▲▲

    /// <summary>
    /// 강제로 상호작용을 중단하고 정리합니다 (터치/홀드 시 호출됨)
    /// </summary>
    public void ForceCleanup()
    {
        Debug.Log("[RideAndWalk] ForceCleanup 호출됨 - 강제로 상호작용 정리");

        // 모든 코루틴 즉시 중단
        StopAllCoroutines();

        // 현재 라이더와 마운트 찾기 (부모-자식 관계로 판별)
        PetController rider = null;
        PetController mount = null;

        // 모든 펫 확인하여 부모-자식 관계 찾기
        PetController[] allPets = FindObjectsOfType<PetController>();
        foreach (var pet in allPets)
        {
            if (pet.transform.parent != null)
            {
                var parentPet = pet.transform.parent.GetComponent<PetController>();
                if (parentPet != null)
                {
                    // 부모-자식 관계 발견
                    rider = pet;
                    mount = parentPet;
                    break;
                }
            }
        }

        // 라이더와 마운트를 찾았다면 즉시 분리
        if (rider != null && mount != null)
        {
            Debug.Log($"[RideAndWalk] ForceCleanup - {rider.petName}을(를) {mount.petName}에서 분리");

            // 1. 부모-자식 관계 즉시 해제
            rider.transform.SetParent(null, true);

            // 2. 라이더를 마운트 옆 안전한 위치로 이동
            Vector3 safePosition = mount.transform.position + mount.transform.right * 2f;
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(safePosition, out navHit, 5f, NavMesh.AllAreas))
            {
                rider.transform.position = navHit.position;
            }
            else
            {
                // 실패 시 마운트 위치 사용
                rider.transform.position = mount.transform.position + Vector3.right * 2f;
            }

            // 3. 라이더 NavMeshAgent 재활성화
            if (rider.agent != null)
            {
                if (!rider.agent.enabled)
                {
                    rider.agent.enabled = true;
                }
                // 잠시 대기 후 위치 설정
                rider.StartCoroutine(DelayedAgentSetup(rider));
            }

            // 4. 라이더 스케일 복원
            rider.transform.localScale = Vector3.one;

            // 5. 마운트 상태 정리
            if (mount.agent != null && mount.agent.enabled && mount.agent.isOnNavMesh)
            {
                mount.agent.isStopped = false;
            }

            // 6. 애니메이션 정지
            var riderAnimController = rider.GetComponent<PetAnimationController>();
            if (riderAnimController != null)
            {
                riderAnimController.StopContinuousAnimation();
                riderAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            }

            var mountAnimController = mount.GetComponent<PetAnimationController>();
            if (mountAnimController != null)
            {
                mountAnimController.StopContinuousAnimation();
                mountAnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            }

            // 7. 감정 표현 숨기기
            rider.HideEmotion();
            mount.HideEmotion();

            // 8. 상호작용 상태 종료
            EndInteraction(rider, mount);

            Debug.Log("[RideAndWalk] ForceCleanup 완료 - 두 펫이 안전하게 분리됨");
        }
        else
        {
            Debug.LogWarning("[RideAndWalk] ForceCleanup - 라이더와 마운트를 찾을 수 없음");
        }
    }

    /// <summary>
    /// NavMeshAgent 설정을 지연 실행 (안정성을 위해)
    /// </summary>
    private IEnumerator DelayedAgentSetup(PetController pet)
    {
        yield return null; // 한 프레임 대기

        if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
        {
            pet.agent.Warp(pet.transform.position);
            pet.agent.isStopped = false;
            pet.agent.avoidancePriority = 50; // 기본값
        }
    }

    /// <summary>
    /// 라이더가 마운트 펫의 등에서 내리는 단계를 처리합니다.
    /// </summary>
    private IEnumerator DismountPet(PetController rider, PetController mount)
    {
        Debug.Log($"[RideAndWalk] 4단계: {rider.petName}이(가) 등에서 내립니다.");

        // 마운트가 멈춰서 앉아줍니다
        if (IsAgentSafelyReady(mount))
        {
            mount.agent.isStopped = true;
            mount.agent.velocity = Vector3.zero;
        }

        yield return StartCoroutine(mount.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 1.5f, false, false));

        // 라이더가 내릴 위치를 마운트의 약간 '앞쪽 대각선'으로 설정
        rider.transform.SetParent(null, true);

        // 마운트의 오른쪽 앞 대각선 방향을 계산합니다.
        Vector3 dismountDirection = (mount.transform.forward + mount.transform.right).normalized;
        Vector3 dismountLandPos = mount.transform.position + dismountDirection * farewellDistance;
        dismountLandPos = FindValidPositionOnNavMesh(dismountLandPos, farewellDistance + 1f);

        // 점프 애니메이션과 함께 내리기
        StartCoroutine(rider.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, mountDuration, true, false));

        // 부드러운 착지
        yield return StartCoroutine(SmoothDismountTransition(rider, dismountLandPos, mountDuration));

        if (rider.agent != null)
        {
            rider.agent.enabled = true;
            yield return null;

            if (rider.agent.enabled && rider.agent.isOnNavMesh)
            {
                rider.agent.Warp(dismountLandPos);
            }
        }
        // ▲▲▲ [여기까지 수정] ▲▲▲
    }

    /// <summary>
    /// 두 펫이 작별 인사를 나누는 단계를 처리합니다.
    /// </summary>
    private IEnumerator SayFarewell(PetController meerkat, PetController boar)
    {
        Debug.Log("[RideAndWalk] 5단계: 작별 인사하기");

        // ▼▼▼ [수정] Dismount 단계에서 거리를 확보했으므로, 여기서는 서로 부드럽게 마주보기만 하면 됩니다. ▼▼▼
        if (IsAgentSafelyReady(meerkat) && IsAgentSafelyReady(boar))
        {
            // 기존의 즉시 회전(LookAtEachOther) 대신 부드러운 회전을 사용합니다.
            yield return StartCoroutine(SmoothlyLookAtEachOther(meerkat, boar, 0.7f));
        }
        else
        {
            Debug.LogWarning("[RideAndWalk] 작별인사 시 펫의 NavMeshAgent가 준비되지 않았습니다.");
        }
        // ▲▲▲ [여기까지 수정] ▲▲▲
        yield return new WaitForSeconds(0.5f);

        // 서로 즐거웠다는 듯한 애니메이션
        meerkat.ShowEmotion(EmotionType.Happy, 5f); // ★★★ 추가: 감정 표현 ★★★
        boar.ShowEmotion(EmotionType.Happy, 5f);

        yield return StartCoroutine(PlaySimultaneousAnimations(
            meerkat, boar,
            PetAnimationController.PetAnimationType.Jump,
            PetAnimationController.PetAnimationType.Attack,
            2.0f));

        yield return new WaitForSeconds(1.0f);
    }

    // ★★★ 새로 추가할 헬퍼 메서드들 ★★★

    /// <summary>
    /// 안전하게 NavMeshAgent가 준비되었는지 확인하는 헬퍼 메서드
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }

    /// <summary>
    /// 미어캣이 부드럽게 탑승 위치로 이동하는 코루틴
    /// </summary>
    private IEnumerator SmoothMountTransition(PetController meerkat, Vector3 targetLocalPos, float duration)
    {
        Vector3 startLocalPos = meerkat.transform.localPosition;
        Quaternion startLocalRot = meerkat.transform.localRotation;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            float smoothT = t * t * (3f - 2f * t); // Smooth step

            meerkat.transform.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, smoothT);
            meerkat.transform.localRotation = Quaternion.Slerp(startLocalRot, Quaternion.identity, smoothT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        meerkat.transform.localPosition = targetLocalPos;
        meerkat.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 미어캣이 부드럽게 내리는 코루틴
    /// </summary>
    private IEnumerator SmoothDismountTransition(PetController meerkat, Vector3 targetWorldPos, float duration)
    {
        Vector3 startPos = meerkat.transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            float smoothT = t * t * (3f - 2f * t); // Smooth step

            meerkat.transform.position = Vector3.Lerp(startPos, targetWorldPos, smoothT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        meerkat.transform.position = targetWorldPos;
    }
}