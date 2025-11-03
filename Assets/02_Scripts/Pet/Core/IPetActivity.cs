using UnityEngine;
using InTheEgg.Constants;

/// <summary>
/// 펫의 활동(Activity)을 정의하는 인터페이스
/// 기존 IPetAction을 대체하여 더 명확한 이름과 구조를 제공합니다.
/// </summary>
public interface IPetActivity
{
    /// <summary>
    /// 활동의 이름 (디버깅 및 로깅용)
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 이 활동을 시작할 수 있는지 확인합니다.
    /// 상태와 욕구를 모두 고려하여 더 명확한 조건 체크를 합니다.
    /// </summary>
    /// <param name="state">현재 펫의 상태</param>
    /// <param name="needs">현재 펫의 욕구</param>
    /// <returns>시작 가능 여부</returns>
    bool CanStart(PetState state, PetNeeds needs);
    
    /// <summary>
    /// 활동의 우선순위를 반환합니다.
    /// CanStart가 true일 때만 호출됩니다.
    /// </summary>
    /// <param name="state">현재 펫의 상태</param>
    /// <param name="needs">현재 펫의 욕구</param>
    /// <returns>우선순위 (높을수록 우선)</returns>
    float GetPriority(PetState state, PetNeeds needs);
    
    /// <summary>
    /// 활동이 시작될 때 호출됩니다.
    /// </summary>
    void Start();
    
    /// <summary>
    /// 활동이 진행되는 동안 매 프레임 호출됩니다.
    /// </summary>
    void Update();
    
    /// <summary>
    /// 활동이 종료될 때 호출됩니다.
    /// </summary>
    void Stop();
    
    /// <summary>
    /// 활동이 완료되었는지 여부
    /// </summary>
    bool IsComplete { get; }
    
    /// <summary>
    /// 활동이 중단 가능한지 여부
    /// 예: 긴급 상황이나 플레이어 개입 시 중단할 수 있는지
    /// </summary>
    bool IsInterruptible { get; }
}

/// <summary>
/// Activity 구현을 위한 기본 어댑터 클래스
/// 기존 Activity들과의 호환성 유지
/// </summary>
public abstract class PetActivityAdapter : IPetActivity
{
    protected readonly PetController pet;

    public abstract string Name { get; }
    public virtual bool IsComplete => false;
    public virtual bool IsInterruptible => true;

    protected PetActivityAdapter(PetController petController)
    {
        pet = petController;
    }

    // IPetActivity 구현 - 하위 클래스에서 구현
    public abstract bool CanStart(PetState state, PetNeeds needs);
    public abstract float GetPriority(PetState state, PetNeeds needs);
    public abstract void Start();
    public abstract void Update();
    public abstract void Stop();

    #region Emotion Helper Methods

    /// <summary>
    /// 활동 중 지속되는 감정 표시
    /// </summary>
    /// <param name="emotion">표시할 감정</param>
    /// <param name="duration">지속 시간 (기본: PERSISTENT)</param>
    protected void ShowActivityEmotion(EmotionType emotion, float duration = -1)
    {
        if (duration < 0)
        {
            duration = EmotionConstants.DURATION_PERSISTENT;
        }
        pet.ShowEmotion(emotion, duration);
    }

    /// <summary>
    /// 짧은 임시 감정 표시
    /// </summary>
    /// <param name="emotion">표시할 감정</param>
    /// <param name="duration">지속 시간 (기본: SHORT)</param>
    protected void ShowTemporaryEmotion(EmotionType emotion, float duration = -1)
    {
        if (duration < 0)
        {
            duration = EmotionConstants.DURATION_SHORT;
        }
        pet.ShowEmotion(emotion, duration);
    }

    /// <summary>
    /// 순간적인 감정 표시
    /// </summary>
    /// <param name="emotion">표시할 감정</param>
    protected void ShowFlashEmotion(EmotionType emotion)
    {
        pet.ShowEmotion(emotion, EmotionConstants.DURATION_VERY_SHORT);
    }

    #endregion
}

/// <summary>
/// 개선된 Activity 베이스 클래스 (선택적 사용)
/// 템플릿 메서드 패턴으로 감정 처리 자동화
/// 새로운 Activity를 만들 때 이 클래스를 상속받으면 자동 감정 관리 기능을 사용할 수 있습니다.
/// </summary>
public abstract class EnhancedPetActivityAdapter : PetActivityAdapter
{
    // 감정 관련 필드
    protected EmotionType? defaultEmotion = null;
    protected float defaultEmotionDuration = EmotionConstants.DURATION_PERSISTENT;
    protected bool autoHideEmotionOnStop = true;

    protected EnhancedPetActivityAdapter(PetController petController) : base(petController)
    {
    }

    // 템플릿 메서드 패턴으로 감정 처리 자동화
    public override void Start()
    {
        // 활동 시작 시 기본 감정 자동 표시
        if (defaultEmotion.HasValue)
        {
            ShowActivityEmotion(defaultEmotion.Value, defaultEmotionDuration);
        }
        OnActivityStart();
    }

    public override void Update()
    {
        OnActivityUpdate();
    }

    public override void Stop()
    {
        OnActivityStop();

        // 활동 종료 시 감정 자동 제거
        if (autoHideEmotionOnStop)
        {
            pet.HideEmotion();
        }
    }

    // 하위 클래스에서 오버라이드할 수 있는 메서드
    protected virtual void OnActivityStart() { }
    protected virtual void OnActivityUpdate() { }
    protected virtual void OnActivityStop() { }

    /// <summary>
    /// 활동별 기본 감정 설정 (생성자에서 호출)
    /// </summary>
    protected void SetDefaultEmotion(EmotionType emotion, float duration = -1)
    {
        defaultEmotion = emotion;
        defaultEmotionDuration = duration < 0 ? EmotionConstants.DURATION_PERSISTENT : duration;
    }
}
