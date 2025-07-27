using UnityEngine;

/// <summary>
/// 펫 이벤트 시스템 통합 클래스
/// 새로운 컴포넌트들이 이벤트 시스템을 사용하도록 연결
/// </summary>
public class PetEventSystem : MonoBehaviour
{
    private PetController petController;
    private PetEventBus eventBus;
    
    // 컴포넌트들
    private PetState petState;
    private PetNeeds petNeeds;
    private PetMovement petMovement;
    private PetAnimator petAnimator;
    private PetSensor petSensor;
    private PetEffects petEffects;
    private PetInteractor petInteractor;
    
    [Header("Event System Settings")]
    [SerializeField] private bool enableEventSystem = true;
    [SerializeField] private bool debugMode = false;
    
    /// <summary>
    /// 이벤트 시스템 초기화
    /// </summary>
    public void Init(PetController controller)
    {
        if (!enableEventSystem) return;
        
        petController = controller;
        
        // EventBus 생성 또는 찾기
        eventBus = GetComponent<PetEventBus>();
        if (eventBus == null)
        {
            eventBus = gameObject.AddComponent<PetEventBus>();
        }
        
        // 컴포넌트 참조 가져오기
        GetComponentReferences();
        
        // 이벤트 연결
        SetupEventHandlers();
        
        Debug.Log($"[PetEventSystem] {petController.petName}: 이벤트 시스템 초기화 완료");
    }
    
    /// <summary>
    /// 컴포넌트 참조 가져오기
    /// </summary>
    private void GetComponentReferences()
    {
        petState = GetComponent<PetState>();
        petNeeds = GetComponent<PetNeeds>();
        petMovement = GetComponent<PetMovement>();
        petAnimator = GetComponent<PetAnimator>();
        petSensor = GetComponent<PetSensor>();
        petEffects = GetComponent<PetEffects>();
        petInteractor = GetComponent<PetInteractor>();
    }
    
    /// <summary>
    /// 이벤트 핸들러 설정
    /// </summary>
    private void SetupEventHandlers()
    {
        // 1. PetState 이벤트 연결
        if (petState != null)
        {
            petState.OnStatusChanged += (oldStatus, newStatus) => {
                eventBus.Publish(new StateChangedEvent(petController, oldStatus, newStatus));
            };
        }
        
        // 2. PetNeeds 이벤트 연결
        if (petNeeds != null)
        {
            petNeeds.OnNeedChanged += (needType, value) => {
                eventBus.Publish(new NeedChangedEvent(petController, needType, 0, value));
            };
            
            petNeeds.OnNeedCritical += (needType) => {
                eventBus.Publish(new NeedCriticalEvent(petController, needType));
            };
            
            petNeeds.OnEmotionRequired += (emotionType) => {
                eventBus.Publish(new EmotionExpressedEvent(petController, emotionType));
            };
        }
        
        // 3. PetInteractor 이벤트 연결
        if (petInteractor != null)
        {
            petInteractor.OnTouched += () => {
                eventBus.Publish(new SimpleEvent(petController, PetEventType.TouchStarted));
            };
            
            petInteractor.OnSelected += () => {
                eventBus.Publish(new SimpleEvent(petController, PetEventType.Selected));
            };
            
            petInteractor.OnDeselected += () => {
                eventBus.Publish(new SimpleEvent(petController, PetEventType.Deselected));
            };
        }
        
        // 4. PetEffects 이벤트 연결
        if (petEffects != null)
        {
            petEffects.OnEmotionDisplayed += (emotionType) => {
                eventBus.Publish(new EmotionExpressedEvent(petController, emotionType));
            };
        }
        
        // 이벤트 구독 예시
        SetupEventSubscriptions();
    }
    
    /// <summary>
    /// 이벤트 구독 설정 예시
    /// </summary>
    private void SetupEventSubscriptions()
    {
        // 상태 변경 이벤트 구독
        eventBus.Subscribe<StateChangedEvent>(OnStateChanged);
        
        // 욕구 관련 이벤트 구독
        eventBus.Subscribe<NeedCriticalEvent>(OnNeedCritical);
        
        // 감정 표현 이벤트 구독
        eventBus.Subscribe<EmotionExpressedEvent>(OnEmotionExpressed);
        
        // 모든 이벤트 구독 (디버그용)
        if (debugMode)
        {
            eventBus.Subscribe<PetEvent>(OnAnyEvent);
        }
    }
    
    // ===== 이벤트 핸들러 예시 =====
    
    private void OnStateChanged(StateChangedEvent e)
    {
        if (debugMode)
            Debug.Log($"[EventSystem] {e.Sender.petName}: 상태 변경 {e.OldStatus} → {e.NewStatus}");
        
        // 상태에 따른 애니메이션 변경
        if (petAnimator != null)
        {
            switch (e.NewStatus)
            {
                case PetStatus.Idle:
                    petAnimator.PlayContinuous(PetAnimator.AnimationType.Idle);
                    break;
                case PetStatus.PlayerControl:
                    petAnimator.Stop();
                    break;
            }
        }
    }
    
    private void OnNeedCritical(NeedCriticalEvent e)
    {
        if (debugMode)
            Debug.Log($"[EventSystem] {e.Sender.petName}: {e.NeedType} 위험 수준!");
        
        // 긴급 상태로 전환
        petState?.TrySetStatus(PetStatus.Emergency);
    }
    
    private void OnEmotionExpressed(EmotionExpressedEvent e)
    {
        if (debugMode)
            Debug.Log($"[EventSystem] {e.Sender.petName}: {e.EmotionType} 감정 표현");
    }
    
    private void OnAnyEvent(PetEvent e)
    {
        Debug.Log($"[EventSystem] 이벤트 발생: {e.GetType().Name} from {e.Sender?.petName}");
    }
    
    /// <summary>
    /// 외부에서 이벤트 발행
    /// </summary>
    public void PublishEvent<T>(T petEvent) where T : PetEvent
    {
        if (enableEventSystem && eventBus != null)
        {
            eventBus.Publish(petEvent);
        }
    }
    
    /// <summary>
    /// 이벤트 구독
    /// </summary>
    public void Subscribe<T>(System.Action<T> handler) where T : PetEvent
    {
        if (enableEventSystem && eventBus != null)
        {
            eventBus.Subscribe(handler);
        }
    }
    
    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    public void Unsubscribe<T>(System.Action<T> handler) where T : PetEvent
    {
        if (enableEventSystem && eventBus != null)
        {
            eventBus.Unsubscribe(handler);
        }
    }
    
    private void OnDestroy()
    {
        // 구독 해제
        if (eventBus != null)
        {
            eventBus.Unsubscribe<StateChangedEvent>(OnStateChanged);
            eventBus.Unsubscribe<NeedCriticalEvent>(OnNeedCritical);
            eventBus.Unsubscribe<EmotionExpressedEvent>(OnEmotionExpressed);
            
            if (debugMode)
            {
                eventBus.Unsubscribe<PetEvent>(OnAnyEvent);
            }
        }
    }
}