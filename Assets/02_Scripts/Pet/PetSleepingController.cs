// PetSleepingController.cs - 'Tree' 서식지 펫 로직 추가 버전
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
    private int sleepingAreaLayer;

    // ★★★ 추가: 나무 펫 수면 관련 상태 플래그
    private bool isSeekingTreeToSleep = false;

    // 성격별 강제 수면 임계값
    private float forceSleepThreshold = 95f;
    private float lazyForceSleepThreshold = 80f;
    private float playfulForceSleepThreshold = 95f;
    private float braveForceSleepThreshold = 100f;
    private float shyForceSleepThreshold = 90f;

    public void Init(PetController controller)
    {
        petController = controller;
        sleepingAreaLayer = LayerMask.GetMask("SleepingArea");
    }

    public void UpdateSleeping()
    {

                if (petController.isActionLocked) return;

        // 공통 방해 조건 (상호작용, 들기 등)
        if (petController.isGathering || petController.isInteracting || petController.isHolding ||
            (petController.GetComponent<PetFeedingController>() != null && petController.GetComponent<PetFeedingController>().IsEatingOrSeeking()))
        {
            if (isSleeping) InterruptSleep();
            return;
        }

        // 이미 자고 있다면 로직 중단
        if (isSleeping) return;

        // 졸음 수치 증가
        petController.sleepiness = Mathf.Clamp(petController.sleepiness + Time.deltaTime * sleepIncreaseRate, 0, 100);

        // ★★★ 'Tree' 서식지 펫을 위한 특별 로직 분기 ★★★
        if (petController.habitat == PetAIProperties.Habitat.Tree)
        {
            HandleTreePetSleeping();
            return; // Tree 펫 로직을 수행했으면 아래 일반 로직은 건너뜀
        }

        // --- 기존 수면 로직 (Tree 서식지가 아닌 펫들) ---
        if (petController.sleepiness > 70f && targetSleepingArea == null)
        {
            if (petController.habitat == PetAIProperties.Habitat.Field)
            {
                StartCoroutine(SleepInPlace(true));
                return;
            }
            DetectSleepingArea();
        }

        float personalityThreshold = GetPersonalityForceSleepThreshold();
        if (petController.sleepiness >= personalityThreshold && targetSleepingArea == null)
        {
            StartCoroutine(ForceSleepAtCurrentLocation());
        }

        HandleMovementToTarget();
    }

    /// <summary>
    /// ★★★ 새로 추가: 'Tree' 서식지 펫의 수면 로직을 처리합니다.
    /// </summary>
    private void HandleTreePetSleeping()
    {
        // 졸음이 70%를 넘었을 때 행동 개시
        if (petController.sleepiness > 70f)
        {
            // 1. 이미 나무에 올라가 쉬고 있는 경우
            if (petController.isClimbingTree)
            {
                // 그 자리에서 바로 잠자기 시작
                StartCoroutine(SleepInTree());
            }
            // 2. 땅에 있고, 아직 나무를 찾기 시작하지 않은 경우
            else if (!isSeekingTreeToSleep)
            {
                // 잠을 자기 위해 나무를 찾고 올라가는 전체 과정을 시작
                StartCoroutine(FindAndClimbTreeToSleep());
            }
        }

        // 3. 나무를 못 찾은 상태에서 졸음이 한계에 도달한 경우
        float personalityThreshold = GetPersonalityForceSleepThreshold();
        if (petController.sleepiness >= personalityThreshold && !petController.isClimbingTree && !isSeekingTreeToSleep)
        {
            Debug.Log($"{petController.petName}이(가) 나무를 찾지 못해 땅에서 잠듭니다.");
            StartCoroutine(ForceSleepAtCurrentLocation()); // 불완전 회복 수면
        }
    }

    /// <summary>
    /// ★★★ 새로 추가: 잠잘 나무를 찾고, 올라가서 잠드는 전체 과정을 관리하는 코루틴
    /// </summary>
    private IEnumerator FindAndClimbTreeToSleep()
    {
        isSeekingTreeToSleep = true;
        ResetPetStateForSeeking();

        var treeClimber = petController.GetComponent<PetTreeClimbingController>();
        if (treeClimber != null)
        {
            // 나무에 올라가서 잠을 자는 행동을 위임하고 끝날 때까지 대기
            yield return StartCoroutine(treeClimber.ClimbAndExecuteAction(SleepInTree()));
        }

        isSeekingTreeToSleep = false;
    }

    /// <summary>
    /// ★★★ 새로 추가: 나무 위에서 잠을 자는 코루틴
    /// </summary>
   // PetSleepingController.cs의 SleepInTree 메서드 수정

