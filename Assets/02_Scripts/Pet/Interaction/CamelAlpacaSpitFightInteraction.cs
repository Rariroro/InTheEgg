// Pet.zip/Interaction/CamelAlpacaSpitFightInteraction.cs

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 낙타와 알파카의 침 뱉기 싸움 상호작용을 처리합니다.
/// RaceInteraction, ChameleonCamouflageInteraction 등의 구조를 참고하여 최적화되었습니다.
/// </summary>
public class CamelAlpacaSpitFightInteraction : BasePetInteraction
{
    public override string InteractionName => "CamelAlpacaSpitFight";

    // =================================================================
    // ▼▼▼ [수정됨] 인스펙터에서 조절 가능한 설정 변수들 ▼▼▼
    // =================================================================
    [Header("Visual Effects")]
    [Tooltip("침 뱉기 효과로 사용될 프리팹입니다. (파티클 시스템 등)")]
    public GameObject spitPrefab;
    [Tooltip("침에 맞았을 때 표시될 효과 프리팹입니다.")]
    public GameObject hitEffectPrefab;

    [Header("Fight Settings")]
    [Tooltip("싸움을 시작할 때 두 펫 사이의 거리입니다.")]
    public float fightDistance = 7f;
    [Tooltip("침이 날아가는 데 걸리는 시간입니다.")]
    public float spitTravelDuration = 0.7f;
    [Tooltip("공격을 주고받는 횟수입니다.")]
    public int spitRounds = 3;
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

    // 상호작용이 가능한 조합인지 확인합니다. (낙타와 알파카)
    public override bool CanInteract(PetController pet1, PetController pet2)
    {
        return (pet1.PetType == PetType.Camel && pet2.PetType == PetType.Alpaca) ||
               (pet1.PetType == PetType.Alpaca && pet2.PetType == PetType.Camel);
    }

