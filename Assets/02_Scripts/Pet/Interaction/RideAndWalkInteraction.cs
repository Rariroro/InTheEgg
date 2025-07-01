// RideAndWalkInteraction.cs (수정된 버전)

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class RideAndWalkInteraction : BasePetInteraction
{
    public override string InteractionName => "RideAndWalk";

    [Header("Ride Settings")]
    [Tooltip("멧돼지 등 위에 미어캣이 위치할 로컬 좌표 오프셋입니다.")]
    public Vector3 ridePositionOffset = new Vector3(0, 1.7f, 0.2f);
    [Tooltip("미어캣이 멧돼지에 올라타거나 내릴 때 걸리는 시간입니다.")]
    public float mountDuration = 1.0f;

    [Header("Walk Together Settings")]
    [Tooltip("함께 걷는 총 시간입니다.")]
    public float walkTogetherDuration = 20f;
    [Tooltip("함께 걷는 동안 새로운 목적지를 설정하는 주기입니다.")]
    public float pathUpdateInterval = 7f;
    [Tooltip("함께 걷는 동안의 이동 속도 배율입니다.")]
    public float walkingSpeedMultiplier = 0.9f;
  // ▼▼▼ [수정] 작별인사 거리를 조절할 수 있는 변수 추가 ▼▼▼
    [Tooltip("미어캣이 내린 후 작별인사를 할 때 유지할 거리입니다.")]
    public float farewellDistance = 4f;
    // ▲▲▲ [여기까지 수정] ▲▲▲

    [Header("Safety Settings")]
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;


    // ★★★ 추가: 미어캣의 원래 회피 우선순위를 저장할 변수 ★★★
    private int meerkatOriginalPriority;

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.RideAndWalk;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        return (pet1.PetType == PetType.Meerkat && pet2.PetType == PetType.Boar) ||
               (pet1.PetType == PetType.Boar && pet2.PetType == PetType.Meerkat);
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[RideAndWalk] {pet1.petName}와(과) {pet2.petName}의 타고 걷기 상호작용이 시작됩니다!");

        // 역할 식별
        PetController meerkat = (pet1.PetType == PetType.Meerkat) ? pet1 : pet2;
        PetController boar = (pet1.PetType == PetType.Boar) ? pet1 : pet2;

        // ★★★ 추가: NavMeshAgent 준비 상태 확인 ★★★
        yield return StartCoroutine(WaitUntilAgentIsReady(meerkat, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(boar, agentSafetyTimeout));

        if (!IsAgentSafelyReady(meerkat) || !IsAgentSafelyReady(boar))
        {
            Debug.LogError("[RideAndWalk] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(meerkat, boar);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState meerkatState = new PetOriginalState(meerkat);
        PetOriginalState boarState = new PetOriginalState(boar);
        // ★★★ 수정된 부분 시작 ★★★
    // 미어캣의 원래 부모와 스케일 정보를 정확히 저장합니다.
    Transform originalMeerkatParent = meerkat.transform.parent;
    Vector3 originalMeerkatLocalScale = meerkat.transform.localScale;
    Vector3 originalMeerkatWorldScale = meerkat.transform.lossyScale;
    // ★★★ 수정된 부분 끝 ★★★
    meerkatOriginalPriority = meerkat.agent.avoidancePriority;

        try
        {
            // ★★★ 추가: 감정 표현 시작 ★★★
            meerkat.ShowEmotion(EmotionType.Love, walkTogetherDuration + 15f);
            boar.ShowEmotion(EmotionType.Love, walkTogetherDuration + 15f);

            // 1. 만나서 노는 단계
            yield return StartCoroutine(MeetAndPlay(meerkat, boar));

            // 2. 멧돼지 등에 올라타는 단계
            yield return StartCoroutine(MountBoar(meerkat, boar));

            // 3. 함께 주변을 산책하는 단계
            yield return StartCoroutine(WalkTogether(meerkat, boar));

            // 4. 멧돼지 등에서 내리는 단계
            yield return StartCoroutine(DismountBoar(meerkat, boar));

            // 5. 작별 인사를 하는 단계
            yield return StartCoroutine(SayFarewell(meerkat, boar));
        }
        finally
        {
            Debug.Log("[RideAndWalk] 상호작용 정리 시작.");
           // ★★★ 수정된 부분 시작 ★★★
        // 부모 관계 복원
        if (meerkat.transform.parent == boar.transform)
        {
            meerkat.transform.SetParent(originalMeerkatParent, true);
        }
        
        // 원래 부모가 있었는지 여부에 따라 스케일을 정확하게 복원합니다.
        if (originalMeerkatParent == null)
        {
            // 원래 부모가 없었다면 월드 스케일 기준으로 복원
            meerkat.transform.localScale = originalMeerkatWorldScale;
        }
        else
        {
            // 원래 부모가 있었다면 로컬 스케일 기준으로 복원
            meerkat.transform.localScale = originalMeerkatLocalScale;
        }
        // ★★★ 수정된 부분 끝 ★★★

        // ★★★ 추가: 미어캣의 회피 우선순위 복원 ★★★
        if (IsAgentSafelyReady(meerkat))
        {
            meerkat.agent.avoidancePriority = meerkatOriginalPriority;
        }

        // 상태 복원
        meerkatState.Restore(meerkat);
        boarState.Restore(boar);

        // 상호작용 종료
        EndInteraction(meerkat, boar);
        Debug.Log("[RideAndWalk] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 두 펫이 만나서 노는 초기 단계를 처리합니다.
    /// </summary>
    private IEnumerator MeetAndPlay(PetController meerkat, PetController boar)
    {
        Debug.Log("[RideAndWalk] 1단계: 만나서 놀기");

        // 서로 마주볼 위치로 이동
        Vector3 meerkatPos, boarPos;
        CalculateStartPositions(meerkat, boar, out meerkatPos, out boarPos, 5f);

        // ★★★ 수정: 안전한 이동 확인 ★★★
        if (IsAgentSafelyReady(meerkat) && IsAgentSafelyReady(boar))
        {
            yield return StartCoroutine(MoveToPositions(meerkat, boar, meerkatPos, boarPos, 10f));
            LookAtEachOther(meerkat, boar);
        }

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
    /// 미어캣이 멧돼지 등에 올라타는 단계를 처리합니다.
    /// </summary>
    private IEnumerator MountBoar(PetController meerkat, PetController boar)
    {
        Debug.Log($"[RideAndWalk] 2단계: {meerkat.petName}이(가) {boar.petName}의 등에 올라탑니다.");

        // ★★★ 추가: 미어캣의 회피 우선순위를 낮춰서 멧돼지를 가로막지 않도록 ★★★
        if (IsAgentSafelyReady(meerkat))
        {
            meerkat.agent.avoidancePriority = 99;
        }

        // 멧돼지가 앉아서 기다려주는 애니메이션
        if (IsAgentSafelyReady(boar))
        {
            boar.agent.isStopped = true;
        }

        yield return StartCoroutine(boar.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 2.0f, false, false));

        // 미어캣이 점프해서 올라타는 애니메이션
        yield return StartCoroutine(meerkat.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, mountDuration, false, false));

        // 미어캣의 NavMeshAgent를 비활성화
        if (meerkat.agent != null && meerkat.agent.enabled)
        {
            meerkat.agent.enabled = false;
        }

        // 미어캣을 멧돼지의 자식으로 만들기
        meerkat.transform.SetParent(boar.transform, true);

        // 부드러운 위치 이동
        yield return StartCoroutine(SmoothMountTransition(meerkat, ridePositionOffset, mountDuration));
    }

    /// <summary>
    /// 멧돼지가 미어캣을 태우고 함께 산책하는 단계를 처리합니다.
    /// </summary>
    private IEnumerator WalkTogether(PetController meerkat, PetController boar)
    {
        Debug.Log($"[RideAndWalk] 3단계: 함께 산책하기");

        // ★★★ 추가: 안전한 NavMeshAgent 체크 ★★★
        if (!IsAgentSafelyReady(boar))
        {
            Debug.LogWarning("[RideAndWalk] 멧돼지의 NavMeshAgent가 준비되지 않아 산책을 건너뜁니다.");
            yield break;
        }

        // 멧돼지 설정
        boar.agent.speed = boar.baseSpeed * walkingSpeedMultiplier;
        boar.agent.updateRotation = true;
        boar.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        // 미어캣 설정
        meerkat.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Eat);

        float walkStartTime = Time.time;
        float lastPathUpdateTime = 0f;

        while (Time.time - walkStartTime < walkTogetherDuration)
        {
            // ★★★ 추가: 매 프레임마다 안전성 체크 ★★★
            if (!IsAgentSafelyReady(boar))
            {
                Debug.LogWarning("[RideAndWalk] 산책 중 멧돼지의 NavMeshAgent 문제 발생");
                break;
            }

            // 일정 주기마다 새로운 목적지로 갱신
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

            // 미어캣이 가끔씩 즐거워하는 모션
            if (Random.value < 0.01f)
            {
                meerkat.ShowEmotion(EmotionType.Happy, 3f); // ★★★ 추가: 감정 표현 ★★★
                StartCoroutine(meerkat.GetComponent<PetAnimationController>()
                    .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, 1.0f, false, false));
            }

            yield return null;
        }
    }

    /// <summary>
    /// 미어캣이 멧돼지 등에서 내리는 단계를 처리합니다.
    /// </summary>
    private IEnumerator DismountBoar(PetController meerkat, PetController boar)
    {
        Debug.Log($"[RideAndWalk] 4단계: {meerkat.petName}이(가) 등에서 내립니다.");

        // 멧돼지가 멈춰서 앉아줍니다
        if (IsAgentSafelyReady(boar))
        {
            boar.agent.isStopped = true;
            boar.agent.velocity = Vector3.zero;
        }

        yield return StartCoroutine(boar.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Eat, 1.5f, false, false));

        // 미어캣을 부모-자식 관계에서 해제
        meerkat.transform.SetParent(null, true);

           // ▼▼▼ [수정] 미어캣이 내릴 위치를 farewellDistance 만큼 떨어진 곳으로 계산 ▼▼▼
        Vector3 sideDirection = boar.transform.right; // 멧돼지의 오른쪽 방향
        Vector3 dismountLandPos = boar.transform.position + sideDirection * farewellDistance;
        dismountLandPos = FindValidPositionOnNavMesh(dismountLandPos, farewellDistance + 1f);

        // 점프 애니메이션과 함께 내리기
        StartCoroutine(meerkat.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, mountDuration, true, false));

        // 부드러운 착지
        yield return StartCoroutine(SmoothDismountTransition(meerkat, dismountLandPos, mountDuration));

        if (meerkat.agent != null)
        {
            meerkat.agent.enabled = true;
            yield return null; 

            if (meerkat.agent.enabled && meerkat.agent.isOnNavMesh)
            {
                meerkat.agent.Warp(dismountLandPos);
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

        // ▼▼▼ [수정] 이제 Dismount 단계에서 거리를 확보했으므로, 여기서는 서로 마주보기만 하면 됩니다. ▼▼▼
        if (IsAgentSafelyReady(meerkat) && IsAgentSafelyReady(boar))
        {
            LookAtEachOther(meerkat, boar);
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