public IEnumerator SleepInTree()
{
    if (isSleeping) yield break;

    isSleeping = true;
    petController.StopMovement();

    Debug.Log($"{petController.petName}이(가) 나무 위에서 잠을 잡니다.");

    // 수면 애니메이션 재생
    yield return petController.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, sleepDuration, false, false);

    // 피로 완전 회복
    petController.sleepiness = 0f;
    petController.ShowEmotion(EmotionType.Happy, 3f);

    Debug.Log($"{petController.petName}이(가) 나무 위에서 상쾌하게 일어났습니다.");

    isSleeping = false;

    // ★★★ 추가: 잠에서 깬 후 배고픔 체크 ★★★
    if (petController.hunger > 70f)
    {
        Debug.Log($"{petController.petName}이(가) 배가 고파서 나무에서 내려가기로 결정했습니다.");
        
        // 나무에서 즉시 내려가도록 신호 전송
        var treeClimber = petController.GetComponent<PetTreeClimbingController>();
        if (treeClimber != null)
        {
            // 나무에서 내려가기 위해 강제로 쉬는 상태 종료
            yield break; // SleepInTree 종료하면 ClimbAndExecuteAction이 내려가기 시작
        }
    }
    else
    {
        // 배가 고프지 않으면 다시 휴식 애니메이션으로
        var animController = petController.GetComponent<PetAnimationController>();
        animController?.SetContinuousAnimation(5);
    }
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
        {            
            ResetPetStateForSeeking();
            targetSleepingArea = nearestMatchingArea;
            petController.agent.SetDestination(targetSleepingArea.transform.position);
            petController.ResumeMovement();
        }
    }
    
    private void HandleMovementToTarget()
    {
        if (isSleeping || targetSleepingArea == null || !petController.agent.enabled) return;

        if (petController.agent.remainingDistance < sleepingDistance && !petController.agent.pathPending)
        {
            StartCoroutine(SleepInPlace(true)); // 지정된 장소에서 자므로 완전 회복
        }
    }
    
    /// <summary>
    /// 잠을 자기 위해 이동해야 할 때, 다른 행동들을 강제로 중지시킵니다.
    /// </summary>
    private void ResetPetStateForSeeking()
    {
        petController.GetComponent<PetMovementController>()?.ForceStopCurrentBehavior();
        petController.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
        petController.ResumeMovement();
    }

    /// <summary>
    /// 지정된 장소나 들판에서 편안하게 잠을 잡니다. (피로 완전 회복)
    /// </summary>
    private IEnumerator SleepInPlace(bool isProperArea)
    {
        isSleeping = true;
        petController.StopMovement();

        // 수면 애니메이션 재생
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, sleepDuration, true, false));

        if (isProperArea)
        {
            petController.sleepiness = 0f; // 완전 회복
        }
        else
        {
            petController.sleepiness = Mathf.Max(0f, petController.sleepiness - 60f); // 불완전 회복
        }

        petController.ShowEmotion(EmotionType.Happy, 3f);

        // 상태 초기화 및 이동 재개
        targetSleepingArea = null;
        isSleeping = false;
        petController.ResumeMovement();
        petController.GetComponent<PetMovementController>().SetRandomDestination();
    }

    /// <summary>
    /// 너무 졸려서 현재 위치에서 강제로 잠듭니다. (피로 불완전 회복)
    /// </summary>
    private IEnumerator ForceSleepAtCurrentLocation()
    {
        isSleeping = true;
        petController.StopMovement();

        Debug.Log($"{petController.petName}이(가) 너무 졸려서 현재 위치에서 불편하게 잠듭니다.");
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, sleepDuration, true, false));

        // 불완전 회복
        petController.sleepiness = Mathf.Max(0f, petController.sleepiness - 60f);
        petController.ShowEmotion(EmotionType.Sleepy, 3f);

        // 상태 초기화
        isSleeping = false;
        targetSleepingArea = null;
        petController.ResumeMovement();
        petController.GetComponent<PetMovementController>().SetRandomDestination();
    }

    public bool IsSleepingOrSeeking()
    {
        // ★★★ 나무 찾는 상태도 포함
        return isSleeping || (targetSleepingArea != null) || isSeekingTreeToSleep;
    }
    
    public void InterruptSleep()
    {
        if (isSleeping)
        {
            StopAllCoroutines();
            isSleeping = false;
            isSeekingTreeToSleep = false;
            targetSleepingArea = null;
            petController.ShowEmotion(EmotionType.Angry, 3f);
            petController.ResumeMovement();
            petController.GetComponent<PetMovementController>().SetRandomDestination();
            Debug.Log($"{petController.petName}의 잠을 깨웠습니다!");
        }
    }
    
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