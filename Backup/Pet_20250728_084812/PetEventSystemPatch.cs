using UnityEngine;

/// <summary>
/// 기존 PetController에 이벤트 시스템을 추가하는 패치
/// PetControllerPatch와 함께 사용 가능
/// </summary>
[RequireComponent(typeof(PetController))]
public class PetEventSystemPatch : MonoBehaviour
{
    [Header("Event System Configuration")]
    [SerializeField] private bool enableEventSystem = true;
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool useGlobalEventBus = false;
    
    private PetController petController;
    private PetEventBus localEventBus;
    private PetEventSystem eventSystem;
    
    // 전역 이벤트 버스 (선택적)
    private static PetEventBus globalEventBus;
    
    /// <summary>
    /// 전역 이벤트 버스 접근
    /// </summary>
    public static PetEventBus GlobalEventBus
    {
        get
        {
            if (globalEventBus == null)
            {
                GameObject globalBusObject = new GameObject("GlobalPetEventBus");
                globalEventBus = globalBusObject.AddComponent<PetEventBus>();
                DontDestroyOnLoad(globalBusObject);
            }
            return globalEventBus;
        }
    }
    
    void Awake()
    {
        petController = GetComponent<PetController>();
        if (petController == null)
        {
            Debug.LogError("[PetEventSystemPatch] PetController를 찾을 수 없습니다!");
            return;
        }
        
        if (enableEventSystem)
        {
            InitializeEventSystem();
        }
    }
    
    private void InitializeEventSystem()
    {
        // 이벤트 시스템 컴포넌트 추가
        eventSystem = gameObject.AddComponent<PetEventSystem>();
        
        // 로컬 또는 전역 이벤트 버스 설정
        if (useGlobalEventBus)
        {
            // 전역 이벤트 버스 사용
            Debug.Log($"[PetEventSystemPatch] {petController.petName}: 전역 이벤트 버스 사용");
        }
        else
        {
            // 로컬 이벤트 버스 생성
            localEventBus = gameObject.AddComponent<PetEventBus>();
            Debug.Log($"[PetEventSystemPatch] {petController.petName}: 로컬 이벤트 버스 생성");
        }
        
        // 이벤트 시스템 초기화
        eventSystem.Init(petController);
        
        // 기존 시스템과 연결
        ConnectLegacySystems();
    }
    
    /// <summary>
    /// 기존 시스템과 이벤트 시스템 연결
    /// </summary>
    private void ConnectLegacySystems()
    {
        // 예시: 기존 PetController의 주요 이벤트들을 새 이벤트 시스템으로 브릿지
        
        // 1. 선택/해제 이벤트
        StartCoroutine(MonitorSelectionState());
        
        // 2. 활동 변경 감지
        StartCoroutine(MonitorActivityChanges());
        
        // 3. 욕구 변화 감지
        StartCoroutine(MonitorNeedChanges());
    }
    
