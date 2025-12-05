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

    // 강제 종료 시 복원을 위한 상태 저장
    private PetController activeRider;
    private PetController activeMount;
    private Transform originalRiderParent;
    private Vector3 originalRiderLocalScale;
    private Vector3 originalRiderWorldScale;

    // 애니메이션 컨트롤러 캐싱
    private PetAnimationController riderAnim;
    private PetAnimationController mountAnim;

    // WaitForSeconds 캐싱 (성능 최적화)
    private static readonly WaitForSeconds Wait02 = new WaitForSeconds(0.2f);
    private static readonly WaitForSeconds Wait05 = new WaitForSeconds(0.5f);
    private static readonly WaitForSeconds Wait10 = new WaitForSeconds(1.0f);

    // 캐시 초기화 (씬 로드/재로드 시 이전 잘못된 값 제거)
    private void OnEnable()
    {
        ridePositionCache.Clear();
    }

    /// <summary>
    /// 강제 종료 시 RideAndWalk 고유 리소스를 정리합니다.
    /// </summary>
    protected override void OnForceCleanup()
    {
        Debug.Log("[RideAndWalk] OnForceCleanup 호출됨 - 고유 리소스 정리 시작");

        // 1. 감정 숨기기
        if (activeRider != null) activeRider.HideEmotion();
        if (activeMount != null) activeMount.HideEmotion();

        // 2. 라이더의 부모 관계 및 스케일 복원
        if (activeRider != null)
        {
            // 부모 관계 복원
            if (activeMount != null && activeRider.transform.parent == activeMount.transform)
            {
                activeRider.transform.SetParent(originalRiderParent, true);
            }

            // 스케일 복원
            if (originalRiderParent == null)
            {
                activeRider.transform.localScale = originalRiderWorldScale;
            }
            else
            {
                activeRider.transform.localScale = originalRiderLocalScale;
            }

            // NavMeshAgent 재활성화
            if (activeRider.agent != null && !activeRider.agent.enabled)
            {
                activeRider.agent.enabled = true;
            }

            // 회피 우선순위 복원
            if (activeRider.agent != null && activeRider.agent.enabled && activeRider.agent.isOnNavMesh)
            {
                activeRider.agent.avoidancePriority = riderOriginalPriority;
            }
        }

        // 3. 애니메이션 컨트롤러 정리
        if (riderAnim != null)
        {
            riderAnim.StopContinuousAnimation();
            riderAnim = null;
        }
        if (mountAnim != null)
        {
            mountAnim.StopContinuousAnimation();
            mountAnim = null;
        }

        // 4. 상태 초기화
        activeRider = null;
        activeMount = null;
        originalRiderParent = null;

        Debug.Log("[RideAndWalk] OnForceCleanup 완료 - 고유 리소스 정리됨");
    }

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.RideAndWalk;
    }

    /// <summary>
    /// 템플릿 인스턴스의 설정을 복사합니다 (인스턴스 생성 시 사용)
    /// </summary>
    public void CopySettingsFrom(RideAndWalkInteraction template)
    {
        if (template == null) return;

        this.meetingDistance = template.meetingDistance;
        this.defaultRideOffset = template.defaultRideOffset;
        this.mountDuration = template.mountDuration;
        this.walkTogetherDuration = template.walkTogetherDuration;
        this.pathUpdateInterval = template.pathUpdateInterval;
        this.walkingSpeedMultiplier = template.walkingSpeedMultiplier;
        this.farewellDistance = template.farewellDistance;
        this.autoHeightMultiplier = template.autoHeightMultiplier;
        this.autoDepthMultiplier = template.autoDepthMultiplier;
        this.agentSafetyTimeout = template.agentSafetyTimeout;
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
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState riderState = new PetOriginalState(rider);
        PetOriginalState mountState = new PetOriginalState(mount);

        // 클래스 필드에 상태 저장 (강제 종료 시 복원을 위해)
        activeRider = rider;
        activeMount = mount;
        originalRiderParent = rider.transform.parent;
        originalRiderLocalScale = rider.transform.localScale;
        originalRiderWorldScale = rider.transform.lossyScale;
        riderOriginalPriority = rider.agent.avoidancePriority;

        // 애니메이션 컨트롤러 캐싱
        riderAnim = rider.GetComponent<PetAnimationController>();
        mountAnim = mount.GetComponent<PetAnimationController>();

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

            // 감정 숨기기
            rider.HideEmotion();
            mount.HideEmotion();

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

            // 애니메이션 컨트롤러 정리
            if (riderAnim != null) riderAnim.StopContinuousAnimation();
            if (mountAnim != null) mountAnim.StopContinuousAnimation();

            // 클래스 필드 초기화
            activeRider = null;
            activeMount = null;
            riderAnim = null;
            mountAnim = null;

            // 주의: EndInteraction은 BasePetInteraction.InteractionLifecycle에서 자동 호출됨
            // 여기서 직접 호출하면 중복 호출 발생
            Debug.Log("[RideAndWalk] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 두 펫이 만나서 노는 초기 단계를 처리합니다.
    /// </summary>
    private IEnumerator MeetAndPlay(PetController rider, PetController mount)
    {
        Debug.Log("[RideAndWalk] 1단계: 만나서 놀기");

        // BasePetInteraction이 이미 위치를 정렬했으므로 중복 이동 제거
        // 펫들이 이미 적절한 거리에서 마주보고 있는 상태

        // 안정성을 위해 잠시 대기
        yield return Wait02;

        // 서로 즐겁게 노는 애니메이션
        yield return StartCoroutine(PlaySimultaneousAnimations(
            rider, mount,
            PetAnimationController.PetAnimationType.Jump,
            PetAnimationController.PetAnimationType.Idle,
            1.5f));

        yield return StartCoroutine(PlaySimultaneousAnimations(
            mount, rider,
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

        // 캐싱된 애니메이션 컨트롤러 사용
        if (mountAnim != null)
            yield return StartCoroutine(mountAnim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Eat, 2.0f, false, false));

        // 라이더가 점프해서 올라타는 애니메이션
        if (riderAnim != null)
            yield return StartCoroutine(riderAnim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, mountDuration, false, false));

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
    /// 마운트 펫이 라이더를 태우고 함께 산책하는 단계를 처리합니다.
    /// </summary>
    private IEnumerator WalkTogether(PetController rider, PetController mount)
    {
        Debug.Log($"[RideAndWalk] 3단계: 함께 산책하기");

        if (!IsAgentSafelyReady(mount))
        {
            Debug.LogWarning("[RideAndWalk] 마운트의 NavMeshAgent가 준비되지 않아 산책을 건너뜁니다.");
            yield break;
        }

        // 마운트 설정 (캐싱된 애니메이션 컨트롤러 사용)
        mount.agent.speed = mount.baseSpeed * walkingSpeedMultiplier;
        mount.agent.updateRotation = true;
        if (mountAnim != null)
            mountAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        // 라이더의 기본 자세를 Eat(앉기)으로 설정 (캐싱된 애니메이션 컨트롤러 사용)
        if (riderAnim != null)
            riderAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Eat);

        float walkStartTime = Time.time;
        float lastPathUpdateTime = 0f;

        while (Time.time - walkStartTime < walkTogetherDuration)
        {
            // 펫이 잡히면 상호작용 종료
            if (rider.State.IsHolding || rider.State.IsSelected ||
                mount.State.IsHolding || mount.State.IsSelected)
            {
                Debug.Log("[RideAndWalk] 펫이 잡혀서 산책을 종료합니다.");
                break;
            }

            if (!IsAgentSafelyReady(mount))
            {
                Debug.LogWarning("[RideAndWalk] 산책 중 마운트의 NavMeshAgent 문제 발생");
                break;
            }

            // 일정 주기마다 새로운 목적지로 갱신
            if (Time.time - lastPathUpdateTime > pathUpdateInterval)
            {
                lastPathUpdateTime = Time.time;
                Vector3 randomDirection = Random.insideUnitSphere * 25f;
                randomDirection.y = 0;
                Vector3 newDestination = mount.transform.position + randomDirection;
                mount.agent.updateRotation = true;

                mount.agent.SetDestination(FindValidPositionOnNavMesh(newDestination, 30f));
                mount.agent.isStopped = false;
                Debug.Log($"[RideAndWalk] 새로운 목적지로 이동: {mount.agent.destination}");
            }

            // 라이더가 등 위에서 다양한 행동을 하도록 로직
            // 행동 빈도 2%, 현재 다른 특별 행동 중이 아닐 때만 실행
            if (Random.value < 0.02f && !IsPetPlayingRidingAnimation(rider))
            {
                int randomAction = Random.Range(0, 4);

                switch (randomAction)
                {
                    case 0: // 점프하며 즐거워하기
                        rider.ShowEmotion(EmotionType.Happy, 2f);
                        StartCoroutine(PlayRidingAnimation(rider, PetAnimationController.PetAnimationType.Jump, 1.0f));
                        break;

                    case 1: // 주변을 두리번거리며 경계하기 (Idle 애니메이션 활용)
                        rider.ShowEmotion(EmotionType.Surprised, 2f);
                        StartCoroutine(PlayRidingAnimation(rider, PetAnimationController.PetAnimationType.Idle, 1.5f));
                        break;

                    case 2: // 신나게 소리치기 (Attack 애니메이션 활용)
                        rider.ShowEmotion(EmotionType.Love, 2f);
                        StartCoroutine(PlayRidingAnimation(rider, PetAnimationController.PetAnimationType.Attack, 1.2f));
                        break;

                    case 3: // 잠시 편하게 눕기 (Rest 애니메이션 활용)
                        rider.ShowEmotion(EmotionType.Sleepy, 3f);
                        StartCoroutine(PlayRidingAnimation(rider, PetAnimationController.PetAnimationType.Rest, 2.0f));
                        break;
                }
            }

            yield return null;
        }
    }
    /// <summary>
    /// 마운트 등 위에서 라이더의 짧은 행동을 재생하고 기본 자세로 돌리는 헬퍼 메서드
    /// </summary>
    private IEnumerator PlayRidingAnimation(PetController rider, PetAnimationController.PetAnimationType animType, float duration)
    {
        // 캐싱된 애니메이션 컨트롤러 사용
        var animController = riderAnim;
        if (animController == null) yield break;

        // 1. 일시적인 애니메이션 재생
        animController.SetContinuousAnimation(animType);
        yield return new WaitForSeconds(duration);

        // 2. 다시 기본 탑승 애니메이션(Eat)으로 복귀
        // 단, 코루틴 실행 중에 다른 애니메이션으로 바뀌지 않았을 경우에만 복귀
        if (animController != null && IsPetPlayingAnimation(rider, animType))
        {
            animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Eat);
        }
    }

    private bool IsPetPlayingAnimation(PetController pet, PetAnimationController.PetAnimationType animType)
    {
        if (pet.animator == null) return false;
        return pet.animator.GetInteger("animation") == (int)animType;
    }

    private bool IsPetPlayingRidingAnimation(PetController rider)
    {
        if (rider.animator == null) return true; // 애니메이터 없으면 실행 방지

        // 기본 자세(Eat)가 아닐 경우, 다른 행동 중인 것으로 간주
        return rider.animator.GetInteger("animation") != (int)PetAnimationController.PetAnimationType.Eat;
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

        // 캐싱된 애니메이션 컨트롤러 사용
        if (mountAnim != null)
            yield return StartCoroutine(mountAnim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Eat, 1.5f, false, false));

        // 라이더가 내릴 위치를 마운트의 약간 '앞쪽 대각선'으로 설정
        rider.transform.SetParent(null, true);

        // 마운트의 오른쪽 앞 대각선 방향을 계산합니다.
        Vector3 dismountDirection = (mount.transform.forward + mount.transform.right).normalized;
        Vector3 dismountLandPos = mount.transform.position + dismountDirection * farewellDistance;
        dismountLandPos = FindValidPositionOnNavMesh(dismountLandPos, farewellDistance + 1f);

        // 점프 애니메이션과 함께 내리기 (캐싱된 애니메이션 컨트롤러 사용)
        if (riderAnim != null)
            StartCoroutine(riderAnim.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Jump, mountDuration, true, false));

        // 부드러운 착지
        yield return StartCoroutine(SmoothDismountTransition(rider, dismountLandPos, mountDuration));

        // NavMeshAgent 재활성화 및 안정성 대기
        if (rider.agent != null)
        {
            rider.agent.enabled = true;

            // 다른 상호작용(WalkTogetherInteraction)과 동일하게 안정성을 위해 대기
            yield return Wait02;

            if (rider.agent.enabled && rider.agent.isOnNavMesh)
            {
                rider.agent.Warp(dismountLandPos);
            }
        }
    }

    /// <summary>
    /// 두 펫이 작별 인사를 나누는 단계를 처리합니다.
    /// </summary>
    private IEnumerator SayFarewell(PetController rider, PetController mount)
    {
        Debug.Log("[RideAndWalk] 5단계: 작별 인사하기");

        // Dismount 단계에서 거리를 확보했으므로, 여기서는 서로 부드럽게 마주보기만 하면 됩니다.
        if (IsAgentSafelyReady(rider) && IsAgentSafelyReady(mount))
        {
            // 부드러운 회전
            yield return StartCoroutine(SmoothlyLookAtEachOther(rider, mount, 0.7f));
        }
        else
        {
            Debug.LogWarning("[RideAndWalk] 작별인사 시 펫의 NavMeshAgent가 준비되지 않았습니다.");
        }

        yield return Wait05;

        // 서로 즐거웠다는 듯한 애니메이션
        rider.ShowEmotion(EmotionType.Happy, 5f);
        mount.ShowEmotion(EmotionType.Happy, 5f);

        yield return StartCoroutine(PlaySimultaneousAnimations(
            rider, mount,
            PetAnimationController.PetAnimationType.Jump,
            PetAnimationController.PetAnimationType.Attack,
            2.0f));

        yield return Wait10;
    }

    // ===== 헬퍼 메서드들 =====

    /// <summary>
    /// 안전하게 NavMeshAgent가 준비되었는지 확인하는 헬퍼 메서드
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }

    /// <summary>
    /// 라이더가 부드럽게 탑승 위치로 이동하는 코루틴
    /// </summary>
    private IEnumerator SmoothMountTransition(PetController rider, Vector3 targetLocalPos, float duration)
    {
        Vector3 startLocalPos = rider.transform.localPosition;
        Quaternion startLocalRot = rider.transform.localRotation;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            float smoothT = t * t * (3f - 2f * t); // Smooth step

            rider.transform.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, smoothT);
            rider.transform.localRotation = Quaternion.Slerp(startLocalRot, Quaternion.identity, smoothT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        rider.transform.localPosition = targetLocalPos;
        rider.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 라이더가 부드럽게 내리는 코루틴
    /// </summary>
    private IEnumerator SmoothDismountTransition(PetController rider, Vector3 targetWorldPos, float duration)
    {
        Vector3 startPos = rider.transform.position;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            float smoothT = t * t * (3f - 2f * t); // Smooth step

            rider.transform.position = Vector3.Lerp(startPos, targetWorldPos, smoothT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        rider.transform.position = targetWorldPos;
    }
}