using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 벌 공격을 받았을 때 펫이 도망가는 활동
/// </summary>
public class BeeEscapeActivity : IPetActivity
{
    private readonly PetController _pet;
    private readonly PetAnimationController _animationController;
    private readonly PetMovementController _movementController;
    private readonly EmotionManager _emotionManager;
    
    private bool _hasShowedEmotion = false;
    private bool _isEscaping = false;
    private Vector3 _escapeDestination;
    
    private const float ESCAPE_DISTANCE = 15f;
    private const float ESCAPE_SPEED_MULTIPLIER = 1.5f;
    
    public string Name => "BeeEscape";
    public bool IsComplete => !_isEscaping && !_pet.isBeingAttackedByBees;
    public bool IsInterruptible => false; // 긴급 상황이므로 중단 불가
    
    public BeeEscapeActivity(PetController pet, PetMovementController movementController, PetAnimationController animationController)
    {
        _pet = pet;
        _movementController = movementController;
        _animationController = animationController;
        _emotionManager = EmotionManager.Instance;
    }
    
    public bool CanStart(PetState state, PetNeeds needs)
    {
        // 벌 공격을 받고 있으면 시작 가능
        if (_pet.isBeingAttackedByBees)
        {
            // 아직 먹고 있는 중이면 먹기를 우선시
            PetFeedingController feedingController = _pet.GetComponent<PetFeedingController>();
            if (feedingController != null && feedingController.IsEatingOrSeeking())
            {
                return false;
            }
            return true;
        }
        
        // 이미 도망 중이면 계속
        return _isEscaping && _pet.beeAttackSource != Vector3.zero;
    }
    
    public float GetPriority(PetState state, PetNeeds needs)
    {
        if (_pet.isBeingAttackedByBees)
        {
            PetFeedingController feedingController = _pet.GetComponent<PetFeedingController>();
            if (feedingController != null && feedingController.IsEatingOrSeeking())
            {
                return 1.5f; // 먹기보다 낮은 우선순위
            }
            return 100f; // 최우선순위
        }
        
        if (_isEscaping && _pet.beeAttackSource != Vector3.zero)
        {
            return 20f; // 계속 도망
        }
        
        return 0f;
    }
    
    public void Start()
    {
        Debug.Log($"[BeeEscapeActivity] {_pet.petName}이(가) 벌 공격으로부터 도망가기 시작!");
        
        _hasShowedEmotion = false;
        _isEscaping = true;
        
        // 속도 증가
        if (_pet.agent != null)
        {
            _pet.agent.speed = _pet.baseSpeed * ESCAPE_SPEED_MULTIPLIER;
            _pet.agent.acceleration = _pet.baseAcceleration * 2f;
        }
        
        // 도망갈 방향 계산
        CalculateEscapeDestination();
        
        // 도망 애니메이션 시작
        _animationController?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
    }
    
    public void Update()
    {
        // 놀람 감정 표현 (한 번만)
        if (!_hasShowedEmotion && _emotionManager != null)
        {
            _emotionManager.ShowPetEmotion(_pet, EmotionType.Scared, 3f);
            _hasShowedEmotion = true;
        }
        
        // 도망 중
        if (_isEscaping)
        {
            // 목적지에 도착했거나 갈 수 없는 경우
            if (!_pet.agent.pathPending && _pet.agent.remainingDistance < 1f)
            {
                // 벌 공격이 계속되고 있으면 다시 도망
                if (_pet.isBeingAttackedByBees)
                {
                    CalculateEscapeDestination();
                }
                else
                {
                    _isEscaping = false;
                }
            }
        }
        
        // 방향 전환 처리
        _pet.HandleRotation();
    }
    
    public void Stop()
    {
        Debug.Log($"[BeeEscapeActivity] {_pet.petName}의 벌 도망 행동 종료");
        
        // 속도 원래대로
        if (_pet.agent != null)
        {
            _pet.agent.speed = _pet.baseSpeed;
            _pet.agent.acceleration = _pet.baseAcceleration;
        }
        
        _isEscaping = false;
        
        // 애니메이션 정지
        _animationController?.StopContinuousAnimation();
        
        // 안도 감정 표현
        if (_emotionManager != null && !_pet.isBeingAttackedByBees)
        {
            _emotionManager.ShowPetEmotion(_pet, EmotionType.Sad, 2f);
        }
    }
    
    private void CalculateEscapeDestination()
    {
        if (_pet.agent == null || !_pet.agent.enabled) return;
        
        // 벌 공격 소스로부터 반대 방향 계산
        Vector3 escapeDirection = (_pet.transform.position - _pet.beeAttackSource).normalized;
        
        // 약간의 랜덤성 추가
        float randomAngle = Random.Range(-30f, 30f);
        escapeDirection = Quaternion.Euler(0, randomAngle, 0) * escapeDirection;
        
        // 도망갈 위치 계산
        _escapeDestination = _pet.transform.position + escapeDirection * ESCAPE_DISTANCE;
        
        // NavMesh 상의 유효한 위치 찾기
        if (NavMesh.SamplePosition(_escapeDestination, out NavMeshHit hit, ESCAPE_DISTANCE, NavMesh.AllAreas))
        {
            _escapeDestination = hit.position;
            _pet.agent.SetDestination(_escapeDestination);
            
            Debug.Log($"[BeeEscapeActivity] {_pet.petName}이(가) {_escapeDestination} 방향으로 도망!");
        }
        else
        {
            // 유효한 위치를 찾지 못했다면 랜덤한 방향으로 도망
            _movementController?.SetRandomDestination(ESCAPE_DISTANCE);
        }
    }
}