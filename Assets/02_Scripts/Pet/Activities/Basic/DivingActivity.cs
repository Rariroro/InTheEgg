using UnityEngine;
using System.Collections;
using PetAIProperties = PetTraits;

/// <summary>
/// 펫의 다이빙 활동을 제어하는 클래스
/// Playful 성격의 펫이 DivingSpot으로 이동하여 물 속으로 다이빙하는 행동을 구현
/// </summary>
public class DivingActivity : PetActivityAdapter
{
    // ===== 동시 다이빙 방지를 위한 정적 변수 =====
    /// <summary>
    /// 현재 다이빙 중인 펫을 추적. 한 번에 한 펫만 다이빙 가능하도록 제한
    /// </summary>
    private static PetController currentDiver = null;
    
    // ===== 캐싱된 물 오브젝트 =====
    private GameObject cachedWaterObject;

    // ===== 다이빙 상태 관리 변수들 =====
    /// <summary>다이빙 지점의 Transform (DivingSpot 태그를 가진 오브젝트)</summary>
    private Transform divingSpot;
    /// <summary>펫이 다이빙 지점으로 이동 중인지 여부</summary>
    private bool isMovingToSpot = false;
    /// <summary>펫이 실제로 다이빙 중인지 여부</summary>
    private bool isDiving = false;
    /// <summary>마지막 다이빙 시간을 기록 (쿨다운 관리용)</summary>
    private float lastDivingTime = -60f;
    /// <summary>실패한 다이빙 시도 시간 (실패 후 재시도 쿨다운용)</summary>
    private float failedAttemptTime = -60f;
    /// <summary>현재 실행 중인 다이빙 코루틴 참조</summary>
    private Coroutine divingCoroutine = null;
    
    // ===== 다이빙 관련 상수 설정 =====
    /// <summary>다이빙 후 재시도까지 대기 시간 (30초)</summary>
    private const float DIVING_COOLDOWN = 30f;
    /// <summary>다이빙 실패 후 재시도까지 대기 시간 (60초)</summary>
    private const float FAILED_ATTEMPT_COOLDOWN = 60f;
    /// <summary>다이빙 지점 도착 판정 거리 (2유닛)</summary>
    private const float SPOT_ARRIVAL_DISTANCE = 2f;
    /// <summary>다이빙 지점까지 최대 허용 거리 (50유닛)</summary>
    private const float MAX_DISTANCE_TO_WATER = 50f;

    // ===== 점프 애니메이션 관련 변수들 =====
    /// <summary>점프 시작 위치</summary>
    private Vector3 jumpStartPosition;
    /// <summary>점프 목표 위치 (물 속)</summary>
    private Vector3 jumpTargetPosition;
    /// <summary>점프 진행률 (0~1)</summary>
    private float jumpProgress = 0f;
    /// <summary>점프 최대 높이 (포물선 궤적의 정점)</summary>
    private float jumpHeight = 5f;
    /// <summary>점프 전체 소요 시간</summary>
    private float jumpDuration = 1.5f;

    public override string Name => "Diving";
    public override bool IsInterruptible => !isDiving;

    public DivingActivity(PetController petController) : base(petController)
    {
    }

    /// <summary>
    /// 다이빙 활동을 시작할 수 있는지 확인
    /// </summary>
    /// <param name="state">펫의 현재 상태</param>
    /// <param name="needs">펫의 욕구 상태</param>
    /// <returns>다이빙 가능 여부</returns>
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 1. 성격 체크: Playful 성격만 다이빙 가능
        if (pet.personality != PetAIProperties.Personality.Playful)
        {
            // Playful 성격이 아니면 다이빙 불가
            return false;
        }

        // 2. 이미 진행 중인 다이빙 활동은 계속 수행
        if (isMovingToSpot || isDiving)
            return true;
            
        // 3. 플레이어 제어 상태 체크
        if (state.IsHolding || state.IsSelected || state.IsGathering)
            return false;
            
        // 4. 욕구 상태 체크: 배고프거나 졸리면 다이빙 안 함
        if (needs.Hunger > 70f || needs.Sleepiness > 70f)
            return false;
            
        // 5. 쿨다운 체크: 최근 다이빙 후 30초 대기
        if (Time.time - lastDivingTime < DIVING_COOLDOWN)
            return false;
            
        // 6. 실패 쿨다운 체크: 실패 후 60초 대기
        if (Time.time - failedAttemptTime < FAILED_ATTEMPT_COOLDOWN)
            return false;
            
        // 7. 다른 펫이 다이빙 중인지 체크
        if (currentDiver != null && currentDiver != pet)
            return false;

        // 8. 다이빙 지점 존재 여부 체크
        GameObject spotObject = GameObject.FindWithTag("DivingSpot");
        if (spotObject == null)
        {
            // 다이빙 지점이 씬에 없으면 불가
            return false;
        }
            
