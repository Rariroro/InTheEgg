using UnityEngine;

/// <summary>
/// 모이기 명령에 대한 활동
/// </summary>
public class GatherActivity : IPetActivity
{
    private readonly PetController _pet;
    private readonly PetMovementController _movementController;
    
    private Vector3 _gatherPosition;
    private bool _hasArrivedAtGatherPoint = false;
    
    public string Name => "Gather";
    public bool IsComplete => !_pet.isGathering;
    public bool IsInterruptible => false; // 명령이므로 중단 불가
    
    public GatherActivity(PetController pet, PetMovementController movementController)
    {
        _pet = pet;
        _movementController = movementController;
    }
    
    public bool CanStart(PetState state, PetNeeds needs)
    {
        return _pet.isGathering && !_pet.isHolding;
    }
    
    public float GetPriority(PetState state, PetNeeds needs)
    {
        if (_pet.isHolding) return 0f;
        return _pet.isGathering ? 20f : 0f;
    }
    
    public void Start()
    {
        _gatherPosition = _pet.gatherTargetPosition;
        _hasArrivedAtGatherPoint = false;
        
        // 목적지 설정
        if (_pet.agent != null && _pet.agent.enabled)
        {
            _pet.agent.SetDestination(_gatherPosition);
            Debug.Log($"[GatherActivity] {_pet.petName}이(가) {_gatherPosition}로 모이기 시작!");
        }
    }
    
    public void Update()
    {
        if (!_hasArrivedAtGatherPoint && _pet.agent != null)
        {
            // 목적지 도착 확인
            if (!_pet.agent.pathPending && _pet.agent.remainingDistance < 1f)
            {
                _hasArrivedAtGatherPoint = true;
                Debug.Log($"[GatherActivity] {_pet.petName}이(가) 모이기 지점에 도착!");
                
                // 행복한 감정 표현
                if (EmotionManager.Instance != null)
                {
                    EmotionManager.Instance.ShowPetEmotion(_pet, EmotionType.Happy, 2f);
                }
            }
        }
        
        // 방향 전환 처리
        _pet.HandleRotation();
    }
    
    public void Stop()
    {
        Debug.Log($"[GatherActivity] {_pet.petName}의 모이기 행동 종료");
    }
}