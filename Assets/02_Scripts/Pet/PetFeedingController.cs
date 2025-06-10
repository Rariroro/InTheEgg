// ---------------------------------------------------------------------------
// PetFeedingController (최적화 + 상세 주석)
// ---------------------------------------------------------------------------
// 이 스크립트는 펫(PetController)의 배고픔을 관리하고, 주변의 먹이‧먹이공간을
// 탐색하여 이동/섭취/행동 변화를 제어합니다.
//   1) 정적 캐시(List<GameObject>)를 활용해 장면 전체의 Food/FeedingArea를
//      모든 펫이 공유 → FindGameObjectsWithTag 호출 최소화
//   2) sqrMagnitude로 거리 비교 → sqrt 비용 절감
//   3) 일정 간격(detectionInterval, hungerCheckInterval 등)으로만 연산하여
//      Update 호출 부하 감소
//   4) 퍼스낼리티, 배고픔 단계, 애정도에 따른 속도·애니메이션·감정 처리
//   5) 모이기(집결) 상태와의 충돌 방지, 앉기(sit) 등 특수 행동 포함
//
// *** 모든 주석은 한국어로 상세 설명하며, Unity 상호 작용/최적화 포인트에
//     초점을 맞추었습니다. 실제 프로젝트에서는 필요에 따라 난이도별로
//     주석을 간소화할 수 있습니다.
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PetFeedingController : MonoBehaviour
{
    //---------------------------------------------------------------------
    // 🐾 1. 필드 선언부
    //---------------------------------------------------------------------
    // ── 외부 컴포넌트 참조 ─────────────────────────────────────────────
    private PetController petController;      // 펫 상태·모션·NavMeshAgent 소유 클래스

    // ── 현재 타깃 ────────────────────────────────────────────────────
    private GameObject targetFood;            // 목표 음식
    private GameObject targetFeedingArea;     // 목표 먹이공간(급식소)

    // ── 탐색 / 거리 파라미터 ─────────────────────────────────────────
    private float detectionRadius = 100f;     // 음식·급식소 탐색 반경
    private float eatingDistance  = 4f;       // 음식과 붙어서 먹기 시작할 거리
    private float feedingAreaDistance = 2f;   // 급식소에 들어왔다고 간주할 거리

    // ── 상태 플래그 ─────────────────────────────────────────────────
    private bool  isEating   = false;         // 현재 먹는 중?
    private bool  isSitting  = false;         // 배고픔 때문에 앉아버린 상태?

    // ── 배고픔 / 애정도 파라미터 ─────────────────────────────────────
    private float hungerIncreaseRate = 0.1f;  // 배고픔 자연 증가량(/s)
    private float affectionDecreaseRate = 0.05f; // 굶주림 시 애정도 감소량(/tick)
    private float affectionIncreaseSmall = 2f;   // 간식 먹을 때 애정도 +
    private float affectionIncreaseLarge = 5f;   // 급식소에서 먹을 때 애정도 ++

    // ── 시간 캐싱용 ──────────────────────────────────────────────────
    private float lastHungryEmotionTime  = 0f;
    private float hungryEmotionInterval  = 35f;  // n초마다 "배고파" 이모션

    private float lastDetectionTime      = 0f;   // 탐색 쿨다운 용도
    private float detectionInterval      = 1f;   // 매 프레임 말고 n초 간격으로만 탐색

    private float lastHungerCheck        = 0f;   // 속도/행동 조절 체크 시점
    private float hungerCheckInterval    = 5f;

    private float lastAffectionDecreaseTime = 0f;
    private float affectionDecreaseInterval = 10f;

    // ── 배고픔 임계값 ────────────────────────────────────────────────
    private float veryHungryThreshold    = 80f;  // 배고픔 80%↑ : 속도 저하
    private float extremeHungryThreshold = 95f;  // 배고픔 95%↑ : 앉아버림

    // ── 성격별 배고픔 배율 ────────────────────────────────────────────
    private float lazyHungerModifier    = 0.8f;  // Lazy: 덜 배고픔
    private float playfulHungerModifier = 1.2f;  // Playful: 빨리 배고픔
    private float braveHungerModifier   = 1.0f;  // Brave: 기본
    private float shyHungerModifier     = 0.9f;  // Shy: 살짝 덜 배고픔

    // ── 속도 최소치 ──────────────────────────────────────────────────
    private float minSpeedFactor = 0.3f;         // 굶주려도 최소 30% 속도 보장

    // ── 급식소 레이어 및 태그 ─────────────────────────────────────────
    private int   feedingAreaLayer;              // 필요하다면 LayerMask 사용
    private const string FEEDING_AREA_TAG = "FeedingArea"; // 급식소 태그명

    // ── 애니메이션 인덱스 ────────────────────────────────────────────
    private int sittingAnimationIndex = 5;       // 프로젝트 규칙에 따른 "앉기" index

    //---------------------------------------------------------------------
    // 🐾 2. 정적(Static) 캐시 : 장면 전체를 모든 펫이 공유
    //---------------------------------------------------------------------
    // 장면에 존재하는 Food / FeedingArea 목록을 전역적으로 보관하여
    // FindGameObjectsWithTag 호출을 최소화한다.
    private static List<GameObject> allFoodItems     = new List<GameObject>();
    private static List<GameObject> allFeedingAreas  = new List<GameObject>();

    private static float lastFoodCacheUpdate = 0f;
    private static float foodCacheUpdateInterval = 2f;   // 2초마다 재빌드

    // ── 씬이 새로 로드될 때 캐시 초기화 ──────────────────────────────
    static PetFeedingController()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            allFoodItems.Clear();
            allFeedingAreas.Clear();
            lastFoodCacheUpdate = 0f;
        };
    }

    //---------------------------------------------------------------------
    // 🐾 3. 초기화 & 캐시 구축
    //---------------------------------------------------------------------
    public void Init(PetController controller)
    {
        petController = controller;

        // FeedingArea 전용 레이어를 가져오고, 없다면 Default로 대체
        feedingAreaLayer = LayerMask.GetMask("FeedingArea");
        if (feedingAreaLayer == 0)
        {
            feedingAreaLayer = Physics.DefaultRaycastLayers;
        }

        // 최초 진입 시 즉시 캐시 구축
        UpdateFoodCache();
        UpdateFeedingAreaCache();
    }

    // ---- (정적) 음식 캐시 업데이트 ------------------------------------
    private static void UpdateFoodCache()
    {
        // 아직 interval 이 지나지 않았다면 패스
        if (Time.time - lastFoodCacheUpdate < foodCacheUpdateInterval)
            return;

        allFoodItems.Clear();
        try
        {
            // 태그 기반 검색 (예외: 태그가 없으면 UnityException 발생)
            GameObject[] foods = GameObject.FindGameObjectsWithTag("Food");
            allFoodItems.AddRange(foods);
        }
        catch (UnityException)
        {
            // 프로젝트에 Food 태그가 없으면 무시 (런타임 에러 방지)
        }

        lastFoodCacheUpdate = Time.time;
    }

    // ---- (인스턴스) 급식소 캐시 업데이트 ------------------------------
    private void UpdateFeedingAreaCache()
    {
        // 이미 비어 있지 않다면 그대로 재사용 (급식소는 대개 고정)
        if (allFeedingAreas.Count > 0)
            return;

        GameObject[] areas = GameObject.FindGameObjectsWithTag(FEEDING_AREA_TAG);
        allFeedingAreas.Clear();
        allFeedingAreas.AddRange(areas);
    }

    //---------------------------------------------------------------------
    // 🐾 4. 메인 루프 : UpdateFeeding()
    //---------------------------------------------------------------------
    // ① 자연 배고픔 증가 & 감정 표시
    // ② 배고픔이 임계값을 넘으면 음식/급식소 탐색 로직 트리거
    // ③ 목표물이 정해지면 HandleMovementToTarget()이 NavMesh 이동 담당
    //---------------------------------------------------------------------
    public void UpdateFeeding()
    {
        // "모이기"(집단 호출) 중이면 먹이 행동 X
        if (petController.isGathering || petController.isGathered) return;
  // [수정 1] 수면 행동에 우선순위를 부여하는 로직 추가
        var sleepingController = petController.GetComponent<PetSleepingController>();
        if (sleepingController != null && sleepingController.IsSleepingOrSeeking())
        {
            // 펫이 잠자러 가는 중이면, 식사 행동을 하지 않음
            // 만약 음식 목표가 있었다면 취소
            if (targetFood != null || targetFeedingArea != null)
            {
                targetFood = null;
                targetFeedingArea = null;
                if (petController.agent.hasPath)
                {
                    petController.agent.ResetPath();
                }
            }
            return;
        }
        // ---- 배고픔 자연 증가 -----------------------------------------
        if (!isEating)
        {
            float personalityHungerModifier = GetPersonalityHungerModifier();
            petController.hunger += Time.deltaTime * hungerIncreaseRate * personalityHungerModifier;
            petController.hunger = Mathf.Clamp(petController.hunger, 0f, 100f);

            // 일정 배고픔 이상 + 쿨다운 → 배고파 감정 표시
            if (petController.hunger > 60f &&
                Time.time - lastHungryEmotionTime > hungryEmotionInterval)
            {
                petController.ShowEmotion(EmotionType.Hungry, 5f);
                lastHungryEmotionTime = Time.time;
            }

            // 속도/행동 업데이트 (N초 간격)
            if (Time.time - lastHungerCheck > hungerCheckInterval)
            {
                UpdateBehaviorBasedOnHunger();
                lastHungerCheck = Time.time;
            }

            // 100% 배고픔 유지 시 애정도 하락
            if (petController.hunger >= 100f &&
                Time.time - lastAffectionDecreaseTime > affectionDecreaseInterval)
            {
                petController.affection = Mathf.Max(0f, petController.affection - affectionDecreaseRate);
                lastAffectionDecreaseTime = Time.time;
                Debug.Log($"{petController.petName} 애정도 감소: {petController.affection:F1}");
            }

            // ---- 음식/급식소 탐색 --------------------------------------
           if (petController.hunger > 70f && 
            targetFood == null && 
            targetFeedingArea == null && 
            !isEating &&
            Time.time - lastDetectionTime > detectionInterval)
        {
            lastDetectionTime = Time.time;
            
            // ★ 음식 아이템 먼저 탐지
            DetectFoodOptimized();

            // ★ 못 찾았으면 음식 구역 탐지
            if (targetFood == null)
            {
                DetectFeedingAreaOptimized();
            }

                // 목표가 생겼고, 혹시 앉아 있었다면 즉시 일어남
                if ((targetFood != null || targetFeedingArea != null) && isSitting)
                {
                    StopSitting();
                }
            }
        }

        // ---- 목표물 이동 & 섭취 처리 -----------------------------------
        HandleMovementToTarget();
    }
  // [수정 2] 현재 식사 관련 행동 중인지 외부에서 확인할 수 있는 메서드 추가
    public bool IsEatingOrSeeking()
    {
        // 현재 밥을 먹고 있거나, 음식을 찾아가는 중이면 true를 반환
        return isEating || (targetFood != null) || (targetFeedingArea != null);
    }
    //---------------------------------------------------------------------
    // 🐾 5. 탐색 로직 (급식소 / 음식)
    //---------------------------------------------------------------------
    // NavMesh 이동 전에 최적 목표를 산출한다.

    // --- 5‑1. 급식소 탐색 ------------------------------------------------
    private void DetectFeedingAreaOptimized()
    {
        GameObject nearestArea = null;
        float       nearestDistSqr = float.MaxValue;
        Vector3     myPos = transform.position;

        // 이미 캐시된 급식소 리스트 순회
        for (int i = allFeedingAreas.Count - 1; i >= 0; i--)
        {
            GameObject area = allFeedingAreas[i];
            if (area == null) { allFeedingAreas.RemoveAt(i); continue; }

            float distSqr = (area.transform.position - myPos).sqrMagnitude;
            if (distSqr < detectionRadius * detectionRadius && distSqr < nearestDistSqr)
            {
                nearestArea = area;
                nearestDistSqr = distSqr;
            }
        }

        if (nearestArea != null)
        {
            targetFeedingArea = nearestArea;
            petController.ShowEmotion(EmotionType.Hungry, 5f);

            // NavMeshAgent가 활성화되어 있다면 즉시 경로 지정
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                Vector3 feedingPosition = GetPositionInFeedingArea(nearestArea);
                petController.agent.SetDestination(feedingPosition);
            }
            Debug.Log($"{petController.petName} 먹이 구역 발견: 거리 {Mathf.Sqrt(nearestDistSqr):F1}m");
        }
    }

    // --- 5‑2. 일반 음식 탐색 -------------------------------------------
    private void DetectFoodOptimized()
    {
        UpdateFoodCache(); // 정적 캐시 갱신 (필요 시)

        GameObject nearestFood = null;
        float       nearestDistSqr = float.MaxValue;
        Vector3     myPos = transform.position;
        float       detectionRadiusSqr = detectionRadius * detectionRadius;

        for (int i = allFoodItems.Count - 1; i >= 0; i--)
        {
            GameObject food = allFoodItems[i];
            if (food == null) { allFoodItems.RemoveAt(i); continue; }

            float distSqr = (food.transform.position - myPos).sqrMagnitude;
            if (distSqr < detectionRadiusSqr && distSqr < nearestDistSqr)
            {
                nearestFood = food;
                nearestDistSqr = distSqr;
            }
        }

        if (nearestFood != null)
        {
            targetFood = nearestFood;
            petController.ShowEmotion(EmotionType.Hungry, 5f);

            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                petController.agent.SetDestination(targetFood.transform.position);
            }
            Debug.Log($"{petController.petName} 음식 발견: 거리 {Mathf.Sqrt(nearestDistSqr):F1}m");
        }
    }

    //---------------------------------------------------------------------
    // 🐾 6. 목표물 이동 & 섭취 트리거
    //---------------------------------------------------------------------
    private void HandleMovementToTarget()
    {
        // 앉아있거나 이미 먹는 중이면 패스
        if (isEating || isSitting) return;

        // NavMeshAgent 상태 확인
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh) return;

        // ---- 급식소 목표 --------------------------------------------------
        if (targetFeedingArea != null)
        {
            if (targetFeedingArea == null) { targetFeedingArea = null; return; }

            float distSqr = (targetFeedingArea.transform.position - transform.position).sqrMagnitude;
            if (distSqr <= feedingAreaDistance * feedingAreaDistance ||
                (petController.agent.remainingDistance <= feedingAreaDistance && !petController.agent.pathPending))
            {
                StartCoroutine(EatAtFeedingArea());
            }
            else if (petController.agent.destination != targetFeedingArea.transform.position)
            {
                petController.agent.SetDestination(targetFeedingArea.transform.position);
            }
        }
        // ---- 일반 음식 목표 --------------------------------------------
        else if (targetFood != null)
        {
            if (targetFood == null) { targetFood = null; return; }

            float distSqr = (targetFood.transform.position - transform.position).sqrMagnitude;
            if (distSqr <= eatingDistance * eatingDistance)
            {
                StartCoroutine(EatFood());
            }
            else if (petController.agent.destination != targetFood.transform.position)
            {
                petController.agent.SetDestination(targetFood.transform.position);
            }
        }
    }

    //---------------------------------------------------------------------
    // 🐾 7. 보조 메서드 : 배고픔 계수, 행동 업데이트
    //---------------------------------------------------------------------
    private float GetPersonalityHungerModifier()
    {
        switch (petController.personality)
        {
            case PetAIProperties.Personality.Lazy:   return lazyHungerModifier;
            case PetAIProperties.Personality.Playful:return playfulHungerModifier;
            case PetAIProperties.Personality.Brave:  return braveHungerModifier;
            case PetAIProperties.Personality.Shy:    return shyHungerModifier;
            default:                                return 1.0f;
        }
    }

    private void UpdateBehaviorBasedOnHunger()
    {
        if (petController.isGathering || petController.isGathered) return;

        if (petController.agent != null && petController.agent.enabled)
        {
            // 95%↑ : 앉아서 대기 (극심한 배고픔)
            if (petController.hunger >= extremeHungryThreshold && !isSitting)
            {
                StartCoroutine(SitDueToHunger());
                return;
            }
            // 80%↑ : 이동 속도 감소
            else if (petController.hunger >= veryHungryThreshold)
            {
                float hungerFactor = 1f - ((petController.hunger / 100f) * (1f - minSpeedFactor));
                petController.agent.speed = petController.baseSpeed * hungerFactor;
            }
            // 그 이하 : 정상 속도 & 앉아 있었다면 기상
            else
            {
                if (isSitting && petController.hunger < extremeHungryThreshold)
                    StopSitting();

                petController.agent.speed = petController.baseSpeed;
            }
        }
    }

    //---------------------------------------------------------------------
    // 🐾 8. (정적) 음식 아이템 등록/해제 API
    //---------------------------------------------------------------------
    // 프로젝트 외부(드롭, 생성, 파괴 이벤트)에서 호출하여 캐시를 동기화
    public static void RegisterFoodItem(GameObject food)
    {
        if (food != null && !allFoodItems.Contains(food))
            allFoodItems.Add(food);
    }

    public static void UnregisterFoodItem(GameObject food)
    {
        if (food != null)
            allFoodItems.Remove(food);
    }

    //---------------------------------------------------------------------
    // 🐾 9. 코루틴 : Extreme Hunger → 앉기 / 급식소에서 먹기 / 일반 음식 먹기
    //---------------------------------------------------------------------
    private IEnumerator SitDueToHunger()
    {
        if (isSitting) yield break; // 중복 방지

        isSitting = true;
        petController.StopMovement();
        petController.ShowEmotion(EmotionType.Hungry, 5f);

        // (선택) 애니메이션 컨트롤러가 있으면 앉기 애니메이션 재생
        PetAnimationController animController = petController.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            yield return StartCoroutine(animController.PlayAnimationWithCustomDuration(
                sittingAnimationIndex, 1f, false, false));
        }

        Debug.Log($"{petController.petName} 너무 배고파서 앉음. 배고픔: {petController.hunger:F0}");

        float lastFoodSearchTime = 0f;
        float foodSearchInterval = 5f;

        // hunger < extremeHungryThreshold 로 떨어질 때까지 대기 루프
        while (petController.hunger >= extremeHungryThreshold)
        {
            // 집결 명령이 오면 즉시 기상
            if (petController.isGathering)
            {
                StopSitting();
                yield break;
            }

            // 주기적으로 배고파 이모션
            if (Time.time - lastHungryEmotionTime > hungryEmotionInterval * 0.5f)
            {
                petController.ShowEmotion(EmotionType.Hungry, 5f);
                lastHungryEmotionTime = Time.time;
            }

            // 주기적으로 주변 다시 탐색
            if (Time.time - lastFoodSearchTime > foodSearchInterval)
            {
                DetectFeedingAreaOptimized();
                if (targetFeedingArea == null) DetectFoodOptimized();

                if (targetFood != null || targetFeedingArea != null)
                {
                    StopSitting();
                    break;
                }
                lastFoodSearchTime = Time.time;
            }

            // 앉기 애니메이션 유지 보정
            if (petController.animator != null &&
                petController.animator.GetInteger("animation") != sittingAnimationIndex)
            {
                petController.animator.SetInteger("animation", sittingAnimationIndex);
            }
            yield return new WaitForSeconds(1f);
        }

        // 배고픔 해소됐으면 기상
        StopSitting();
    }

    // ---- 기상 처리 -----------------------------------------------------
    private void StopSitting()
    {
        if (!isSitting) return;

        isSitting = false;
        PetAnimationController animController = petController.GetComponent<PetAnimationController>();

        // 기본 애니메이션으로 복귀
        if (petController.animator != null)
            petController.animator.SetInteger("animation", 0);

        if (animController != null)
            animController.StopContinuousAnimation();

        petController.ResumeMovement();
        petController.GetComponent<PetMovementController>().SetRandomDestination();
    }

    //---------------------------------------------------------------------
    // 🐾 10. 급식소에서 먹기 코루틴
    //---------------------------------------------------------------------
    private IEnumerator EatAtFeedingArea()
    {
        isEating = true;
        petController.StopMovement();

        // 급식소 방향 바라보기 (부드러운 회전)
        if (petController.petModelTransform != null && targetFeedingArea != null)
        {
            Vector3 lookDir = targetFeedingArea.transform.position - petController.transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                float t = 0f;
                Quaternion startRot = petController.petModelTransform.rotation;
                while (t < 1f)
                {
                    t += Time.deltaTime * 2f;
                    petController.petModelTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                    yield return null;
                }
            }
        }

        // ---------------- 실제 섭취 ----------------
        petController.hunger   = Mathf.Max(0f, petController.hunger - 60f);
        petController.affection = Mathf.Min(100f, petController.affection + affectionIncreaseLarge);
        Debug.Log($"{petController.petName} 먹이 구역 식사 완료. 배고픔: {petController.hunger:F0}, 애정도: {petController.affection:F0}");

        petController.HideEmotion();
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>()
            .PlayAnimationWithCustomDuration(4, 5f, true, false));

        petController.ShowEmotion(EmotionType.Happy, 3f);

        targetFeedingArea = null;
        isEating = false;

        // 기상 or 랜덤 이동 재개
        if (isSitting) StopSitting();
        else
        {
            petController.ResumeMovement();
            petController.GetComponent<PetMovementController>().SetRandomDestination();
        }
    }

    //---------------------------------------------------------------------
    // 🐾 11. 일반 음식 먹기 코루틴
    //---------------------------------------------------------------------
    private IEnumerator EatFood()
    {
        isEating = true;
        petController.StopMovement();

        // 음식 쪽으로 시선 돌리기
        if (petController.petModelTransform != null && targetFood != null)
        {
            Vector3 lookDir = targetFood.transform.position - petController.transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                float t = 0f;
                Quaternion startRot = petController.petModelTransform.rotation;
                while (t < 1f)
                {
                    t += Time.deltaTime * 2f;
                    petController.petModelTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                    yield return null;
                }
            }
        }

        // 배고픔 & 애정도 갱신 (일반 음식은 효과가 더 작음)
        petController.hunger   = Mathf.Max(0f, petController.hunger - 30f);
        petController.affection = Mathf.Min(100f, petController.affection + affectionIncreaseSmall);
        Debug.Log($"{petController.petName} 음식 섭취. 배고픔: {petController.hunger:F0}, 애정도: {petController.affection:F0}");

        petController.HideEmotion();
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(4));

        // 먹은 음식 파괴 & 캐시 해제
        if (targetFood != null)
        {
            UnregisterFoodItem(targetFood);
            Destroy(targetFood);
            targetFood = null;
        }

        petController.ShowEmotion(EmotionType.Happy, 3f);

        if (isSitting) StopSitting();
        else petController.GetComponent<PetMovementController>().SetRandomDestination();

        isEating = false;
    }

    //---------------------------------------------------------------------
    // 🐾 12. 외부 API : 급식소 강제 지정 / 강제 급여
    //---------------------------------------------------------------------
    // (예) UI 버튼으로 플레이어가 급식소를 눌렀을 때 호출
    public void FeedAtArea(GameObject feedingArea)
    {
        if (!isEating && feedingArea != null)
        {
            targetFeedingArea = feedingArea;
            if (isSitting) StopSitting();

            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                Vector3 feedingPos = GetPositionInFeedingArea(feedingArea);
                petController.agent.SetDestination(feedingPos);
                petController.ShowEmotion(EmotionType.Hungry, 3f);
            }
        }
    }

    // 개발/디버그용: 즉시 배고픔 감소 (예: 아이템 사용)
    public void ForceFeed(float amount)
    {
        petController.hunger = Mathf.Max(0f, petController.hunger - amount);
        petController.affection = Mathf.Min(100f, petController.affection + affectionIncreaseSmall);

        StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(4));
        petController.ShowEmotion(EmotionType.Happy, 3f);

        if (isSitting) StopSitting();
    }

    //---------------------------------------------------------------------
    // 🐾 13. Utility : 급식소 Collider 내부에서 NavMesh 위치 샘플링
    //---------------------------------------------------------------------
    private Vector3 GetPositionInFeedingArea(GameObject feedingArea)
    {
        Collider col = feedingArea.GetComponent<Collider>();
        if (col == null) return feedingArea.transform.position; // 콜라이더가 없다면 중심 사용

        // Box / Sphere / 기타 케이스별 중심 좌표 계산
        Vector3 center;
        if (col is BoxCollider box)
            center = feedingArea.transform.TransformPoint(box.center);
        else if (col is SphereCollider sphere)
            center = feedingArea.transform.TransformPoint(sphere.center);
        else
            center = col.bounds.center;

        // 1) 중심에서 아래로 Raycast → 지면 높이 
        if (Physics.Raycast(center, Vector3.down, out RaycastHit hit, 10f))
        {
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                return navHit.position;
        }

        // 2) 실패 시 중심점 근처 NavMesh 샘플링
        if (NavMesh.SamplePosition(center, out NavMeshHit directHit, 5f, NavMesh.AllAreas))
            return directHit.position;

        // 3) 최후의 수단: XZ 유지, Y 는 펫 높이(원본)
        return new Vector3(center.x, petController.transform.position.y, center.z);
    }
}
