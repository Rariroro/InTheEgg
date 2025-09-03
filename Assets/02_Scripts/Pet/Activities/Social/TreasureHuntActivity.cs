using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 펫의 보물찾기 활동을 담당하는 클래스
/// 친밀도가 높은 펫들이 보물을 찾아다니는 특별한 활동
/// </summary>
public class TreasureHuntActivity : PetActivityAdapter
{
    private readonly NavMeshAgent agent;
    private readonly PetMovementController moveController;
    private TreasureSpot targetSpot;
    private bool isSearching = false;
    private bool hasFoundTreasure = false;
    private bool hasDroppedTreasure = false;
    private GameObject carriedTreasure;
    private GameObject droppedTreasureObject;  // 내려놓은 보물 GameObject 추적
    private float searchTimer = 0f;
    private const float SEARCH_INTERVAL = 0.5f; // 0.5초마다 새 타겟 검색
    
    // 보물 획득 보호 변수
    private float approachStartTime = 0f;  // 보물 접근 시작 시간
    private bool isPathRecalculating = false;  // 경로 재계산 중인지
    private float lastValidPathTime = 0f;  // 마지막 유효 경로 시간
    private const float MIN_APPROACH_TIME = 0.5f;  // 최소 접근 시간 (획득 전 필요)
    
    // 배회 시스템 추가 변수
    private bool isWandering = false;  // 현재 배회 중인지
    private Vector3 wanderDirection;    // 현재 배회 방향
    private int wanderAttempts = 0;     // 배회 시도 횟수
    private Vector3 lastWanderTarget = Vector3.zero;  // 마지막 배회 목표 위치
    private const float MIN_WANDER_DISTANCE = 30f;  // 최소 배회 거리
    private const float MAX_WANDER_DISTANCE = 50f;  // 최대 배회 거리
    
    // 보물찾기 속도 배율
    private const float SEARCH_SPEED_MULTIPLIER = 2f;  // 탐색 중 속도
    private const float FOUND_SPEED_MULTIPLIER = 3f;   // 보물 발견 후 속도
    private const float ANGULAR_SPEED_MULTIPLIER = 3f;
    private const float ACCELERATION_MULTIPLIER = 3f;
    
    // 탐색 거리 설정
    private const float SEARCH_DISTANCE = 70f;    // 보물 탐색 거리
    
    public override string Name => "TreasureHunt";
    public override bool IsInterruptible => false; // 보물찾기는 중단 불가
    
    public TreasureHuntActivity(PetController petController, PetMovementController movementController) : base(petController)
    {
        agent = pet.agent;
        moveController = movementController;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 보물찾기 모드가 활성화되어 있거나, 보물을 찾은 상태일 때
        bool canStart = (state.IsTreasureHuntActive || state.CurrentStatus == PetStatus.TreasureFound) 
                       && needs.Affection >= 75f;
        
        // 이미 다른 중요한 상태에 있으면 불가
        if (state.IsHolding || state.IsSelected) return false;
        if (state.CurrentStatus == PetStatus.Emergency) return false;
        
        return canStart && (state.CurrentStatus == PetStatus.TreasureHunting || 
                           state.CurrentStatus == PetStatus.TreasureFound ||
                           state.CurrentStatus == PetStatus.Idle);
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 보물을 찾은 상태면 최고 우선순위 (계속 점프하기 위해)
        if (state.CurrentStatus == PetStatus.TreasureFound || hasFoundTreasure)
            return 20.0f;
            
        // 일반 보물찾기는 높은 우선순위 (모이기보다는 낮음)
        return 15.0f;
    }
    