    /// <summary>
    /// 선택 상태 모니터링
    /// </summary>
    private System.Collections.IEnumerator MonitorSelectionState()
    {
        bool wasSelected = petController.isSelected;
        
        while (enabled)
        {
            if (petController.isSelected != wasSelected)
            {
                wasSelected = petController.isSelected;
                
                var eventBus = useGlobalEventBus ? GlobalEventBus : localEventBus;
                if (wasSelected)
                {
                    eventBus?.Publish(new SimpleEvent(petController, PetEventType.Selected));
                    Debug.Log($"[EventSystemPatch] {petController.petName}: 선택됨 이벤트 발행");
                }
                else
                {
                    eventBus?.Publish(new SimpleEvent(petController, PetEventType.Deselected));
                    Debug.Log($"[EventSystemPatch] {petController.petName}: 선택 해제됨 이벤트 발행");
                }
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    /// <summary>
    /// 활동 변경 모니터링
    /// </summary>
    private System.Collections.IEnumerator MonitorActivityChanges()
    {
        string lastActivity = "";
        
        while (enabled)
        {
            // 현재 활동 확인 (예시)
            string currentActivity = GetCurrentActivity();
            
            if (currentActivity != lastActivity && !string.IsNullOrEmpty(currentActivity))
            {
                var eventBus = useGlobalEventBus ? GlobalEventBus : localEventBus;
                
                // 이전 활동 종료 이벤트
                if (!string.IsNullOrEmpty(lastActivity))
                {
                    eventBus?.Publish(new ActivityCompletedEvent(petController, lastActivity));
                }
                
                // 새 활동 시작 이벤트
                eventBus?.Publish(new ActivityStartedEvent(petController, currentActivity));
                
                lastActivity = currentActivity;
                
                if (debugMode)
                    Debug.Log($"[EventSystemPatch] {petController.petName}: 활동 변경 {lastActivity} → {currentActivity}");
            }
            
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    /// <summary>
    /// 욕구 변화 모니터링
    /// </summary>
    private System.Collections.IEnumerator MonitorNeedChanges()
    {
        var needs = petController.Needs;
        if (needs == null)
            yield break;
            
        float lastHunger = needs.Hunger;
        float lastSleepiness = needs.Sleepiness;
        float lastAffection = needs.Affection;
        
        while (enabled)
        {
            var eventBus = useGlobalEventBus ? GlobalEventBus : localEventBus;
            
            // 배고픔 변화
            if (Mathf.Abs(needs.Hunger - lastHunger) > 0.1f)
            {
                eventBus?.Publish(new NeedChangedEvent(petController, PetNeeds.NeedType.Hunger, lastHunger, needs.Hunger));
                lastHunger = needs.Hunger;
            }
            
            // 졸림 변화
            if (Mathf.Abs(needs.Sleepiness - lastSleepiness) > 0.1f)
            {
                eventBus?.Publish(new NeedChangedEvent(petController, PetNeeds.NeedType.Sleepiness, lastSleepiness, needs.Sleepiness));
                lastSleepiness = needs.Sleepiness;
            }
            
            // 친밀도 변화
            if (Mathf.Abs(needs.Affection - lastAffection) > 0.1f)
            {
                eventBus?.Publish(new NeedChangedEvent(petController, PetNeeds.NeedType.Affection, lastAffection, needs.Affection));
                lastAffection = needs.Affection;
            }
            
            yield return new WaitForSeconds(1f);
        }
    }
    
    /// <summary>
    /// 현재 활동 가져오기 (예시)
    /// </summary>
    private string GetCurrentActivity()
    {
        // 기존 시스템의 상태를 확인하여 활동 이름 반환
        if (petController.isHolding)
            return "Holding";
        if (petController.isInteracting)
            return "Interacting";
        if (petController.isClimbingTree)
            return "ClimbingTree";
        if (petController.isInWater)
            return "Swimming";
        if (petController.isExhausted)
            return "Exhausted";
            
        // 기본 활동
        if (petController.agent != null && petController.agent.velocity.magnitude > 0.1f)
            return "Moving";
            
        return "Idle";
    }
    
    /// <summary>
    /// 이벤트 발행 (외부에서 사용)
    /// </summary>
    public void PublishEvent<T>(T petEvent) where T : PetEvent
    {
        if (!enableEventSystem) return;
        
        var eventBus = useGlobalEventBus ? GlobalEventBus : localEventBus;
        eventBus?.Publish(petEvent);
    }
    
    /// <summary>
    /// 이벤트 구독 (외부에서 사용)
    /// </summary>
    public void Subscribe<T>(System.Action<T> handler) where T : PetEvent
    {
        if (!enableEventSystem) return;
        
        var eventBus = useGlobalEventBus ? GlobalEventBus : localEventBus;
        eventBus?.Subscribe(handler);
    }
    
    /// <summary>
    /// 디버그 정보 출력
    /// </summary>
    [ContextMenu("Print Event System Debug Info")]
    private void PrintDebugInfo()
    {
        if (localEventBus != null)
        {
            Debug.Log($"=== {petController.petName} 로컬 이벤트 버스 ===");
            localEventBus.PrintDebugInfo();
        }
        
        if (useGlobalEventBus && GlobalEventBus != null)
        {
            Debug.Log("=== 전역 이벤트 버스 ===");
            GlobalEventBus.PrintDebugInfo();
        }
    }
}