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

    /// <summary>
    /// ★★★ 추가: SleepAction의 OnEnter에서 호출될 메서드입니다.
    /// 잠잘 곳을 찾아 이동을 시작하고, 탐색 성공 여부를 반환합니다.
    /// </summary>
    public bool TryStartSleepingSequence()
    {
        // 다른 중요 행동 중이면 시작하지 않음
        if (petController.isActionLocked || petController.isGathering || petController.isInteracting || petController.isHolding || isSleeping)
        {
            return false;
        }

        if (IsSleepingOrSeeking()) return true; // 이미 잠을 자러 가는 중

        // 졸음 수치 증가 (상태 업데이트는 Action에서 담당하는게 더 좋지만, 편의상 여기에 유지)
        // petController.sleepiness = Mathf.Clamp(petController.sleepiness + Time.deltaTime * sleepIncreaseRate, 0, 100);

        if (petController.sleepiness < 70f) return false;

        // 'Tree' 서식지 펫 로직
        if (petController.habitat == PetAIProperties.Habitat.Tree)
        {
            return HandleTreePetSleeping(); // 잠잘 나무를 찾거나, 이미 나무 위면 자기 시작
        }

        // 일반 펫 로직
        if (targetSleepingArea == null)
        {
            if (petController.habitat == PetAIProperties.Habitat.Field)
            {
                StartCoroutine(SleepInPlace(true));
                return true; // 들판 펫은 바로 잠
            }
            DetectSleepingArea(); // 잠잘 곳 탐색
        }

        // 강제 수면 체크
        float personalityThreshold = GetPersonalityForceSleepThreshold();
        if (petController.sleepiness >= personalityThreshold && targetSleepingArea == null && !isSeekingTreeToSleep)
        {
            StartCoroutine(ForceSleepAtCurrentLocation());
            return true; // 강제로 잠
        }

        return targetSleepingArea != null;
    }

    /// <summary>
    /// ★★★ 추가: SleepAction의 OnUpdate에서 호출될 메서드입니다.
    /// (지상 펫 전용) 목표에 도착했는지 확인하고 잠자기 코루틴을 시작합니다.
    /// </summary>
    public void UpdateMovementToSleep()
    {
        // 나무 펫은 코루틴이 모든 것을 처리하므로 이 메서드는 지상 펫에게만 의미가 있습니다.
        if (petController.habitat == PetAIProperties.Habitat.Tree) return;
        petController.HandleRotation();

        HandleMovementToTarget();
    }


    /// <summary>
    /// ★★★ 수정: 메서드 반환 타입을 void에서 bool로 변경하고, 행동 시작 시 true를 반환하도록 수정합니다.
    /// </summary>
    private bool HandleTreePetSleeping()
    {
        // 졸음이 70%를 넘었을 때 행동 개시
        if (petController.sleepiness > 70f)
        {
            if (petController.isClimbingTree)
            {
                StartCoroutine(SleepInTree());
                return true; // 행동 시작
            }
            else if (!isSeekingTreeToSleep)
            {
                StartCoroutine(FindAndClimbTreeToSleep());
                return true; // 행동 시작
            }
        }

        // 나무를 못 찾은 상태에서 졸음이 한계에 도달한 경우
        float personalityThreshold = GetPersonalityForceSleepThreshold();
        if (petController.sleepiness >= personalityThreshold && !petController.isClimbingTree && !isSeekingTreeToSleep)
        {
            Debug.Log($"{petController.petName}이(가) 나무를 찾지 못해 땅에서 잠듭니다.");
            StartCoroutine(ForceSleepAtCurrentLocation());
            return true; // 행동 시작
        }

        return false; // 아무 행동도 시작하지 않음
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

    // PetSleepingController.cs

    public IEnumerator SleepInTree()
    {
        if (isSleeping) yield break;

        isSleeping = true;
        petController.isActionLocked = true; // 자는 동안에는 다른 액션 방지

        try
        {
            Debug.Log($"{petController.petName}이(가) 나무 위에서 잠을 잡니다.");

            // 수면 애니메이션 재생
            yield return petController.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(5, sleepDuration, false, false);

            // 피로 완전 회복
            petController.sleepiness = 0f;
            petController.ShowEmotion(EmotionType.Happy, 3f);

            Debug.Log($"{petController.petName}이(가) 나무 위에서 상쾌하게 일어났습니다.");
        }
        finally
        {
            isSleeping = false;
            petController.isActionLocked = false; // 잠에서 깨면 액션 잠금 해제
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

    // IsSleepingOrSeeking() 메서드는 계속 유용하게 사용됩니다.
    public bool IsSleepingOrSeeking()
    {
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