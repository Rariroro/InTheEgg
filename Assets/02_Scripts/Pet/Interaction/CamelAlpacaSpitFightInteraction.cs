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
 // ▼▼▼ 이 부분을 클래스 상단에 추가해주세요 ▼▼▼
    [Header("Fine-Tuning Settings")]
    [Tooltip("침 발사 위치를 펫의 앞쪽으로 미세 조정합니다. (단위: 미터)")]
    public float spitForwardOffset = 0.5f;

    [Tooltip("침 발사 위치를 위아래로 미세 조정합니다. (단위: 미터)")]
    public float spitUpwardOffset = 0.0f;
    [Header("Visual Effects")]
    [Tooltip("침을 뱉을 때 입에서 발사되는 효과 프리팹입니다.")]
    public GameObject spitEmissionPrefab; // spitPrefab -> spitEmissionPrefab 으로 이름 변경
    [Tooltip("침에 맞았을 때 몸에서 나타나는 피격 효과 프리팹입니다.")]
    public GameObject spitHitPrefab;      // hitEffectPrefab -> spitHitPrefab 으로 이름 변경


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

   
    // SpitExchangePhase 코루틴에서 코루틴 호출 부분만 수정합니다.
    private IEnumerator SpitExchangePhase(PetController camel, PetController alpaca)
    {
        Debug.Log($"[{InteractionName}] 2단계: 침 뱉기 공방");

        for (int i = 0; i < spitRounds; i++)
        {
            PetController attacker = (i % 2 == 0) ? camel : alpaca;
            PetController target = (attacker == camel) ? alpaca : camel;

            Debug.Log($"[{InteractionName}] 라운드 {i + 1}: {attacker.petName}의 공격!");

            yield return StartCoroutine(attacker.GetComponent<PetAnimationController>()
                .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Attack, attackAnimationDuration, true, false));

            // ▼▼▼ [수정] 호출하는 코루틴 이름 변경 ▼▼▼
            StartCoroutine(SpitEffectCoroutine(attacker, target));

            yield return new WaitForSeconds(spitTravelDuration);

            if (Random.value < dodgeChance)
            {
                Debug.Log($"[{InteractionName}] {target.petName}이(가) 공격을 회피했습니다!");
                target.ShowEmotion(EmotionType.Happy, 2f);
                yield return StartCoroutine(target.GetComponent<PetAnimationController>()
                    .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Jump, dodgeAnimationDuration, true, false));
            }
            else
            {
                Debug.Log($"[{InteractionName}] {target.petName}이(가) 침에 맞았습니다!");
                // 피격 효과 생성은 이제 SpitEffectCoroutine이 담당하므로 여기서 생성 코드를 제거합니다.
                yield return StartCoroutine(target.GetComponent<PetAnimationController>()
                    .PlayAnimationWithCustomDuration(PetAnimationController.PetAnimationType.Damage, damageAnimationDuration, true, false));
            }

            yield return new WaitForSeconds(1.0f);
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