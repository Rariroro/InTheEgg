// 박치기 상호작용 구현 - 염소-양, 황소-버팔로, 코뿔소-들소 조합 지원
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using InTheEgg.Extensions;
using InTheEgg.Constants;

/// <summary>
/// 박치기 상호작용을 처리하는 클래스입니다.
/// 염소-양, 황소-버팔로, 코뿔소-들소 조합을 지원합니다.
/// </summary>
public class HeadbuttInteraction : BasePetInteraction
{
    public override string InteractionName => "Headbutt";

    // 우선순위: 60 (9순위)
    public override int Priority => 60;

    #region Constants

    // NavMesh 관련 상수
    private const float NAVMESH_SEARCH_RADIUS = 10f;
    private const float NAVMESH_SEARCH_RADIUS_SMALL = 5f;
    private const float NAVMESH_SAMPLE_DISTANCE = 1f;

    // 타이밍 관련 상수
    private const float TENSION_WAIT_TIME = 1.0f;
    private const float NEXT_HEADBUTT_DELAY = 0.7f;
    private const float CHARGE_PREPARATION_DELAY = 0.5f;
    private const float SMOOTH_ROTATION_DURATION = 1.0f;  // 0.5초 → 1.0초 (더 부드러운 회전)
    private const float QUICK_ROTATION_DURATION = 0.5f;   // 0.3초 → 0.5초
    private const float WINNER_CELEBRATION_DELAY = 2.0f;
    private const float POSITION_MOVE_TIMEOUT = 10f;      // 위치 이동 최대 대기 시간

    // 애니메이션 관련 상수
    private const float HEAD_HEIGHT_RATIO = 0.8f;
    private const float ANIMATION_BUFFER_TIME = 0.5f;

    #endregion

    // BasePetInteraction의 자동 이동을 비활성화하고 PreparePhase에서 직접 거리 조정
    public override bool ShouldPerformInitialMovement => false;

    [Header("Headbutt Settings")]
    [Tooltip("박치기를 위해 떨어지는 기본 거리입니다.")]
    public float baseHeadbuttDistance = 8f;
    
    [Tooltip("큰 동물들(황소-버팔로, 코뿔소-들소)의 박치기 거리입니다.")]
    public float largeAnimalHeadbuttDistance = 12f;
    
    [Tooltip("박치기 준비를 위해 뒤로 물러나는 거리입니다.")]
    public float backupDistance = 3.0f;
    
    [Tooltip("큰 동물들이 뒤로 물러나는 거리입니다.")]
    public float largeAnimalBackupDistance = 5.0f;
    
    [Tooltip("박치기 충돌로 뒤로 밀려나는 거리입니다. (현재 미사용 - KnockbackAfterCollision 비활성화됨)")]
    public float knockbackDistance = 1.5f;

    [Tooltip("큰 동물들이 뒤로 밀려나는 거리입니다. (현재 미사용 - KnockbackAfterCollision 비활성화됨)")]
    public float largeAnimalKnockbackDistance = 2.5f;
    
    [Header("Animation Settings")]
    [Tooltip("뒤로 물러나는 데 걸리는 시간입니다.")]
    public float backupDuration = 1.0f;
    
    [Tooltip("충돌 후 뒤로 밀려나는 시간입니다. (현재 미사용 - KnockbackAfterCollision 비활성화됨)")]
    public float knockbackDuration = 0.5f;
    
    [Tooltip("박치기 충돌 감지 거리입니다. (충돌 지점 오프셋 계산에 사용됨)")]
    public float collisionDetectionDistance = 1.5f;

    [Tooltip("큰 동물들의 충돌 감지 거리입니다. (충돌 지점 오프셋 계산에 사용됨)")]
    public float largeAnimalCollisionDistance = 2.5f;
    
    [Tooltip("염소-양의 충돌 지점 거리 비율입니다. (1.0 = 충돌 감지 거리의 100%)")]
    [Range(0.3f, 2.0f)]
    public float smallAnimalOffsetRatio = 0.5f;
    
    [Tooltip("황소-버팔로의 충돌 지점 거리 비율입니다.")]
    [Range(0.5f, 2.5f)]
    public float mediumAnimalOffsetRatio = 1.2f;
    
    [Tooltip("코뿔소-들소의 충돌 지점 거리 비율입니다.")]
    [Range(0.8f, 3.0f)]
    public float largeAnimalOffsetRatio = 1.5f;
    
    [Header("Headbutt Count")]
    [Tooltip("최소 박치기 횟수입니다.")]
    public int minHeadbuttCount = 3;
    
    [Tooltip("최대 박치기 횟수입니다.")]
    public int maxHeadbuttCount = 6;
    
    [Tooltip("큰 동물들의 최소 박치기 횟수입니다.")]
    public int largeAnimalMinCount = 2;
    
    [Tooltip("큰 동물들의 최대 박치기 횟수입니다.")]
    public int largeAnimalMaxCount = 4;
    
    [Header("Speed Settings")]
    [Tooltip("달려들 때의 속도 배율입니다.")]
    public float chargeSpeedMultiplier = 2.5f;
    
