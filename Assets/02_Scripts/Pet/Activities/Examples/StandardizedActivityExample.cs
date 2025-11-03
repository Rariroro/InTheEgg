using UnityEngine;
using InTheEgg.Constants;

/// <summary>
/// 표준화된 Activity 패턴 예제
/// 새로운 Activity를 만들 때 이 패턴을 따르세요.
/// EnhancedPetActivityAdapter를 상속받아 자동 감정 관리 기능 사용
/// </summary>
public class StandardizedEatActivity : EnhancedPetActivityAdapter
{
    private readonly PetFeedingController feedingController;

    public override string Name => "Eat";
    public override bool IsInterruptible => !isEatingCriticalFood;

    private bool isEatingCriticalFood = false;

    public StandardizedEatActivity(PetController petController, PetFeedingController feeding)
        : base(petController)
    {
        feedingController = feeding;

        // 방법 1: 생성자에서 기본 감정 설정
        // 이 Activity는 시작 시 자동으로 Hungry 감정을 표시합니다
        SetDefaultEmotion(EmotionType.Hungry);
    }

    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // Early return 패턴 사용
        if (IsUnderPlayerControl(state))
            return false;

        if (IsClimbingTree(state))
            return false;

        if (IsAlreadyEating())
            return true;

        return IsHungry(needs);
    }

    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;

        if (feedingController.IsEatingOrSeeking())
            return EmotionConstants.PRIORITY_EATING_IN_PROGRESS;

        float hunger = needs?.Hunger ?? pet.Needs.Hunger;

        if (hunger >= 90f)
            return EmotionConstants.PRIORITY_EATING_VERY_HUNGRY;

        if (hunger >= 70f)
            return EmotionConstants.PRIORITY_EATING_MODERATELY_HUNGRY;

        return 0f;
    }

    // Start/Update/Stop는 반드시 override해야 함 (abstract)
    public override void Start()
    {
        base.Start(); // EnhancedPetActivityAdapter의 감정 관리 호출
    }

    public override void Update()
    {
        base.Update(); // EnhancedPetActivityAdapter의 Update 호출
    }

    public override void Stop()
    {
        base.Stop(); // EnhancedPetActivityAdapter의 Stop 호출
    }

    // OnActivityStart/Update/Stop에서 실제 로직 구현
    protected override void OnActivityStart()
    {
        // 방법 2: 동적으로 감정 설정도 가능
        // base.Start()가 이미 기본 감정(Hungry)을 표시했으므로
        // 추가 감정이 필요한 경우에만 사용

        if (!feedingController.TryStartFeedingSequence(50f))
        {
            // 음식을 못 찾으면 배회하면서 음식 생각
            // PetEmotionController가 자동으로 음식 종류별 감정 표시
        }
    }

    protected override void OnActivityUpdate()
    {
        if (feedingController.IsEatingOrSeeking())
        {
            // 특별한 음식 먹는 중 체크
            if (IsEatingHoney() && !isEatingCriticalFood)
            {
                isEatingCriticalFood = true;
                // 꿀 먹을 때 특별 감정
                ShowTemporaryEmotion(EmotionType.Love);
            }

            feedingController.UpdateMovementToFood();
        }
    }

    protected override void OnActivityStop()
    {
        // base.Stop()이 자동으로 감정을 제거하므로
        // 추가 정리 작업만 수행

        feedingController.CancelFeeding();
        isEatingCriticalFood = false;
    }

    #region Helper Methods
    private bool IsUnderPlayerControl(PetState state)
    {
        return state.IsHolding || state.IsSelected;
    }

    private bool IsClimbingTree(PetState state)
    {
        return state.IsClimbingTree;
    }

    private bool IsAlreadyEating()
    {
        return feedingController.IsEatingOrSeeking();
    }

    private bool IsHungry(PetNeeds needs)
    {
        return needs?.IsHungry ?? pet.Needs.Hunger >= EmotionConstants.HUNGER_EMOTION_THRESHOLD;
    }

    private bool IsEatingHoney()
    {
        // 꿀 먹기 체크 로직
        return pet.State.IsBeingAttacked; // 벌에게 공격받는 중 = 꿀 먹는 중
    }
    #endregion
}

