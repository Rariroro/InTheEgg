using UnityEngine;
using System.Collections;
using PetAIProperties = PetTraits;

/// <summary>
/// Playful 성격의 펫이 다이빙 스팟에서 물로 점프하는 활동
/// </summary>
public class DivingActivity : PetActivityAdapter
{
    // 현재 다이빙 중인 펫 (한 번에 한 펫만 다이빙 가능)
    private static PetController currentDiver = null;
    
    // 다이빙 관련 상태
    private Transform divingSpot;
    private bool isMovingToSpot = false;
    private bool isDiving = false;
    private float lastDivingTime = -60f; // 마지막 다이빙 시간 (쿨다운용)
    private float failedAttemptTime = -60f; // 실패한 시도 시간 기록
    private const float DIVING_COOLDOWN = 30f; // 개별 펫 다이빙 쿨다운
    private const float FAILED_ATTEMPT_COOLDOWN = 60f; // 실패 후 재시도 쿨다운
    private const float SPOT_ARRIVAL_DISTANCE = 2f; // 스팟 도착 판정 거리
    private const float MAX_DISTANCE_TO_WATER = 50f; // 물에서 최대 거리
    
    // 점프 관련
    private Vector3 jumpStartPosition;
    private Vector3 jumpTargetPosition;
    private float jumpProgress = 0f;
    private float jumpHeight = 5f; // 점프 높이
    private float jumpDuration = 1.5f; // 점프 지속 시간
    
    public override string Name => "Diving";
    public override bool IsInterruptible => false; // 다이빙 중에는 중단 불가
    
    public DivingActivity(PetController petController) : base(petController)
    {
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // Playful 성격이 아니면 불가
        if (pet.personality != PetAIProperties.Personality.Playful)
        {
            // Debug.Log($"[DivingActivity] {pet.petName}은 Playful 성격이 아닙니다. (성격: {pet.personality})");
            return false;
        }
            
        // 이미 다이빙 중이면 계속
        if (isMovingToSpot || isDiving)
            return true;
            
        // 다른 중요한 상태 체크
        if (state.IsHolding || state.IsSelected || state.IsGathering)
            return false;
            
        // 욕구 체크
        if (needs.Hunger > 70f || needs.Sleepiness > 70f)
            return false;
            
        // 쿨다운 체크
        if (Time.time - lastDivingTime < DIVING_COOLDOWN)
            return false;
            
        // 실패 후 쿨다운 체크
        if (Time.time - failedAttemptTime < FAILED_ATTEMPT_COOLDOWN)
            return false;
            
        // 다른 펫이 사용 중이면 불가
        if (currentDiver != null && currentDiver != pet)
            return false;
            
        // 다이빙 스팟 찾기
        GameObject spotObject = GameObject.FindWithTag("DivingSpot");
        if (spotObject == null)
        {
            Debug.LogWarning("[DivingActivity] DivingSpot 태그를 가진 오브젝트를 찾을 수 없습니다!");
            return false;
        }
            
        divingSpot = spotObject.transform;
        
        // 물 근처에 있는지 체크 (NavMesh의 Water 영역 근처)
        float distanceToSpot = Vector3.Distance(pet.transform.position, divingSpot.position);
        if (distanceToSpot > MAX_DISTANCE_TO_WATER)
            return false;
            
        // 테스트를 위해 100% 확률로 설정
        Debug.Log($"[DivingActivity] {pet.petName}: 다이빙 가능 체크 통과! (거리: {distanceToSpot:F1})");
        return true; // 테스트용 100% 확률
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 이미 다이빙 중이면 높은 우선순위
        if (isMovingToSpot || isDiving)
            return 7f; // 높은 우선순위로 완료까지 보장
            
        // 테스트를 위해 높은 우선순위 설정
        return 5f;
    }
    
    public override void Start()
    {
        Debug.Log($"[DivingActivity] {pet.petName}: 다이빙 활동 시작!");
        
        // 현재 다이버로 등록
        currentDiver = pet;
        isMovingToSpot = true;
        isDiving = false;
        
        // 다이빙 스팟 재확인
        if (divingSpot == null)
        {
            GameObject spotObject = GameObject.FindWithTag("DivingSpot");
            if (spotObject != null)
            {
                divingSpot = spotObject.transform;
                Debug.Log($"[DivingActivity] {pet.petName}: 다이빙 스팟 재발견");
            }
            else
            {
                Debug.LogError($"[DivingActivity] {pet.petName}: 다이빙 스팟을 찾을 수 없음!");
                Stop();
                return;
            }
        }
        
        // NavMeshAgent 상태 확인 및 활성화
        if (pet.agent != null)
        {
            if (!pet.agent.enabled)
            {
                Debug.Log($"[DivingActivity] {pet.petName}: NavMeshAgent 활성화");
                pet.agent.enabled = true;
            }
            if (!pet.agent.isOnNavMesh)
            {
                Debug.LogWarning($"[DivingActivity] {pet.petName}: NavMesh 위에 없음!");
                pet.agent.Warp(pet.transform.position);
            }
        }
        else
        {
            Debug.LogError($"[DivingActivity] {pet.petName}: NavMeshAgent가 없음!");
            Stop();
            return;
        }
        
        // 다이빙 스팟으로 이동 시작
        pet.StartCoroutine(MoveToSpotAndDive());
    }
    