    [Tooltip("큰 동물들의 속도 배율입니다.")]
    public float largeAnimalSpeedMultiplier = 2.0f;
    
    [Tooltip("가속도 배율입니다.")]
    public float accelerationMultiplier = 2.0f;
    
    [Tooltip("큰 동물들의 가속도 배율입니다.")]
    public float largeAnimalAccelerationMultiplier = 1.5f;
    
    [Header("Safety Settings")]
    [Tooltip("NavMeshAgent 준비 대기 시간입니다.")]
    public float agentSafetyTimeout = 3f;
    
    [Tooltip("박치기 충돌 대기 최대 시간입니다.")]
    public float chargeTimeout = 2.0f;
    
    [Tooltip("충돌 시 NavMeshAgent의 회피 반경입니다. 0으로 설정하면 충돌 시 서로를 회피하지 않습니다.")]
    public float collisionAvoidanceRadius = 0f;
    
    [Header("Visual Effects")]
    [Tooltip("박치기 충돌 시 생성될 파티클 효과 프리팹입니다.")]
    public GameObject collisionEffectPrefab;
    
    [Tooltip("충돌 효과의 크기 배율입니다.")]
    public float effectScale = 1f;
    
    [Tooltip("큰 동물들의 충돌 효과 크기 배율입니다.")]
    public float largeAnimalEffectScale = 1.5f;
    
    [Tooltip("파티클 효과가 유지되는 시간입니다.")]
    public float effectDuration = 3f;
    
    [Tooltip("충돌 애니메이션 동안 파티클이 반복되는 횟수입니다.")]
    public int effectRepeatCount = 5;
    
    [Tooltip("파티클 반복 간격입니다.")]
    public float effectRepeatInterval = 0.7f;
    
    [Header("Win Probability")]
    [Tooltip("황소가 버팔로를 이길 확률입니다.")]
    [Range(0f, 1f)]
    public float bullWinProbability = 0.55f;
    
    [Tooltip("코뿔소가 들소를 이길 확률입니다.")]
    [Range(0f, 1f)]
    public float rhinoWinProbability = 0.6f;
    
    // 박치기 동물 쌍 타입
    private enum HeadbuttPair { GoatSheep, BullBuffalo, RhinoBison, Other }

    // ★★★ 추가: 인스턴스별 이펙트 관리 - 동시 실행 격리 ★★★
    private List<GameObject> myHeadbuttEffects = new List<GameObject>();
    private List<Coroutine> activeCoroutines = new List<Coroutine>();

    // 컴포넌트 캐싱 (성능 최적화)
    private readonly Dictionary<PetController, PetAnimationController> animControllerCache = new();
    private readonly Dictionary<PetController, Collider> colliderCache = new();
    
