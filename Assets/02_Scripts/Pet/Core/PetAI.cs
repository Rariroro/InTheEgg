using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 펫의 AI 시스템. Activity 우선순위 기반으로 행동을 결정합니다.
/// </summary>
public class PetAI : MonoBehaviour
{
    private PetController petController;
    private PetState petState;
    private PetNeeds petNeeds;
    
    // 사용 가능한 모든 활동들
    private List<IPetActivity> availableActivities = new List<IPetActivity>();
    
    // 현재 실행 중인 활동
    private IPetActivity currentActivity;
    
    [Header("AI Settings")]
    [SerializeField] private float updateInterval = 0.5f; // AI 업데이트 주기
    private float lastUpdateTime;
    
    /// <summary>
    /// AI 시스템 초기화
    /// </summary>
    public void Init(PetController controller)
    {
        petController = controller;
        petState = controller.State;  // PetController의 PetState 사용
        petNeeds = controller.Needs;  // PetController의 PetNeeds 사용
        
        // 모든 활동들 등록
        RegisterActivities();
    }
    
    /// <summary>
    /// 사용 가능한 모든 활동들을 등록
    /// </summary>
    private void RegisterActivities()
    {
        // 컨트롤러들 가져오기 (PetController를 통해)
        var feedingController = petController.feedingController;
        var sleepingController = petController.sleepingController;
        var climbingController = petController.GetComponent<PetTreeClimbingController>();
        var movementController = petController.GetComponent<PetMovementController>();
        
        // Basic Activities
        availableActivities.Add(new WanderActivity(petController, movementController));
        availableActivities.Add(new ClimbTreeActivity(petController, climbingController));
        availableActivities.Add(new DivingActivity(petController));
        
        // Needs Activities
        availableActivities.Add(new EatActivity(petController, feedingController));
        availableActivities.Add(new SleepActivity(petController, sleepingController));
        availableActivities.Add(new ExhaustedActivity(petController));
        
        // Emergency Activities
        availableActivities.Add(new BeeEscapeActivity(petController));
        
        // Social Activities
        availableActivities.Add(new GatherActivity(petController));
        availableActivities.Add(new InteractWithPetActivity(petController));
        availableActivities.Add(new TreasureHuntActivity(petController, movementController)); // 보물찾기 탐색
        availableActivities.Add(new TreasureFoundActivity(petController, movementController)); // 보물 발견 후 처리
        
        // Environment Activities
        availableActivities.Add(new EnvironmentGatherActivity(petController));
        availableActivities.Add(new ButterflyPlayActivity(petController));
        
        // PetDebug.Log($"{petController.petName}: {availableActivities.Count}개의 활동 등록 완료", this);
    }
    
    private void Update()
    {
        // 주기적으로 AI 업데이트
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdateAI();
        }
        
        // 현재 활동 업데이트
        currentActivity?.Update();
    }
    
    /// <summary>
    /// AI 업데이트 - 가장 우선순위가 높은 활동 선택
    /// </summary>
    public void UpdateAI()
    {
        // 플레이어가 펫을 들고 있을 때만 AI 중단
        if (petState?.IsHolding == true)
            return;
            
        // 현재 활동이 완료되었는지 확인
        if (currentActivity != null && currentActivity.IsComplete)
        {
        // Debug.Log($"[PetAI] {petController.petName}: {currentActivity.Name} 활동이 완료되어 종료");
            currentActivity.Stop();
            currentActivity = null;
        }
            
        // 가장 우선순위가 높은 활동 찾기
        IPetActivity bestActivity = null;
        float highestPriority = 0f;
        
        foreach (var activity in availableActivities)
        {
            if (activity.CanStart(petState, petNeeds))
            {
                float priority = activity.GetPriority(petState, petNeeds);
                if (priority > highestPriority)
                {
                    highestPriority = priority;
                    bestActivity = activity;
                }
            }
        }
        
        // 현재 활동과 다르면 전환
        if (bestActivity != currentActivity)
        {
            // 현재 활동이 중단 불가능한 상태인지 확인
            if (currentActivity != null && !currentActivity.IsInterruptible)
            {
                // 긴급 상황(우선순위 50 이상)이 아니면 전환하지 않음
                if (highestPriority < 50f)
                {
                    // Debug.Log($"[PetAI] {petController.petName}: 현재 활동({currentActivity.Name})이 중단 불가능하여 전환 취소");
                    return;
                }
                
                // 긴급 상황이면 강제 전환 허용
        // Debug.Log($"[PetAI] {petController.petName}: 긴급 상황(우선순위 {highestPriority:F1})으로 {currentActivity.Name}에서 {bestActivity?.Name ?? "None"}으로 강제 전환");
            }
            
            // 현재 활동 중단
            if (currentActivity != null)
            {
                // PetDebug.LogActivityChange(petController.petName, currentActivity.Name, "None");
                currentActivity.Stop();
            }
            
            // 새 활동 시작
            currentActivity = bestActivity;
            if (currentActivity != null)
            {
                // PetDebug.LogActivityChange(petController.petName, currentActivity?.Name ?? "None", bestActivity.Name, highestPriority);
                currentActivity.Start();
            }
        }
    }
    
    /// <summary>
    /// 현재 활동을 강제로 중단하고 AI를 재평가
    /// </summary>
    public void InterruptAndResetAI()
    {
        if (currentActivity != null)
        {
            // PetDebug.LogActivityChange(petController.petName, currentActivity.Name, "None");
            currentActivity.Stop();
            currentActivity = null;
        }
        
        // 즉시 AI 재평가
        UpdateAI();
    }
    
    /// <summary>
    /// 특정 활동을 강제로 설정 (SelectedActivity 등에서 사용)
    /// </summary>
    public void ForceSetActivity(IPetActivity activity)
    {
        if (activity == null) return;
        
        // 현재 활동 중단
        if (currentActivity != null && currentActivity != activity)
        {
            currentActivity.Stop();
        }
        
        // 새 활동 시작
        currentActivity = activity;
        // PetDebug.LogActivityChange(petController.petName, currentActivity?.Name ?? "None", activity.Name);
        currentActivity.Start();
    }
    
    /// <summary>
    /// 현재 활동을 강제로 중단 (보물찾기 종료 등에서 사용)
    /// </summary>
    public void ForceStopCurrentActivity()
    {
        if (currentActivity != null)
        {
        // Debug.Log($"[PetAI] {petController.petName}: 현재 활동 {currentActivity.Name} 강제 중단");
            currentActivity.Stop();
            currentActivity = null;
        }
    }
    
    /// <summary>
    /// 현재 실행 중인 활동 가져오기
    /// </summary>
    public IPetActivity GetCurrentActivity() => currentActivity;
    
    /// <summary>
    /// 특정 타입의 활동 찾기
    /// </summary>
    public T GetActivity<T>() where T : IPetActivity
    {
        foreach (var activity in availableActivities)
        {
            if (activity is T)
                return (T)activity;
        }
        return default(T);
    }
    
    /// <summary>
    /// 현재 활동이 특정 타입인지 확인
    /// </summary>
    public bool IsCurrentActivity<T>() where T : IPetActivity
    {
        return currentActivity is T;
    }
    
    /// <summary>
    /// 현재 활동 이름 가져오기 (디버깅용)
    /// </summary>
    public string GetCurrentActivityName()
    {
        return currentActivity?.Name ?? "None";
    }
}