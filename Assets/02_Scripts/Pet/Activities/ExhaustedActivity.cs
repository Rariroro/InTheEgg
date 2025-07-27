using UnityEngine;

/// <summary>
/// 펫이 탈진했을 때의 활동
/// </summary>
public class ExhaustedActivity : IPetActivity
{
    private readonly PetController _pet;
    private readonly PetAnimationController _animationController;
    
    private bool _hasShowedEmotion = false;
    
    public string Name => "Exhausted";
    public bool IsComplete => !_pet.isExhausted;
    public bool IsInterruptible => false; // 긴급 상황이므로 중단 불가
    
    public ExhaustedActivity(PetController pet, PetAnimationController animationController)
    {
        _pet = pet;
        _animationController = animationController;
    }
    
    public bool CanStart(PetState state, PetNeeds needs)
    {
        return _pet.isExhausted;
    }
    
    public float GetPriority(PetState state, PetNeeds needs)
    {
        return _pet.isExhausted ? 50f : 0f;
    }
    
    public void Start()
    {
        Debug.Log($"[ExhaustedActivity] {_pet.petName}이(가) 탈진했습니다!");
        
        _hasShowedEmotion = false;
        
        // 이동 정지
        if (_pet.agent != null)
        {
            _pet.agent.isStopped = true;
            _pet.agent.velocity = Vector3.zero;
        }
        
        // 쓰러진 애니메이션
        _animationController?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Die);
    }
    
    public void Update()
    {
        if (!_hasShowedEmotion && EmotionManager.Instance != null)
        {
            EmotionManager.Instance.ShowPetEmotion(_pet, EmotionType.Dizzy, 5f);
            _hasShowedEmotion = true;
        }
        
        // 탈진 상태에서는 아무것도 하지 않음
    }
    
    public void Stop()
    {
        Debug.Log($"[ExhaustedActivity] {_pet.petName}의 탈진 상태가 해제되었습니다.");
        
        // 이동 재개
        if (_pet.agent != null)
        {
            _pet.agent.isStopped = false;
        }
        
        // 애니메이션 정지
        _animationController?.StopContinuousAnimation();
    }
}