    private IEnumerator MoveToSpotAndDive()
    {
        Debug.Log($"[DivingActivity] {pet.petName}: 코루틴 시작, 스팟으로 이동 중...");
        
        // 1. 다이빙 스팟으로 이동
        if (pet.agent == null)
        {
            Debug.LogError($"[DivingActivity] {pet.petName}: agent가 null!");
            Stop();
            yield break;
        }
        
        if (!pet.agent.enabled)
        {
            Debug.Log($"[DivingActivity] {pet.petName}: agent 활성화");
            pet.agent.enabled = true;
        }
        
        if (!pet.agent.isOnNavMesh)
        {
            Debug.LogWarning($"[DivingActivity] {pet.petName}: NavMesh에 위치시킴");
            pet.agent.Warp(pet.transform.position);
        }
        
        pet.agent.SetDestination(divingSpot.position);
        Debug.Log($"[DivingActivity] {pet.petName}: 목적지 설정 완료 - {divingSpot.position}");
        
        // 스팟에 도착할 때까지 대기
        float timeoutCounter = 0f;
        while (isMovingToSpot && Vector3.Distance(pet.transform.position, divingSpot.position) > SPOT_ARRIVAL_DISTANCE)
        {
            // 타임아웃 체크 (30초)
            timeoutCounter += 0.1f;
            if (timeoutCounter > 30f)
            {
                Debug.LogWarning($"[DivingActivity] {pet.petName}: 이동 타임아웃!");
                failedAttemptTime = Time.time; // 실패 시간 기록
                isMovingToSpot = false;
                isDiving = false;
                if (currentDiver == pet)
                {
                    currentDiver = null;
                }
                // NavMeshAgent 재활성화
                if (pet.agent != null && !pet.agent.enabled)
                {
                    pet.agent.enabled = true;
                    pet.agent.Warp(pet.transform.position);
                }
                yield break;
            }
            
            // agent가 멈춰있는지 체크
            if (pet.agent.velocity.magnitude < 0.1f && timeoutCounter > 1f)
            {
                Debug.Log($"[DivingActivity] {pet.petName}: 재이동 시도");
                pet.agent.SetDestination(divingSpot.position);
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.Log($"[DivingActivity] {pet.petName}: 스팟 도착!");
        
        // 2. 도착 후 점프 준비
        isMovingToSpot = false;
        isDiving = true;
        
        // NavMeshAgent 비활성화 (점프 중에는 직접 제어)
        if (pet.agent != null)
        {
            pet.agent.enabled = false;
        }
        
        // 3. 점프 시작 위치와 목표 위치 설정
        jumpStartPosition = pet.transform.position;
        
        // 다이빙 스팟이 향하는 방향(forward)으로 점프
        // Unity 씬에서 DivingSpot의 Z축(파란 화살표)이 물을 향하도록 설정해야 함
        Vector3 toWater = divingSpot.forward;
        jumpTargetPosition = divingSpot.position + toWater * 6f; // 6유닛 앞으로
        jumpTargetPosition.y = 4f; // 물 속으로 깊이 다이빙 (물 표면보다 1.5 유닛 아래)
        
        // 4. Happy 감정 표현
        pet.ShowEmotion(EmotionType.Happy);
        
        // 5. 점프 애니메이션 시작
        var animController = pet.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            pet.StartCoroutine(animController.PlaySpecialAnimation(PetAnimationController.PetAnimationType.Jump, false));
        }
        
        // 6. 점프 실행
        jumpProgress = 0f;
        while (jumpProgress < 1f)
        {
            jumpProgress += Time.deltaTime / jumpDuration;
            
            // 포물선 궤적 계산
            Vector3 currentPos = Vector3.Lerp(jumpStartPosition, jumpTargetPosition, jumpProgress);
            float parabola = 4f * jumpHeight * jumpProgress * (1f - jumpProgress);
            currentPos.y += parabola;
            
            pet.transform.position = currentPos;
            
            // 목표 방향으로 회전
            Vector3 direction = jumpTargetPosition - jumpStartPosition;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                pet.transform.rotation = Quaternion.LookRotation(direction);
            }
            
            yield return null;
        }
        
        // 7. 착수 - 큰 물보라 효과
        var waterController = pet.GetComponent<PetWaterBehaviorController>();
        if (waterController != null)
        {
            waterController.CreateDivingSplash();
        }
        
        // 8. NavMeshAgent 재활성화
        if (pet.agent != null)
        {
            pet.agent.enabled = true;
            pet.agent.Warp(pet.transform.position);
        }
        
        // 9. 다이빙 완료
        isDiving = false;
        lastDivingTime = Time.time;
        
        // 10. 점유 해제
        if (currentDiver == pet)
        {
            currentDiver = null;
        }
        
        Debug.Log($"[DivingActivity] {pet.petName}: 다이빙 완료!");
    }
    
    public override void Update()
    {
        // 코루틴에서 처리하므로 Update는 비워둠
    }
    
    public override void Stop()
    {
        Debug.Log($"[DivingActivity] {pet.petName}: 다이빙 활동 중단");
        
        // 상태 초기화
        isMovingToSpot = false;
        isDiving = false;
        
        // NavMeshAgent 재활성화 (혹시 비활성화되어 있을 경우)
        if (pet.agent != null && !pet.agent.enabled)
        {
            pet.agent.enabled = true;
            pet.agent.Warp(pet.transform.position);
        }
        
        // 점유 해제
        if (currentDiver == pet)
        {
            currentDiver = null;
        }
    }
}