    /// <summary>
    /// 상호작용의 전체 흐름을 관리하는 메인 코루틴입니다.
    /// </summary>
    protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] {pet1.petName}와(과) {pet2.petName}의 침 뱉기 싸움 시작!");

        // 역할 식별
        PetController camel = (pet1.PetType == PetType.Camel) ? pet1 : pet2;
        PetController alpaca = (pet1.PetType == PetType.Alpaca) ? pet1 : pet2;

        // NavMeshAgent 준비 상태 확인
        yield return StartCoroutine(WaitUntilAgentIsReady(camel, agentSafetyTimeout));
        yield return StartCoroutine(WaitUntilAgentIsReady(alpaca, agentSafetyTimeout));

        if (!IsAgentSafelyReady(camel) || !IsAgentSafelyReady(alpaca))
        {
            Debug.LogError($"[{InteractionName}] NavMeshAgent 준비 실패로 상호작용을 중단합니다.");
            EndInteraction(camel, alpaca);
            yield break;
        }

        // 펫들의 원래 상태 저장
        PetOriginalState camelState = new PetOriginalState(camel);
        PetOriginalState alpacaState = new PetOriginalState(alpaca);

        try
        {
            // 각 단계별 코루틴 순차 실행
            yield return StartCoroutine(MeetAndConfrontPhase(camel, alpaca));
            yield return StartCoroutine(SpitExchangePhase(camel, alpaca));
            yield return StartCoroutine(DetermineWinnerPhase(camel, alpaca));
        }
        finally
        {
            // 상호작용이 어떤 이유로든 종료될 때 항상 정리 작업을 수행합니다.
            Debug.Log($"[{InteractionName}] 상호작용 정리 시작.");
            EndInteraction(camel, alpaca);
            Debug.Log($"[{InteractionName}] 상호작용 정리 완료.");
        }
    }

    #region Interaction Phases

    /// <summary>
    /// 1. 두 펫이 만나서 대치하는 단계
    /// </summary>
    private IEnumerator MeetAndConfrontPhase(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] 1단계: 대치");

        // 감정 표현 (화난 표정)
        pet1.ShowEmotion(EmotionType.Angry, 30f);
        pet2.ShowEmotion(EmotionType.Angry, 30f);

        // 서로 마주볼 위치 계산
        Vector3 direction = (pet2.transform.position - pet1.transform.position).normalized;
        if (direction == Vector3.zero) direction = pet1.transform.forward;
        Vector3 midpoint = (pet1.transform.position + pet2.transform.position) / 2f;

        Vector3 pet1TargetPos = midpoint - direction * (fightDistance / 2f);
        Vector3 pet2TargetPos = midpoint + direction * (fightDistance / 2f);

        // 계산된 위치로 이동
        yield return StartCoroutine(MoveToPositions(pet1, pet2, pet1TargetPos, pet2TargetPos, 10f));

        // 서로 부드럽게 마주보게 회전
        yield return StartCoroutine(SmoothlyLookAtEachOther(pet1, pet2, 0.5f));

        // 긴장감 조성을 위한 잠시 대기
        yield return new WaitForSeconds(1.0f);
    }

    /// <summary>
    /// 2. 침 뱉기 공방을 주고받는 단계
    /// </summary>
    private IEnumerator SpitExchangePhase(PetController camel, PetController alpaca)
    {
        Debug.Log($"[{InteractionName}] 2단계: 침 뱉기 공방");

        for (int i = 0; i < spitRounds; i++)
        {
            // 번갈아 가며 공격
            PetController attacker = (i % 2 == 0) ? camel : alpaca;
            PetController target = (attacker == camel) ? alpaca : camel;

            Debug.Log($"[{InteractionName}] 라운드 {i + 1}: {attacker.petName}의 공격!");

            // 공격 애니메이션
            yield return StartCoroutine(attacker.GetComponent<PetAnimationController>()
                .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, attackAnimationDuration, true, false));

            // 침 발사
            StartCoroutine(SpitProjectileCoroutine(attacker, target));

            // 잠시 후 타겟 반응
            yield return new WaitForSeconds(spitTravelDuration);

            // 확률적으로 회피 또는 피격
            if (Random.value < dodgeChance)
            {
                // 회피 성공
                Debug.Log($"[{InteractionName}] {target.petName}이(가) 공격을 회피했습니다!");
                target.ShowEmotion(EmotionType.Happy, 2f);
                yield return StartCoroutine(target.GetComponent<PetAnimationController>()
                    .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, dodgeAnimationDuration, true, false));
            }
            else
            {
                // 피격
                Debug.Log($"[{InteractionName}] {target.petName}이(가) 침에 맞았습니다!");
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, GetPetHeadPosition(target), Quaternion.identity);
                }
                yield return StartCoroutine(target.GetComponent<PetAnimationController>()
                    .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Damage, damageAnimationDuration, true, false));
            }

            yield return new WaitForSeconds(1.0f); // 다음 턴을 위한 딜레이
        }
    }

    /// <summary>
    /// 3. 승패를 결정하고 마무리하는 단계
    /// </summary>
    private IEnumerator DetermineWinnerPhase(PetController pet1, PetController pet2)
    {
        Debug.Log($"[{InteractionName}] 3단계: 승패 결정");

        // 랜덤으로 승자 결정
        PetController winner = DetermineWinner(pet1, pet2, 0.5f);
        PetController loser = (winner == pet1) ? pet2 : pet1;

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
    /// 침 발사체를 생성하고 목표를 향해 포물선으로 날려 보내는 코루틴
    /// </summary>
    private IEnumerator SpitProjectileCoroutine(PetController attacker, PetController target)
    {
        if (spitPrefab == null)
        {
            Debug.LogWarning("[CamelAlpacaSpitFight] 침 프리팹이 할당되지 않았습니다.");
            yield break;
        }

        Vector3 startPos = GetPetHeadPosition(attacker);
        GameObject spitInstance = Instantiate(spitPrefab, startPos, Quaternion.identity);

        float elapsedTime = 0f;
        while (elapsedTime < spitTravelDuration)
        {
            if (target == null || spitInstance == null) break;

            Vector3 targetPos = GetPetHeadPosition(target);
            float t = elapsedTime / spitTravelDuration;

            // 포물선 궤적 계산
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * 1.5f; // 위로 볼록한 포물선

            spitInstance.transform.position = currentPos;
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (spitInstance != null)
        {
            Destroy(spitInstance);
        }
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