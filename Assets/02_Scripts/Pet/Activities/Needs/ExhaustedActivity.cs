using UnityEngine;

/// <summary>
/// 펫이 배고픔으로 완전히 탈진했을 때의 활동
/// 이 상태에서는 주변의 음식을 적극적으로 탐색하고, 발견 시 먹으려고 시도합니다.
/// </summary>
public class ExhaustedActivity : PetActivityAdapter
{
    private readonly PetAnimationController animController;
    private readonly PetFeedingController feedingController;
    
    private float searchTimer = 0f;
    private const float FOOD_SEARCH_INTERVAL = 1.0f; // 1초마다 주변 음식 재탐색
    private bool isTryingToEat = false; // 음식을 발견하고 먹으러 가는 중인지 여부
    
    private const int REST_ANIMATION_INDEX = 5; // 휴식 애니메이션 인덱스
    
    public override string Name => "Exhausted";
    public override bool IsInterruptible => false; // 탈진 상태는 중단 불가
    
    public ExhaustedActivity(PetController petController) : base(petController)
    {
        animController = pet.GetComponent<PetAnimationController>();
        feedingController = pet.GetComponent<PetFeedingController>();
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 플레이어가 직접 선택했을 때는 탈진 상태 진입을 막음
        if (pet.State.IsSelected)
            return false;
            
        // 배고픔 수치가 100 이상일 때 시작
        float hunger = needs != null ? needs.Hunger : pet.Needs.Hunger;
        return hunger >= 100f;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 탈진은 매우 높은 우선순위
        return 50.0f;
    }
    
    
    public override void Start()
    {
        // ★ [Phase 4] PetState를 통한 상태 업데이트
        pet.State.SetEmergencyState(exhausted: true);
        isTryingToEat = false;
        searchTimer = 0f;
        
        Debug.LogWarning($"[ExhaustedActivity] {pet.petName}: 배고픔으로 탈진. 주변의 음식을 찾습니다.");
        
        // 기존의 모든 행동 중지
        pet.StopAllCoroutines();
        pet.GetComponent<PetMovementController>()?.ForceStopCurrentBehavior();
        pet.GetComponent<PetSleepingController>()?.InterruptSleep();
        pet.GetComponent<PetTreeClimbingController>()?.ForceCancelClimbing();
        if (pet.movementController != null)
        {
            pet.movementController.StopMovement();
        }
        
        // 즉시 음식 탐색 시도
        AttemptToEat();
        
        // 음식을 찾지 못했다면, 탈진 상태를 표시
        if (!isTryingToEat)
        {
            animController?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Rest);
            if (pet.emotionController != null)
            {
                pet.emotionController.ShowEmotion(EmotionType.Hungry, 2f);
            }
        }
    }
    
    public override void Update()
    {
        if (isTryingToEat)
        {
            // 음식을 찾아 이동 중이거나 먹는 중인 경우
            feedingController?.UpdateMovementToFood();
        }
        else
        {
            // 아직 음식을 찾지 못했다면, 주기적으로 다시 탐색
            searchTimer += Time.deltaTime;
            if (searchTimer >= FOOD_SEARCH_INTERVAL)
            {
                searchTimer = 0f;
                AttemptToEat();
            }
        }
    }
    
    public override void Stop()
    {
        // ★ [Phase 4] PetState를 통한 상태 업데이트
        pet.State.SetEmergencyState(exhausted: false);
        isTryingToEat = false;
        
        Debug.Log($"[ExhaustedActivity] {pet.petName}: 탈진 상태에서 회복됩니다.");
        
        // 식사 시도를 하고 있었다면 완전히 취소
        feedingController?.CancelFeeding();
        
        // 애니메이션과 이모티콘을 정리
        animController?.StopContinuousAnimation();
        if (pet.emotionController != null)
        {
            pet.emotionController.HideEmotion();
        }
        
        // 다시 움직일 수 있도록 허용
        if (pet.movementController != null)
        {
            pet.movementController.ResumeMovement();
        }
    }
    
    /// <summary>
    /// PetFeedingController를 통해 음식 탐색을 시도하고 결과를 내부 상태에 반영합니다.
    /// </summary>
    private void AttemptToEat()
    {
        // isEatingOrSeeking 상태를 먼저 초기화해야 새로운 탐색이 가능
        feedingController?.CancelFeeding(); 
        
        if (feedingController != null && feedingController.TryStartFeedingSequence())
        {
            isTryingToEat = true;
            animController?.StopContinuousAnimation(); // 휴식 애니메이션 중지
            if (pet.emotionController != null)
        {
            pet.emotionController.HideEmotion();
        } // 배고픔 이모티콘 숨기기
            Debug.Log($"[ExhaustedActivity] {pet.petName}: 탈진 상태에서 음식을 발견하여 이동을 시작합니다.");
        }
        else
        {
            isTryingToEat = false;
        }
    }
}