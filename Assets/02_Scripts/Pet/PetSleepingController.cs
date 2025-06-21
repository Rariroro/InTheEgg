// PetSleepingController.cs - 완성된 버전
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class PetSleepingController : MonoBehaviour
{
    private PetController petController;
    private GameObject targetSleepingArea;

    // 탐색 및 상태 관련 변수
    private float detectionRadius = 100f;
    private float sleepingDistance = 2f;
    private bool isSleeping = false;
    private float sleepDuration = 15f;
    private float sleepIncreaseRate = 0.1f;
    private float lastSleepyEmotionTime = 0f;
    private float sleepyEmotionInterval = 30f;
    private int sleepingAreaLayer;

    // 졸음 관련 변수
    private float minSpeedFactor = 0.3f;
    private float lastSleepinessCheck = 0f;
    private float sleepinessCheckInterval = 5f;

    // 성격별 강제 수면 임계값
    private float forceSleepThreshold = 95f; // 기본 강제 수면 임계값
    private float lazyForceSleepThreshold = 80f;
    private float playfulForceSleepThreshold = 95f;
    private float braveForceSleepThreshold = 100f;
    private float shyForceSleepThreshold = 90f;

    public void Init(PetController controller)
    {
        petController = controller;
        sleepingAreaLayer = LayerMask.GetMask("SleepingArea"); // 필요시 "SleepingArea" 레이어로 변경 가능
    }

    public void UpdateSleeping()
    {
        if (petController.isGathering || petController.isInteracting || petController.isHolding ||
            (petController.GetComponent<PetFeedingController>() != null && petController.GetComponent<PetFeedingController>().IsEatingOrSeeking()))
        {
            if (isSleeping) InterruptSleep();
            return;
        }

        if (!isSleeping)
        {
            // 졸음 수치 증가
            petController.sleepiness = Mathf.Clamp(petController.sleepiness + Time.deltaTime * sleepIncreaseRate, 0, 100);

            // 졸음이 70%를 넘으면 잠자리 탐색 시작
            if (petController.sleepiness > 70f && targetSleepingArea == null && !isSleeping)
            {
                // ★★★ 'Field' 서식지 펫 특별 처리 ★★★
                if (petController.habitat == PetAIProperties.Habitat.Field)
                {
                    // '들판' 펫은 졸리면 바로 그 자리에서 편안하게 잠듭니다.
                    StartCoroutine(Sleep(true)); // isProperArea = true로 완전 회복
                    return;
                }
                
                // 그 외 서식지 펫은 알맞은 잠자리를 탐색합니다.
                DetectSleepingArea();
            }

            // ★★★ 강제 수면 로직 ★★★
            // 알맞은 잠자리를 못 찾고 졸음이 임계치를 넘으면 아무 데서나 잠듭니다.
            float personalityThreshold = GetPersonalityForceSleepThreshold();
            if (petController.sleepiness >= personalityThreshold && targetSleepingArea == null && !isSleeping)
            {
                StartCoroutine(ForceSleepAtCurrentLocation());
            }
        }

        HandleMovementToTarget();
    }
    
    /// <summary>
    /// ★★★ 수정된 잠자리 탐색 로직 ★★★
    /// 자신의 서식지(Habitat)와 일치하는 가장 가까운 수면 공간을 찾습니다.
    /// </summary>
    private void DetectSleepingArea()
    {
        Collider[] areas = Physics.OverlapSphere(transform.position, detectionRadius, sleepingAreaLayer);
        GameObject nearestMatchingArea = null;
        float nearestDistSqr = detectionRadius * detectionRadius;

        foreach (var areaCollider in areas)
        {
            SleepingArea areaInfo = areaCollider.GetComponent<SleepingArea>();
            // [핵심] SleepingArea 컴포넌트가 있고, 펫의 서식지와 장소의 서식지가 일치하는지 확인
            if (areaInfo != null && areaInfo.habitatType == petController.habitat)
            {
                float distSqr = (areaCollider.transform.position - transform.position).sqrMagnitude;
                if (distSqr < nearestDistSqr)
                {
                    nearestMatchingArea = areaCollider.gameObject;
                    nearestDistSqr = distSqr;
                }
            }
        }

        if (nearestMatchingArea != null)
        {            ResetPetStateForSeeking();

            targetSleepingArea = nearestMatchingArea;
            petController.agent.SetDestination(targetSleepingArea.transform.position);
            petController.ResumeMovement();
        }
    }
   /// <summary>
    /// 펫이 잠자리를 찾아가기 직전에 현재 행동과 애니메이션을 강제로 초기화합니다.
    /// </summary>
    private void ResetPetStateForSeeking()
    {
        // 현재 진행 중인 행동(휴식, 둘러보기 등) 코루틴을 강제로 중단시킵니다.
        var moveController = petController.GetComponent<PetMovementController>();
        moveController?.ForceStopCurrentBehavior();

        // 재생 중인 연속 애니메이션(휴식 등)을 중단하고 Idle 상태로 되돌립니다.
        var animController = petController.GetComponent<PetAnimationController>();
        animController?.StopContinuousAnimation();

        // 휴식 등으로 인해 멈춰있었을 수 있으므로, NavMeshAgent의 이동을 다시 활성화합니다.
        petController.ResumeMovement();
    }
    private void HandleMovementToTarget()
    {
        if (isSleeping || targetSleepingArea == null || !petController.agent.enabled) return;

        if (petController.agent.remainingDistance < sleepingDistance && !petController.agent.pathPending)
        {
            StartCoroutine(Sleep(true)); // 지정된 장소에서 자므로 완전 회복
        }
    }

    /// <summary>
    /// ★★★ 수정된 수면 코루틴 ★★★
    /// </summary>
    /// <param name="isProperArea">알맞은 장소(서식지 일치 또는 Field 펫)에서 자는지 여부</param>
    private IEnumerator Sleep(bool isProperArea)
    {
        isSleeping = true;
        petController.StopMovement();

        // 수면 애니메이션 재생 (PlayAnimationWithCustomDuration 사용)
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, sleepDuration, true, false));

        // ★ 수면의 질에 따라 피로 회복량 조절 ★
        if (isProperArea)
        {
            petController.sleepiness = 0f; // 완전 회복
            Debug.Log($"{petController.petName}이(가) 편안한 잠을 자고 완벽히 회복했습니다.");
        }
        else
        {
            petController.sleepiness = Mathf.Max(0f, petController.sleepiness - 60f); // 60만 회복 (불완전 회복)
            Debug.Log($"{petController.petName}이(가) 불편한 잠을 자고 조금만 회복했습니다.");
        }

        petController.ShowEmotion(EmotionType.Happy, 3f);

        // 상태 초기화 및 이동 재개
        targetSleepingArea = null;
        isSleeping = false;
        petController.ResumeMovement();
        petController.GetComponent<PetMovementController>().SetRandomDestination();
    }

    /// <summary>
    /// 너무 졸려서 현재 위치에서 강제로 잠드는 코루틴
    /// </summary>
    private IEnumerator ForceSleepAtCurrentLocation()
    {
        Debug.Log($"{petController.petName}이(가) 너무 졸려서 현재 위치에서 잠듭니다.");
        yield return StartCoroutine(Sleep(false)); // isProperArea = false로 불완전 회복
    }
    
    // 외부에서 현재 수면 관련 행동 중인지 확인하는 메서드
    public bool IsSleepingOrSeeking()
    {
        return isSleeping || (targetSleepingArea != null);
    }
    
    // 잠을 방해받았을 때 호출
    public void InterruptSleep()
    {
        if (isSleeping)
        {
            StopAllCoroutines();
            isSleeping = false;
            targetSleepingArea = null;
            petController.ShowEmotion(EmotionType.Angry, 3f);
            petController.ResumeMovement();
            petController.GetComponent<PetMovementController>().SetRandomDestination();
            Debug.Log($"{petController.petName}의 잠을 깨웠습니다!");
        }
    }

    // 성격별 강제 수면 임계값을 반환하는 헬퍼 함수
    private float GetPersonalityForceSleepThreshold()
    {
        switch (petController.personality)
        {
            case PetAIProperties.Personality.Lazy: return lazyForceSleepThreshold;
            case PetAIProperties.Personality.Playful: return playfulForceSleepThreshold;
            case PetAIProperties.Personality.Brave: return braveForceSleepThreshold;
            case PetAIProperties.Personality.Shy: return shyForceSleepThreshold;
            default: return forceSleepThreshold;
        }
    }
}