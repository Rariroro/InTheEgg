using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 펫 이벤트 시스템 사용 예시
/// </summary>
public class PetEventSystemExample : MonoBehaviour
{
    [Header("Event Monitoring")]
    [SerializeField] private bool monitorAllPets = true;
    [SerializeField] private List<PetController> petsToMonitor = new List<PetController>();
    
    [Header("Event Statistics")]
    [SerializeField] private int totalEventsReceived = 0;
    [SerializeField] private Dictionary<string, int> eventCounts = new Dictionary<string, int>();
    
    private void Start()
    {
        // 전역 이벤트 버스 구독
        SubscribeToGlobalEvents();
        
        // 특정 펫들의 이벤트 구독
        if (!monitorAllPets)
        {
            foreach (var pet in petsToMonitor)
            {
                SubscribeToPetEvents(pet);
            }
        }
    }
    
    /// <summary>
    /// 전역 이벤트 구독
    /// </summary>
    private void SubscribeToGlobalEvents()
    {
        var globalBus = PetEventSystemPatch.GlobalEventBus;
        
        // 모든 이벤트 타입 구독
        globalBus.Subscribe<PetEvent>(OnAnyPetEvent);
        
        // 특정 이벤트 타입 구독
        globalBus.Subscribe<StateChangedEvent>(OnStateChanged);
        globalBus.Subscribe<NeedCriticalEvent>(OnNeedCritical);
        globalBus.Subscribe<EmotionExpressedEvent>(OnEmotionExpressed);
        globalBus.Subscribe<ActivityStartedEvent>(OnActivityStarted);
        globalBus.Subscribe<ActivityCompletedEvent>(OnActivityCompleted);
    }
    
    /// <summary>
    /// 특정 펫의 이벤트 구독
    /// </summary>
    private void SubscribeToPetEvents(PetController pet)
    {
        var eventPatch = pet.GetComponent<PetEventSystemPatch>();
        if (eventPatch != null)
        {
            eventPatch.Subscribe<PetEvent>((e) => {
                if (e.Sender == pet)
                {
                    Debug.Log($"[Example] {pet.petName}의 이벤트: {e.GetType().Name}");
                }
            });
        }
    }
    
    // ===== 이벤트 핸들러 예시 =====
    
    private void OnAnyPetEvent(PetEvent e)
    {
        totalEventsReceived++;
        
        string eventTypeName = e.GetType().Name;
        if (!eventCounts.ContainsKey(eventTypeName))
        {
            eventCounts[eventTypeName] = 0;
        }
        eventCounts[eventTypeName]++;
        
        if (monitorAllPets || petsToMonitor.Contains(e.Sender))
        {
            Debug.Log($"[EventExample] {e.Sender?.petName}: {eventTypeName} at {e.Timestamp:F2}");
        }
    }
    
    private void OnStateChanged(StateChangedEvent e)
    {
        Debug.Log($"[EventExample] {e.Sender.petName}: 상태 변경 {e.OldStatus} → {e.NewStatus}");
        
        // 예시: 특정 상태 변경에 반응
        if (e.NewStatus == PetStatus.Emergency)
        {
            // 긴급 상황 처리
            HandleEmergency(e.Sender);
        }
    }
    
    private void OnNeedCritical(NeedCriticalEvent e)
    {
        Debug.LogWarning($"[EventExample] {e.Sender.petName}: {e.NeedType} 위험 수준!");
        
        // 예시: 자동으로 필요한 아이템 생성
        if (e.NeedType == PetNeeds.NeedType.Hunger)
        {
            SpawnFoodNearPet(e.Sender);
        }
    }
    
    private void OnEmotionExpressed(EmotionExpressedEvent e)
    {
        Debug.Log($"[EventExample] {e.Sender.petName}: {e.EmotionType} 감정 표현");
        
        // 예시: 감정에 따른 점수 변경
        UpdateEmotionScore(e.Sender, e.EmotionType);
    }
    
    private void OnActivityStarted(ActivityStartedEvent e)
    {
        Debug.Log($"[EventExample] {e.Sender.petName}: {e.ActivityName} 활동 시작");
    }
    
    private void OnActivityCompleted(ActivityCompletedEvent e)
    {
        Debug.Log($"[EventExample] {e.Sender.petName}: {e.ActivityName} 활동 완료");
    }
    
    // ===== 유틸리티 메서드 =====
    
    private void HandleEmergency(PetController pet)
    {
        Debug.LogWarning($"[EventExample] {pet.petName}의 긴급 상황 처리!");
        // 긴급 상황 처리 로직
    }
    
    private void SpawnFoodNearPet(PetController pet)
    {
        Debug.Log($"[EventExample] {pet.petName} 근처에 음식 생성");
        // 음식 생성 로직
    }
    
    private void UpdateEmotionScore(PetController pet, EmotionType emotion)
    {
        int scoreChange = emotion switch
        {
            EmotionType.Happy => 10,
            EmotionType.Love => 20,
            EmotionType.Sad => -5,
            EmotionType.Angry => -10,
            _ => 0
        };
        
        Debug.Log($"[EventExample] {pet.petName}: 감정 점수 {scoreChange:+#;-#;0}");
    }
    
    /// <summary>
    /// 이벤트 통계 출력
    /// </summary>
    [ContextMenu("Print Event Statistics")]
    private void PrintEventStatistics()
    {
        Debug.Log($"=== 이벤트 통계 ===");
        Debug.Log($"총 이벤트 수: {totalEventsReceived}");
        
        foreach (var kvp in eventCounts)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value}회");
        }
    }
    
    /// <summary>
    /// 테스트 이벤트 발행
    /// </summary>
    [ContextMenu("Publish Test Event")]
    private void PublishTestEvent()
    {
        if (petsToMonitor.Count > 0)
        {
            var pet = petsToMonitor[0];
            var testEvent = new EmotionExpressedEvent(pet, EmotionType.Happy);
            PetEventSystemPatch.GlobalEventBus.Publish(testEvent);
            Debug.Log("테스트 이벤트 발행됨");
        }
    }
    
    private void OnDestroy()
    {
        // 구독 해제
        var globalBus = PetEventSystemPatch.GlobalEventBus;
        if (globalBus != null)
        {
            globalBus.Unsubscribe<PetEvent>(OnAnyPetEvent);
            globalBus.Unsubscribe<StateChangedEvent>(OnStateChanged);
            globalBus.Unsubscribe<NeedCriticalEvent>(OnNeedCritical);
            globalBus.Unsubscribe<EmotionExpressedEvent>(OnEmotionExpressed);
            globalBus.Unsubscribe<ActivityStartedEvent>(OnActivityStarted);
            globalBus.Unsubscribe<ActivityCompletedEvent>(OnActivityCompleted);
        }
    }
}