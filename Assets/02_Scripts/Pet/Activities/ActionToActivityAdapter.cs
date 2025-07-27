using UnityEngine;

/// <summary>
/// 기존 IPetAction을 IPetActivity로 래핑하는 범용 어댑터
/// Phase 3에서 점진적 마이그레이션을 위해 사용
/// </summary>
public class ActionToActivityAdapter : IPetActivity
{
    private readonly IPetAction action;
    private readonly PetController pet;
    
    public string Name => action.GetType().Name.Replace("Action", "");
    public bool IsComplete => false;
    public bool IsInterruptible => true;
    
    public ActionToActivityAdapter(IPetAction petAction, PetController petController)
    {
        action = petAction;
        pet = petController;
    }
    
    public bool CanStart(PetState state, PetNeeds needs)
    {
        // 기본적으로 GetPriority > 0이면 시작 가능
        return action.GetPriority() > 0f;
    }
    
    public float GetPriority(PetState state, PetNeeds needs)
    {
        return action.GetPriority();
    }
    
    public void Start()
    {
        action.OnEnter();
    }
    
    public void Update()
    {
        action.OnUpdate();
    }
    
    public void Stop()
    {
        action.OnExit();
    }
}