/// <summary>
/// 더 간단한 Activity 예제
/// </summary>
public class StandardizedSleepActivity : EnhancedPetActivityAdapter
{
    public override string Name => "Sleep";

    public StandardizedSleepActivity(PetController petController) : base(petController)
    {
        // 자는 동안 계속 Sleepy 감정 표시
        SetDefaultEmotion(EmotionType.Sleepy);
    }

    public override bool CanStart(PetState state, PetNeeds needs)
    {
        if (state.IsHolding || state.IsSelected)
            return false;

        return needs.Sleepiness > 70f;
    }

    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;

        if (needs.Sleepiness >= 90f)
            return EmotionConstants.PRIORITY_SLEEPING_VERY_TIRED;

        if (needs.Sleepiness >= 70f)
            return EmotionConstants.PRIORITY_SLEEPING_MODERATELY_TIRED;

        return 0f;
    }

    // Start/Update/Stop는 반드시 override해야 함 (abstract)
    public override void Start()
    {
        base.Start(); // EnhancedPetActivityAdapter의 감정 관리 호출
    }

    public override void Update()
    {
        base.Update(); // EnhancedPetActivityAdapter의 Update 호출
    }

    public override void Stop()
    {
        base.Stop(); // EnhancedPetActivityAdapter의 Stop 호출
    }

    protected override void OnActivityStart()
    {
        // 기본 감정(Sleepy)은 base.Start()가 자동 표시
        // 추가 작업만 수행
        StartSleeping();
    }

    protected override void OnActivityUpdate()
    {
        // 수면 중 업데이트
        if (pet.Needs.Sleepiness <= 0)
        {
            // 충분히 잤으면 일어나기
            ShowTemporaryEmotion(EmotionType.Happy); // 개운함 표시
        }
    }

    protected override void OnActivityStop()
    {
        // base.Stop()이 감정 자동 제거
        // 추가 정리만 수행
        StopSleeping();
    }

    private void StartSleeping()
    {
        // 수면 시작 로직
    }

    private void StopSleeping()
    {
        // 수면 종료 로직
    }
}

/// <summary>
/// 감정 시퀀스가 있는 Activity 예제
/// </summary>
public class StandardizedTreasureHuntActivity : EnhancedPetActivityAdapter
{
    public override string Name => "TreasureHunt";

    public StandardizedTreasureHuntActivity(PetController petController) : base(petController)
    {
        // 보물찾기는 시작할 때 감정 시퀀스를 사용하므로
        // 기본 감정 설정하지 않음
        // defaultEmotion은 null로 유지
    }

    public override bool CanStart(PetState state, PetNeeds needs)
    {
        return state.IsTreasureHuntActive && !state.IsHolding;
    }

    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;

        return EmotionConstants.PRIORITY_TREASURE_HUNTING;
    }

    // Start/Update/Stop는 반드시 override해야 함 (abstract)
    public override void Start()
    {
        base.Start(); // EnhancedPetActivityAdapter의 감정 관리 호출
    }

    public override void Update()
    {
        base.Update(); // EnhancedPetActivityAdapter의 감정 관리 호출
    }

    public override void Stop()
    {
        base.Stop(); // EnhancedPetActivityAdapter의 감정 관리 호출
    }

    protected override void OnActivityStart()
    {
        // 감정 시퀀스 시작
        pet.StartCoroutine(ShowTreasureEmotionSequence());
    }

    private System.Collections.IEnumerator ShowTreasureEmotionSequence()
    {
        // 1. 놀람
        ShowFlashEmotion(EmotionType.Surprised);
        yield return new WaitForSeconds(1f);

        // 2. 보물 생각
        ShowActivityEmotion(EmotionType.Thought_TresureHunt);
    }

    protected override void OnActivityUpdate()
    {
        // 보물 찾기 로직
    }

    protected override void OnActivityStop()
    {
        // base.Stop()이 감정 제거
    }
}