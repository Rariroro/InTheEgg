// FightInteraction.cs (최적화된 버전)
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;
public class FightInteraction : BasePetInteraction
{
    public override string InteractionName => "Fight";

    // 우선순위: 55 (10순위)
    public override int Priority => 55;

    // BasePetInteraction의 자동 이동을 비활성화하고 PrepareFightPhase에서 직접 처리
    public override bool ShouldPerformInitialMovement => false;

    [Header("크기 조합별 싸움 거리")]
    [Tooltip("소형-소형 펫 싸움 거리")]
    public float smallSmallDistance = 3f;

    [Tooltip("소형-중형 펫 싸움 거리")]
    public float smallMediumDistance = 4f;

    [Tooltip("소형-대형 펫 싸움 거리")]
    public float smallLargeDistance = 5f;

    [Tooltip("중형-중형 펫 싸움 거리")]
    public float mediumMediumDistance = 5f;

    [Tooltip("중형-대형 펫 싸움 거리")]
    public float mediumLargeDistance = 6f;

    [Tooltip("대형-대형 펫 싸움 거리")]
    public float largeLargeDistance = 7f;

    [Header("싸움 설정")]
    [Tooltip("싸움 시작 전 대기 시간")]
    public float preFightDelay = 1.0f;

    [Tooltip("싸움 위치로 이동하는 최대 시간")]
    public float moveTimeout = 10f;

    [Header("애니메이션 설정")]
    [Tooltip("각 공격 애니메이션 지속 시간")]
    public float attackAnimationDuration = 2.0f;

    [Tooltip("피격 애니메이션 지속 시간")]
    public float damageAnimationDuration = 2.0f;

    [Tooltip("싸움 라운드 수")]
    public int fightRounds = 3;

    [Tooltip("라운드 사이 대기 시간")]
    public float roundInterval = 0.5f;

    [Header("승부 설정")]
    [Tooltip("승자 결정 확률 (0.5 = 50:50)")]
    [Range(0f, 1f)]
    public float winProbability = 0.5f;

    [Tooltip("승리 애니메이션 지속 시간")]
    public float victoryAnimationDuration = 2.0f;

    [Tooltip("패배 애니메이션 지속 시간")]
    public float defeatAnimationDuration = 2.0f;

    [Header("감정 표현")]
    [Tooltip("싸움 중 감정 표현 지속 시간")]
    public float emotionDuration = 30f;

    [Tooltip("싸움 종료 후 감정 표현 시간")]
    public float postFightEmotionDuration = 5f;

    [Header("시각 효과")]
    [Tooltip("타격 파티클 프리팹")]
    public GameObject hitParticlePrefab;

    [Tooltip("파티클 생성 위치 오프셋")]
    public Vector3 hitParticleOffset = new Vector3(0, 1f, 0);

    [Tooltip("파티클 지속 시간")]
    public float particleDuration = 1f;

    [Tooltip("싸움 시작 시 먼지 파티클")]
    public GameObject fightStartDustPrefab;

    [Tooltip("공격 시 휘두르기 효과 파티클")]
    public GameObject attackSwingPrefab;

    [Tooltip("승리 시 축하 파티클")]
    public GameObject victoryParticlePrefab;

    [Tooltip("패배 시 별 파티클")]
    public GameObject defeatStarsPrefab;

    [Tooltip("파티클 생성 개수 (먼지 효과용)")]
    public int dustParticleCount = 3;

    [Header("안전 설정")]
    [Tooltip("NavMeshAgent 안전 체크 최대 대기 시간")]
    public float agentSafetyTimeout = 3f;

