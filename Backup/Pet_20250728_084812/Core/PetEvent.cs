using UnityEngine;

/// <summary>
/// 펫 이벤트의 기본 클래스
/// </summary>
public abstract class PetEvent
{
    public PetController Sender { get; private set; }
    public float Timestamp { get; private set; }
    
    protected PetEvent(PetController sender)
    {
        Sender = sender;
        Timestamp = Time.time;
    }
}

/// <summary>
/// 펫 이벤트 타입 정의
/// </summary>
public enum PetEventType
{
    // 상태 변경 이벤트
    StateChanged,
    
    // 욕구 관련 이벤트
    NeedChanged,
    NeedCritical,
    
    // 상호작용 이벤트
    TouchStarted,
    TouchEnded,
    Selected,
    Deselected,
    HoldStarted,
    HoldEnded,
    
    // 활동 이벤트
    ActivityStarted,
    ActivityCompleted,
    
    // 감정 이벤트
    EmotionExpressed,
    
    // 이동 이벤트
    MovementStarted,
    MovementStopped,
    DestinationReached,
    
    // 환경 이벤트
    EnteredWater,
    ExitedWater,
    ClimbedTree,
    DescendedTree
}

/// <summary>
/// 단순 이벤트 (추가 데이터 없음)
/// </summary>
public class SimpleEvent : PetEvent
{
    public PetEventType EventType { get; private set; }
    
    public SimpleEvent(PetController sender, PetEventType eventType) : base(sender)
    {
        EventType = eventType;
    }
}

// ===== 구체적인 이벤트 클래스들 =====

/// <summary>
/// 상태 변경 이벤트
/// </summary>
public class StateChangedEvent : PetEvent
{
    public PetStatus OldStatus { get; private set; }
    public PetStatus NewStatus { get; private set; }
    
    public StateChangedEvent(PetController sender, PetStatus oldStatus, PetStatus newStatus) : base(sender)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}

/// <summary>
/// 욕구 변경 이벤트
/// </summary>
public class NeedChangedEvent : PetEvent
{
    public PetNeeds.NeedType NeedType { get; private set; }
    public float OldValue { get; private set; }
    public float NewValue { get; private set; }
    
    public NeedChangedEvent(PetController sender, PetNeeds.NeedType needType, float oldValue, float newValue) : base(sender)
    {
        NeedType = needType;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>
/// 욕구 위험 수준 이벤트
/// </summary>
public class NeedCriticalEvent : PetEvent
{
    public PetNeeds.NeedType NeedType { get; private set; }
    
    public NeedCriticalEvent(PetController sender, PetNeeds.NeedType needType) : base(sender)
    {
        NeedType = needType;
    }
}

/// <summary>
/// 감정 표현 이벤트
/// </summary>
public class EmotionExpressedEvent : PetEvent
{
    public EmotionType EmotionType { get; private set; }
    
    public EmotionExpressedEvent(PetController sender, EmotionType emotionType) : base(sender)
    {
        EmotionType = emotionType;
    }
}

/// <summary>
/// 활동 시작 이벤트
/// </summary>
public class ActivityStartedEvent : PetEvent
{
    public string ActivityName { get; private set; }
    
    public ActivityStartedEvent(PetController sender, string activityName) : base(sender)
    {
        ActivityName = activityName;
    }
}

/// <summary>
/// 활동 완료 이벤트
/// </summary>
public class ActivityCompletedEvent : PetEvent
{
    public string ActivityName { get; private set; }
    
    public ActivityCompletedEvent(PetController sender, string activityName) : base(sender)
    {
        ActivityName = activityName;
    }
}

/// <summary>
/// 이동 관련 이벤트
/// </summary>
public class MovementEvent : PetEvent
{
    public Vector3 Destination { get; private set; }
    public bool IsMoving { get; private set; }
    
    public MovementEvent(PetController sender, Vector3 destination, bool isMoving) : base(sender)
    {
        Destination = destination;
        IsMoving = isMoving;
    }
}

/// <summary>
/// 환경 상호작용 이벤트
/// </summary>
public class EnvironmentInteractionEvent : PetEvent
{
    public string InteractionType { get; private set; }
    public GameObject TargetObject { get; private set; }
    
    public EnvironmentInteractionEvent(PetController sender, string interactionType, GameObject targetObject) : base(sender)
    {
        InteractionType = interactionType;
        TargetObject = targetObject;
    }
}