    public override void Start()
    {
        Debug.Log($"[TreasureHunt] {pet.petName}: 보물찾기 시작!");
        
        // 상태 설정
        pet.State.TrySetStatus(PetStatus.TreasureHunting);
        isSearching = true;
        hasFoundTreasure = false;
        hasDroppedTreasure = false;
        searchTimer = 0f;
        
        // 초기 속도 설정 (탐색 모드)
        if (agent != null && agent.enabled)
        {
            agent.speed = pet.Movement.walkSpeed * SEARCH_SPEED_MULTIPLIER;
            agent.angularSpeed = pet.Movement.angularSpeed * ANGULAR_SPEED_MULTIPLIER;
            agent.acceleration = pet.Movement.acceleration * ACCELERATION_MULTIPLIER;
        }
        
        // 첫 타겟 찾기
        FindNewTarget();
        
        // 탐색 애니메이션 시작
        if (pet.animator)
        {
            pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Run);
        }
        
        // 호기심 이모티콘 표시
        pet.ShowEmotion(EmotionType.Surprised);
    }
    
    public override void Update()
    {
        if (!isSearching) return;
        
        // 보물을 내려놓고 점프 중이면 다른 업데이트 처리 안 함
        if (hasDroppedTreasure)
        {
            // 카메라 바라보기만 수행
            if (Camera.main != null)
            {
                Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
                directionToCamera.y = 0;
                if (directionToCamera.magnitude > 0.1f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                    pet.transform.rotation = Quaternion.Slerp(pet.transform.rotation, targetRotation, 
                        pet.Movement.rotationSmoothness * Time.deltaTime);
                }
            }
            return;
        }
        
        // agent 목적지 유실 체크 및 복구
        if (agent != null && agent.enabled && !agent.isStopped)
        {
            // 목적지가 없거나 경로가 없는 경우
            if (!agent.hasPath || agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
            {
                // 보물을 찾은 상태면 목적지 재설정
                if (hasFoundTreasure && targetSpot != null && !hasDroppedTreasure)
                {
                    Vector3 destination = carriedTreasure != null ? targetSpot.WaitingPosition : targetSpot.transform.position;
                    Debug.Log($"[TreasureHunt] {pet.petName}: 목적지 유실 감지! 재설정 (hasFoundTreasure={hasFoundTreasure}, destination={destination})");
                    agent.SetDestination(destination);
                }
                // 탐색 중이고 타겟이 있으면 재설정
                else if (!hasFoundTreasure && targetSpot != null)
                {
                    Debug.Log($"[TreasureHunt] {pet.petName}: 탐색 목적지 유실! 재설정 - {targetSpot.name}");
                    agent.SetDestination(targetSpot.transform.position);
                }
            }
        }
        
        // 회전 처리
        if (moveController != null)
        {
            moveController.HandleRotation();
        }
        
        // 보물을 찾은 경우
        if (hasFoundTreasure && targetSpot != null)
        {
            HandleTreasureFound();
            return;
        }
        
        // 주기적으로 타겟 재검색 (보물 찾기 전에만)
        if (!hasFoundTreasure)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= SEARCH_INTERVAL)
            {
                searchTimer = 0f;
                CheckCurrentTarget();
            }
        }
        
        // 현재 타겟으로 이동
        if (targetSpot != null && agent != null && agent.enabled)
        {
            isWandering = false;  // 보물 타겟이 있으면 배회 중단
            
            // 경로 유효성 체크 (보물 내려놓기 전까지만)
            if (!hasDroppedTreasure)
            {
                if (!agent.pathPending && agent.hasPath)
                {
                    lastValidPathTime = Time.time;
                    isPathRecalculating = false;
                }
                else if (!agent.hasPath && Time.time - lastValidPathTime > 1f)
                {
                    // 경로가 없고 1초 이상 지났으면 재계산 중으로 표시
                    isPathRecalculating = true;
                    Debug.Log($"[TreasureHunt] {pet.petName}: 경로 재계산 중 (경로 없음)");
                }
            }
            
            // 접근 시간 추적
            float distance = agent.remainingDistance;
            if (distance <= 3f && distance > 0.1f && approachStartTime == 0f)
            {
                approachStartTime = Time.time;
                Debug.Log($"[TreasureHunt] {pet.petName}: 보물 접근 시작 (거리: {distance:F1}m)");
            }
            
            // 목표 지점 도착 체크 (더 엄격한 조건)
            if (!agent.pathPending && agent.remainingDistance <= 1.5f && !isPathRecalculating)
            {
                // 추가 검증: 물 속에 있지 않고, 충분한 접근 시간이 지났는지
                bool canPickup = true;
                string blockReason = "";
                
                // 1. 물 속 체크
                if (pet.State.IsInWater)
                {
                    canPickup = false;
                    blockReason = "물 속에 있음";
                }
                // 2. 최소 접근 시간 체크
                else if (approachStartTime > 0 && Time.time - approachStartTime < MIN_APPROACH_TIME)
                {
                    canPickup = false;
                    blockReason = $"접근 시간 부족 ({Time.time - approachStartTime:F1}초)";
                }
                // 3. 실제 거리 재확인
                else if (Vector3.Distance(pet.transform.position, targetSpot.transform.position) > 2f)
                {
                    canPickup = false;
                    blockReason = $"실제 거리 멀음 ({Vector3.Distance(pet.transform.position, targetSpot.transform.position):F1}m)";
                }
                
                if (!canPickup)
                {
                    Debug.Log($"[TreasureHunt] {pet.petName}: 보물 획득 차단 - {blockReason}");
                    return;
                }
                
                Debug.Log($"[TreasureHunt] {pet.petName}: 보물 획득 조건 충족 (거리: {agent.remainingDistance:F1}m)");
                
                // 이 지점에 보물이 있는지 확인
                if (targetSpot.HasTreasure)
                {
                    // 보물 획득 시도
                    if (targetSpot.TryCollect(pet))
                    {
                        // 성공: 보물 줍기
                        Debug.Log($"[TreasureHunt] {pet.petName}: 보물 획득 성공!");
                        approachStartTime = 0f;  // 리셋
                        pet.StartCoroutine(PickupTreasureSequence());
                    }
                    else
                    {
                        // 실패: 다른 펫이 먼저 가져감
                        Debug.Log($"[TreasureHunt] {pet.petName}: 도착했지만 이미 다른 펫이 보물을 가져감 (TryCollect 실패)");
                        approachStartTime = 0f;  // 리셋
                        
                        // 즉시 다른 보물 찾기
                        FindNewTarget();
                    }
                }
                else
                {
                    // 보물이 이미 없음
                    Debug.Log($"[TreasureHunt] {pet.petName}: 보물이 이미 없음");
                    approachStartTime = 0f;  // 리셋
                    FindNewTarget();
                }
            }
        }
        else if (targetSpot == null && !hasFoundTreasure)  // 보물 찾은 후에는 배회 안 함
        {
            // 배회 중이고 도착했으면 다음 위치로
            if (isWandering && agent != null && agent.enabled)
            {
                // 도착 판정을 더 일찍 (5m 이내 또는 거의 도착)
                if (!agent.pathPending && (agent.remainingDistance <= 5f || !agent.hasPath))
                {
                    // 다음 배회 위치 설정
                    WanderToNextLocation();
                }
            }
            else
            {
                // 타겟이 없고 배회도 안하고 있으면 새로 찾기
                FindNewTarget();
            }
        }
    }
    
    public override void Stop()
    {
        Debug.Log($"[TreasureHunt] {pet.petName}: 보물찾기 종료 (hasFoundTreasure={hasFoundTreasure}, hasDroppedTreasure={hasDroppedTreasure})");
        
        // 속도 원래대로 복구
        if (agent != null && agent.enabled)
        {
            agent.speed = pet.Movement.walkSpeed;
            agent.angularSpeed = pet.Movement.angularSpeed;
            agent.acceleration = pet.Movement.acceleration;
            agent.isStopped = false;
        }
        
        // 타겟 해제
        if (targetSpot != null)
        {
            targetSpot.Release(pet);
            targetSpot = null;
        }
        
        // 들고 있던 보물 정리
        if (carriedTreasure != null)
        {
            // 보물을 놓거나 제거
            carriedTreasure.transform.SetParent(null);
            carriedTreasure = null;
        }
        
        // 애니메이션 정상화 (점프 중이 아닌 경우만)
        if (!hasDroppedTreasure)
        {
            pet.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
        }
        
        // 보물을 찾아서 내려놓은 상태면 점프 코루틴 계속 실행
        if (hasFoundTreasure && hasDroppedTreasure && droppedTreasureObject != null)
        {
            Debug.Log($"[TreasureHunt] {pet.petName}: Activity는 종료하지만 점프는 계속할 예정");
            // 점프 코루틴이 이미 실행 중이므로 추가 작업 불필요
            // CelebrationJump 코루틴이 독립적으로 계속 실행됨
        }
        else
        {
            // 점프 중이 아니면 모든 상태 초기화
            hasFoundTreasure = false;
            hasDroppedTreasure = false;
            droppedTreasureObject = null;
        }
        
        isSearching = false;
        isWandering = false;
        approachStartTime = 0f;
        isPathRecalculating = false;
        lastValidPathTime = 0f;
    }
    
    /// <summary>
    /// 새로운 타겟 찾기
    /// </summary>
    private void FindNewTarget()
    {
        // 보물을 이미 찾았으면 새 타겟 찾지 않음
        if (hasFoundTreasure) return;
        
        if (TreasureHuntManager.Instance == null) return;
        
        // 이전 타겟 해제
        if (targetSpot != null)
        {
            targetSpot.Release(pet);
            targetSpot = null;
        }
        
        // 보물 탐색 (70m)
        targetSpot = TreasureHuntManager.Instance.FindNearestAvailableSpot(
            pet.transform.position, SEARCH_DISTANCE);
        
        if (targetSpot != null)
        {
            // 즉시 이 보물을 점유 시도 (예약)
            if (!targetSpot.TryOccupy(pet))
            {
                // 다른 펫이 이미 예약함 - 다른 보물 찾기
                Debug.Log($"{pet.petName}: {targetSpot.name}은 이미 다른 펫이 예약함. 다른 보물 찾기");
                targetSpot = null;
                FindNewTarget();
                return;
            }
            
            // 성공적으로 예약한 경우만 이동 시작
            Debug.Log($"{pet.petName}: {targetSpot.name} 보물 예약 성공!");
            
            // 보물 발견! 속도 증가
            if (agent != null && agent.enabled)
            {
                agent.speed = pet.Movement.walkSpeed * FOUND_SPEED_MULTIPLIER;
                agent.SetDestination(targetSpot.transform.position);
                agent.isStopped = false;
            }
            
            Debug.Log($"{pet.petName}: 새 보물 타겟 설정 - {targetSpot.name}");
        }
        else
        {
            // 찾을 보물이 없으면 랜덤 위치로 탐색 (탐색 속도)
            if (agent != null && agent.enabled)
            {
                agent.speed = pet.Movement.walkSpeed * SEARCH_SPEED_MULTIPLIER;
            }
            WanderRandomly();
            Debug.Log($"{pet.petName}: 근처에 보물이 없어 배회 탐색 시작");
        }
    }
    
    /// <summary>
    /// 현재 타겟 유효성 체크
    /// </summary>
    private void CheckCurrentTarget()
    {
        // 보물을 이미 찾았으면 타겟 체크 안 함
        if (hasFoundTreasure) return;
        
        if (targetSpot == null || !targetSpot.HasTreasure)
        {
            Debug.Log($"[TreasureHunt] {pet.petName}: 주기적 체크에서 타겟 무효 감지 (0.5초 간격)");
            FindNewTarget();
        }
    }
    
    /// <summary>
    /// 랜덤하게 배회하며 탐색 시작
    /// </summary>
    private void WanderRandomly()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        
        isWandering = true;
        wanderAttempts = 0;
        
        // 처음 방향 설정 - 현재 진행 방향이 있으면 그것을 기준으로
        if (agent.velocity.magnitude > 0.1f)
        {
            // 현재 이동 방향을 기준으로 약간 변경
            wanderDirection = agent.velocity.normalized;
            float angleChange = Random.Range(-45f, 45f);
            wanderDirection = Quaternion.Euler(0, angleChange, 0) * wanderDirection;
        }
        else
        {
            // 정지 상태면 랜덤 방향
            wanderDirection = Random.insideUnitSphere;
            wanderDirection.y = 0;
            wanderDirection.Normalize();
        }
        
        // 마지막 목표 위치 초기화
        lastWanderTarget = pet.transform.position;
        
        WanderToNextLocation();
    }
    
    /// <summary>
    /// 다음 배회 위치로 이동
    /// </summary>
    private void WanderToNextLocation()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        
        wanderAttempts++;
        
        // 2번마다 방향 조정 (이전보다 자주)
        if (wanderAttempts > 2)
        {
            wanderAttempts = 0;
            // 현재 방향에서 ±90도 범위로 회전 (완전 랜덤이 아닌 연속성 있는 방향)
            float angleChange = Random.Range(-90f, 90f);
            wanderDirection = Quaternion.Euler(0, angleChange, 0) * wanderDirection;
            wanderDirection.Normalize();
        }
        
        // 거리를 더 크게 설정 (40m에서 80m까지)
        float distance = Mathf.Lerp(40f, 80f, wanderAttempts / 2f);
        
        // 목표 위치 계산 - 마지막 배회 목표 위치 기준 (연속성 보장)
        Vector3 basePosition;
        if (lastWanderTarget != Vector3.zero && Vector3.Distance(lastWanderTarget, pet.transform.position) > 5f)
        {
            // 마지막 목표가 있고 아직 멀리 있으면 그것을 기준으로
            basePosition = lastWanderTarget;
            Debug.Log($"{pet.petName}: 마지막 목표 기준 이동 (거리: {Vector3.Distance(lastWanderTarget, pet.transform.position):F1}m)");
        }
        else
        {
            // 그렇지 않으면 현재 위치 기준
            basePosition = pet.transform.position;
            Debug.Log($"{pet.petName}: 현재 위치 기준 이동");
        }
        
        Vector3 targetPosition = basePosition + wanderDirection * distance;
        
        // 랜덤 요소 최소화 (10m → 3m)
        targetPosition += Random.insideUnitSphere * 3f;
        targetPosition.y = pet.transform.position.y;
        
        // NavMesh 상의 유효한 위치 찾기
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, distance * 1.2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            agent.isStopped = false;
            
            // 새로운 목표 위치 저장
            lastWanderTarget = hit.position;
            
            Vector3 actualDirection = (hit.position - pet.transform.position).normalized;
            float actualDistance = Vector3.Distance(pet.transform.position, hit.position);
            
            Debug.Log($"{pet.petName}: 배회 이동 - 실제방향: {actualDirection}, 실제거리: {actualDistance:F1}m, 목표: {hit.position}");
        }
        else
        {
            // 유효한 위치를 찾지 못하면 방향을 약간만 조정 (재귀 호출 제거)
            wanderDirection = Quaternion.Euler(0, Random.Range(30f, 60f), 0) * wanderDirection;
            wanderDirection.Normalize();
            
            // 더 가까운 거리로 다시 시도
            distance = MIN_WANDER_DISTANCE;
            targetPosition = pet.transform.position + wanderDirection * distance;
            
            if (NavMesh.SamplePosition(targetPosition, out hit, distance, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                agent.isStopped = false;
                lastWanderTarget = hit.position;
                Debug.Log($"{pet.petName}: 대체 경로로 이동 - 목표: {hit.position}");
            }
        }
    }
    
    /// <summary>
    /// 보물을 줍는 시퀀스 (먹는 애니메이션 포함)
    /// </summary>
    private IEnumerator PickupTreasureSequence()
    {
        // 일단 멈추기
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
        }
        
        // 먹는 애니메이션 재생
        if (pet.animator)
        {
            pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Eat);
        }
        
        // 애니메이션 대기
        yield return new WaitForSeconds(0.5f);
        
        // 이제 보물 들기
        OnTreasureFound();
    }
    
    /// <summary>
    /// 보물 발견 처리
    /// </summary>
    private void OnTreasureFound()
    {
        hasFoundTreasure = true;
        pet.State.TrySetStatus(PetStatus.TreasureFound);
        
        // 보물 들기 (시각적 효과)
        if (targetSpot != null && targetSpot.CurrentTreasure != null)
        {
            carriedTreasure = targetSpot.CurrentTreasure;
            
            // 1순위: 펫에 설정된 treasureHoldPoint 사용
            if (pet.treasureHoldPoint != null)
            {
                carriedTreasure.transform.SetParent(pet.treasureHoldPoint);
                carriedTreasure.transform.localPosition = Vector3.zero;
                carriedTreasure.transform.localRotation = Quaternion.identity;
            }
            // 2순위: 본 찾기 (기존 방식)
            else
            {
                Transform mouthBone = FindMouthBone();
                if (mouthBone != null)
                {
                    carriedTreasure.transform.SetParent(mouthBone);
                    carriedTreasure.transform.localPosition = Vector3.forward * 0.3f;
                }
                else
                {
                    // 3순위: 펫 위에 띄우기
                    carriedTreasure.transform.SetParent(pet.transform);
                    carriedTreasure.transform.localPosition = Vector3.up * 1.5f;
                }
            }
            
            // TreasureController의 StartCarrying 호출
            TreasureController treasureController = carriedTreasure.GetComponent<TreasureController>();
            if (treasureController != null)
            {
                treasureController.StartCarrying(pet);
            }
            
            // 매니저에 보물 찾음 알림 (이 시점에 카운팅!)
            if (TreasureHuntManager.Instance != null)
            {
                TreasureHuntManager.Instance.OnPetFoundTreasure(targetSpot, pet);
            }
        }
        
        // 대기 위치로 이동
        if (targetSpot != null && agent != null && agent.enabled)
        {
            agent.isStopped = false; // 다시 이동 시작
            Vector3 waitingPos = targetSpot.WaitingPosition;
            agent.SetDestination(waitingPos);
            
            // 달리기 애니메이션 유지
            if (pet.animator)
            {
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Run);
            }
        }
        
        // 기쁨 표현
        pet.ShowEmotion(EmotionType.Happy);
        
        Debug.Log($"{pet.petName}: 보물 발견! 대기 위치로 이동 중...");
    }
    
    /// <summary>
    /// 보물을 찾은 후 대기 처리
    /// </summary>
    private void HandleTreasureFound()
    {
        // 이미 보물을 내려놓았으면 아무것도 하지 않음 (점프만 계속)
        if (hasDroppedTreasure)
        {
            // 카메라 바라보기만 수행
            if (Camera.main != null)
            {
                Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
                directionToCamera.y = 0;
                if (directionToCamera.magnitude > 0.1f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                    pet.transform.rotation = Quaternion.Slerp(pet.transform.rotation, targetRotation, 
                        pet.Movement.rotationSmoothness * Time.deltaTime);
                }
            }
            return;
        }
        
        if (agent == null || !agent.enabled) return;
        
        // 대기 위치가 변경되었는지 확인하고 재설정
        if (targetSpot != null)
        {
            Vector3 expectedDestination = targetSpot.WaitingPosition;
            if (Vector3.Distance(agent.destination, expectedDestination) > 1f)
            {
                Debug.Log($"{pet.petName}: 경로 이탈 감지! 대기 위치로 재설정");
                agent.SetDestination(expectedDestination);
                isPathRecalculating = true;  // 경로 재계산 플래그 설정
                return;  // 아직 도착 안 함
            }
            
            // 물 속에 있으면 대기
            if (pet.State.IsInWater)
            {
                Debug.Log($"[TreasureHunt] {pet.petName}: 물 속에서 대기 중...");
                return;
            }
        }
        
        // 대기 위치 도착 체크
        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
        {
            agent.isStopped = true;
            
            // 보물을 아직 내려놓지 않았다면
            if (!hasDroppedTreasure && carriedTreasure != null)
            {
                // 내려놓기 시퀀스 시작
                pet.StartCoroutine(DropTreasureSequence());
            }
        }
    }
    
    /// <summary>
    /// 보물을 내려놓는 시퀀스 (먹는 애니메이션 포함)
    /// </summary>
    private IEnumerator DropTreasureSequence()
    {
        // 먹는 애니메이션 재생 (내려놓기 전)
        if (pet.animator)
        {
            pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Eat);
        }
        
        // 애니메이션 대기
        yield return new WaitForSeconds(0.5f);
        
        // 보물 내려놓기
        DropTreasure();
        hasDroppedTreasure = true;
        
        // 점프 애니메이션 시작
        pet.StartCoroutine(CelebrationJump());
    }
    
    /// <summary>
    /// 보물 발견 축하 점프
    /// </summary>
    private IEnumerator CelebrationJump()
    {
        var animController = pet.GetComponent<PetAnimationController>();
        
        Debug.Log($"[TreasureHunt] {pet.petName}: 축하 점프 시작!");
        
        // 보물찾기가 종료되어도 계속 점프 (유저가 보물 가져갈 때까지)
        while (droppedTreasureObject != null)
        {
            // 점프 애니메이션 - PlayAnimationWithCustomDuration 사용으로 완전한 재생 보장
            if (animController != null)
            {
                // 점프 애니메이션을 2초 동안 완전히 재생
                yield return pet.StartCoroutine(animController.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Jump, 
                    2f,    // 애니메이션 재생 시간
                    true,  // returnToIdle - 자동으로 Idle로 복귀
                    false  // resumeMovementAfter - 이동 재개하지 않음
                ));
            }
            else if (pet.animator)
            {
                // 폴백: AnimationController가 없는 경우 직접 제어
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Jump);
                yield return new WaitForSeconds(2f);
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Idle);
            }
            
            // 점프 중 행복 표현
            if (Random.value < 0.3f) // 30% 확륥로 감정 표현
            {
                pet.ShowEmotion(EmotionType.Love);
            }
            
            // Idle 상태에서 대기
            yield return new WaitForSeconds(2f);
        }
        
        Debug.Log($"[TreasureHunt] {pet.petName}: 보물이 수집됨, 점프 종료");
        
        // 점프 종료 후 처리
        hasFoundTreasure = false;
        hasDroppedTreasure = false;
        droppedTreasureObject = null;
        
        // 보물찾기가 아직 진행 중인지 확인
        if (TreasureHuntManager.Instance != null && TreasureHuntManager.Instance.IsTreasureHuntActive)
        {
            Debug.Log($"[TreasureHunt] {pet.petName}: 축하 점프 종료, 다른 보물 찾으러 갑니다!");
            // TreasureHunting 상태로 설정하면 다시 보물 찾기 시작
            pet.State.TrySetStatus(PetStatus.TreasureHunting);
        }
        else
        {
            Debug.Log($"[TreasureHunt] {pet.petName}: 축하 점프 종료, 일상으로 복귀");
            pet.State.TrySetStatus(PetStatus.Idle);
        }
    }
    
    /// <summary>
    /// 보물을 바닥에 내려놓기
    /// </summary>
    private void DropTreasure()
    {
        if (carriedTreasure == null) return;
        
        // 시작 위치: TreasureHoldPoint 또는 현재 보물 위치
        Vector3 startPos;
        if (pet.treasureHoldPoint != null)
        {
            startPos = pet.treasureHoldPoint.position;  // TreasureHoldPoint의 월드 좌표
        }
        else
        {
            startPos = carriedTreasure.transform.position;  // 보물의 현재 위치
        }
        
        // 끝 위치: 시작점에서 펫의 앞쪽으로 떨어뜨리기
        Vector3 endPos = startPos + pet.transform.forward * 0.7f;
        endPos.y = pet.transform.position.y + 1f; // 바닥 높이
        
        // 부드럽게 놓는 애니메이션 시작
        pet.StartCoroutine(DropTreasureAnimation(startPos, endPos));
    }
    
    
    /// <summary>
    /// 보물을 부드럽게 앞에 놓는 애니메이션
    /// </summary>
    private IEnumerator DropTreasureAnimation(Vector3 from, Vector3 to)
    {
        // carriedTreasure를 지역 변수에 저장 (코루틴 실행 중 null이 되는 것 방지)
        GameObject treasureToAnimate = carriedTreasure;
        
        // null 체크
        if (treasureToAnimate == null)
        {
            Debug.LogWarning($"[TreasureHuntActivity] {pet.petName}: 보물이 null입니다. 애니메이션 취소");
            yield break;
        }
        
        float duration = 0.3f;
        float elapsed = 0f;

        // 부모 해제 (월드 좌표로 전환)
        treasureToAnimate.transform.SetParent(null);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 포물선 움직임 (자연스럽게 떨어지는 효과)
            Vector3 pos = Vector3.Lerp(from, to, t);
            // 시작 높이에서 최종 높이로 포물선 형태로 이동
            float heightCurve = 1f - (t - 0.5f) * (t - 0.5f) * 4f; // 중간에 살짝 올라갔다가 내려옴
            pos.y = Mathf.Lerp(from.y, to.y, t) + heightCurve * 0.2f;

            // null 체크 추가 (애니메이션 중 삭제될 수 있음)
            if (treasureToAnimate == null)
            {
                Debug.LogWarning($"[TreasureHuntActivity] {pet.petName}: 애니메이션 중 보물이 삭제됨");
                yield break;
            }
            
            treasureToAnimate.transform.position = pos;
            yield return null;
        }

        // 최종 null 체크
        if (treasureToAnimate == null)
        {
            Debug.LogWarning($"[TreasureHuntActivity] {pet.petName}: 애니메이션 종료 시 보물이 null");
            yield break;
        }

        // 최종 위치 설정
        treasureToAnimate.transform.position = to;
        treasureToAnimate.transform.rotation = Quaternion.identity;

        // 내려놓은 보물 GameObject 저장
        droppedTreasureObject = treasureToAnimate;
        carriedTreasure = null;  // carriedTreasure는 null로 설정

        // TreasureController의 EnableCollection 호출
        TreasureController treasureController = droppedTreasureObject.GetComponent<TreasureController>();
        if (treasureController != null)
        {
            Debug.Log($"[TreasureHuntActivity] {pet.petName}: EnableCollection 호출");
            treasureController.EnableCollection();
        }

        Debug.Log($"[TreasureHuntActivity] {pet.petName}: 보물을 내려놓고 대기 중! 위치: {droppedTreasureObject.transform.position}");
    }
    
    /// <summary>
    /// 펫의 입 본 찾기 (모델에 따라 다를 수 있음)
    /// </summary>
    private Transform FindMouthBone()
    {
        // 일반적인 입 본 이름들
        string[] mouthBoneNames = { "Mouth", "Head", "Jaw", "Bip001 Head", "Head_Bone" };
        
        foreach (string boneName in mouthBoneNames)
        {
            Transform bone = pet.transform.FindDeepChild(boneName);
            if (bone != null) return bone;
        }
        
        return null;
    }
}

// Transform 확장 메서드
public static class TransformExtensions
{
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        Transform result = parent.Find(name);
        if (result != null) return result;
        
        foreach (Transform child in parent)
        {
            result = child.FindDeepChild(name);
            if (result != null) return result;
        }
        
        return null;
    }
}