    /// <summary>
    /// 템플릿에서 이 인스턴스로 설정값을 복사합니다 (동시 실행 격리)
    /// </summary>
    public void CopySettingsFrom(FightInteraction template)
    {
        if (template == null) return;

        // 크기 조합별 싸움 거리
        this.smallSmallDistance = template.smallSmallDistance;
        this.smallMediumDistance = template.smallMediumDistance;
        this.smallLargeDistance = template.smallLargeDistance;
        this.mediumMediumDistance = template.mediumMediumDistance;
        this.mediumLargeDistance = template.mediumLargeDistance;
        this.largeLargeDistance = template.largeLargeDistance;

        // 싸움 설정
        this.preFightDelay = template.preFightDelay;
        this.moveTimeout = template.moveTimeout;

        // 애니메이션 설정
        this.attackAnimationDuration = template.attackAnimationDuration;
        this.damageAnimationDuration = template.damageAnimationDuration;
        this.fightRounds = template.fightRounds;
        this.roundInterval = template.roundInterval;

        // 승부 설정
        this.winProbability = template.winProbability;
        this.victoryAnimationDuration = template.victoryAnimationDuration;
        this.defeatAnimationDuration = template.defeatAnimationDuration;

        // 감정 표현
        this.emotionDuration = template.emotionDuration;
        this.postFightEmotionDuration = template.postFightEmotionDuration;

        // 시각 효과
        this.hitParticlePrefab = template.hitParticlePrefab;
        this.hitParticleOffset = template.hitParticleOffset;
        this.particleDuration = template.particleDuration;
        this.fightStartDustPrefab = template.fightStartDustPrefab;
        this.attackSwingPrefab = template.attackSwingPrefab;
        this.victoryParticlePrefab = template.victoryParticlePrefab;
        this.defeatStarsPrefab = template.defeatStarsPrefab;
        this.dustParticleCount = template.dustParticleCount;

        // 안전 설정
        this.agentSafetyTimeout = template.agentSafetyTimeout;
    }

    protected override InteractionType DetermineInteractionType()
    {
        return InteractionType.Fight;
    }

    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        

        // 사자(표범)와 호랑이는 싸움
        if ((pet1.PetType == PetType.Leopard && pet2.PetType == PetType.Tiger) ||
            (pet1.PetType == PetType.Tiger && pet2.PetType == PetType.Leopard))
        {
            return true;
        }

        // 추가 싸움 조건들
        // 육식동물끼리 싸움 (고기를 먹는 동물들)
        bool pet1IsMeatEater = (pet1.diet & PetAIProperties.DietaryFlags.Meat) != 0;
        bool pet2IsMeatEater = (pet2.diet & PetAIProperties.DietaryFlags.Meat) != 0;

        // 같은 육식동물끼리만 싸움 (서로 다른 종일 때)
        if (pet1IsMeatEater && pet2IsMeatEater && pet1.PetType != pet2.PetType)
        {
            // [변경] Brave 성격을 가진 펫들만 싸움
            bool pet1IsBrave = pet1.personality == PetAIProperties.Personality.Brave;
            bool pet2IsBrave = pet2.personality == PetAIProperties.Personality.Brave;

            if (pet1IsBrave && pet2IsBrave)
            {
                return true;
            }
        }