    /// <summary>
    /// 템플릿에서 이 인스턴스로 설정값을 복사합니다 (동시 실행 격리)
    /// </summary>
    public void CopySettingsFrom(HeadbuttInteraction template)
    {
        if (template == null) return;

        // Headbutt Settings
        this.baseHeadbuttDistance = template.baseHeadbuttDistance;
        this.largeAnimalHeadbuttDistance = template.largeAnimalHeadbuttDistance;
        this.backupDistance = template.backupDistance;
        this.largeAnimalBackupDistance = template.largeAnimalBackupDistance;
        this.knockbackDistance = template.knockbackDistance;
        this.largeAnimalKnockbackDistance = template.largeAnimalKnockbackDistance;

        // Animation Settings
        this.backupDuration = template.backupDuration;
        this.knockbackDuration = template.knockbackDuration;
        this.collisionDetectionDistance = template.collisionDetectionDistance;
        this.largeAnimalCollisionDistance = template.largeAnimalCollisionDistance;
        this.smallAnimalOffsetRatio = template.smallAnimalOffsetRatio;
        this.mediumAnimalOffsetRatio = template.mediumAnimalOffsetRatio;
        this.largeAnimalOffsetRatio = template.largeAnimalOffsetRatio;

        // Headbutt Count
        this.minHeadbuttCount = template.minHeadbuttCount;
        this.maxHeadbuttCount = template.maxHeadbuttCount;
        this.largeAnimalMinCount = template.largeAnimalMinCount;
        this.largeAnimalMaxCount = template.largeAnimalMaxCount;

        // Speed Settings
        this.chargeSpeedMultiplier = template.chargeSpeedMultiplier;
        this.largeAnimalSpeedMultiplier = template.largeAnimalSpeedMultiplier;
        this.accelerationMultiplier = template.accelerationMultiplier;
        this.largeAnimalAccelerationMultiplier = template.largeAnimalAccelerationMultiplier;

        // Safety Settings
        this.agentSafetyTimeout = template.agentSafetyTimeout;
        this.chargeTimeout = template.chargeTimeout;
        this.collisionAvoidanceRadius = template.collisionAvoidanceRadius;

        // Visual Effects
        this.collisionEffectPrefab = template.collisionEffectPrefab;
        this.effectScale = template.effectScale;
        this.largeAnimalEffectScale = template.largeAnimalEffectScale;
        this.effectDuration = template.effectDuration;
        this.effectRepeatCount = template.effectRepeatCount;
        this.effectRepeatInterval = template.effectRepeatInterval;

        // Win Probability
        this.bullWinProbability = template.bullWinProbability;
        this.rhinoWinProbability = template.rhinoWinProbability;
    }

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.Fight;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        return IsPetPair(pet1, pet2, PetType.Goat, PetType.Sheep) ||
               IsPetPair(pet1, pet2, PetType.Bull, PetType.Buffalo) ||
               IsPetPair(pet1, pet2, PetType.Rhino, PetType.Bison);
    }
    
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        // ★★★ 수정: 이 인스턴스의 이펙트만 정리 ★★★
        foreach (var effect in myHeadbuttEffects)
        {
            if (effect != null) Destroy(effect);
        }
        myHeadbuttEffects.Clear();

        // ★★★ 수정: 활성 코루틴 정리 ★★★
        foreach (var coroutine in activeCoroutines)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
        activeCoroutines.Clear();

        HeadbuttPair pairType = GetPairType(pet1, pet2);
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 박치기 상호작용 시작! (타입: {pairType})");

        // 역할 식별
        PetController firstPet, secondPet;
        IdentifyPets(pet1, pet2, pairType, out firstPet, out secondPet);
        
        // NavMeshAgent 준비 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(firstPet, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(secondPet, agentSafetyTimeout));
        
        if (!IsAgentSafelyReady(firstPet) || !IsAgentSafelyReady(secondPet))
        {
            Debug.LogError($"[{InteractionName}] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(firstPet, secondPet);
            yield break;
        }
        
        // 원래 상태 저장
        PetOriginalState firstPetState = new PetOriginalState(firstPet);
        PetOriginalState secondPetState = new PetOriginalState(secondPet);
        
        try
        {
            // 단계별 상호작용 수행
            yield return StartCoroutine(PreparePhase(firstPet, secondPet, pairType));
            yield return StartCoroutine(HeadbuttBattlePhase(firstPet, secondPet, pairType, firstPetState, secondPetState));
            yield return StartCoroutine(DetermineWinnerPhase(firstPet, secondPet, pairType));
        }
        finally
        {
            Debug.Log($"[{InteractionName}] 상호작용 정리 시작.");

            // ★★★ 수정: 이 인스턴스의 이펙트와 코루틴만 정리 ★★★
            // 이펙트 정리
            foreach (var effect in myHeadbuttEffects)
            {
                if (effect != null) Destroy(effect);
            }
            myHeadbuttEffects.Clear();

            // 활성 코루틴 정리
            foreach (var coroutine in activeCoroutines)
            {
                if (coroutine != null) StopCoroutine(coroutine);
            }
            activeCoroutines.Clear();

            // 감정 숨기기
            if (firstPet != null) firstPet.HideEmotion();
            if (secondPet != null) secondPet.HideEmotion();

            // 상태 복원
            firstPetState.Restore(firstPet);
            secondPetState.Restore(secondPet);

            // 상호작용 종료
            EndInteraction(firstPet, secondPet);
            Debug.Log($"[{InteractionName}] 상호작용 정리 완료.");
        }
    }
    
    #region Phase Methods
    
    /// <summary>
    /// 박치기 준비 단계 - 위치 조정, 감정 표현, 마주보기
    /// </summary>
    private IEnumerator PreparePhase(PetController firstPet, PetController secondPet, HeadbuttPair pairType)
    {
        Debug.Log($"[{InteractionName}] 1단계: 박치기 준비");

        // 감정 표현
        firstPet.ShowEmotion(EmotionType.Angry, EmotionConstants.DURATION_PERSISTENT);
        secondPet.ShowEmotion(EmotionType.Angry, EmotionConstants.DURATION_PERSISTENT);

        // 거리 계산: 동물 종류별 기본 거리 + 펫 크기 배율
        float baseDistance = GetHeadbuttDistance(pairType); // 8f or 12f
        float multiplier1 = firstPet.Profile.GetInteractionDistanceMultiplier();
        float multiplier2 = secondPet.Profile.GetInteractionDistanceMultiplier();
        float averageMultiplier = (multiplier1 + multiplier2) / 2f;
        float adjustedDistance = baseDistance * averageMultiplier;

        Debug.Log($"[{InteractionName}] 거리 계산: 기본={baseDistance}m, 크기배율={averageMultiplier:F2}, 최종={adjustedDistance:F2}m");

        // 중간 지점 계산
        Vector3 midpoint = (firstPet.transform.position + secondPet.transform.position) / 2f;
        midpoint = FindValidPositionOnNavMesh(midpoint, NAVMESH_SEARCH_RADIUS);

        // 방향 벡터 계산
        Vector3 direction = (secondPet.transform.position - firstPet.transform.position).normalized;
        if (direction == Vector3.zero) direction = firstPet.transform.forward;

        // 중간 지점에서 양쪽으로 거리의 절반씩 떨어진 위치 계산
        Vector3 firstPetPos = midpoint - direction * (adjustedDistance / 2f);
        Vector3 secondPetPos = midpoint + direction * (adjustedDistance / 2f);

        firstPetPos = FindValidPositionOnNavMesh(firstPetPos, NAVMESH_SEARCH_RADIUS);
        secondPetPos = FindValidPositionOnNavMesh(secondPetPos, NAVMESH_SEARCH_RADIUS);

        // 위치 이동
        yield return StartCoroutine(MoveToPositions(firstPet, secondPet, firstPetPos, secondPetPos, POSITION_MOVE_TIMEOUT));

        // 서로 마주보기
        yield return StartCoroutine(SmoothlyLookAtEachOther(firstPet, secondPet, SMOOTH_ROTATION_DURATION));

        // 긴장감 조성
        yield return new WaitForSeconds(TENSION_WAIT_TIME);
    }
    
    /// <summary>
    /// 박치기 전투 단계 - 반복적인 박치기 수행
    /// </summary>
    private IEnumerator HeadbuttBattlePhase(PetController firstPet, PetController secondPet, HeadbuttPair pairType,
        PetOriginalState firstPetState, PetOriginalState secondPetState)
    {
        Debug.Log($"[{InteractionName}] 2단계: 박치기 전투");
        
        int headbuttCount = GetHeadbuttCount(pairType);
        
        for (int i = 0; i < headbuttCount; i++)
        {
            Debug.Log($"[{InteractionName}] {i+1}번째 박치기 시작");
            
            // 뒤로 물러나기
            yield return StartCoroutine(BackupForCharge(firstPet, secondPet, pairType));
            
            // 잠시 대기 (긴장감)
            yield return new WaitForSeconds(CHARGE_PREPARATION_DELAY);

            // 달려들어 충돌
            yield return StartCoroutine(ChargeAndCollide(firstPet, secondPet, pairType, firstPetState, secondPetState));

            // 충돌 후 밀려나기
            // yield return StartCoroutine(KnockbackAfterCollision(firstPet, secondPet, pairType));

            // 다음 박치기 준비
            yield return new WaitForSeconds(NEXT_HEADBUTT_DELAY);

            // 다시 마주보기
            yield return StartCoroutine(SmoothlyLookAtEachOther(firstPet, secondPet, QUICK_ROTATION_DURATION));
        }
    }
    
    /// <summary>
    /// 승자 결정 단계
    /// </summary>
    private IEnumerator DetermineWinnerPhase(PetController firstPet, PetController secondPet, HeadbuttPair pairType)
    {
        Debug.Log($"[{InteractionName}] 3단계: 승자 결정");
        
        float winProbability = GetWinProbability(pairType, firstPet);
        PetController winner = DetermineWinner(firstPet, secondPet, winProbability);
        PetController loser = winner == firstPet ? secondPet : firstPet;
        
        Debug.Log($"[{InteractionName}] 승자: {winner.petName}!");

        // 감정 표현
        winner.ShowEmotion(EmotionType.Victory, EmotionConstants.DURATION_SHORT);
        loser.ShowEmotion(EmotionType.Defeat, EmotionConstants.DURATION_SHORT);

        // 승리/패배 애니메이션
        yield return StartCoroutine(PlayWinnerLoserAnimations(
            winner, loser,
            PetAnimationController.PetAnimationType.Jump,
            PetAnimationController.PetAnimationType.Damage
        ));

        yield return new WaitForSeconds(WINNER_CELEBRATION_DELAY);
    }
    
    #endregion

    #region Helper Methods

    /// <summary>
    /// AnimationController를 가져옵니다 (캐싱됨).
    /// </summary>
    private PetAnimationController GetCachedAnimController(PetController pet)
    {
        if (pet == null) return null;

        if (!animControllerCache.TryGetValue(pet, out var controller))
        {
            controller = pet.GetComponent<PetAnimationController>();
            if (controller != null)
            {
                animControllerCache[pet] = controller;
            }
        }
        return controller;
    }

    /// <summary>
    /// Collider를 가져옵니다 (캐싱됨).
    /// </summary>
    private Collider GetCachedCollider(PetController pet)
    {
        if (pet == null) return null;

        if (!colliderCache.TryGetValue(pet, out var collider))
        {
            collider = pet.GetComponent<Collider>();
            if (collider != null)
            {
                colliderCache[pet] = collider;
            }
        }
        return collider;
    }

    /// <summary>
    /// 큰 동물 쌍인지 확인합니다.
    /// </summary>
    private bool IsLargeAnimalPair(HeadbuttPair pairType)
    {
        return pairType == HeadbuttPair.BullBuffalo || pairType == HeadbuttPair.RhinoBison;
    }

    /// <summary>
    /// 두 펫이 특정 타입 조합인지 확인합니다.
    /// </summary>
    private bool IsPetPair(PetController pet1, PetController pet2, PetType type1, PetType type2)
    {
        return (pet1.PetType == type1 && pet2.PetType == type2) ||
               (pet1.PetType == type2 && pet2.PetType == type1);
    }

    /// <summary>
    /// 두 펫의 조합 유형을 결정합니다.
    /// </summary>
    private HeadbuttPair GetPairType(PetController pet1, PetController pet2)
    {
        if (IsPetPair(pet1, pet2, PetType.Goat, PetType.Sheep))
            return HeadbuttPair.GoatSheep;

        if (IsPetPair(pet1, pet2, PetType.Bull, PetType.Buffalo))
            return HeadbuttPair.BullBuffalo;

        if (IsPetPair(pet1, pet2, PetType.Rhino, PetType.Bison))
            return HeadbuttPair.RhinoBison;

        return HeadbuttPair.Other;
    }
    
    /// <summary>
    /// 펫 쌍에서 첫 번째와 두 번째 펫을 식별합니다.
    /// </summary>
    private void IdentifyPets(PetController pet1, PetController pet2, HeadbuttPair pairType, 
        out PetController firstPet, out PetController secondPet)
    {
        switch (pairType)
        {
            case HeadbuttPair.GoatSheep:
                firstPet = pet1.PetType == PetType.Goat ? pet1 : pet2;
                secondPet = pet1.PetType == PetType.Goat ? pet2 : pet1;
                break;
            case HeadbuttPair.BullBuffalo:
                firstPet = pet1.PetType == PetType.Bull ? pet1 : pet2;
                secondPet = pet1.PetType == PetType.Bull ? pet2 : pet1;
                break;
            case HeadbuttPair.RhinoBison:
                firstPet = pet1.PetType == PetType.Rhino ? pet1 : pet2;
                secondPet = pet1.PetType == PetType.Rhino ? pet2 : pet1;
                break;
            default:
                firstPet = pet1;
                secondPet = pet2;
                break;
        }
    }
    
    /// <summary>
    /// 동물 크기에 따른 박치기 거리를 반환합니다.
    /// </summary>
    private float GetHeadbuttDistance(HeadbuttPair pairType)
    {
        return IsLargeAnimalPair(pairType) ? largeAnimalHeadbuttDistance : baseHeadbuttDistance;
    }

    /// <summary>
    /// 동물 크기에 따른 후진 거리를 반환합니다.
    /// </summary>
    private float GetBackupDistance(HeadbuttPair pairType)
    {
        return IsLargeAnimalPair(pairType) ? largeAnimalBackupDistance : backupDistance;
    }

    /// <summary>
    /// 동물 크기에 따른 넉백 거리를 반환합니다.
    /// </summary>
    private float GetKnockbackDistance(HeadbuttPair pairType)
    {
        return IsLargeAnimalPair(pairType) ? largeAnimalKnockbackDistance : knockbackDistance;
    }

    /// <summary>
    /// 동물 크기에 따른 박치기 횟수를 반환합니다.
    /// </summary>
    private int GetHeadbuttCount(HeadbuttPair pairType)
    {
        return IsLargeAnimalPair(pairType)
            ? Random.Range(largeAnimalMinCount, largeAnimalMaxCount)
            : Random.Range(minHeadbuttCount, maxHeadbuttCount);
    }

    /// <summary>
    /// 충돌 감지 거리를 반환합니다.
    /// </summary>
    private float GetCollisionDistance(HeadbuttPair pairType)
    {
        return IsLargeAnimalPair(pairType) ? largeAnimalCollisionDistance : collisionDetectionDistance;
    }
    
    /// <summary>
    /// 속도 배율을 반환합니다.
    /// </summary>
    private void GetSpeedSettings(HeadbuttPair pairType, out float speedMult, out float accelMult)
    {
        if (IsLargeAnimalPair(pairType))
        {
            speedMult = largeAnimalSpeedMultiplier;
            accelMult = largeAnimalAccelerationMultiplier;
        }
        else
        {
            speedMult = chargeSpeedMultiplier;
            accelMult = accelerationMultiplier;
        }
    }
    
    /// <summary>
    /// 승리 확률을 계산합니다.
    /// </summary>
    private float GetWinProbability(HeadbuttPair pairType, PetController firstPet)
    {
        switch (pairType)
        {
            case HeadbuttPair.GoatSheep:
                return 0.5f; // 염소-양은 동등한 확률
            case HeadbuttPair.BullBuffalo:
                return firstPet.PetType == PetType.Bull ? bullWinProbability : (1f - bullWinProbability);
            case HeadbuttPair.RhinoBison:
                return firstPet.PetType == PetType.Rhino ? rhinoWinProbability : (1f - rhinoWinProbability);
            default:
                return 0.5f;
        }
    }
    
    /// <summary>
    /// NavMeshAgent가 안전하게 준비되었는지 확인합니다.
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.IsReady();
    }
    
    /// <summary>
    /// 동물 크기에 따른 충돌 오프셋 비율을 반환합니다.
    /// </summary>
    private float GetCollisionOffsetRatio(HeadbuttPair pairType)
    {
        switch (pairType)
        {
            case HeadbuttPair.GoatSheep:
                return smallAnimalOffsetRatio;
            case HeadbuttPair.BullBuffalo:
                return mediumAnimalOffsetRatio;
            case HeadbuttPair.RhinoBison:
                return largeAnimalOffsetRatio;
            default:
                return smallAnimalOffsetRatio;
        }
    }
    
    /// <summary>
    /// 동물 크기에 따른 충돌 효과 크기를 반환합니다.
    /// </summary>
    private float GetEffectScale(HeadbuttPair pairType)
    {
        return IsLargeAnimalPair(pairType) ? largeAnimalEffectScale : effectScale;
    }
    
    /// <summary>
    /// 충돌 효과가 나타날 위치를 계산합니다.
    /// </summary>
    private Vector3 GetCollisionEffectPosition(PetController firstPet, PetController secondPet)
    {
        // 두 펫의 머리 위치를 추정
        float firstPetHeadHeight = GetCachedCollider(firstPet).bounds.size.y * HEAD_HEIGHT_RATIO;
        float secondPetHeadHeight = GetCachedCollider(secondPet).bounds.size.y * HEAD_HEIGHT_RATIO;

        Vector3 firstPetHeadPos = firstPet.transform.position + Vector3.up * firstPetHeadHeight;
        Vector3 secondPetHeadPos = secondPet.transform.position + Vector3.up * secondPetHeadHeight;

        // 두 머리 위치의 중간점
        return (firstPetHeadPos + secondPetHeadPos) / 2f;
    }
    
    /// <summary>
    /// 충돌 효과를 반복적으로 생성하는 코루틴
    /// </summary>
    private IEnumerator CreateRepeatingCollisionEffect(PetController firstPet, PetController secondPet, HeadbuttPair pairType)
    {
        float scale = GetEffectScale(pairType);

        for (int i = 0; i < effectRepeatCount; i++)
        {
            // 현재 위치에서 효과 생성
            Vector3 effectPosition = GetCollisionEffectPosition(firstPet, secondPet);
            GameObject effect = Instantiate(collisionEffectPrefab, effectPosition, Quaternion.identity);
            effect.transform.localScale = Vector3.one * scale;

            // ★★★ 추가: 이펙트를 인스턴스별 리스트에 추가 ★★★
            myHeadbuttEffects.Add(effect);

            // 설정된 시간 후 제거 (리스트에서도 제거)
            StartCoroutine(DestroyEffectAfterDelay(effect, effectDuration));

            Debug.Log($"[{InteractionName}] 충돌 효과 {i + 1}/{effectRepeatCount} 생성!");

            // 마지막 효과가 아니면 대기
            if (i < effectRepeatCount - 1)
            {
                yield return new WaitForSeconds(effectRepeatInterval);
            }
        }
    }

    /// <summary>
    /// 지연 시간 후 이펙트를 파괴하고 리스트에서 제거하는 코루틴
    /// </summary>
    private IEnumerator DestroyEffectAfterDelay(GameObject effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (effect != null)
        {
            myHeadbuttEffects.Remove(effect);
            Destroy(effect);
        }
    }
    
    #endregion
    
    #region Movement Coroutines
    
    /// <summary>
    /// 박치기를 위해 뒤로 물러나는 코루틴
    /// </summary>
    private IEnumerator BackupForCharge(PetController firstPet, PetController secondPet, HeadbuttPair pairType)
    {
        float backDistance = GetBackupDistance(pairType);
        
        // 뒤로 물러날 위치 계산
        Vector3 firstPetBackupPos = firstPet.transform.position - firstPet.transform.forward * backDistance;
        Vector3 secondPetBackupPos = secondPet.transform.position - secondPet.transform.forward * backDistance;
        
        firstPetBackupPos = FindValidPositionOnNavMesh(firstPetBackupPos, NAVMESH_SEARCH_RADIUS);
        secondPetBackupPos = FindValidPositionOnNavMesh(secondPetBackupPos, NAVMESH_SEARCH_RADIUS);
        
        // 병렬로 뒤로 이동
        Coroutine firstBackupCoroutine = StartCoroutine(MoveBackward(firstPet, firstPetBackupPos, backupDuration));
        Coroutine secondBackupCoroutine = StartCoroutine(MoveBackward(secondPet, secondPetBackupPos, backupDuration));
        activeCoroutines.Add(firstBackupCoroutine);
        activeCoroutines.Add(secondBackupCoroutine);

        // 두 펫 모두 이동 완료 대기
        yield return firstBackupCoroutine;
        yield return secondBackupCoroutine;
    }
    
    /// <summary>
    /// 마주본 채로 뒤로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveBackward(PetController pet, Vector3 targetPos, float duration)
    {
        Vector3 startPos = pet.transform.position;
        float elapsedTime = 0f;
        
        // 애니메이션 컨트롤러 가져오기
        var animController = GetCachedAnimController(pet);
        
        // NavMeshAgent 임시 비활성화 전에 상태 저장
        bool wasAgentEnabled = pet.agent.enabled;
        bool wasAgentStopped = pet.agent.isStopped;
        
        // agent를 정지시키고 회전 업데이트 비활성화
        if (pet.agent.enabled)
        {
            pet.agent.isStopped = true;
            pet.agent.updateRotation = false;
        }
        
        // 원래 방향 유지
        Quaternion originalRotation = pet.transform.rotation;
        
        // Walk 애니메이션을 루프로 재생
        if (animController != null)
        {
            // PlayAnimationWithCustomDuration을 사용하여 애니메이션 재생
            // loop=true로 설정하여 계속 재생되도록 함
            Coroutine animCoroutine = StartCoroutine(animController.PlayAnimationWithCustomDuration(
                PetAnimationController.PetAnimationType.Walk,
                duration + ANIMATION_BUFFER_TIME,  // 이동 시간보다 약간 길게 설정
                true,  // loop
                false  // stopPrevious
            ));
            activeCoroutines.Add(animCoroutine);
            Debug.Log($"[{InteractionName}] {pet.petName} 뒤로 걷기 애니메이션 시작");
        }
        
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            pet.transform.position = Vector3.Lerp(startPos, targetPos, t);
            
            // 회전 유지
            pet.transform.rotation = originalRotation;
            if (pet.petModelTransform != null)
                pet.petModelTransform.rotation = originalRotation;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        pet.transform.position = targetPos;

        // NavMeshAgent가 활성화되어 있었다면 위치 동기화 및 상태 복원
        if (wasAgentEnabled)
        {
            if (!pet.agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(pet.transform.position, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas))
                {
                    pet.transform.position = hit.position;
                }
            }

            // agent 상태 복원 (다른 Activity에 영향을 주지 않도록)
            pet.agent.isStopped = wasAgentStopped;
            pet.agent.updateRotation = true;
        }
    }
    
    /// <summary>
    /// 달려들어 충돌하는 코루틴
    /// </summary>
    private IEnumerator ChargeAndCollide(PetController firstPet, PetController secondPet, HeadbuttPair pairType,
        PetOriginalState firstPetState, PetOriginalState secondPetState)
    {
        // 달리기 애니메이션
        GetCachedAnimController(firstPet).SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        GetCachedAnimController(secondPet).SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        
        // 속도 설정
        float speedMult, accelMult;
        GetSpeedSettings(pairType, out speedMult, out accelMult);
        
        firstPet.agent.speed = firstPetState.originalSpeed * speedMult;
        secondPet.agent.speed = secondPetState.originalSpeed * speedMult;
        firstPet.agent.acceleration = firstPetState.originalAcceleration * accelMult;
        secondPet.agent.acceleration = secondPetState.originalAcceleration * accelMult;
        
        // 충돌 시 서로 회피하지 않도록 설정 (옵션)
        if (collisionAvoidanceRadius == 0f)
        {
            firstPet.agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
            secondPet.agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
        }
        else
        {
            firstPet.agent.radius = collisionAvoidanceRadius;
            secondPet.agent.radius = collisionAvoidanceRadius;
        }
        
        // 충돌 지점 계산
        Vector3 midPoint = (firstPet.transform.position + secondPet.transform.position) / 2;
        midPoint = FindValidPositionOnNavMesh(midPoint, NAVMESH_SEARCH_RADIUS_SMALL);
        
        // 각 펫이 향하는 방향 계산
        Vector3 firstToSecond = (secondPet.transform.position - firstPet.transform.position).normalized;
        Vector3 secondToFirst = -firstToSecond;
        
        // 충돌 감지 거리
        float collisionDist = GetCollisionDistance(pairType);
        
        // 각 펫의 목표 지점을 충돌 지점에서 약간 떨어진 곳으로 설정
        // 이렇게 하면 두 펫이 정확히 같은 지점을 향하지 않아 겹치지 않음
        // 동물 크기별로 다른 offset ratio 적용
        float offsetRatio = GetCollisionOffsetRatio(pairType);
        Vector3 firstPetTarget = midPoint + secondToFirst * (collisionDist * offsetRatio);
        Vector3 secondPetTarget = midPoint + firstToSecond * (collisionDist * offsetRatio);
        
        firstPetTarget = FindValidPositionOnNavMesh(firstPetTarget, NAVMESH_SEARCH_RADIUS_SMALL / 2);
        secondPetTarget = FindValidPositionOnNavMesh(secondPetTarget, NAVMESH_SEARCH_RADIUS_SMALL / 2);
        
        // 이동 시작
        firstPet.agent.isStopped = false;
        secondPet.agent.isStopped = false;
        firstPet.agent.SetDestination(firstPetTarget);
        secondPet.agent.SetDestination(secondPetTarget);
        
        // 충돌 감지 - 두 펫 사이의 거리로 판단
        float startTime = Time.time;
        bool collision = false;
        // 목표 거리를 더 정확하게 계산 (두 펫이 멈출 위치 사이의 거리)
        float targetDistance = Vector3.Distance(firstPetTarget, secondPetTarget);
        Debug.Log($"[{InteractionName}] 충돌 감지 시작. 목표 거리: {targetDistance}, 충돌 감지 거리: {collisionDist}");
        
        while (Time.time - startTime < chargeTimeout && !collision)
        {
            float petDistance = Vector3.Distance(firstPet.transform.position, secondPet.transform.position);
            
            // 두 펫이 충분히 가까워졌을 때 충돌로 판정
            if (petDistance <= targetDistance)
            {
                collision = true;
                Debug.Log($"[{InteractionName}] 충돌 발생! 펫 간 거리: {petDistance:F2}");
            }
            
            yield return null;
        }
        
        // 충돌 처리
        firstPet.agent.isStopped = true;
        secondPet.agent.isStopped = true;

        // 충돌 애니메이션 시간 계산 (파티클 4번 반복 시간)
        float totalAnimationDuration = effectRepeatInterval * effectRepeatCount;

        // Attack 애니메이션 직접 설정 (PlayAnimationWithCustomDuration 대신 직접 제어)
        if (GetCachedAnimController(firstPet) != null && firstPet.animator != null)
        {
            firstPet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Attack);
            Debug.Log($"[{InteractionName}] {firstPet.petName} Attack 애니메이션 시작");
        }
        if (GetCachedAnimController(secondPet) != null && secondPet.animator != null)
        {
            secondPet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Attack);
            Debug.Log($"[{InteractionName}] {secondPet.petName} Attack 애니메이션 시작");
        }

        // 충돌 효과 생성
        if (collisionEffectPrefab != null)
        {
            // 반복 효과 코루틴을 시작하고 리스트에 추가
            Coroutine effectCoroutine = StartCoroutine(CreateRepeatingCollisionEffect(firstPet, secondPet, pairType));
            activeCoroutines.Add(effectCoroutine);

            if (!collision)
            {
                Debug.LogWarning($"[{InteractionName}] 충돌이 감지되지 않았지만 효과는 생성됩니다.");
                float currentDistance = Vector3.Distance(firstPet.transform.position, secondPet.transform.position);
                Debug.Log($"[{InteractionName}] 현재 펫 간 거리: {currentDistance}, 목표 거리: {targetDistance}");
            }
        }
        else
        {
            Debug.LogError($"[{InteractionName}] collisionEffectPrefab이 설정되지 않았습니다!");
        }

        // 파티클 효과와 동일한 시간 대기
        yield return new WaitForSeconds(totalAnimationDuration);

        // Attack 애니메이션을 Idle로 전환
        if (firstPet.animator != null)
        {
            firstPet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Idle);
            Debug.Log($"[{InteractionName}] {firstPet.petName} Idle로 전환");
        }
        if (secondPet.animator != null)
        {
            secondPet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Idle);
            Debug.Log($"[{InteractionName}] {secondPet.petName} Idle로 전환");
        }
    }
    
    /// <summary>
    /// 충돌 후 뒤로 밀려나는 코루틴
    /// </summary>
    private IEnumerator KnockbackAfterCollision(PetController firstPet, PetController secondPet, HeadbuttPair pairType)
    {
        float knockDistance = GetKnockbackDistance(pairType);
        
        // 뒤로 밀려날 위치 계산
        Vector3 firstPetKnockbackPos = firstPet.transform.position - firstPet.transform.forward * knockDistance;
        Vector3 secondPetKnockbackPos = secondPet.transform.position - secondPet.transform.forward * knockDistance;
        
        firstPetKnockbackPos = FindValidPositionOnNavMesh(firstPetKnockbackPos, NAVMESH_SEARCH_RADIUS_SMALL);
        secondPetKnockbackPos = FindValidPositionOnNavMesh(secondPetKnockbackPos, NAVMESH_SEARCH_RADIUS_SMALL);
        
        // 원래 회전 저장
        Quaternion firstPetRot = firstPet.transform.rotation;
        Quaternion secondPetRot = secondPet.transform.rotation;
        
        // 넉백 애니메이션
        float elapsedTime = 0f;
        Vector3 firstPetStartPos = firstPet.transform.position;
        Vector3 secondPetStartPos = secondPet.transform.position;
        
        while (elapsedTime < knockbackDuration)
        {
            float t = elapsedTime / knockbackDuration;
            
            firstPet.transform.position = Vector3.Lerp(firstPetStartPos, firstPetKnockbackPos, t);
            secondPet.transform.position = Vector3.Lerp(secondPetStartPos, secondPetKnockbackPos, t);
            
            // 회전 유지
            firstPet.transform.rotation = firstPetRot;
            secondPet.transform.rotation = secondPetRot;
            
            if (firstPet.petModelTransform != null) 
                firstPet.petModelTransform.rotation = firstPetRot;
            if (secondPet.petModelTransform != null) 
                secondPet.petModelTransform.rotation = secondPetRot;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 최종 위치 설정
        firstPet.transform.position = firstPetKnockbackPos;
        secondPet.transform.position = secondPetKnockbackPos;
    }

    #endregion

    /// <summary>
    /// 컴포넌트가 파괴될 때 남아있는 리소스 정리
    /// </summary>
    private void OnDestroy()
    {
        // ★★★ 추가: 컴포넌트 파괴 시 남은 이펙트 정리 ★★★
        foreach (var effect in myHeadbuttEffects)
        {
            if (effect != null) Destroy(effect);
        }
        myHeadbuttEffects.Clear();

        // 활성 코루틴 정리
        foreach (var coroutine in activeCoroutines)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
        activeCoroutines.Clear();

        // 캐시 정리 (메모리 누수 방지)
        animControllerCache.Clear();
        colliderCache.Clear();
    }
}