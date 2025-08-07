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
    private Coroutine divingCoroutine = null; // 코루틴 참조 저장
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
    public override bool IsInterruptible => !isDiving; // 실제 다이빙 중일 때만 중단 불가, 이동 중에는 중단 가능
    
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
        
        // 다른 이동 관련 컴포넌트 중지
        if (pet.movementController != null)
        {
            pet.movementController.StopMovement();
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
            
            // agent 설정 초기화 - 중요!
            pet.agent.isStopped = false;  // 멈춤 상태 해제
            pet.agent.speed = pet.baseSpeed;  // 속도 설정
            pet.agent.acceleration = 8f;  // 가속도 설정
            pet.agent.updateRotation = true;  // 회전 업데이트 활성화
            pet.agent.ResetPath();  // 이전 경로 클리어
            
            Debug.Log($"[DivingActivity] {pet.petName}: Agent 상태 - isStopped: {pet.agent.isStopped}, speed: {pet.agent.speed}, acceleration: {pet.agent.acceleration}");
        }
        else
        {
            Debug.LogError($"[DivingActivity] {pet.petName}: NavMeshAgent가 없음!");
            Stop();
            return;
        }
        
        // 다이빙 스팟으로 이동 시작
        divingCoroutine = pet.StartCoroutine(MoveToSpotAndDive());
    }
    
    private IEnumerator MoveToSpotAndDive()
    {
        Debug.Log($"[DivingActivity] {pet.petName}: 코루틴 시작, 스팟으로 이동 중...");
        
        // 펫이 들렸는지 체크
        if (pet.State.IsHolding)
        {
            Debug.Log($"[DivingActivity] {pet.petName}: 펫이 들려있어 다이빙 중단");
            Stop();
            yield break;
        }
        
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
        
        
        
        
        // agent가 멈춰있지 않은지 다시 한번 확인
        if (pet.agent.isStopped)
        {
            Debug.LogWarning($"[DivingActivity] {pet.petName}: agent가 멈춰있어서 재시작");
            pet.agent.isStopped = false;
        }
        
        pet.agent.SetDestination(divingSpot.position);
        Debug.Log($"[DivingActivity] {pet.petName}: 목적지 설정 완료 - {divingSpot.position}");
        
        // 경로 유효성 체크
        yield return null; // 한 프레임 대기
        
        // agent 이동 상태 디버그
        Debug.Log($"[DivingActivity] {pet.petName}: SetDestination 후 - hasPath: {pet.agent.hasPath}, pathPending: {pet.agent.pathPending}, velocity: {pet.agent.velocity.magnitude}, isStopped: {pet.agent.isStopped}");
        if (!pet.agent.hasPath && !pet.agent.pathPending)
        {
            Debug.LogWarning($"[DivingActivity] {pet.petName}: 다이빙 스팟까지 경로를 찾을 수 없음!");
            failedAttemptTime = Time.time;
            isMovingToSpot = false;
            isDiving = false;
            if (currentDiver == pet)
            {
                currentDiver = null;
            }
            yield break;
        }
        
        // 스팟에 도착할 때까지 대기
        float timeoutCounter = 0f;
        int retryCount = 0;
        const int MAX_RETRIES = 3;
        float lastDebugTime = 0f;
        
        while (isMovingToSpot && Vector3.Distance(pet.transform.position, divingSpot.position) > SPOT_ARRIVAL_DISTANCE)
        {
            // 펫이 들렸는지 체크
            if (pet.State.IsHolding)
            {
                Debug.Log($"[DivingActivity] {pet.petName}: 이동 중 펫이 들려서 다이빙 중단");
                Stop();
                yield break;
            }
            
            // 펫이 선택되었는지 체크 (유저가 터치한 경우)
            if (pet.State.IsSelected)
            {
                Debug.Log($"[DivingActivity] {pet.petName}: 이동 중 펫이 선택되어 다이빙 중단");
                Stop();
                yield break;
            }
            
            // 1초마다 agent 상태 디버그 출력
            if (Time.time - lastDebugTime > 1f)
            {
                lastDebugTime = Time.time;
                Debug.Log($"[DivingActivity] {pet.petName}: 이동 중 - 거리: {Vector3.Distance(pet.transform.position, divingSpot.position):F1}, velocity: {pet.agent.velocity.magnitude:F2}, isStopped: {pet.agent.isStopped}");
            }
            
            // 타임아웃 체크 (30초)
            timeoutCounter += 0.1f;
            if (timeoutCounter > 30f)
            {
                Debug.LogWarning($"[DivingActivity] {pet.petName}: 이동 타임아웃!");
                failedAttemptTime = Time.time;
                isMovingToSpot = false;
                isDiving = false;
                if (currentDiver == pet)
                {
                    currentDiver = null;
                }
                if (pet.agent != null && !pet.agent.enabled)
                {
                    pet.agent.enabled = true;
                    pet.agent.Warp(pet.transform.position);
                }
                yield break;
            }
            
            // agent가 멈춰있고 경로가 없는 경우 체크
            if (pet.agent.velocity.magnitude < 0.1f && timeoutCounter > 1f)
            {
                // 경로 상태 확인
                if (!pet.agent.hasPath || pet.agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
                {
                    retryCount++;
                    if (retryCount >= MAX_RETRIES)
                    {
                        Debug.LogWarning($"[DivingActivity] {pet.petName}: 경로 찾기 실패 ({retryCount}회 시도)");
                        failedAttemptTime = Time.time;
                        isMovingToSpot = false;
                        isDiving = false;
                        if (currentDiver == pet)
                        {
                            currentDiver = null;
                        }
                        yield break;
                    }
                    
                    Debug.Log($"[DivingActivity] {pet.petName}: 재이동 시도 ({retryCount}/{MAX_RETRIES})");
                    
                    // NavMeshAgent 안전 체크
                    if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
                    {
                        pet.agent.ResetPath();
                        yield return new WaitForSeconds(0.5f);
                        
                        // 다시 체크 (대기 후 상태가 변경될 수 있음)
                        if (pet.State.IsHolding || pet.State.IsSelected)
                        {
                            Debug.Log($"[DivingActivity] {pet.petName}: 재이동 시도 중 펫이 들려서 중단");
                            Stop();
                            yield break;
                        }
                        
                        if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
                        {
                            // agent 재설정
                            pet.agent.isStopped = false;
                            pet.agent.speed = pet.baseSpeed;
                            pet.agent.SetDestination(divingSpot.position);
                            Debug.Log($"[DivingActivity] {pet.petName}: 재이동 명령 - speed: {pet.agent.speed}, isStopped: {pet.agent.isStopped}");
                        }
                        else
                        {
                            Debug.LogWarning($"[DivingActivity] {pet.petName}: NavMeshAgent가 유효하지 않아 이동 중단");
                            failedAttemptTime = Time.time;
                            Stop();
                            yield break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[DivingActivity] {pet.petName}: NavMeshAgent가 유효하지 않아 재이동 불가");
                        failedAttemptTime = Time.time;
                        Stop();
                        yield break;
                    }
                }
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.Log($"[DivingActivity] {pet.petName}: 스팟 도착!");
        
        // 2. 도착 후 점프 준비
        // 도착 직전에 다시 체크
        if (pet.State.IsHolding)
        {
            Debug.Log($"[DivingActivity] {pet.petName}: 점프 직전 펫이 들려서 다이빙 중단");
            Stop();
            yield break;
        }
        
        isMovingToSpot = false;
        isDiving = true;
        
        // NavMeshAgent 비활성화 (점프 중에는 직접 제어)
        if (pet.agent != null && pet.agent.enabled)
        {
            pet.agent.enabled = false;
        }
        
        // 3. 점프 시작 위치와 목표 위치 설정
        jumpStartPosition = pet.transform.position;
        
        // 디버그: 높이 정보 출력
        Debug.Log($"[DivingActivity] 다이빙 스팟 높이: Y={divingSpot.position.y:F1}");
        Debug.Log($"[DivingActivity] 펫 현재 높이: Y={pet.transform.position.y:F1}");
        
        // 다이빙 스팟이 향하는 방향(forward)으로 점프
        // Unity 씬에서 DivingSpot의 Z축(파란 화살표)이 물을 향하도록 설정해야 함
        Vector3 toWater = divingSpot.forward;
        jumpTargetPosition = divingSpot.position + toWater * 6f; // 6유닛 앞으로
        
        // 물 표면이 Y=5~6 정도이므로 그 근처로 착수
        // 다이빙 스팟이 Y=7이므로 물 표면은 약 Y=5 정도
        jumpTargetPosition.y = 5f; // 물 표면 근처 착수
        
        Debug.Log($"[DivingActivity] 착수 목표 높이: Y={jumpTargetPosition.y:F1}");
        
        // 4. Happy 감정 표현
        pet.ShowEmotion(EmotionType.Happy);
        
        // 5. 점프 애니메이션은 제거 (포물선 움직임만으로 충분)
        // 애니메이터에서 직접 점프 애니메이션 트리거 (한 번만)
        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Jump);
        }
        
        // 6. 점프 실행
        jumpProgress = 0f;
        while (jumpProgress < 1f)
        {
            // 점프 중에도 펫이 들렸는지 체크
            if (pet.State.IsHolding)
            {
                Debug.Log($"[DivingActivity] {pet.petName}: 점프 중 펫이 들려서 다이빙 중단");
                // NavMeshAgent 재활성화 시도
                if (pet.agent != null && !pet.agent.enabled)
                {
                    pet.agent.enabled = true;
                    pet.agent.Warp(pet.transform.position);
                }
                Stop();
                yield break;
            }
            
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
        
        // 7. 착수 - 큰 물보라 효과 및 다이빙 모드 시작
        var waterController = pet.GetComponent<PetWaterBehaviorController>();
        if (waterController != null)
        {
            waterController.StartDivingSequence();
        }
        
        // 8. 부상 대기 (PetWaterBehaviorController가 자동으로 처리)
        // 3초 정도 대기하여 자연스러운 부상 시간 확보
        yield return new WaitForSeconds(3f);
        
        // 애니메이션을 기본 상태로 리셋
        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", 0);
        }
        
        // 9. NavMeshAgent 재활성화
        // 펫이 들려있지 않을 때만 재활성화
        if (!pet.State.IsHolding && pet.agent != null && !pet.agent.enabled)
        {
            pet.agent.enabled = true;
            pet.agent.Warp(pet.transform.position);
        }
        
        // 10. 다이빙 완료
        isDiving = false;
        lastDivingTime = Time.time;
        
        // 11. 점유 해제
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
        
        // 코루틴 중단
        if (divingCoroutine != null)
        {
            pet.StopCoroutine(divingCoroutine);
            divingCoroutine = null;
            Debug.Log($"[DivingActivity] {pet.petName}: 다이빙 코루틴 중단됨");
        }
        
        // 상태 초기화
        isMovingToSpot = false;
        isDiving = false;
        
        // NavMeshAgent 재활성화 (펫이 들려있지 않고 비활성화되어 있을 경우)
        if (!pet.State.IsHolding && pet.agent != null && !pet.agent.enabled)
        {
            // NavMesh 위에 있는지 확인 후 재활성화
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(pet.transform.position, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                pet.agent.enabled = true;
                pet.agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"[DivingActivity] {pet.petName}: NavMesh 위치를 찾을 수 없어 agent 재활성화 실패");
            }
        }
        
        // 점유 해제
        if (currentDiver == pet)
        {
            currentDiver = null;
        }
    }
}