        return false;
    }

    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 싸움 시작!");

        // NavMeshAgent 준비 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(pet1, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(pet2, agentSafetyTimeout));

        if (!IsAgentSafelyReady(pet1) || !IsAgentSafelyReady(pet2))
        {
            Debug.LogError($"[{InteractionName}] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(pet1, pet2);
            yield break;
        }

        // 원래 상태 저장
        PetOriginalState pet1State = new PetOriginalState(pet1);
        PetOriginalState pet2State = new PetOriginalState(pet2);

        try
        {
            // 감정 표현
            pet1.ShowEmotion(EmotionType.Angry, emotionDuration);
            pet2.ShowEmotion(EmotionType.Angry, emotionDuration);

            // 1. 준비 단계
            yield return StartCoroutine(PrepareFightPhase(pet1, pet2));

            // 2. 싸움 단계
            yield return StartCoroutine(FightPhase(pet1, pet2));

            // 3. 승부 결정 단계
            PetController winner = DetermineWinner(pet1, pet2, winProbability);
            PetController loser = (winner == pet1) ? pet2 : pet1;

            // 4. 결과 표현 단계
            yield return StartCoroutine(ShowResultPhase(winner, loser));

            Debug.Log($"[{InteractionName}] 싸움 종료! 승자: {winner.petName}");
        }
        finally
        {
            // 최종 정리
            Debug.Log($"[{InteractionName}] 상호작용 정리 시작.");

            // 원래 상태 복원
            pet1State.Restore(pet1);
            pet2State.Restore(pet2);

            // 애니메이션 정리
            pet1.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
            pet2.GetComponent<PetAnimationController>()?.StopContinuousAnimation();

            // 공통 종료 처리
            EndInteraction(pet1, pet2);
            Debug.Log($"[{InteractionName}] 상호작용 정리 완료.");
        }
    }

    /// <summary>
    /// 1단계: 싸움 준비
    /// </summary>
    private IEnumerator PrepareFightPhase(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] 1단계: 싸움 준비");

        // 펫 크기에 따른 싸움 거리 계산
        float adjustedFightDistance = CalculateDistanceBySize(pet1, pet2);

        // 중간 지점 기반 위치 계산 (HeadbuttInteraction 패턴)
        Vector3 midpoint = (pet1.transform.position + pet2.transform.position) / 2f;
        midpoint = FindValidPositionOnNavMesh(midpoint, 10f);

        // 방향 벡터 계산 (HeadbuttInteraction 패턴 - y축 처리 없이 사용)
        Vector3 direction = (pet2.transform.position - pet1.transform.position).normalized;
        if (direction == Vector3.zero) direction = pet1.transform.forward;

        Debug.Log($"[{InteractionName}] 거리 계산: 최종={adjustedFightDistance:F2}m, 중간점={midpoint}");

        // 중간 지점에서 양쪽으로 거리의 절반씩 떨어진 위치 계산
        Vector3 pet1Target = midpoint - direction * (adjustedFightDistance / 2f);
        Vector3 pet2Target = midpoint + direction * (adjustedFightDistance / 2f);

        pet1Target = FindValidPositionOnNavMesh(pet1Target, adjustedFightDistance);
        pet2Target = FindValidPositionOnNavMesh(pet2Target, adjustedFightDistance);

        // 위치로 이동
        yield return StartCoroutine(MoveToPositions(pet1, pet2, pet1Target, pet2Target, moveTimeout));

        // 서로 마주보기 (HeadbuttInteraction과 동일하게 1.0초 사용)
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 1.0f));

        // 싸움 시작 먼지 효과
        if (fightStartDustPrefab != null)
        {
            SpawnDustParticles(pet1.transform.position, pet2.transform.position);
        }

        // 싸움 전 긴장감 조성
        yield return new WaitForSeconds(preFightDelay);
    }

    /// <summary>
    /// 2단계: 실제 싸움
    /// </summary>
    private IEnumerator FightPhase(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] 2단계: 싸움 진행");

        var pet1Anim = pet1.GetComponent<PetAnimationController>();
        var pet2Anim = pet2.GetComponent<PetAnimationController>();

        for (int round = 0; round < fightRounds; round++)
        {
            Debug.Log($"[{InteractionName}] 라운드 {round + 1}/{fightRounds}");

            // 번갈아가며 공격
            if (round % 2 == 0)
            {
                // Pet1이 먼저 공격
                yield return StartCoroutine(PerformAttack(pet1, pet2, pet1Anim, pet2Anim));
            }
            else
            {
                // Pet2가 먼저 공격
                yield return StartCoroutine(PerformAttack(pet2, pet1, pet2Anim, pet1Anim));
            }

            // 라운드 사이 대기
            if (round < fightRounds - 1)
            {
                yield return new WaitForSeconds(roundInterval);
            }
        }
    }

    /// <summary>
    /// 공격 수행
    /// </summary>
    private IEnumerator PerformAttack(PetController attacker, PetController defender,
        PetAnimationController attackerAnim, PetAnimationController defenderAnim)
    {
        // 공격 전 서로 마주보기 (방향 보정)
        yield return StartCoroutine(SmoothlyLookAtEachOther(attacker, defender, 0.3f));

        // 공격 휘두르기 효과
        if (attackSwingPrefab != null)
        {
            Vector3 swingPos = attacker.transform.position + attacker.transform.forward * 1f + Vector3.up * 1f;
            GameObject swing = Instantiate(attackSwingPrefab, swingPos, attacker.transform.rotation);
            Destroy(swing, particleDuration);
        }
   
        // 공격자가 공격 애니메이션
        StartCoroutine(attackerAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Attack, attackAnimationDuration, false, false));
 
        // 약간의 딜레이 후 피격 애니메이션
        yield return new WaitForSeconds(attackAnimationDuration * 0.5f);

    
