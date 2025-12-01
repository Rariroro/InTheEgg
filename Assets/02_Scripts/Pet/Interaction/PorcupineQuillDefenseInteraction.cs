// PorcupineQuillDefenseInteraction.cs
// 고슴도치 가시 방어 상호작용
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;

/// <summary>
/// 고슴도치가 다른 동물에게 건드려지면 가시를 발사하여 방어하는 상호작용
/// </summary>
public class PorcupineQuillDefenseInteraction : BasePetInteraction
{
    public override string InteractionName => "PorcupineQuillDefense";

    // 우선순위: 75 (SkunkDefense와 동일)
    public override int Priority => 75;

    [Header("거리 설정")]
    [Tooltip("상호작용 시 유지할 거리")]
    public float interactionDistance = 3f;

    [Tooltip("접근자가 접근하는 속도 배율")]
    public float approacherSpeedMultiplier = 1.2f;

    [Tooltip("접근 시도 최대 시간")]
    public float approachTimeout = 8f;

    [Header("가시 방어 설정")]
    [Tooltip("고슴도치가 돌아서는 시간")]
    public float quillTurnDuration = 0.5f;

    [Tooltip("가시 발사 애니메이션 시간")]
    public float quillDefenseAnimationDuration = 2.0f;

    [Tooltip("접근자가 물러나는 거리")]
    public float approacherRetreatDistance = 10f;

    [Tooltip("접근자가 물러나는 속도 배율")]
    public float approacherRetreatSpeedMultiplier = 2.0f;

    [Header("도망 설정")]
    [Tooltip("고슴도치가 도망가는 거리")]
    public float porcupineEscapeDistance = 15f;

    [Tooltip("고슴도치가 도망가는 속도 배율")]
    public float porcupineEscapeSpeedMultiplier = 1.5f;

    [Tooltip("도망 시도 최대 시간")]
    public float escapeTimeout = 6f;

    [Header("가시 설정")]
    [Tooltip("가시 발사 시 이펙트 프리팹 (먼지 효과 등)")]
    public GameObject quillLaunchEffectPrefab;

    [Tooltip("이펙트 생성 위치 오프셋 (고슴도치 기준)")]
    public Vector3 effectOffset = new Vector3(0, 0.5f, -0.8f);

    [Tooltip("이펙트 지속 시간")]
    public float effectDuration = 2f;

    [Tooltip("박히는 가시 개수")]
    public int stuckQuillCount = 5;

    [Tooltip("박힌 가시 지속 시간")]
    public float stuckQuillDuration = 10f;

    [Tooltip("박힌 가시 크기 배율")]
    public float stuckQuillScale = 1f;

    [Tooltip("발사 이펙트 크기 배율")]
    public float effectScale = 1f;

    [Tooltip("머리 가시 위치 오프셋 (head_end 기준)")]
    public Vector3 headQuillOffset = new Vector3(0f, 0f, 0.2f);

    [Tooltip("머리 가시 위치 랜덤 범위")]
    public Vector3 headQuillRandomRange = new Vector3(0.3f, 0.25f, 0.1f);

    [Header("크로커다일 전용 가시 설정")]
    [Tooltip("크로커다일 턱당 가시 개수")]
    public int crocodileQuillsPerJaw = 5;

    [Tooltip("크로커다일 가시 위치 오프셋 (턱 본 기준)")]
    public Vector3 crocodileQuillOffset = new Vector3(0f, 0f, 0.2f);

    [Tooltip("크로커다일 가시 위치 랜덤 범위")]
    public Vector3 crocodileQuillRandomRange = new Vector3(0.3f, 0.25f, 0.1f);

    [Tooltip("크로커다일 가시 크기 배율")]
    public float crocodileQuillScale = 1f;

    [Header("감정 표현 설정")]
    [Tooltip("기본 감정 표현 지속 시간")]
    public float emotionDuration = 30f;

