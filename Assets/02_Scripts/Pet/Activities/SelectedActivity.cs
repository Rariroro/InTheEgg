using UnityEngine;

/// <summary>
/// 플레이어에게 선택된 상태에서의 활동
/// </summary>
public class SelectedActivity : IPetActivity
{
    private readonly PetController _pet;
    private bool _hasShowedEmotion = false;
    
    public string Name => "Selected";
    public bool IsComplete => !_pet.isSelected;
    public bool IsInterruptible => true;
    
    public SelectedActivity(PetController pet)
    {
        _pet = pet;
    }
    
    public bool CanStart(PetState state, PetNeeds needs)
    {
        // 홀딩 중이거나 PlayerControl 상태에서는 불가
        if (_pet.isHolding || state.CurrentStatus == PetStatus.PlayerControl)
            return false;
            
        return _pet.isSelected;
    }
    
    public float GetPriority(PetState state, PetNeeds needs)
    {
        // 기존 플래그와 새 상태 시스템 모두 체크
        if (_pet.isHolding || (_pet.State != null && _pet.State.IsPlayerControlled))
            return 0f;
            
        return _pet.isSelected ? 5f : 0f;
    }
    
    public void Start()
    {
        Debug.Log($"[SelectedActivity] {_pet.petName}이(가) 선택되었습니다!");
        _hasShowedEmotion = false;
        
        // 선택되면 이동 정지
        if (_pet.agent != null && _pet.agent.enabled)
        {
            _pet.agent.isStopped = true;
            _pet.agent.velocity = Vector3.zero;
        }
    }
    
    public void Update()
    {
        // 한 번만 행복한 감정 표현
        if (!_hasShowedEmotion && EmotionManager.Instance != null)
        {
            EmotionManager.Instance.ShowPetEmotion(_pet, EmotionType.Happy, 2f);
            _hasShowedEmotion = true;
        }
        
        // 선택된 상태에서는 플레이어를 바라봄
        if (Camera.main != null)
        {
            Vector3 lookDirection = Camera.main.transform.position - _pet.transform.position;
            lookDirection.y = 0;
            if (lookDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                _pet.transform.rotation = Quaternion.Slerp(
                    _pet.transform.rotation, 
                    targetRotation, 
                    Time.deltaTime * 5f
                );
            }
        }
    }
    
    public void Stop()
    {
        Debug.Log($"[SelectedActivity] {_pet.petName}의 선택이 해제되었습니다.");
        
        // 이동 재개
        if (_pet.agent != null && _pet.agent.enabled)
        {
            _pet.agent.isStopped = false;
        }
    }
}