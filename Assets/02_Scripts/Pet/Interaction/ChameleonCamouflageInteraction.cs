// ChameleonCamouflageInteraction.cs (최적화된 버전)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ChameleonCamouflageInteraction : BasePetInteraction
{
    public override string InteractionName => "ChameleonCamouflage";

    [Header("Material Settings")]
    [Tooltip("카멜레온이 투명해질 때 사용할 투명 머티리얼입니다.")]
    [SerializeField] private Material transparentMaterial;

    [Header("Interaction Settings")]
    [Tooltip("포식자가 위협을 감지하고 위장을 시작할 거리입니다.")]
    public float camouflageTriggerDistance = 8f;
    
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

        // 상대방이 육식 또는 잡식성인지 확인
        bool isPredator = (otherPet.diet & (PetAIProperties.DietaryFlags.Meat | PetAIProperties.DietaryFlags.Fish)) != 0;

        return isPredator;
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
            // 감정 표현 시작
            chameleon.ShowEmotion(EmotionType.Scared, camouflageDuration + predatorConfusionDuration + 10f);
            predator.ShowEmotion(EmotionType.Hungry, predatorConfusionDuration + 5f);

            // 1. 포식자 접근 단계 (개선된 추적)
            yield return StartCoroutine(ImprovedApproachPhase(predator, chameleon));

            // 2. 카멜레온 위장 준비
            originalMaterials = BackupChameleonMaterials(chameleon);
            
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

            // 원래 상태 복원
            chameleonState.Restore(chameleon);
            predatorState.Restore(predator);

            // 공통 종료 처리
            EndInteraction(chameleon, predator);
            Debug.Log("[ChameleonCamouflage] 상호작용 정리 완료.");
        }
    }

    // 개선된 접근 단계 - 더 자연스러운 추적
    private IEnumerator ImprovedApproachPhase(PetController predator, PetController chameleon)
    {
        Debug.Log($"[ChameleonCamouflage] 1단계: {predator.petName}이(가) {chameleon.petName}을(를) 추적합니다.");
         // ======================= [수정 코드 시작] =======================
        // 상호작용 시작 전 멈춰있던 NavMeshAgent의 이동을 다시 재개시킵니다.
        if (predator.agent != null && predator.agent.enabled)
        {
            predator.agent.isStopped = false;
        }
        if (chameleon.agent != null && chameleon.agent.enabled)
        {
            chameleon.agent.isStopped = false;
        }
        // ======================= [수정 코드 종료] =======================
        // 포식자 속도 설정
        predator.agent.speed = predator.baseSpeed * predatorApproachSpeedMultiplier;
        predator.agent.acceleration = predator.baseAcceleration * 2f;
        predator.agent.updateRotation = true;
        
        // 카멜레온 초기 반응 - 약간 뒤로 물러남
        chameleon.agent.speed = chameleon.baseSpeed * 0.5f;
        chameleon.agent.updateRotation = true;
        
        // 애니메이션 설정
        predator.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        chameleon.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);

        float chaseTimer = 0f;
        float maxChaseTime = 10f;
        Vector3 lastChameleonPosition = chameleon.transform.position;

        while (Vector3.Distance(predator.transform.position, chameleon.transform.position) > camouflageTriggerDistance)
        {
            // 카멜레온이 도망가는 동작
            Vector3 escapeDirection = (chameleon.transform.position - predator.transform.position).normalized;
            Vector3 escapeTarget = chameleon.transform.position + escapeDirection * 3f;
            
            if (NavMesh.SamplePosition(escapeTarget, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                chameleon.agent.SetDestination(hit.position);
            }

            // 포식자가 계속 추적
            predator.agent.SetDestination(chameleon.transform.position);

            // 시간 초과 체크
            chaseTimer += Time.deltaTime;
            if (chaseTimer > maxChaseTime)
            {
                Debug.LogWarning("[ChameleonCamouflage] 추적 시간 초과!");
                break;
            }

            // 회전 처리
            predator.HandleRotation();
            chameleon.HandleRotation();

            yield return null;
        }

        // 카멜레온 정지
        chameleon.agent.isStopped = true;
        chameleon.GetComponent<PetAnimationController>().SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
        
        Debug.Log($"[ChameleonCamouflage] {chameleon.petName}이(가) 위협을 감지했습니다!");
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

                // 도착 후 주변을 둘러봄
                predator.agent.isStopped = true;
                predatorAnim.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
                
                // 좌우로 회전하며 찾기
                float lookDuration = 1.5f;
                float lookTimer = 0f;
                Quaternion startRotation = predator.transform.rotation;

                while (lookTimer < lookDuration)
                {
                    float angle = Mathf.Sin(lookTimer * 3f) * 60f;
                    predator.transform.rotation = startRotation * Quaternion.Euler(0, angle, 0);
                    
                    lookTimer += Time.deltaTime;
                    yield return null;
                }

                predator.agent.isStopped = false;
            }
        }

        predator.agent.isStopped = true;
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

    // 머티리얼 백업
    private Dictionary<Renderer, Material[]> BackupChameleonMaterials(PetController chameleon)
    {
        Dictionary<Renderer, Material[]> backup = new Dictionary<Renderer, Material[]>();
        
        if (chameleon.petModelTransform != null)
        {
            Renderer[] renderers = chameleon.petModelTransform.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
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
}