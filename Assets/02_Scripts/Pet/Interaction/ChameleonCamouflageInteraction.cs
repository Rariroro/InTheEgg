// ChameleonCamouflageInteraction.cs (최적화된 버전)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;
public class ChameleonCamouflageInteraction : BasePetInteraction
{
    public override string InteractionName => "ChameleonCamouflage";

    // 우선순위: 80 (5순위)
    public override int Priority => 80;

    [Header("Material Settings")]
    [Tooltip("카멜레온이 투명해질 때 사용할 투명 머티리얼입니다.")]
    [SerializeField] private Material transparentMaterial;

    [Header("거리 설정")]
    [Tooltip("상호작용 시 유지할 거리")]
    public float interactionDistance = 8f;
    
    [Tooltip("카멜레온이 위장(투명화)을 유지하는 시간입니다.")]
    public float camouflageDuration = 5f;
    
    [Tooltip("포식자가 혼란스러워하며 주변을 맴도는 시간입니다.")]
    public float predatorConfusionDuration = 4f;
    
    [Tooltip("포식자가 포기하고 떠나기 시작할 때까지의 대기 시간입니다.")]
    public float predatorGiveUpDelay = 2f;

    [Header("Predator Behavior")]
    [Tooltip("포식자가 카멜레온에게 접근할 때의 속도 배율입니다.")]
    public float predatorApproachSpeedMultiplier = 1.5f;
    
    [Tooltip("포식자가 포기하고 떠날 때 이동할 거리입니다.")]
    public float predatorLeaveDistance = 20f;
    
    [Tooltip("포식자가 충분히 멀어졌다고 판단하는 거리입니다.")]
    public float safeDistanceForChameleon = 15f;
    
    [Tooltip("포식자가 혼란 중 돌아다닐 반경입니다.")]
    public float confusionSearchRadius = 5f;

    [Header("Visual Effects")]
    [Tooltip("카멜레온이 투명해지는 속도입니다.")]
    public float fadeOutDuration = 1.5f;
    
    [Tooltip("카멜레온이 다시 나타나는 속도입니다.")]
    public float fadeInDuration = 1.0f;

    [Header("Safety Settings")]
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;

    // 기본 위치 이동(중간 지점으로 모이기)을 하지 않고, 공격자가 직접 접근하도록 설정
    public override bool ShouldPerformInitialMovement => false;

    // 강제 종료 시 머티리얼 복원을 위한 클래스 레벨 변수
    private Dictionary<Renderer, Material[]> currentOriginalMaterials = null;
    private PetController currentChameleon = null;

    /// <summary>
    /// 템플릿에서 이 인스턴스로 설정값을 복사합니다 (동시 실행 격리)
    /// </summary>
    public void CopySettingsFrom(ChameleonCamouflageInteraction template)
    {
        if (template == null) return;

        // Material Settings
        this.transparentMaterial = template.transparentMaterial;

        // Interaction Settings
        this.interactionDistance = template.interactionDistance;
        this.camouflageDuration = template.camouflageDuration;
        this.predatorConfusionDuration = template.predatorConfusionDuration;
        this.predatorGiveUpDelay = template.predatorGiveUpDelay;

        // Predator Behavior
        this.predatorApproachSpeedMultiplier = template.predatorApproachSpeedMultiplier;
        this.predatorLeaveDistance = template.predatorLeaveDistance;
        this.safeDistanceForChameleon = template.safeDistanceForChameleon;
        this.confusionSearchRadius = template.confusionSearchRadius;

        // Visual Effects
        this.fadeOutDuration = template.fadeOutDuration;
        this.fadeInDuration = template.fadeInDuration;

        // Safety Settings
        this.agentSafetyTimeout = template.agentSafetyTimeout;
    }