    [Header("안전 설정")]
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;

    // 기본 위치 이동을 하지 않고, 접근자가 직접 접근하도록 설정
    public override bool ShouldPerformInitialMovement => false;

    // 인스턴스별 가시 추적 (동시 상호작용 시 충돌 방지)
    private List<GameObject> myStuckQuills = new List<GameObject>();

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.ChaseAndRun;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        // 한 쪽이 고슴도치인지 확인
        bool hasPorcupine = pet1.PetType == PetType.Porcupine || pet2.PetType == PetType.Porcupine;
        if (!hasPorcupine) return false;

        PetController otherPet = (pet1.PetType == PetType.Porcupine) ? pet2 : pet1;

        // 고기를 먹는 육식동물과의 상호작용 (Fish만 먹는 동물은 제외)
        bool isMeatEater = (otherPet.diet & PetAIProperties.DietaryFlags.Meat) != 0;
        return isMeatEater;
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 가시 방어 상호작용 시작!");

        // 역할 식별
        PetController approacher = null;
        PetController porcupine = null;

        if (pet1.PetType == PetType.Porcupine)
        {
            porcupine = pet1;
            approacher = pet2;
        }
        else if (pet2.PetType == PetType.Porcupine)
        {
            porcupine = pet2;
            approacher = pet1;
        }
        else
        {
            Debug.LogError($"[{InteractionName}] 고슴도치가 없는 상호작용이 시작되었습니다.");
            yield break;
        }

        Debug.Log($"[{InteractionName}] 접근자: {approacher.petName}, 고슴도치: {porcupine.petName}");

        // NavMeshAgent 준비 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(approacher, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(porcupine, agentSafetyTimeout));