        divingSpot = spotObject.transform;
        
        // 9. 거리 체크: 너무 멀면 다이빙 시도 안 함
        float distanceToSpot = Vector3.Distance(pet.transform.position, divingSpot.position);
        if (distanceToSpot > MAX_DISTANCE_TO_WATER)
            return false;
            
        // 모든 조건 만족 시 다이빙 가능
        return true;
    }

    /// <summary>
    /// 다이빙 활동의 우선순위를 계산
    /// </summary>
    /// <returns>우선순위 값 (높을수록 우선)</returns>
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;

        // 이미 진행 중인 다이빙은 매우 높은 우선순위로 완료까지 유지 (중단 방지)
        if (isMovingToSpot || isDiving)
            return 30f; // 진행 중 보호 (7→30)

        // 새로 시작하는 다이빙은 나비보다 높은 우선순위
        return 18f; // 나비(10-15)보다 높게 (5→18)
    }

    /// <summary>
    /// 다이빙 활동을 시작
    /// NavMesh를 이용해 다이빙 지점으로 이동 후 점프 다이빙 수행
    /// </summary>
    public override void Start()
    {
        // Water 오브젝트 캐싱 (한 번만)
        if (cachedWaterObject == null)
        {
            cachedWaterObject = GameObject.FindWithTag("Water");
        }
        
        // 현재 펫을 다이빙 중인 펫으로 등록 (다른 펫의 다이빙 방지)
        currentDiver = pet;
        isMovingToSpot = true;
        isDiving = false;

        // 다이빙 지점 재확인 (null 체크)
        if (divingSpot == null)
        {
            GameObject spotObject = GameObject.FindWithTag("DivingSpot");
            if (spotObject != null)
            {
                divingSpot = spotObject.transform;
                // 다이빙 지점 찾음
            }
            else
            {
                // 다이빙 지점이 없으면 활동 중단
                Stop();
                return;
            }
        }
        
        // 기존 이동 중단
        if (pet.movementController != null)
        {
            pet.movementController.StopMovement();
        }
        
        // NavMesh 에이전트 설정
        if (pet.agent != null)
        {
            if (!pet.agent.enabled)
            {
                // 에이전트가 비활성화되어 있으면 활성화
                pet.agent.enabled = true;
            }
            if (!pet.agent.isOnNavMesh)
            {
                // NavMesh 위에 없으면 현재 위치로 워프
                pet.agent.Warp(pet.transform.position);
            }
            
            // NavMesh 에이전트 파라미터 초기화
            pet.agent.isStopped = false;      // 이동 시작
            pet.agent.speed = pet.baseSpeed;   // 기본 속도로 설정
            pet.agent.acceleration = 8f;       // 가속도 설정
            pet.agent.updateRotation = true;   // 회전 업데이트 활성화
            pet.agent.ResetPath();             // 기존 경로 초기화


        }
        else
        {

            Stop();
            return;
        }

        // 다이빙 지점으로 이동 시작할 때 Thought_Diving 감정 표시 (점프 직전까지 지속)
        pet.ShowEmotion(EmotionType.Thought_Diving, 999f);

        divingCoroutine = pet.StartCoroutine(MoveToSpotAndDive());
    }

    /// <summary>
    /// 다이빙 지점으로 이동 후 다이빙을 수행하는 코루틴
    /// 1. NavMesh를 통해 다이빙 지점으로 이동
    /// 2. 도착 후 포물선 궤적으로 점프 다이빙
    /// 3. 물 속 행동 트리거
    /// </summary>
    private IEnumerator MoveToSpotAndDive()
    {
        // 물 표면 높이 가져오기 (캐싱된 오브젝트 사용)
        float waterSurfaceY = 5.7f;  // 기본값
        if (cachedWaterObject != null)
        {
            var waterTrigger = cachedWaterObject.GetComponent<WaterZoneTrigger>();
            if (waterTrigger != null)
            {
                waterSurfaceY = waterTrigger.WaterSurfaceY;
            }
            else
            {
                waterSurfaceY = cachedWaterObject.transform.position.y;
            }
        }
        // ===== Phase 1: 이동 준비 및 초기 상태 체크 =====
        
        // 플레이어가 펫을 들고 있으면 즉시 중단
        if (pet.State.IsHolding)
        {
            // 들려있는 상태에서는 다이빙 불가
            Stop();
            yield break;
        }

        // NavMesh 에이전트 존재 및 상태 확인
        if (pet.agent == null)
        {
            // 에이전트가 없으면 이동 불가
            Stop();
            yield break;
        }
        
        if (!pet.agent.enabled)
        {
            // 에이전트 재활성화
            pet.agent.enabled = true;
        }
        
        if (!pet.agent.isOnNavMesh)
        {
            // NavMesh로 재배치
            pet.agent.Warp(pet.transform.position);
        }

        // 에이전트가 정지 상태면 이동 시작
        if (pet.agent.isStopped)
        {
            pet.agent.isStopped = false;
        }
        
        // 다이빙 지점을 목적지로 설정
        pet.agent.SetDestination(divingSpot.position);
        
        // 경로 계산을 위해 한 프레임 대기
        yield return null;
        
        // 경로 생성 실패 체크
        if (!pet.agent.hasPath && !pet.agent.pathPending)
        {
            // 경로를 찾을 수 없으면 실패 처리
            failedAttemptTime = Time.time;
            isMovingToSpot = false;
            isDiving = false;
            if (currentDiver == pet)
            {
                currentDiver = null;
            }
            yield break;
        }

        // ===== Phase 2: 다이빙 지점으로 이동 =====
        float timeoutCounter = 0f;     // 타임아웃 카운터
        int retryCount = 0;            // 재시도 횟수
        const int MAX_RETRIES = 3;     // 최대 재시도 횟수
        
        // 다이빙 지점에 도착할 때까지 반복
        while (isMovingToSpot && Vector3.Distance(pet.transform.position, divingSpot.position) > SPOT_ARRIVAL_DISTANCE)
        {
            // 플레이어 개입 체크 (매 프레임)
            if (pet.State.IsHolding)
            {
                // 들려있으면 즉시 중단
                Stop();
                yield break;
            }
            
            // 선택된 상태 체크
            if (pet.State.IsSelected)
            {
                // 선택되면 플레이어 제어로 전환
                Stop();
                yield break;
            }



            {


            }

            // 타임아웃 체크 (30초 제한)
            timeoutCounter += 0.1f;
            if (timeoutCounter > 30f)
            {
                // 30초 동안 도착 못하면 실패 처리
                failedAttemptTime = Time.time;
                isMovingToSpot = false;
                isDiving = false;
                if (currentDiver == pet)
                {
                    currentDiver = null;
                }
                // NavMesh 에이전트 복구
                if (pet.agent != null && !pet.agent.enabled)
                {
                    pet.agent.enabled = true;
                    pet.agent.Warp(pet.transform.position);
                }
                yield break;
            }

            // 이동 막힘 감지 (1초 후부터 체크)
            if (pet.agent.velocity.magnitude < 0.1f && timeoutCounter > 1f)
            {
                // 경로가 없거나 유효하지 않은 경우
                if (!pet.agent.hasPath || pet.agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
                {
                    retryCount++;
                    if (retryCount >= MAX_RETRIES)
                    {
                        // 최대 재시도 횟수 초과 시 실패
                        failedAttemptTime = Time.time;
                        isMovingToSpot = false;
                        isDiving = false;
                        if (currentDiver == pet)
                        {
                            currentDiver = null;
                        }
                        yield break;
                    }

                    // 경로 재계산 시도
                    if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
                    {
                        pet.agent.ResetPath();
                        yield return new WaitForSeconds(0.5f);
                        
                        // 재계산 중 상태 재확인
                        if (pet.State.IsHolding || pet.State.IsSelected)
                        {
                            // 플레이어 개입 시 중단
                            Stop();
                            yield break;
                        }
                        
                        if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
                        {
                            // 새로운 경로로 다시 이동 시작
                            pet.agent.isStopped = false;
                            pet.agent.speed = pet.baseSpeed;
                            pet.agent.SetDestination(divingSpot.position);

                        }
                        else
                        {

                            failedAttemptTime = Time.time;
                            Stop();
                            yield break;
                        }
                    }
                    else
                    {

                        failedAttemptTime = Time.time;
                        Stop();
                        yield break;
                    }
                }
            }

            yield return new WaitForSeconds(0.1f);
        }

        // ===== Phase 3: 다이빙 지점 도착, 다이빙 준비 =====
        // 다이빙 직전 마지막 상태 체크
        if (pet.State.IsHolding)
        {
            Stop();
            yield break;
        }
        
        isMovingToSpot = false;
        isDiving = true;
        
        // 다이빙 중에는 NavMesh 에이전트 비활성화
        // (점프 애니메이션을 직접 제어하기 위해)
        if (pet.agent != null && pet.agent.enabled)
        {
            pet.agent.enabled = false;
        }

        // ===== Phase 4: 점프 궤적 계산 =====
        
        // 점프 시작 위치 저장
        jumpStartPosition = pet.transform.position;
        
        // 점프 목표 위치 계산
        // 다이빙 지점을 중심으로 랜덤한 각도로 6유닛 떨어진 위치
        float randomAngle = Random.Range(-60f, 60f);
        Vector3 toWater = Quaternion.AngleAxis(randomAngle, Vector3.up) * divingSpot.forward;
        jumpTargetPosition = divingSpot.position + toWater * 6f;
        
        // 목표 높이는 실제 물 표면으로 설정 (물 속으로 들어가기 위해)
        jumpTargetPosition.y = waterSurfaceY;

        // ===== Phase 5: 다이빙 시작 =====

        // 점프 직전에는 감정 표시 없음 (기존 Thought_Diving 자동 제거됨)

        // 점프 애니메이션 재생
        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Jump);
        }

        // ===== Phase 6: 포물선 궤적 점프 애니메이션 =====
        
        jumpProgress = 0f;
        while (jumpProgress < 1f)
        {
            // 점프 중에도 플레이어 개입 체크
            if (pet.State.IsHolding)
            {
                // 점프 중 들려지면 NavMesh 복구 후 중단
                if (pet.agent != null && !pet.agent.enabled)
                {
                    pet.agent.enabled = true;
                    pet.agent.Warp(pet.transform.position);
                }
                Stop();
                yield break;
            }
            
            // 점프 진행률 업데이트
            jumpProgress += Time.deltaTime / jumpDuration;
            
            // 포물선 궤적 계산 (2차 함수)
            Vector3 currentPos = Vector3.Lerp(jumpStartPosition, jumpTargetPosition, jumpProgress);
            float parabola = 4f * jumpHeight * jumpProgress * (1f - jumpProgress);
            currentPos.y += parabola;
            
            pet.transform.position = currentPos;
            
            // 점프 방향으로 회전
            Vector3 direction = jumpTargetPosition - jumpStartPosition;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                pet.transform.rotation = Quaternion.LookRotation(direction);
            }
            
            yield return null;
        }

        // ===== Phase 7: 물 속 행동 트리거 =====

        // PetWaterBehaviorController에 다이빙 시퀀스 시작 알림
        // 이 컨트롤러가 물 속에서의 특별한 행동을 처리
        var waterController = pet.GetComponent<PetWaterBehaviorController>();
        if (waterController != null)
        {
            // 물 표면 높이를 전달하여 정확한 위치에서 다이빙
            waterController.StartDivingSequence(waterSurfaceY);
            // 물 속 행동 시퀀스 시작 (수영, 물방울 효과 등)
        }

        // 물 속에 들어간 후 Happy 감정 표시
        pet.ShowEmotion(EmotionType.Happy, 3f);

        // 물 속에서 3초간 대기 (다이빙 연출)
        yield return new WaitForSeconds(3f);

        // ===== Phase 8: 다이빙 종료 및 정리 =====
        
        // 애니메이션을 기본 상태로 복귀
        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", 0);
        }
        
        // NavMesh 에이전트 재활성화
        // (들려있지 않은 경우에만)
        if (!pet.State.IsHolding && pet.agent != null && !pet.agent.enabled)
        {
            pet.agent.enabled = true;
            pet.agent.Warp(pet.transform.position);
        }
        
        // 상태 플래그 정리
        isDiving = false;
        lastDivingTime = Time.time;  // 쿨다운 시작
        
        // 다이빙 슬롯 해제 (다른 펫이 다이빙 가능하도록)
        if (currentDiver == pet)
        {
            currentDiver = null;
        }


    }

    /// <summary>
    /// 매 프레임 호출되는 업데이트
    /// 다이빙은 코루틴으로 처리되므로 별도 업데이트 로직 불필요
    /// </summary>
    public override void Update()
    {
        // 다이빙 로직은 모두 코루틴에서 처리
    }

    /// <summary>
    /// 다이빙 활동을 중단하고 정리
    /// 코루틴 중단, 상태 초기화, NavMesh 복구 등 수행
    /// </summary>
    public override void Stop()
    {
        // 실행 중인 다이빙 코루틴 중단
        if (divingCoroutine != null)
        {
            pet.StopCoroutine(divingCoroutine);
            divingCoroutine = null;
            // 코루틴 중단 완료
        }

        // 다이빙 감정 제거
        pet.HideEmotion();

        // 상태 플래그 초기화
        isMovingToSpot = false;
        isDiving = false;
        
        // NavMesh 에이전트 복구 (들려있지 않은 경우)
        if (!pet.State.IsHolding && pet.agent != null && !pet.agent.enabled)
        {
            // 현재 위치에서 가장 가까운 NavMesh 위치 찾기
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(pet.transform.position, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                pet.agent.enabled = true;
                pet.agent.Warp(hit.position);
            }
            else
            {
                // NavMesh 위치를 찾을 수 없는 경우 (매우 드물게 발생)
            }
        }
        
        // 다이빙 슬롯 해제
        if (currentDiver == pet)
        {
            currentDiver = null;
        }
    }
}