// 타격 파티클 효과
        if (hitParticlePrefab != null)
        {
            Vector3 hitPos = defender.transform.position + hitParticleOffset;
            GameObject particle = Instantiate(hitParticlePrefab, hitPos, Quaternion.identity);
            Destroy(particle, particleDuration);
        }
        // 방어자가 피격 애니메이션
        yield return StartCoroutine(defenderAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Damage, damageAnimationDuration, false, false));
    }

    /// <summary>
    /// 3단계: 결과 표현
    /// </summary>
    private IEnumerator ShowResultPhase(PetController winner, PetController loser)
    {
        Debug.Log($"[{InteractionName}] 3단계: 결과 발표");

        // 감정 변경
        winner.ShowEmotion(EmotionType.Victory, postFightEmotionDuration);
        loser.ShowEmotion(EmotionType.Defeat, postFightEmotionDuration);

        var winnerAnim = winner.GetComponent<PetAnimationController>();
        var loserAnim = loser.GetComponent<PetAnimationController>();
   // 승리 파티클
        if (victoryParticlePrefab != null)
        {
            Vector3 victoryPos = winner.transform.position + Vector3.up * 2f;
            GameObject victoryParticle = Instantiate(victoryParticlePrefab, victoryPos, Quaternion.identity);
            Destroy(victoryParticle, 5f);
        }
        // 승자는 점프하며 기뻐함
        StartCoroutine(winnerAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, victoryAnimationDuration, true, false));

       // 패배 별 파티클
        if (defeatStarsPrefab != null)
        {
            Vector3 defeatPos = loser.transform.position + Vector3.up * 2.5f;
            // 90도 회전 (X축 기준으로 90도 회전하여 수평으로 만듦)
            Quaternion defeatRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject defeatParticle = Instantiate(defeatStarsPrefab, defeatPos, defeatRotation);
            Destroy(defeatParticle, 3f);
        }

        // 패자는 쓰러짐
        yield return StartCoroutine(loserAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Die, defeatAnimationDuration, false, false));

      

        // 패자가 다시 일어남
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(loserAnim.PlayAnimationWithCustomDuration(
            PetAnimationController.PetAnimationType.Jump, 1f, false, false));

        yield return new WaitForSeconds(2f);
    }

    /// <summary>
    /// NavMeshAgent가 안전하게 준비되었는지 확인
    /// </summary>
    private bool IsAgentSafelyReady(PetController pet)
    {
        return pet != null && pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh;
    }

    /// <summary>
    /// 먼지 파티클 생성
    /// </summary>
    private void SpawnDustParticles(Vector3 pos1, Vector3 pos2)
    {
        Vector3 midPoint = (pos1 + pos2) / 2f;
        
        for (int i = 0; i < dustParticleCount; i++)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-2f, 2f),
                0.1f,
                Random.Range(-2f, 2f)
            );
            
            Vector3 spawnPos = midPoint + randomOffset;
            GameObject dust = Instantiate(fightStartDustPrefab, spawnPos, Quaternion.identity);
            
            // 파티클이 위로 올라가는 효과
            if (dust.TryGetComponent<ParticleSystem>(out ParticleSystem ps))
            {
                var velocityModule = ps.velocityOverLifetime;
                velocityModule.enabled = true;
                velocityModule.y = new ParticleSystem.MinMaxCurve(2f, 4f);
            }
            
            Destroy(dust, particleDuration * 2f);
        }
    }

    /// <summary>
    /// 펫 크기에 따른 싸움 거리 계산
    /// </summary>
    private float CalculateDistanceBySize(PetController pet1, PetController pet2)
    {
        var size1 = pet1.Profile.size;
        var size2 = pet2.Profile.size;

        // 크기 순서 정렬 (Small < Medium < Large)
        if (size1 > size2) (size1, size2) = (size2, size1);

        float distance = GetDistanceBySizePair(size1, size2);

        Debug.Log($"[{InteractionName}] {pet1.petName}({pet1.Profile.size}) & {pet2.petName}({pet2.Profile.size}) => 싸움 거리: {distance:F2}");

        return distance;
    }

    /// <summary>
    /// 크기 조합에 따른 거리 반환
    /// </summary>
    /// <param name="size1">작은 쪽 크기 (size1 <= size2 보장됨)</param>
    /// <param name="size2">큰 쪽 크기</param>
    private float GetDistanceBySizePair(PetTraits.Size size1, PetTraits.Size size2)
    {
        // size1 <= size2 보장됨
        if (size1 == PetTraits.Size.Small)
        {
            if (size2 == PetTraits.Size.Small) return smallSmallDistance;
            if (size2 == PetTraits.Size.Medium) return smallMediumDistance;
            return smallLargeDistance;
        }
        if (size1 == PetTraits.Size.Medium)
        {
            if (size2 == PetTraits.Size.Medium) return mediumMediumDistance;
            return mediumLargeDistance;
        }
        return largeLargeDistance;
    }
}