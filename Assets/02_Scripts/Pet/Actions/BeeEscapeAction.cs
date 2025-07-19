using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 벌 공격을 받았을 때 펫이 도망가는 행동을 처리하는 액션
/// </summary>
public class BeeEscapeAction : IPetAction
{
    private readonly PetController _pet;
    private readonly PetAnimationController _animationController;
    private readonly PetMovementController _movementController;
    private readonly EmotionManager _emotionManager;
    
    private bool _hasShowedEmotion = false;
    private bool _isEscaping = false;
    private Vector3 _escapeDestination;
    
    private const float ESCAPE_DISTANCE = 15f; // 도망갈 거리
    private const float ESCAPE_SPEED_MULTIPLIER = 1.5f; // 도망갈 때 속도 증가
    
    public BeeEscapeAction(PetController pet)
    {
        _pet = pet;
        _animationController = pet.GetComponent<PetAnimationController>();
        _movementController = pet.GetComponent<PetMovementController>();
        _emotionManager = EmotionManager.Instance;
    }
    
    public float GetPriority()
    {
        // 벌 공격을 받고 있으면서
        if (_pet.isBeingAttackedByBees)
        {
            // 아직 먹고 있는 중이면 먹기를 우선시 (EatAction의 우선순위 2.0보다 낮게)
            PetFeedingController feedingController = _pet.GetComponent<PetFeedingController>();
            if (feedingController != null && feedingController.IsEatingOrSeeking())
            {
                return 1.5f; // 먹기(2.0)보다 낮은 우선순위
            }
            
            // 다 먹었으면 최우선순위로 도망
            return 100f; // 탈진(50)보다도 높은 최우선순위
        }
        
        // 벌 공격이 끝난 후에도 계속 도망
        if (_isEscaping && !_pet.isBeingAttackedByBees)
        {
            // beeAttackSource가 초기화되었는지 확인 (Vector3.zero면 5초 경과)
            if (_pet.beeAttackSource == Vector3.zero)
            {
                // 벌이 완전히 돌아갔으므로 도망 종료
                _isEscaping = false;
                return 0f;
            }
            
            // 아직 벌이 돌아가는 중이면 계속 도망
            return 20f; // 높은 우선순위 유지
        }
        
        return 0f;
    }
    
    public void OnEnter()
    {
        Debug.Log($"[BeeEscapeAction] {_pet.petName}이(가) 벌 공격으로부터 도망가기 시작!");
        
        _hasShowedEmotion = false;
        _isEscaping = true;
        
        // 속도 증가
        if (_pet.agent != null)
        {
            _pet.agent.speed = _pet.baseSpeed * ESCAPE_SPEED_MULTIPLIER;
            _pet.agent.acceleration = _pet.baseAcceleration * 2f;
        }
        
        // 도망갈 방향 계산 (벌 공격 소스로부터 반대 방향)
        CalculateEscapeDestination();
        
        // 도망 애니메이션 시작
        if (_animationController != null)
        {
            _animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        }
    }
    
    public void OnUpdate()
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
            // 목적지에 도착했거나 갈 수 없는 경우 새로운 목적지 계산
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
    
    public void OnExit()
    {
        Debug.Log($"[BeeEscapeAction] {_pet.petName}의 벌 도망 행동 종료");
        
        // 속도 원래대로
        if (_pet.agent != null)
        {
            _pet.agent.speed = _pet.baseSpeed;
            _pet.agent.acceleration = _pet.baseAcceleration;
        }
        
        _isEscaping = false;
        
        // 애니메이션 정지
        if (_animationController != null)
        {
            _animationController.StopContinuousAnimation();
        }
        
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
        
        // 약간의 랜덤성 추가 (좌우로 30도 정도)
        float randomAngle = Random.Range(-30f, 30f);
        escapeDirection = Quaternion.Euler(0, randomAngle, 0) * escapeDirection;
        
        // 도망갈 위치 계산
        _escapeDestination = _pet.transform.position + escapeDirection * ESCAPE_DISTANCE;
        
        // NavMesh 상의 유효한 위치 찾기
        if (NavMesh.SamplePosition(_escapeDestination, out NavMeshHit hit, ESCAPE_DISTANCE, NavMesh.AllAreas))
        {
            _escapeDestination = hit.position;
            _pet.agent.SetDestination(_escapeDestination);
            
            Debug.Log($"[BeeEscapeAction] {_pet.petName}이(가) {_escapeDestination} 방향으로 도망!");
        }
        else
        {
            // 유효한 위치를 찾지 못했다면 랜덤한 방향으로 도망
            if (_movementController != null)
            {
                _movementController.SetRandomDestination(ESCAPE_DISTANCE);
            }
        }
    }
}