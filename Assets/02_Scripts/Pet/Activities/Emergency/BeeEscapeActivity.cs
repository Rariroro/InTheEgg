using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 벌 공격을 받았을 때 펫이 도망가는 활동
/// </summary>
public class BeeEscapeActivity : PetActivityAdapter
{
    private readonly PetAnimationController animationController;
    private readonly PetMovementController movementController;
    private readonly EmotionManager emotionManager;
    
    private bool hasShowedEmotion = false;
    private bool isEscaping = false;
    private Vector3 escapeDestination;
    
    private const float ESCAPE_DISTANCE = 15f; // 도망갈 거리
    private const float ESCAPE_SPEED_MULTIPLIER = 1.5f; // 도망갈 때 속도 증가
    
    public override string Name => "BeeEscape";
    public override bool IsInterruptible => false; // 벌 도망은 중단 불가
    
    public BeeEscapeActivity(PetController petController) : base(petController)
    {
        animationController = pet.GetComponent<PetAnimationController>();
        movementController = pet.GetComponent<PetMovementController>();
        emotionManager = EmotionManager.Instance;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 벌 공격을 받고 있거나 도망 중인 경우
        return pet.State.IsBeingAttacked || isEscaping;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 벌 공격을 받고 있으면서
        if (pet.State.IsBeingAttacked)
        {
            // 아직 먹고 있는 중이면 먹기를 우선시
            PetFeedingController feedingController = pet.GetComponent<PetFeedingController>();
            if (feedingController != null && feedingController.IsEatingOrSeeking())
            {
                // 배고픔이 아직 남아있으면 계속 먹어야 함
                if (pet.Needs != null && pet.Needs.Hunger >= 70f)
                {
                    Debug.Log($"[BeeEscapeActivity] {pet.petName}: 아직 배고픔({pet.Needs.Hunger:F1}), 계속 먹기");
                    return 0.5f; // 매우 낮은 우선순위 - 먹기를 방해하지 않음
                }
                
                // 배고픔이 해소되었으면 이제 도망갈 수 있음
                Debug.Log($"[BeeEscapeActivity] {pet.petName}: 배고픔 해소({pet.Needs.Hunger:F1}), 이제 도망!");
                return 100f; // 최우선순위로 도망
            }
            
            // 다 먹었으면 최우선순위로 도망
            return 100f; // 탈진(50)보다도 높은 최우선순위
        }
        
        // 벌 공격이 끝난 후에도 계속 도망
        if (isEscaping && !pet.State.IsBeingAttacked)
        {
            // beeAttackSource가 초기화되었는지 확인 (Vector3.zero면 5초 경과)
            if (pet.State.BeeAttackSource == Vector3.zero)
            {
                // 벌이 완전히 돌아갔으므로 도망 종료
                isEscaping = false;
                return 0f;
            }
            
            // 아직 벌이 돌아가는 중이면 계속 도망
            return 20f; // 높은 우선순위 유지
        }
        
        return 0f;
    }
    
    
    public override void Start()
    {
        Debug.Log($"[BeeEscapeActivity] {pet.petName}이(가) 벌 공격으로부터 도망가기 시작!");
        
        hasShowedEmotion = false;
        isEscaping = true;
        
        // 속도 증가
        if (pet.agent != null)
        {
            pet.agent.speed = pet.baseSpeed * ESCAPE_SPEED_MULTIPLIER;
            pet.agent.acceleration = pet.baseAcceleration * 2f;
        }
        
        // 도망갈 방향 계산 (벌 공격 소스로부터 반대 방향)
        CalculateEscapeDestination();
        
        // 도망 애니메이션 시작
        if (animationController != null)
        {
            animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        }
    }
    
    public override void Update()
    {
        // 놀람 감정 표현 (한 번만)
        if (!hasShowedEmotion && emotionManager != null)
        {
            emotionManager.ShowPetEmotion(pet, EmotionType.Scared, 3f);
            hasShowedEmotion = true;
        }
        
        // 도망 중
        if (isEscaping)
        {
            // 목적지에 도착했거나 갈 수 없는 경우 새로운 목적지 계산
            if (!pet.agent.pathPending && pet.agent.remainingDistance < 1f)
            {
                // 벌 공격이 계속되고 있으면 다시 도망
                if (pet.State.IsBeingAttacked)
                {
                    CalculateEscapeDestination();
                }
                else
                {
                    isEscaping = false;
                }
            }
        }
        
        // 방향 전환 처리
        if (pet.movementController != null)
        {
            pet.movementController.HandleRotation();
        }
    }
    
    public override void Stop()
    {
        Debug.Log($"[BeeEscapeActivity] {pet.petName}의 벌 도망 행동 종료");
        
        // 속도 원래대로
        if (pet.agent != null)
        {
            pet.agent.speed = pet.baseSpeed;
            pet.agent.acceleration = pet.baseAcceleration;
        }
        
        isEscaping = false;
        
        // 애니메이션 정지
        if (animationController != null)
        {
            animationController.StopContinuousAnimation();
        }
        
        // 안도 감정 표현
        if (emotionManager != null && !pet.State.IsBeingAttacked)
        {
            emotionManager.ShowPetEmotion(pet, EmotionType.Sad, 2f);
        }
    }
    
    private void CalculateEscapeDestination()
    {
        if (pet.agent == null || !pet.agent.enabled) return;
        
        // 벌 공격 소스로부터 반대 방향 계산
        Vector3 escapeDirection = (pet.transform.position - pet.State.BeeAttackSource).normalized;
        
        // 약간의 랜덤성 추가 (좌우로 30도 정도)
        float randomAngle = Random.Range(-30f, 30f);
        escapeDirection = Quaternion.Euler(0, randomAngle, 0) * escapeDirection;
        
        // 도망갈 위치 계산
        escapeDestination = pet.transform.position + escapeDirection * ESCAPE_DISTANCE;
        
        // NavMesh 상의 유효한 위치 찾기
        if (NavMesh.SamplePosition(escapeDestination, out NavMeshHit hit, ESCAPE_DISTANCE, NavMesh.AllAreas))
        {
            escapeDestination = hit.position;
            pet.agent.SetDestination(escapeDestination);
            
            Debug.Log($"[BeeEscapeActivity] {pet.petName}이(가) {escapeDestination} 방향으로 도망!");
        }
        else
        {
            // 유효한 위치를 찾지 못했다면 랜덤한 방향으로 도망
            if (movementController != null)
            {
                movementController.SetRandomDestination(ESCAPE_DISTANCE);
            }
        }
    }
}