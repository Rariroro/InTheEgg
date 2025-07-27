using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 펫 이벤트 버스 - 이벤트 기반 통신의 중앙 허브
/// </summary>
public class PetEventBus : MonoBehaviour
{
    private Dictionary<Type, List<Delegate>> eventHandlers = new Dictionary<Type, List<Delegate>>();
    private Queue<PetEvent> eventQueue = new Queue<PetEvent>();
    private bool isProcessingEvents = false;
    
    [Header("Event Bus Settings")]
    [SerializeField] private bool enableDebugLog = false;
    [SerializeField] private int maxEventsPerFrame = 10;
    
    /// <summary>
    /// 이벤트 구독
    /// </summary>
    public void Subscribe<T>(Action<T> handler) where T : PetEvent
    {
        Type eventType = typeof(T);
        
        if (!eventHandlers.ContainsKey(eventType))
        {
            eventHandlers[eventType] = new List<Delegate>();
        }
        
        eventHandlers[eventType].Add(handler);
        
        if (enableDebugLog)
            Debug.Log($"[PetEventBus] 구독 추가: {eventType.Name}");
    }
    
    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    public void Unsubscribe<T>(Action<T> handler) where T : PetEvent
    {
        Type eventType = typeof(T);
        
        if (eventHandlers.ContainsKey(eventType))
        {
            eventHandlers[eventType].Remove(handler);
            
            if (eventHandlers[eventType].Count == 0)
            {
                eventHandlers.Remove(eventType);
            }
        }
        
        if (enableDebugLog)
            Debug.Log($"[PetEventBus] 구독 해제: {eventType.Name}");
    }
    
    /// <summary>
    /// 이벤트 발행 (즉시 처리)
    /// </summary>
    public void Publish<T>(T petEvent) where T : PetEvent
    {
        if (petEvent == null) return;
        
        Type eventType = petEvent.GetType();
        
        if (enableDebugLog)
            Debug.Log($"[PetEventBus] 이벤트 발행: {eventType.Name} from {petEvent.Sender?.petName}");
        
        // 즉시 처리
        ProcessEvent(petEvent);
    }
    
    /// <summary>
    /// 이벤트 발행 (큐에 추가하여 나중에 처리)
    /// </summary>
    public void PublishQueued<T>(T petEvent) where T : PetEvent
    {
        if (petEvent == null) return;
        
        eventQueue.Enqueue(petEvent);
        
        if (enableDebugLog)
            Debug.Log($"[PetEventBus] 이벤트 큐에 추가: {petEvent.GetType().Name}");
    }
    
    /// <summary>
    /// 큐에 있는 이벤트들 처리
    /// </summary>
    private void Update()
    {
        if (eventQueue.Count > 0 && !isProcessingEvents)
        {
            isProcessingEvents = true;
            int processedCount = 0;
            
            while (eventQueue.Count > 0 && processedCount < maxEventsPerFrame)
            {
                var petEvent = eventQueue.Dequeue();
                ProcessEvent(petEvent);
                processedCount++;
            }
            
            isProcessingEvents = false;
        }
    }
    
    /// <summary>
    /// 이벤트 처리
    /// </summary>
    private void ProcessEvent(PetEvent petEvent)
    {
        Type eventType = petEvent.GetType();
        
        // 정확한 타입의 핸들러 호출
        if (eventHandlers.ContainsKey(eventType))
        {
            foreach (var handler in eventHandlers[eventType].ToArray()) // ToArray로 복사본 생성
            {
                try
                {
                    handler.DynamicInvoke(petEvent);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PetEventBus] 이벤트 처리 중 오류: {e.Message}");
                }
            }
        }
        
        // 상위 타입의 핸들러도 호출 (예: PetEvent 타입으로 모든 이벤트 구독)
        Type baseType = eventType.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            if (eventHandlers.ContainsKey(baseType))
            {
                foreach (var handler in eventHandlers[baseType].ToArray())
                {
                    try
                    {
                        handler.DynamicInvoke(petEvent);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[PetEventBus] 상위 타입 이벤트 처리 중 오류: {e.Message}");
                    }
                }
            }
            baseType = baseType.BaseType;
        }
    }
    
    /// <summary>
    /// 모든 이벤트 핸들러 제거
    /// </summary>
    public void Clear()
    {
        eventHandlers.Clear();
        eventQueue.Clear();
        
        if (enableDebugLog)
            Debug.Log("[PetEventBus] 모든 이벤트 핸들러 제거됨");
    }
    
    /// <summary>
    /// 디버그 정보 출력
    /// </summary>
    public void PrintDebugInfo()
    {
        Debug.Log($"[PetEventBus] 등록된 이벤트 타입: {eventHandlers.Count}개");
        foreach (var kvp in eventHandlers)
        {
            Debug.Log($"  - {kvp.Key.Name}: {kvp.Value.Count}개 핸들러");
        }
        Debug.Log($"[PetEventBus] 대기 중인 이벤트: {eventQueue.Count}개");
    }
}