    // 상호작용 타입 결정
    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.ChaseAndRun;
    }

    // 상호작용 가능 여부 체크
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        bool hasChameleon = pet1.PetType == PetType.Chameleon || pet2.PetType == PetType.Chameleon;
        if (!hasChameleon) return false;

        PetController otherPet = (pet1.PetType == PetType.Chameleon) ? pet2 : pet1;

        // 상대방이 고기를 먹는 육식동물인지 확인 (Fish만 먹는 동물은 제외)
        bool isMeatEater = (otherPet.diet & PetAIProperties.DietaryFlags.Meat) != 0;

        return isMeatEater;
    }

    // 메인 상호작용 수행
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[ChameleonCamouflage] {pet1.petName}와(과) {pet2.petName}의 위장 상호작용 시작!");

        // 필수 머티리얼 체크
        if (transparentMaterial == null)
        {
            Debug.LogError("[ChameleonCamouflage] 투명 머티리얼이 할당되지 않았습니다! 상호작용을 취소합니다.");
            yield break;
        }

        // 역할 식별
        PetController chameleon = (pet1.PetType == PetType.Chameleon) ? pet1 : pet2;
        PetController predator = (pet1.PetType == PetType.Chameleon) ? pet2 : pet1;

        // NavMeshAgent 준비 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(chameleon, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(predator, agentSafetyTimeout));

        if (!IsAgentSafelyReady(chameleon) || !IsAgentSafelyReady(predator))
        {
            Debug.LogError("[ChameleonCamouflage] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(chameleon, predator);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState chameleonState = new PetOriginalState(chameleon);
        PetOriginalState predatorState = new PetOriginalState(predator);

        // 머티리얼 백업
        Dictionary<Renderer, Material[]> originalMaterials = null;

        try
        {
            // 0. 위치 및 방향 준비 (거리 조정 + 서로 마주보기)
            yield return StartCoroutine(PreparePositionAndFacing(predator, chameleon));

            // 감정 표현 시작
            chameleon.ShowEmotion(EmotionType.Scared, camouflageDuration + predatorConfusionDuration + 10f);
            predator.ShowEmotion(EmotionType.Hungry, predatorConfusionDuration + 5f);

            // 짧은 대기 (감정 표현이 보이도록)
            yield return new WaitForSeconds(0.5f);

            // 1. 포식자 공격 시도 단계
            yield return StartCoroutine(PredatorAttackPhase(predator, chameleon));

            // 2. 카멜레온 위장 준비
            originalMaterials = BackupChameleonMaterials(chameleon);
            // 클래스 레벨 변수에도 저장 (강제 종료 대비)
            currentOriginalMaterials = originalMaterials;
            currentChameleon = chameleon;
            
            // 3. 위장 및 포식자 혼란 단계
            yield return StartCoroutine(ImprovedHideAndConfusePhase(chameleon, predator, originalMaterials));

            // 4. 포식자 떠나기 단계
            yield return StartCoroutine(ImprovedLeavePhase(predator, chameleon));

            // 5. 카멜레온 안심 및 재등장 단계
            yield return StartCoroutine(ImprovedReappearPhase(chameleon, originalMaterials));

            Debug.Log($"[ChameleonCamouflage] {chameleon.petName}이(가) 위기를 모면했습니다!");
            
            // 성공 감정 표현
            chameleon.ShowEmotion(EmotionType.Happy, 5f);
            yield return new WaitForSeconds(2f);
        }
        finally
        {
            // 최종 정리
            Debug.Log("[ChameleonCamouflage] 상호작용 정리 시작.");

            // 머티리얼 복원 (안전장치)
            if (originalMaterials != null)
            {
                RestoreChameleonMaterials(chameleon, originalMaterials);
            }

            // 클래스 레벨 변수 초기화
            currentOriginalMaterials = null;
            currentChameleon = null;

            // 원래 상태 복원
            chameleonState.Restore(chameleon);
            predatorState.Restore(predator);

            // 공통 종료 처리
            EndInteraction(chameleon, predator);
            Debug.Log("[ChameleonCamouflage] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 0단계: 초기 위치 및 방향 준비 (거리 조정 + 서로 마주보기)
    /// </summary>
    private IEnumerator PreparePositionAndFacing(PetController predator, PetController chameleon)
    {
        Debug.Log($"[ChameleonCamouflage] 0단계: 위치 및 방향 준비 시작");

        // 1. 거리 계산
        float predatorMultiplier = predator.Profile.GetInteractionDistanceMultiplier();
        float chameleonMultiplier = chameleon.Profile.GetInteractionDistanceMultiplier();
        float averageMultiplier = (predatorMultiplier + chameleonMultiplier) / 2f;
        float targetDistance = interactionDistance * averageMultiplier;
        float currentDistance = Vector3.Distance(predator.transform.position, chameleon.transform.position);

        Debug.Log($"[ChameleonCamouflage] 현재 거리: {currentDistance:F2}m, 목표 거리: {targetDistance:F2}m");

        // 2. 카멜레온이 먼저 포식자를 바라보도록 부드럽게 회전
        Vector3 directionToPredator = (predator.transform.position - chameleon.transform.position).normalized;
        directionToPredator.y = 0;
        if (directionToPredator != Vector3.zero)
        {
            Quaternion startRotation = chameleon.transform.rotation;
            Quaternion targetRotation = Quaternion.LookRotation(directionToPredator);

            // 부드럽게 회전
            float turnDuration = 0.5f;
            float elapsedTime = 0f;
            while (elapsedTime < turnDuration)
            {
                float t = elapsedTime / turnDuration;
                chameleon.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                if (chameleon.petModelTransform != null)
                {
                    chameleon.petModelTransform.rotation = chameleon.transform.rotation;
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // 최종 회전 확정
            chameleon.transform.rotation = targetRotation;
            if (chameleon.petModelTransform != null)
            {
                chameleon.petModelTransform.rotation = chameleon.transform.rotation;
            }
        }

        // 카멜레온은 제자리에서 경계
        chameleon.agent.isStopped = true;
        chameleon.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);

        // 3. 거리 조정 필요 여부 확인
        bool needsMovement = currentDistance < targetDistance * 0.8f || currentDistance > targetDistance * 1.5f;

        if (needsMovement)
        {
            Vector3 targetPosition;

            if (currentDistance < targetDistance * 0.8f)
            {
                // 너무 가까움 → 포식자를 뒤로 이동
                Debug.Log($"[ChameleonCamouflage] 너무 가까움 → 포식자를 뒤로 {targetDistance:F2}m 거리로 이동");
                Vector3 directionAway = (predator.transform.position - chameleon.transform.position).normalized;
                if (directionAway == Vector3.zero)
                {
                    directionAway = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                }
                targetPosition = chameleon.transform.position + directionAway * targetDistance;
            }
            else
            {
                // 너무 멀음 → 포식자를 앞으로 이동
                Debug.Log($"[ChameleonCamouflage] 너무 멀음 → 포식자를 앞으로 {targetDistance:F2}m 거리로 이동");
                targetPosition = chameleon.transform.position + chameleon.transform.forward * targetDistance;
            }

            // NavMesh 상 유효한 위치 찾기
            targetPosition = FindValidPositionOnNavMesh(targetPosition, targetDistance * 1.5f);

            // 포식자 이동 시작
            predator.agent.isStopped = false;
            predator.agent.speed = predator.baseSpeed * predatorApproachSpeedMultiplier;
            predator.agent.SetDestination(targetPosition);
            predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

            // 이동 완료 대기
            float timer = 0f;
            float approachTimeout = 10f;
            while (timer < approachTimeout)
            {
                // 카멜레온이 포식자를 부드럽게 계속 바라봄
                directionToPredator = (predator.transform.position - chameleon.transform.position).normalized;
                directionToPredator.y = 0;
                if (directionToPredator.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(directionToPredator);
                    chameleon.transform.rotation = Quaternion.Slerp(chameleon.transform.rotation, targetRot, Time.deltaTime * 5f);
                    if (chameleon.petModelTransform != null)
                    {
                        chameleon.petModelTransform.rotation = chameleon.transform.rotation;
                    }
                }

                // 도착 체크
                if (!predator.agent.pathPending && predator.agent.remainingDistance < 0.5f)
                {
                    Debug.Log($"[ChameleonCamouflage] 위치 조정 완료");
                    break;
                }

                predator.HandleRotation();
                timer += Time.deltaTime;
                yield return null;
            }

            // 포식자 정지
            predator.agent.isStopped = true;
            predator.GetComponent<PetAnimationController>().StopContinuousAnimation();
            yield return new WaitForSeconds(0.2f);
        }
        else
        {
            Debug.Log($"[ChameleonCamouflage] 거리 적절함 ({currentDistance:F2}m) → 이동 생략");
            predator.agent.isStopped = true;
        }

        // 4. 서로 정확히 마주보기
        Debug.Log($"[ChameleonCamouflage] 서로 마주보기 시작");
        yield return StartCoroutine(SmoothlyLookAtEachOther(predator, chameleon, 1f));

        Debug.Log($"[ChameleonCamouflage] 위치 및 방향 준비 완료");
    }

    /// <summary>
    /// 1단계: 포식자 공격 시도
    /// </summary>
    private IEnumerator PredatorAttackPhase(PetController predator, PetController chameleon)
    {
        Debug.Log($"[ChameleonCamouflage] 1단계: {predator.petName}이(가) 공격을 시도합니다.");

        // 포식자 감정 변경
        predator.ShowEmotion(EmotionType.Hungry, 3f);

        // 공격 애니메이션
        var predatorAnim = predator.GetComponent<PetAnimationController>();
        yield return StartCoroutine(predatorAnim.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Attack));

        // 카멜레온 놀람 반응
        chameleon.ShowEmotion(EmotionType.Scared, 3f);
    }

    // 개선된 위장 및 혼란 단계
    private IEnumerator ImprovedHideAndConfusePhase(PetController chameleon, PetController predator, Dictionary<Renderer, Material[]> originalMaterials)
    {
        Debug.Log($"[ChameleonCamouflage] 2단계: {chameleon.petName}이(가) 위장하고 {predator.petName}은(는) 혼란에 빠집니다.");

        // 포식자 정지
        predator.agent.isStopped = true;
        predator.GetComponent<PetAnimationController>().StopContinuousAnimation();

        // 카멜레온 위장 애니메이션과 투명화 시작
        var chameleonAnim = chameleon.GetComponent<PetAnimationController>();
        StartCoroutine(SmoothCamouflageEffect(chameleon, originalMaterials, true));
        yield return StartCoroutine(chameleonAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Eat, 1.5f, false, false));

        // 포식자 혼란 표현
        predator.ShowEmotion(EmotionType.Confused, predatorConfusionDuration);
        
        // 포식자가 주변을 수색하는 동작
        yield return StartCoroutine(PredatorSearchBehavior(predator, chameleon.transform.position));
    }

    // 포식자의 수색 행동
    // 포식자의 수색 행동
private IEnumerator PredatorSearchBehavior(PetController predator, Vector3 lastSeenPosition)
{
    Debug.Log($"[ChameleonCamouflage] {predator.petName}이(가) 주변을 수색합니다.");
    
    var predatorAnim = predator.GetComponent<PetAnimationController>();
    
    // 먼저 놀라는 동작
    yield return StartCoroutine(predatorAnim.PlayAnimationWithCustomDuration(
        PetAnimationController.PetAnimationType.Jump, 1f, true, false));

    // 주변을 돌아다니며 찾기
    predator.agent.isStopped = false;
    predator.agent.speed = predator.baseSpeed * 0.7f;
    predatorAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

    float searchTime = 0f;
    int searchPoints = 3;

    for (int i = 0; i < searchPoints; i++)
    {
        // 랜덤한 수색 지점 생성
        Vector2 randomCircle = Random.insideUnitCircle * confusionSearchRadius;
        Vector3 searchTarget = lastSeenPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

        if (NavMesh.SamplePosition(searchTarget, out NavMeshHit hit, confusionSearchRadius * 1.5f, NavMesh.AllAreas))
        {
            predator.agent.SetDestination(hit.position);

            // 목적지 도착까지 대기
            while (!predator.agent.pathPending && predator.agent.remainingDistance > 0.5f)
            {
                predator.HandleRotation();
                yield return null;
            }

            // ▼▼▼ [수정된 부분] 도착 후에도 계속 걸으면서 주변을 둘러봄 ▼▼▼
            // 도착 지점에서 작은 원을 그리며 수색
            float circleSearchDuration = 1.5f;
            float circleTimer = 0f;
            float circleRadius = 2f;
            Vector3 centerPoint = predator.transform.position;

            while (circleTimer < circleSearchDuration)
            {
                // 원형 경로 계산
                float angle = (circleTimer / circleSearchDuration) * 360f * Mathf.Deg2Rad;
                Vector3 circlePoint = centerPoint + new Vector3(
                    Mathf.Sin(angle) * circleRadius,
                    0,
                    Mathf.Cos(angle) * circleRadius
                );

                // NavMesh 상의 유효한 위치 찾기
                if (NavMesh.SamplePosition(circlePoint, out NavMeshHit circleHit, circleRadius, NavMesh.AllAreas))
                {
                    predator.agent.SetDestination(circleHit.position);
                }

                circleTimer += Time.deltaTime;
                yield return null;
            }
            // ▲▲▲ [여기까지 수정] ▲▲▲
        }
    }

    // 마지막에 제자리에서 한 번 더 둘러보기
    predator.agent.isStopped = true;
    predatorAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
    
    // 360도 회전하며 마지막 확인
    float finalLookDuration = 2f;
    float finalLookTimer = 0f;
    Quaternion startRotation = predator.transform.rotation;

    while (finalLookTimer < finalLookDuration)
    {
        float rotationProgress = finalLookTimer / finalLookDuration;
        predator.transform.rotation = startRotation * Quaternion.Euler(0, rotationProgress * 360f, 0);
        
        finalLookTimer += Time.deltaTime;
        yield return null;
    }

    predatorAnim.StopContinuousAnimation();
}

    // 개선된 떠나기 단계
    private IEnumerator ImprovedLeavePhase(PetController predator, PetController chameleon)
    {
        Debug.Log($"[ChameleonCamouflage] 3단계: {predator.petName}이(가) 포기하고 떠납니다.");
        
        yield return new WaitForSeconds(predatorGiveUpDelay);

        // 포기하는 감정 표현
        predator.ShowEmotion(EmotionType.Sad, 3f);

        // 떠날 방향 계산 (현재 방향의 반대)
        Vector3 leaveDirection = -predator.transform.forward;
        Vector3 leaveTarget = predator.transform.position + leaveDirection * predatorLeaveDistance;
        
        if (NavMesh.SamplePosition(leaveTarget, out NavMeshHit hit, predatorLeaveDistance * 1.5f, NavMesh.AllAreas))
        {
            leaveTarget = hit.position;
        }

        // 천천히 떠나기
        predator.agent.isStopped = false;
        predator.agent.speed = predator.baseSpeed * 0.8f;
        predator.agent.SetDestination(leaveTarget);
        predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        // 안전 거리까지 멀어지기를 기다림
        float maxWaitTime = 20f;
        float waitTimer = 0f;

        while (Vector3.Distance(predator.transform.position, chameleon.transform.position) < safeDistanceForChameleon)
        {
            predator.HandleRotation();
            
            // 목적지 도착 시 새로운 목적지 설정
            if (!predator.agent.pathPending && predator.agent.remainingDistance < 1f)
            {
                break;
            }

            waitTimer += Time.deltaTime;
            if (waitTimer > maxWaitTime)
            {
                Debug.LogWarning("[ChameleonCamouflage] 포식자가 충분히 멀어지지 않았지만 시간 초과.");
                break;
            }

            yield return null;
        }

        predator.GetComponent<PetAnimationController>().StopContinuousAnimation();
    }

    // 개선된 재등장 단계
    private IEnumerator ImprovedReappearPhase(PetController chameleon, Dictionary<Renderer, Material[]> originalMaterials)
    {
        Debug.Log($"[ChameleonCamouflage] 4단계: {chameleon.petName}이(가) 안전함을 느끼고 재등장합니다.");
        
        // 주변을 살피는 동작
        var chameleonAnim = chameleon.GetComponent<PetAnimationController>();
        chameleonAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
        
        // 좌우를 살피기
        float lookDuration = 2f;
        float lookTimer = 0f;
        Quaternion originalRotation = chameleon.transform.rotation;

        while (lookTimer < lookDuration)
        {
            float angle = Mathf.Sin(lookTimer * 2f) * 45f;
            chameleon.transform.rotation = originalRotation * Quaternion.Euler(0, angle, 0);
            
            lookTimer += Time.deltaTime;
            yield return null;
        }

        chameleon.transform.rotation = originalRotation;

        // 투명화 해제
        yield return StartCoroutine(SmoothCamouflageEffect(chameleon, originalMaterials, false));

        // 안심하는 동작
        chameleon.ShowEmotion(EmotionType.Happy, 5f);
        yield return StartCoroutine(chameleonAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 2.0f, true, false));
    }

    // 부드러운 위장 효과
    private IEnumerator SmoothCamouflageEffect(PetController chameleon, Dictionary<Renderer, Material[]> originalMaterials, bool fadeOut)
    {
        if (chameleon.petModelTransform == null || transparentMaterial == null) yield break;

        Renderer[] renderers = chameleon.petModelTransform.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) yield break;

        float duration = fadeOut ? fadeOutDuration : fadeInDuration;
        float elapsedTime = 0f;

        if (fadeOut)
        {
            // 투명화 시작
            Debug.Log($"[ChameleonCamouflage] {chameleon.petName}이(가) 서서히 투명해집니다.");

            // 페이드 아웃 효과 (선택사항 - 바로 투명 머티리얼로 교체해도 됨)
            foreach (var renderer in renderers)
            {
                // 감정 파티클 제외 체크
                if (IsEmotionParticle(renderer.transform))
                {
                    Debug.Log($"[ChameleonCamouflage] 감정 파티클 제외: {renderer.name}");
                    continue;
                }

                Material[] transparentMaterials = new Material[renderer.materials.Length];
                for (int i = 0; i < transparentMaterials.Length; i++)
                {
                    transparentMaterials[i] = transparentMaterial;
                }
                renderer.materials = transparentMaterials;
            }

            yield return new WaitForSeconds(camouflageDuration);
        }
        else
        {
            // 원래 모습으로 복원
            Debug.Log($"[ChameleonCamouflage] {chameleon.petName}이(가) 서서히 나타납니다.");
            RestoreChameleonMaterials(chameleon, originalMaterials);
        }
    }

    // 감정 파티클 체크
    private bool IsEmotionParticle(Transform obj)
    {
        // ParticleSystem 컴포넌트를 가지고 있으면 감정 파티클로 판단
        // (카멜레온 모델 자체에는 ParticleSystem이 없고, 감정 표시만 ParticleSystem 사용)
        if (obj.GetComponent<ParticleSystem>() != null)
        {
            return true;
        }

        // 추가로 이름으로도 체크 (감정 파티클은 보통 특정 이름 패턴을 가짐)
        string objName = obj.name.ToLower();
        if (objName.Contains("emotion") || objName.Contains("particle") || objName.Contains("effect"))
        {
            return true;
        }

        return false;
    }

    // 머티리얼 백업
    private Dictionary<Renderer, Material[]> BackupChameleonMaterials(PetController chameleon)
    {
        Dictionary<Renderer, Material[]> backup = new Dictionary<Renderer, Material[]>();

        if (chameleon.petModelTransform != null)
        {
            Renderer[] renderers = chameleon.petModelTransform.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                // 감정 파티클은 백업에서도 제외
                if (IsEmotionParticle(renderer.transform))
                {
                    continue;
                }
                backup[renderer] = renderer.materials;
            }
        }

        return backup;
    }

    // 머티리얼 복원
    private void RestoreChameleonMaterials(PetController chameleon, Dictionary<Renderer, Material[]> originalMaterials)
    {
        if (chameleon == null || !chameleon.gameObject.activeInHierarchy) return;

        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }
    }

    // NavMeshAgent 안전 체크
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }

    // 컴포넌트 비활성화 시 머티리얼 복원
    private void OnDisable()
    {
        CleanupCamouflage();
    }

    // 컴포넌트 파괴 시 머티리얼 복원
    private void OnDestroy()
    {
        CleanupCamouflage();
    }

    // 강제 종료 시 카멜레온 머티리얼 정리
    private void CleanupCamouflage()
    {
        if (currentOriginalMaterials != null && currentChameleon != null)
        {
            Debug.Log($"[ChameleonCamouflage] 강제 종료 감지 - {currentChameleon.petName}의 머티리얼을 원래대로 복원합니다.");
            RestoreChameleonMaterials(currentChameleon, currentOriginalMaterials);
            currentOriginalMaterials = null;
            currentChameleon = null;
        }
    }

    // 외부에서 강제로 정리를 요청할 때 사용하는 public 메서드
    public void ForceCleanup()
    {
        Debug.Log($"[ChameleonCamouflage] ForceCleanup 호출됨 - 상호작용 강제 종료 처리");
        CleanupCamouflage();
    }
}