        if (!IsAgentSafelyReady(approacher) || !IsAgentSafelyReady(porcupine))
        {
            Debug.LogError($"[{InteractionName}] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(approacher, porcupine);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState approacherState = new PetOriginalState(approacher);
        PetOriginalState porcupineState = new PetOriginalState(porcupine);

        // 인스턴스별 가시 리스트 초기화
        myStuckQuills.Clear();

        try
        {
            // 0. 위치 및 방향 준비 (거리 조정 + 서로 마주보기)
            yield return StartCoroutine(PreparePositionAndFacing(approacher, porcupine));

            // 감정 표현
            approacher.ShowEmotion(EmotionType.Happy, emotionDuration);
            porcupine.ShowEmotion(EmotionType.Surprised, emotionDuration);

            // 짧은 대기 (감정 표현이 보이도록)
            yield return new WaitForSeconds(0.5f);

            // 1. 접근 시도 단계
            yield return StartCoroutine(ApproachAttemptPhase(approacher, porcupine));

            // 2. 고슴도치 가시 방어 단계
            yield return StartCoroutine(QuillDefensePhase(porcupine, approacher));

            // 3. 접근자 후퇴 단계
            yield return StartCoroutine(ApproacherRetreatPhase(approacher, porcupine));

            // 4. 고슴도치 도망 단계
            yield return StartCoroutine(PorcupineEscapePhase(porcupine, approacher));

            // 5. 승리 표현 단계
            yield return StartCoroutine(VictoryCelebrationPhase(porcupine, approacher));

            Debug.Log($"[{InteractionName}] 상호작용 완료!");
        }
        finally
        {
            // 최종 정리
            Debug.Log($"[{InteractionName}] 상호작용 정리 시작.");

            // 원래 상태 복원
            approacherState.Restore(approacher);
            porcupineState.Restore(porcupine);

            // 애니메이션 정리
            approacher.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
            porcupine.GetComponent<PetAnimationController>()?.StopContinuousAnimation();

            // 공통 종료 처리
            EndInteraction(approacher, porcupine);
            Debug.Log($"[{InteractionName}] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 0단계: 초기 위치 및 방향 준비 (거리 조정 + 서로 마주보기)
    /// </summary>
    private IEnumerator PreparePositionAndFacing(PetController approacher, PetController porcupine)
    {
        Debug.Log($"[{InteractionName}] 0단계: 위치 및 방향 준비 시작");

        // 1. 거리 계산
        float approacherMultiplier = approacher.Profile.GetInteractionDistanceMultiplier();
        float porcupineMultiplier = porcupine.Profile.GetInteractionDistanceMultiplier();
        float averageMultiplier = (approacherMultiplier + porcupineMultiplier) / 2f;
        float targetDistance = interactionDistance * averageMultiplier;
        float currentDistance = Vector3.Distance(approacher.transform.position, porcupine.transform.position);

        Debug.Log($"[{InteractionName}] 현재 거리: {currentDistance:F2}m, 목표 거리: {targetDistance:F2}m");

        // 2. 고슴도치가 먼저 접근자를 바라보도록 설정
        Vector3 directionToApproacher = (approacher.transform.position - porcupine.transform.position).normalized;
        directionToApproacher.y = 0;
        if (directionToApproacher != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToApproacher);
            porcupine.transform.rotation = targetRotation;
            if (porcupine.petModelTransform != null)
            {
                porcupine.petModelTransform.rotation = porcupine.transform.rotation;
            }
        }

        // 고슴도치는 제자리에서 경계
        porcupine.agent.isStopped = true;
        porcupine.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);

        // 3. 거리 조정 필요 여부 확인
        bool needsMovement = currentDistance < targetDistance * 0.8f || currentDistance > targetDistance * 1.5f;

        if (needsMovement)
        {
            Vector3 targetPosition;

            if (currentDistance < targetDistance * 0.8f)
            {
                // 너무 가까움 → 접근자를 뒤로 이동
                Debug.Log($"[{InteractionName}] 너무 가까움 → 접근자를 뒤로 {targetDistance:F2}m 거리로 이동");

                Vector3 directionAway = (approacher.transform.position - porcupine.transform.position).normalized;
                if (directionAway == Vector3.zero)
                {
                    directionAway = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                }
                targetPosition = porcupine.transform.position + directionAway * targetDistance;
            }
            else
            {
                // 너무 멀음 → 접근자를 앞으로 이동
                Debug.Log($"[{InteractionName}] 너무 멀음 → 접근자를 앞으로 {targetDistance:F2}m 거리로 이동");
                targetPosition = porcupine.transform.position + porcupine.transform.forward * targetDistance;
            }

            // NavMesh 상 유효한 위치 찾기
            targetPosition = FindValidPositionOnNavMesh(targetPosition, targetDistance * 1.5f);

            // 접근자 이동 시작
            approacher.agent.isStopped = false;
            approacher.agent.speed = approacher.baseSpeed * approacherSpeedMultiplier;
            approacher.agent.SetDestination(targetPosition);
            approacher.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            // 이동 완료 대기
            float timer = 0f;
            while (timer < approachTimeout)
            {
                // 고슴도치가 접근자를 부드럽게 계속 바라봄
                directionToApproacher = (approacher.transform.position - porcupine.transform.position).normalized;
                directionToApproacher.y = 0;
                if (directionToApproacher.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(directionToApproacher);
                    porcupine.transform.rotation = Quaternion.Slerp(porcupine.transform.rotation, targetRot, Time.deltaTime * 5f);
                    if (porcupine.petModelTransform != null)
                    {
                        porcupine.petModelTransform.rotation = porcupine.transform.rotation;
                    }
                }

                // 도착 체크
                if (!approacher.agent.pathPending && approacher.agent.remainingDistance < 0.5f)
                {
                    Debug.Log($"[{InteractionName}] 위치 조정 완료");
                    break;
                }

                approacher.HandleRotation();
                timer += Time.deltaTime;
                yield return null;
            }

            // 접근자 정지
            approacher.agent.isStopped = true;
            approacher.GetComponent<PetAnimationController>().StopContinuousAnimation();

            yield return new WaitForSeconds(0.2f);
        }
        else
        {
            Debug.Log($"[{InteractionName}] 거리 적절함 ({currentDistance:F2}m) → 이동 생략");
            approacher.agent.isStopped = true;
        }

        // 4. 서로 정확히 마주보기
        Debug.Log($"[{InteractionName}] 서로 마주보기 시작");
        yield return StartCoroutine(SmoothlyLookAtEachOther(approacher, porcupine, 1f));

        Debug.Log($"[{InteractionName}] 위치 및 방향 준비 완료");
    }

    /// <summary>
    /// 1단계: 접근 시도
    /// </summary>
    private IEnumerator ApproachAttemptPhase(PetController approacher, PetController porcupine)
    {
        Debug.Log($"[{InteractionName}] 1단계: {approacher.petName}이(가) 고슴도치에게 다가갑니다.");

        // 접근자 감정 변경 - 호기심
        approacher.ShowEmotion(EmotionType.Happy, 3f);

        // 접근 애니메이션 (한 번만 재생)
        var approacherAnim = approacher.GetComponent<PetAnimationController>();
        yield return StartCoroutine(approacherAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Attack));

        // 고슴도치 놀람 반응
        porcupine.ShowEmotion(EmotionType.Scared, 3f);
    }

    /// <summary>
    /// 2단계: 고슴도치 가시 방어
    /// </summary>
    private IEnumerator QuillDefensePhase(PetController porcupine, PetController approacher)
    {
        Debug.Log($"[{InteractionName}] 2단계: {porcupine.petName}이(가) 가시를 발사합니다!");

        // 고슴도치 감정 변경
        // porcupine.ShowEmotion(EmotionType.Angry, 5f);

        var porcupineAnim = porcupine.GetComponent<PetAnimationController>();

        // 고슴도치가 180도 회전 (등을 접근자에게 향하도록)
        Vector3 directionToApproacher = (approacher.transform.position - porcupine.transform.position).normalized;
        Quaternion originalRotation = porcupine.transform.rotation;
        Quaternion backwardRotation = Quaternion.LookRotation(-directionToApproacher);

        // 부드럽게 뒤돌기
        float elapsedTime = 0f;
        while (elapsedTime < quillTurnDuration)
        {
            float t = elapsedTime / quillTurnDuration;
            porcupine.transform.rotation = Quaternion.Slerp(originalRotation, backwardRotation, t);

            if (porcupine.petModelTransform != null)
            {
                porcupine.petModelTransform.rotation = porcupine.transform.rotation;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 가시 발사 이펙트 생성
        if (quillLaunchEffectPrefab != null)
        {
            Vector3 effectPos = porcupine.transform.position + porcupine.transform.TransformDirection(effectOffset);
            // 고슴도치가 바라보는 반대 방향(뒤쪽)으로 발사
            GameObject effect = Instantiate(quillLaunchEffectPrefab, effectPos,
                Quaternion.LookRotation(-porcupine.transform.forward));
            effect.transform.localScale = Vector3.one * effectScale;
            Destroy(effect, effectDuration);
        }

        // 가시 발사 애니메이션
        yield return StartCoroutine(porcupineAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Attack));

        // 접근자에게 가시 부착
        AttachQuillsToApproacher(approacher);

        // 잠시 대기
        yield return new WaitForSeconds(1.0f);

        // 다시 원래 방향으로 돌아오기
        elapsedTime = 0f;
        while (elapsedTime < quillTurnDuration)
        {
            float t = elapsedTime / quillTurnDuration;
            porcupine.transform.rotation = Quaternion.Slerp(backwardRotation, originalRotation, t);

            if (porcupine.petModelTransform != null)
            {
                porcupine.petModelTransform.rotation = porcupine.transform.rotation;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    // 다리 본 이름 목록 (가시 부착용)
    private static readonly string[] legBoneNames = new string[]
    {
        "Hip_R1", "Hip_L1", "Knee_L1", "Knee_R1", "Foot_L1", "Foot_R1", "Toes_R1", "Toes_L1"
    };

    /// <summary>
    /// 접근자 몸에 가시 부착
    /// </summary>
    private void AttachQuillsToApproacher(PetController approacher)
    {
        Collider col = approacher.GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"[{InteractionName}] {approacher.petName}에 Collider가 없어 가시를 부착할 수 없습니다.");
            return;
        }

        Bounds bounds = col.bounds;

        // 머리 본 찾기 (head_end 우선, 없으면 head)
        Transform headBone = FindDeepChild(approacher.transform, "head_end");
        if (headBone == null)
        {
            headBone = FindDeepChild(approacher.transform, "head");
        }

        // 다리 본들 찾기 (랜덤 3개 선택)
        List<Transform> availableLegBones = new List<Transform>();
        foreach (string boneName in legBoneNames)
        {
            Transform bone = FindDeepChild(approacher.transform, boneName);
            if (bone != null)
            {
                availableLegBones.Add(bone);
            }
        }

        // 랜덤하게 섞기
        for (int i = availableLegBones.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Transform temp = availableLegBones[i];
            availableLegBones[i] = availableLegBones[j];
            availableLegBones[j] = temp;
        }

        Debug.Log($"[{InteractionName}] {approacher.petName}에게 가시 부착 시작 (head: {(headBone != null ? headBone.name : "못찾음")}, 다리본: {availableLegBones.Count}개)");

        // 크로커다일 전용 처리: upper_jaw_02, lower_jaw_02에 각각 crocodileQuillsPerJaw개씩
        bool isCrocodile = approacher.petName.Contains("Crocodile") || approacher.petName.Contains("_CrocodileA");
        if (isCrocodile)
        {
            Transform upperJaw = FindDeepChild(approacher.transform, "upper_jaw_02");
            Transform lowerJaw = FindDeepChild(approacher.transform, "lower_jaw_02");

            Debug.Log($"[{InteractionName}] 크로커다일 감지! upperJaw: {(upperJaw != null ? "찾음" : "못찾음")}, lowerJaw: {(lowerJaw != null ? "찾음" : "못찾음")}");

            // 윗턱에 crocodileQuillsPerJaw개
            if (upperJaw != null)
            {
                for (int i = 0; i < crocodileQuillsPerJaw; i++)
                {
                    AttachQuillToCrocodileJaw(upperJaw, bounds);
                }
            }

            // 아랫턱에 crocodileQuillsPerJaw개
            if (lowerJaw != null)
            {
                for (int i = 0; i < crocodileQuillsPerJaw; i++)
                {
                    AttachQuillToCrocodileJaw(lowerJaw, bounds);
                }
            }

            Debug.Log($"[{InteractionName}] 크로커다일 가시 {myStuckQuills.Count}개 부착 완료!");
            return;
        }

        // 일반 동물: 머리에 5개 부착
        for (int i = 0; i < 5; i++)
        {
            if (headBone != null)
            {
                AttachQuillToHead(headBone, bounds);
            }
        }

        // 모든 다리 본에 각각 2~3개씩 부착
        foreach (Transform legBone in availableLegBones)
        {
            int quillsPerLeg = Random.Range(2, 4); // 2~3개
            for (int i = 0; i < quillsPerLeg; i++)
            {
                AttachQuillToLeg(legBone, bounds);
            }
        }

        Debug.Log($"[{InteractionName}] 가시 {myStuckQuills.Count}개 부착 완료!");
    }

    /// <summary>
    /// 머리 본에 가시 부착 (인스펙터 오프셋 사용)
    /// </summary>
    private void AttachQuillToHead(Transform headBone, Bounds bounds)
    {
        GameObject quill = StuckQuill.CreateQuillObject();

        // 인스펙터 오프셋 + 랜덤 범위를 본의 로컬 방향으로 변환
        Vector3 localOffset = headQuillOffset + new Vector3(
            Random.Range(-headQuillRandomRange.x, headQuillRandomRange.x),
            Random.Range(-headQuillRandomRange.y, headQuillRandomRange.y),
            Random.Range(-headQuillRandomRange.z, headQuillRandomRange.z)
        );
        // 본의 로컬 방향으로 변환하여 월드 좌표로 적용
        Vector3 worldOffset = headBone.TransformDirection(localOffset);
        quill.transform.position = headBone.position + worldOffset;

        // 크기 조절
        quill.transform.localScale = Vector3.one * stuckQuillScale;

        // 부모 설정
        quill.transform.SetParent(headBone);

        // 가시가 몸 안쪽을 향하도록 회전 (뾰족한 끝이 몸에 박힌 모양)
        Vector3 inwardDir = (bounds.center - quill.transform.position).normalized;
        if (inwardDir.sqrMagnitude > 0.001f)
        {
            quill.transform.rotation = Quaternion.LookRotation(inwardDir);
        }

        // 약간의 랜덤 회전 추가
        quill.transform.Rotate(Random.Range(-15f, 15f), Random.Range(-15f, 15f), Random.Range(-15f, 15f));

        // 컴포넌트 추가
        quill.AddComponent<StuckQuill>().Initialize(stuckQuillDuration);

        // 인스턴스별 추적 리스트에 추가
        myStuckQuills.Add(quill);

        Debug.Log($"[{InteractionName}] 머리 가시 부착: offset={localOffset}, worldOffset={worldOffset}");
    }

    /// <summary>
    /// 크로커다일 턱에 가시 부착 (크로커다일 전용 설정 사용)
    /// </summary>
    private void AttachQuillToCrocodileJaw(Transform jawBone, Bounds bounds)
    {
        GameObject quill = StuckQuill.CreateQuillObject();

        // 크로커다일 전용 오프셋 + 랜덤 범위를 본의 로컬 방향으로 변환
        Vector3 localOffset = crocodileQuillOffset + new Vector3(
            Random.Range(-crocodileQuillRandomRange.x, crocodileQuillRandomRange.x),
            Random.Range(-crocodileQuillRandomRange.y, crocodileQuillRandomRange.y),
            Random.Range(-crocodileQuillRandomRange.z, crocodileQuillRandomRange.z)
        );
        // 본의 로컬 방향으로 변환하여 월드 좌표로 적용
        Vector3 worldOffset = jawBone.TransformDirection(localOffset);
        quill.transform.position = jawBone.position + worldOffset;

        // 크로커다일 전용 크기 조절
        quill.transform.localScale = Vector3.one * crocodileQuillScale;

        // 부모 설정
        quill.transform.SetParent(jawBone);

        // 가시가 몸 안쪽을 향하도록 회전 (뾰족한 끝이 몸에 박힌 모양)
        Vector3 inwardDir = (bounds.center - quill.transform.position).normalized;
        if (inwardDir.sqrMagnitude > 0.001f)
        {
            quill.transform.rotation = Quaternion.LookRotation(inwardDir);
        }

        // 약간의 랜덤 회전 추가
        quill.transform.Rotate(Random.Range(-15f, 15f), Random.Range(-15f, 15f), Random.Range(-15f, 15f));

        // 컴포넌트 추가
        quill.AddComponent<StuckQuill>().Initialize(stuckQuillDuration);

        // 인스턴스별 추적 리스트에 추가
        myStuckQuills.Add(quill);

        Debug.Log($"[{InteractionName}] 크로커다일 턱 가시 부착: offset={localOffset}, worldOffset={worldOffset}");
    }

    /// <summary>
    /// 다리 본에 가시 부착
    /// </summary>
    private void AttachQuillToLeg(Transform legBone, Bounds bounds)
    {
        GameObject quill = StuckQuill.CreateQuillObject();

        // 다리 본 바깥쪽에 위치 (끝만 박히게 더 바깥으로)
        Vector3 localOffset = new Vector3(
            Random.Range(-0.05f, 0.05f),
            Random.Range(0.15f, 0.25f),  // 위쪽으로 더 띄움
            Random.Range(0.2f, 0.3f)     // 바깥쪽으로 더 띄움 (끝만 박히게)
        );
        Vector3 worldOffset = legBone.TransformDirection(localOffset);
        quill.transform.position = legBone.position + worldOffset;

        // 크기 조절
        quill.transform.localScale = Vector3.one * stuckQuillScale;

        // 부모 설정
        quill.transform.SetParent(legBone);

        // 가시가 몸 안쪽을 향하도록 회전 (머리 가시와 동일한 방식)
        Vector3 inwardDir = (bounds.center - quill.transform.position).normalized;
        if (inwardDir.sqrMagnitude > 0.001f)
        {
            quill.transform.rotation = Quaternion.LookRotation(inwardDir);
        }

        // 약간의 랜덤 회전 추가
        quill.transform.Rotate(Random.Range(-15f, 15f), Random.Range(-15f, 15f), Random.Range(-15f, 15f));

        // 컴포넌트 추가
        quill.AddComponent<StuckQuill>().Initialize(stuckQuillDuration);

        // 인스턴스별 추적 리스트에 추가
        myStuckQuills.Add(quill);
    }

    /// <summary>
    /// 재귀적으로 자식 Transform 찾기 (대소문자 무시)
    /// </summary>
    private Transform FindDeepChild(Transform parent, string name)
    {
        // 대소문자 무시 비교
        foreach (Transform child in parent)
        {
            if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>
    /// 3단계: 접근자 후퇴
    /// </summary>
    private IEnumerator ApproacherRetreatPhase(PetController approacher, PetController porcupine)
    {
        Debug.Log($"[{InteractionName}] 3단계: {approacher.petName}이(가) 아파서 후퇴합니다!");

        // 접근자 감정 변경 - 가시에 찔려서 아픔
        approacher.ShowEmotion(EmotionType.Sad, 8f);

        // 후퇴 위치 계산
        Vector3 retreatDirection = (approacher.transform.position - porcupine.transform.position).normalized;
        Vector3 retreatTarget = approacher.transform.position + retreatDirection * approacherRetreatDistance;
        retreatTarget = FindValidPositionOnNavMesh(retreatTarget, approacherRetreatDistance * 1.5f);

        // 아파하며 비틀거리며 후퇴
        approacher.agent.isStopped = false;
        approacher.agent.speed = approacher.baseSpeed * approacherRetreatSpeedMultiplier;
        approacher.agent.SetDestination(retreatTarget);
        approacher.GetComponent<PetAnimationController>().SetContinuousAnimation(
            PetAnimationController.PetAnimationType.Run);

        // 후퇴하면서 비틀거리는 효과
        float timer = 0f;
        float zigzagTimer = 0f;

        while (timer < 4f)
        {
            if (!approacher.agent.pathPending && approacher.agent.remainingDistance < 0.5f)
            {
                Debug.Log($"[{InteractionName}] {approacher.petName}이(가) 후퇴 완료!");
                break;
            }

            // 지그재그로 비틀거리는 효과 (아픔 표현)
            zigzagTimer += Time.deltaTime * 3f;
            float zigzagOffset = Mathf.Sin(zigzagTimer) * 0.5f;
            Vector3 sideDirection = Vector3.Cross(retreatDirection, Vector3.up);
            Vector3 zigzagTarget = retreatTarget + sideDirection * zigzagOffset * 3f;

            // NavMesh 상의 유효한 위치 찾기
            if (NavMesh.SamplePosition(zigzagTarget, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                approacher.agent.SetDestination(hit.position);
            }

            approacher.HandleRotation();
            timer += Time.deltaTime;
            yield return null;
        }

        // 정지
        approacher.agent.isStopped = true;
        approacher.GetComponent<PetAnimationController>().StopContinuousAnimation();

        // 아파하는 애니메이션
        yield return StartCoroutine(approacher.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Rest, 2f, false, false));
    }

    /// <summary>
    /// 4단계: 고슴도치 도망
    /// </summary>
    private IEnumerator PorcupineEscapePhase(PetController porcupine, PetController approacher)
    {
        Debug.Log($"[{InteractionName}] 4단계: {porcupine.petName}이(가) 안전한 곳으로 이동합니다.");

        // 고슴도치 감정 변경 - 안심
        porcupine.ShowEmotion(EmotionType.Happy, 10f);

        // 도망 방향 설정 (접근자 반대 방향)
        Vector3 escapeDirection = (porcupine.transform.position - approacher.transform.position).normalized;
        // 약간의 랜덤성 추가
        escapeDirection += new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f));
        escapeDirection.Normalize();

        Vector3 escapeTarget = porcupine.transform.position + escapeDirection * porcupineEscapeDistance;
        escapeTarget = FindValidPositionOnNavMesh(escapeTarget, porcupineEscapeDistance * 1.5f);

        // 이동 시작
        porcupine.agent.isStopped = false;
        porcupine.agent.speed = porcupine.baseSpeed * porcupineEscapeSpeedMultiplier;
        porcupine.agent.SetDestination(escapeTarget);
        porcupine.GetComponent<PetAnimationController>().SetContinuousAnimation(
            PetAnimationController.PetAnimationType.Walk);

        // 도망 완료 대기
        float timer = 0f;
        while (timer < escapeTimeout)
        {
            if (!porcupine.agent.pathPending && porcupine.agent.remainingDistance < 1f)
            {
                Debug.Log($"[{InteractionName}] {porcupine.petName}이(가) 안전하게 도망쳤습니다.");
                break;
            }

            porcupine.HandleRotation();
            timer += Time.deltaTime;
            yield return null;
        }

        // 정지
        porcupine.agent.isStopped = true;
        porcupine.GetComponent<PetAnimationController>().StopContinuousAnimation();
    }

    /// <summary>
    /// 5단계: 승리 표현
    /// </summary>
    private IEnumerator VictoryCelebrationPhase(PetController porcupine, PetController approacher)
    {
        Debug.Log($"[{InteractionName}] 5단계: 승리의 기쁨!");

        // 접근자가 Rest 애니메이션 중이면 Idle로 전환
        var approacherAnim = approacher.GetComponent<PetAnimationController>();
        if (approacherAnim != null)
        {
            approacherAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
            yield return new WaitForSeconds(0.5f);
        }

        // 접근자를 향해 돌아보기
        yield return StartCoroutine(SmoothlyLookAtEachOther(porcupine, approacher, 0.5f));

        // 감정 표현
        porcupine.ShowEmotion(EmotionType.Victory, 5f);
        approacher.ShowEmotion(EmotionType.Defeat, 5f);

        // 고슴도치 승리 점프
        var porcupineAnim = porcupine.GetComponent<PetAnimationController>();
        yield return StartCoroutine(porcupineAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Jump));
        yield return StartCoroutine(approacherAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Eat));

        yield return new WaitForSeconds(1f);
    }

    /// <summary>
    /// NavMeshAgent가 안전하게 준비되었는지 확인
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }

    /// <summary>
    /// 컴포넌트 파괴 시 정리
    /// </summary>
    private void OnDestroy()
    {
        // 남아있는 가시들 정리
        foreach (var quill in myStuckQuills)
        {
            if (quill != null)
            {
                Destroy(quill);
            }
        }
        myStuckQuills.Clear();
    }
}
