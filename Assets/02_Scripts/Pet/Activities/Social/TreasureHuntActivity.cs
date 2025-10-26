using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 펫의 보물찾기 탐색 활동을 담당하는 클래스
/// 보물을 찾으면 TreasureFoundActivity로 전환됨
/// </summary>
public class TreasureHuntActivity : PetActivityAdapter
{
    private readonly NavMeshAgent agent;
    private readonly PetMovementController moveController;
    private TreasureSpot targetSpot;
    private bool isSearching = false;
    private float searchTimer = 0f;
    private const float SEARCH_INTERVAL = 0.5f;
    private bool isTransitioningToFound = false; // 중복 전환 방지
    
    // 보물 획득 보호 변수
    private float approachStartTime = 0f;
    private bool isPathRecalculating = false;
    private float lastValidPathTime = 0f;
    private const float MIN_APPROACH_TIME = 0.5f;
    
    // 배회 시스템 변수
    private bool isWandering = false;
    private Vector3 wanderDirection;
    private int wanderAttempts = 0;
    private Vector3 lastWanderTarget = Vector3.zero;
    private const float MIN_WANDER_DISTANCE = 30f;
    private const float MAX_WANDER_DISTANCE = 50f;

    // 막힘 감지 시스템
    private Vector3 lastPositionForStuck;
    private float stuckTimer = 0f;
    private const float STUCK_THRESHOLD = 3f;  // 3초 동안 움직이지 않으면 막힌 것으로 판단
    private const float MIN_MOVE_DISTANCE = 1f;  // 최소 이동 거리
    private HashSet<TreasureSpot> blacklistedSpots = new HashSet<TreasureSpot>();  // 도달 불가능한 보물 목록
    
    // 보물찾기 속도 배율
    private const float SEARCH_SPEED_MULTIPLIER = 2f;
    private const float FOUND_SPEED_MULTIPLIER = 3f;
    private const float ANGULAR_SPEED_MULTIPLIER = 3f;
    private const float ACCELERATION_MULTIPLIER = 3f;

    // 탐색 거리 설정
    private const float SEARCH_DISTANCE = 70f;
    private const float NEAR_SCAN_DISTANCE = 10f;  // 근거리 스캔 거리
    private const float NEAR_SCAN_INTERVAL = 0.2f;  // 근거리 스캔 주기
    private float nearScanTimer = 0f;
    
    public override string Name => "TreasureHunt";
    public override bool IsInterruptible => !pet.State.IsTreasureHuntActive; // 보물찾기 종료 시 중단 가능
    
    public TreasureHuntActivity(PetController petController, PetMovementController movementController) : base(petController)
    {
        agent = pet.agent;
        moveController = movementController;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 보물찾기 모드가 활성화되어 있고, 탐색 중이거나 Idle 상태일 때만
        bool canStart = state.IsTreasureHuntActive && needs.Affection >= 75f;
        
        // 이미 다른 중요한 상태에 있으면 불가
        if (state.IsHolding || state.IsSelected) return false;
        if (state.CurrentStatus == PetStatus.Emergency) return false;
        if (state.CurrentStatus == PetStatus.TreasureFound) return false; // 이미 찾은 상태면 불가
        
        return canStart && (state.CurrentStatus == PetStatus.TreasureHunting || 
                           state.CurrentStatus == PetStatus.Idle);
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 일반 보물찾기는 높은 우선순위
        return 15.0f;
    }
    
    public override void Start()
    {

        // 모든 상태 변수 명시적 초기화
        targetSpot = null;
        isSearching = true;
        searchTimer = 0f;
        isTransitioningToFound = false;
        approachStartTime = 0f;
        isPathRecalculating = false;
        lastValidPathTime = 0f;
        isWandering = false;
        wanderAttempts = 0;
        lastWanderTarget = Vector3.zero;
        nearScanTimer = 0f;
        stuckTimer = 0f;
        lastPositionForStuck = pet.transform.position;
        blacklistedSpots.Clear();
        
        // agent가 NavMesh에 있는지 확인
        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            Debug.LogWarning($"[TreasureHunt] {pet.petName}: Agent가 NavMesh에 없습니다. 위치 재설정 시도.");
            
            // NavMesh에 가장 가까운 위치로 워프
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(pet.transform.position, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogError($"[TreasureHunt] {pet.petName}: NavMesh에 적합한 위치를 찾을 수 없습니다!");
                isSearching = false;
                return;
            }
        }
        
        // 상태 설정
        pet.State.TrySetStatus(PetStatus.TreasureHunting);
        
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

        // 처음엔 놀라고, 이후 보물찾기 생각 표시
        pet.ShowEmotion(EmotionType.Surprised);
        pet.StartCoroutine(ShowTreasureHuntThought());
    }
    
    public override void Update()
    {
        if (!isSearching) return;

        // 보물찾기가 종료되면 즉시 Stop 호출하여 완전히 중단
        if (!pet.State.IsTreasureHuntActive)
        {
            isSearching = false;

            // Stop 메서드 호출하여 정리
            Stop();

            // AI에게 재평가 요청
            if (pet.AI != null)
            {
                pet.AI.InterruptAndResetAI();
            }
            return;
        }

        // 막힘 감지
        CheckIfStuck();
        
        // agent 목적지 유실 체크 및 복구 (NavMesh 상태 확인 추가)
        if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped)
        {
            // 목적지가 없거나 경로가 없는 경우
            if (!agent.hasPath || agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
            {
                // 탐색 중이고 타겟이 있으면 재설정
                if (targetSpot != null)
                {
                    agent.SetDestination(targetSpot.transform.position);
                }
            }
        }
        
        // 회전 처리
        if (moveController != null)
        {
            moveController.HandleRotation();
        }

        // 근거리 보물 스캔 (더 자주 체크)
        nearScanTimer += Time.deltaTime;
        if (nearScanTimer >= NEAR_SCAN_INTERVAL)
        {
            nearScanTimer = 0f;
            ScanForNearbyTreasure();
        }

        // 주기적으로 타겟 재검색
        searchTimer += Time.deltaTime;
        if (searchTimer >= SEARCH_INTERVAL)
        {
            searchTimer = 0f;
            CheckCurrentTarget();
        }
        
        // 현재 타겟으로 이동
        if (targetSpot != null && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            isWandering = false;  // 보물 타겟이 있으면 배회 중단
            
            // 경로 유효성 체크
            if (!agent.pathPending && agent.hasPath)
            {
                lastValidPathTime = Time.time;
                isPathRecalculating = false;
            }
            else if (!agent.hasPath && Time.time - lastValidPathTime > 1f)
            {
                // 경로가 없고 1초 이상 지났으면 재계산 중으로 표시
                isPathRecalculating = true;
            }
            
            // 접근 시간 추적
            float distance = agent.remainingDistance;
            if (distance <= 3f && distance > 0.1f && approachStartTime == 0f)
            {
                approachStartTime = Time.time;
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
                    return;
                }
                
                // 이미 전환 중이면 중복 실행 방지
                if (isTransitioningToFound)
                {
                    return;
                }
                
                
                // 이 지점에 보물이 있는지 확인
                if (targetSpot.HasTreasure)
                {
                    // 보물 획득 시도
                    if (targetSpot.TryCollect(pet))
                    {
                        // 성공: 보물 획득 후 TreasureFound 상태로 전환
                        approachStartTime = 0f;  // 리셋
                        isTransitioningToFound = true;  // 전환 시작
                        isSearching = false;  // Update 즉시 중단
                        
                        // 먹는 애니메이션 잠시 재생 후 상태 전환
                        pet.StartCoroutine(TransitionToFoundState());
                    }
                    else
                    {
                        // 실패: 다른 펫이 먼저 가져감
                        approachStartTime = 0f;  // 리셋
                        
                        // 즉시 다른 보물 찾기
                        FindNewTarget();
                    }
                }
                else
                {
                    // 보물이 이미 없음
                    approachStartTime = 0f;  // 리셋
                    FindNewTarget();
                }
            }
        }
        else if (targetSpot == null)  // 타겟이 없으면 배회
        {
            // 배회 중이고 도착했으면 다음 위치로
            if (isWandering && agent != null && agent.enabled && agent.isOnNavMesh)
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
        
        // 모든 플래그 및 참조 완전 리셋
        isTransitioningToFound = false;
        isSearching = false;
        isWandering = false;
        searchTimer = 0f;
        approachStartTime = 0f;
        isPathRecalculating = false;
        lastValidPathTime = 0f;
        wanderAttempts = 0;
        lastWanderTarget = Vector3.zero;
        wanderDirection = Vector3.zero;
        
        // agent 상태 안전하게 리셋
        if (agent != null && agent.enabled)
        {
            // NavMesh에 있는 경우에만 상태 변경
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
            }
            
            // 속도 원래대로 복구
            agent.speed = pet.Movement.walkSpeed;
            agent.angularSpeed = pet.Movement.angularSpeed;
            agent.acceleration = pet.Movement.acceleration;
        }
        
        // 타겟 해제
        if (targetSpot != null)
        {
            targetSpot.Release(pet);
            targetSpot = null;
        }
        
        // 애니메이션 정상화
        pet.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
        
        isSearching = false;
        isWandering = false;
        approachStartTime = 0f;
        isPathRecalculating = false;
        lastValidPathTime = 0f;
    }
    
    /// <summary>
    /// 막힘 감지
    /// </summary>
    private void CheckIfStuck()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (targetSpot == null) return;  // 타겟이 없으면 체크 불필요

        // 이동 거리 계산
        float movedDistance = Vector3.Distance(pet.transform.position, lastPositionForStuck);

        if (movedDistance < MIN_MOVE_DISTANCE)
        {
            stuckTimer += Time.deltaTime;

            if (stuckTimer >= STUCK_THRESHOLD)
            {
                Debug.Log($"[TreasureHunt] {pet.petName}: 막힘 감지! 타겟 변경");

                // 현재 타겟을 블랙리스트에 추가
                if (targetSpot != null)
                {
                    blacklistedSpots.Add(targetSpot);
                    targetSpot.Release(pet);
                    targetSpot = null;
                }

                // 타이머 리셋
                stuckTimer = 0f;
                lastPositionForStuck = pet.transform.position;

                // 새로운 타겟 찾기
                FindNewTarget();
            }
        }
        else
        {
            // 이동했으면 타이머 리셋
            stuckTimer = 0f;
            lastPositionForStuck = pet.transform.position;
        }
    }

    /// <summary>
    /// 근거리 보물 스캔 - 더 가까운 보물이 있으면 타겟 변경
    /// </summary>
    private void ScanForNearbyTreasure()
    {
        if (TreasureHuntManager.Instance == null) return;

        // agent 상태 체크 및 NavMesh 복귀 시도
        if (agent == null || !agent.enabled) return;

        if (!agent.isOnNavMesh)
        {
            // NavMesh에 없으면 복귀 시도
            if (!TryReturnToNavMesh())
            {
                return;
            }
        }

        var activeTreasures = TreasureHuntManager.Instance.ActiveTreasureSpots;
        TreasureSpot closestSpot = null;
        float closestDistance = float.MaxValue;

        foreach (var spot in activeTreasures)
        {
            // 블랙리스트에 있는 보물은 무시
            if (blacklistedSpots.Contains(spot)) continue;

            // 사용 가능한 보물만 체크
            if (spot == null || !spot.IsAvailable) continue;

            float distance = Vector3.Distance(pet.transform.position, spot.transform.position);

            // 근거리 범위 내의 보물만 체크
            if (distance <= NEAR_SCAN_DISTANCE && distance < closestDistance)
            {
                // NavMesh 경로가 실제로 있는지 확인 (agent 상태 체크 추가)
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    NavMeshPath path = new NavMeshPath();
                    if (agent.CalculatePath(spot.transform.position, path))
                    {
                        if (path.status == NavMeshPathStatus.PathComplete)
                        {
                            closestSpot = spot;
                            closestDistance = distance;
                        }
                    }
                }
            }
        }

        // 더 가까운 보물을 찾았고, 현재 타겟보다 가까우면 변경
        if (closestSpot != null)
        {
            float currentDistance = targetSpot != null ?
                Vector3.Distance(pet.transform.position, targetSpot.transform.position) : float.MaxValue;

            if (closestDistance < currentDistance - 2f)  // 2m 이상 가까우면 변경
            {
                Debug.Log($"[TreasureHunt] {pet.petName}: 더 가까운 보물 발견! ({closestDistance:F1}m)");

                // 이전 타겟 해제
                if (targetSpot != null)
                {
                    targetSpot.Release(pet);
                }

                // 새 타겟으로 변경
                targetSpot = closestSpot;
                if (targetSpot.TryOccupy(pet))
                {
                    agent.SetDestination(targetSpot.transform.position);
                    agent.speed = pet.Movement.walkSpeed * FOUND_SPEED_MULTIPLIER;
                }
                else
                {
                    targetSpot = null;  // 점유 실패 시 다시 찾기
                }
            }
        }
    }

    /// <summary>
    /// 새로운 타겟 찾기
    /// </summary>
    private void FindNewTarget()
    {
        if (TreasureHuntManager.Instance == null) return;

        // agent 상태 체크
        if (agent == null || !agent.enabled) return;

        if (!agent.isOnNavMesh)
        {
            // NavMesh에 없으면 복귀 시도
            if (!TryReturnToNavMesh())
            {
                Debug.LogWarning($"[TreasureHunt] {pet.petName}: NavMesh 복귀 실패");
                return;
            }
        }

        // 이전 타겟 해제
        if (targetSpot != null)
        {
            targetSpot.Release(pet);
            targetSpot = null;
        }

        // 모든 활성 보물 중에서 최적의 타겟 찾기
        var activeTreasures = TreasureHuntManager.Instance.ActiveTreasureSpots;
        TreasureSpot bestSpot = null;
        float bestScore = float.MaxValue;

        foreach (var spot in activeTreasures)
        {
            // 블랙리스트에 있는 보물은 무시
            if (blacklistedSpots.Contains(spot)) continue;

            // 사용 가능한 보물만 체크
            if (spot == null || !spot.IsAvailable) continue;

            float distance = Vector3.Distance(pet.transform.position, spot.transform.position);

            // 탐색 거리 제한
            if (distance > SEARCH_DISTANCE) continue;

            // NavMesh 경로가 실제로 있는지 확인 (agent 상태 체크 추가)
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(spot.transform.position, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        // 경로 길이 계산 (실제 이동 거리)
                        float pathLength = GetPathLength(path);

                        // 점수 계산 (직선 거리와 경로 길이의 조합)
                        float score = pathLength * 0.7f + distance * 0.3f;

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestSpot = spot;
                        }
                    }
                }
            }
        }

        targetSpot = bestSpot;

        if (targetSpot != null)
        {
            // 즉시 이 보물을 점유 시도 (예약)
            if (!targetSpot.TryOccupy(pet))
            {
                // 다른 펫이 이미 예약함 - 다른 보물 찾기
                targetSpot = null;
                FindNewTarget();
                return;
            }

            // 성공적으로 예약한 경우만 이동 시작
            Debug.Log($"[TreasureHunt] {pet.petName}: 새 타겟 설정 (거리: {Vector3.Distance(pet.transform.position, targetSpot.transform.position):F1}m)");

            // 보물 발견! 속도 증가
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.speed = pet.Movement.walkSpeed * FOUND_SPEED_MULTIPLIER;
                agent.SetDestination(targetSpot.transform.position);
                agent.isStopped = false;
            }
        }
        else
        {
            // 찾을 보물이 없으면 랜덤 위치로 탐색 (탐색 속도)
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.speed = pet.Movement.walkSpeed * SEARCH_SPEED_MULTIPLIER;
            }
            WanderRandomly();
        }
    }

    /// <summary>
    /// NavMesh 경로의 실제 길이 계산
    /// </summary>
    private float GetPathLength(NavMeshPath path)
    {
        float length = 0f;
        if (path.corners.Length < 2) return 0f;

        for (int i = 1; i < path.corners.Length; i++)
        {
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }

        return length;
    }

    /// <summary>
    /// NavMesh에서 벗어난 경우 가장 가까운 위치로 복귀 시도
    /// </summary>
    private bool TryReturnToNavMesh()
    {
        NavMeshHit hit;
        float searchRadius = 5f;

        // 가장 가까운 NavMesh 위치 찾기
        if (NavMesh.SamplePosition(pet.transform.position, out hit, searchRadius, NavMesh.AllAreas))
        {
            // agent를 NavMesh 위치로 워프
            if (agent != null && agent.enabled)
            {
                agent.Warp(hit.position);
                Debug.Log($"[TreasureHunt] {pet.petName}: NavMesh로 복귀 성공");
                return true;
            }
        }

        // 더 넓은 범위로 재시도
        searchRadius = 10f;
        if (NavMesh.SamplePosition(pet.transform.position, out hit, searchRadius, NavMesh.AllAreas))
        {
            if (agent != null && agent.enabled)
            {
                agent.Warp(hit.position);
                Debug.Log($"[TreasureHunt] {pet.petName}: NavMesh로 복귀 성공 (확장 범위)");
                return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// 현재 타겟 유효성 체크
    /// </summary>
    private void CheckCurrentTarget()
    {
        if (targetSpot == null || !targetSpot.HasTreasure)
        {
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
        }
        else
        {
            // 그렇지 않으면 현재 위치 기준
            basePosition = pet.transform.position;
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
            
        }
        else
        {
            // 유효한 위치를 찾지 못하면 방향을 약간만 조정
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
            }
        }
    }
    
    /// <summary>
    /// 보물을 찾은 후 TreasureFound 상태로 전환
    /// </summary>
    private IEnumerator TransitionToFoundState()
    {
        // 일단 멈추기 (NavMesh 상태 확인)
        if (agent != null && agent.enabled && agent.isOnNavMesh)
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
        
        // 보물 시각적 처리를 먼저 수행 (상태 전환 전에!)
        if (targetSpot != null && targetSpot.CurrentTreasure != null)
        {
            GameObject treasure = targetSpot.CurrentTreasure;

            // treasureHoldPoint 사용 (없으면 경고)
            if (pet.treasureHoldPoint != null)
            {
                treasure.transform.SetParent(pet.treasureHoldPoint);
                treasure.transform.localPosition = Vector3.zero;
                treasure.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogWarning($"[TreasureHunt] {pet.petName}: treasureHoldPoint가 설정되지 않았습니다!");
                // 기본 위치로 펫 Transform 사용
                treasure.transform.SetParent(pet.transform);
                treasure.transform.localPosition = Vector3.up * 1.5f;
            }

            // TreasureController의 StartCarrying 호출
            TreasureController treasureController = treasure.GetComponent<TreasureController>();
            if (treasureController != null)
            {
                treasureController.StartCarrying(pet);
                // IsCarried는 읽기 전용이므로 StartCarrying에서 설정됨
            }

            // 즉시 TreasureSpot에서 보물 제거 마킹 (다른 펫의 접근 차단)
            targetSpot.MarkTreasureRemoved();

            // 매니저에 보물 찾음 알림
            if (TreasureHuntManager.Instance != null)
            {
                TreasureHuntManager.Instance.OnPetFoundTreasure(targetSpot, pet);
            }

            // targetSpot 참조는 TreasureFoundActivity가 위치를 알 수 있도록 유지
            // 하지만 TreasureSpot의 currentTreasure는 이미 null로 설정됨 (MarkTreasureRemoved에서)
        }
        
        // 보물 발견 감정 표현 (지속)
        pet.ShowEmotion(EmotionType.Tresure, 999f);
        
        // 이제 보물이 부착되었으므로 상태 전환
        pet.State.TrySetStatus(PetStatus.TreasureFound);
        
        // AI에게 즉시 재평가 요청 - 새로운 Activity로 전환하도록
        if (pet.AI != null)
        {
            pet.AI.InterruptAndResetAI();
        }
        
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

    /// <summary>
    /// 보물찾기 생각 풍선 표시 (놀람 이후)
    /// </summary>
    private IEnumerator ShowTreasureHuntThought()
    {
        // 놀람 표정이 사라질 때까지 대기 (1초)
        yield return new WaitForSeconds(1f);

        // 보물찾기 생각 표시 (지속)
        if (isSearching && pet.State.IsTreasureHuntActive)
        {
            pet.ShowEmotion(EmotionType.Thought_TresureHunt);
        }
    }

    /// <summary>
    /// 디버그 시각화 (에디터에서만)
    /// </summary>
    public void OnDrawGizmos()
    {
        if (!isSearching) return;
        if (pet == null) return;

        // 현재 타겟과의 연결선
        if (targetSpot != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(pet.transform.position + Vector3.up * 0.5f,
                          targetSpot.transform.position + Vector3.up * 0.5f);

            // 타겟 위치에 구 표시
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetSpot.transform.position, 0.8f);
        }

        // 근거리 스캔 범위 표시
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(pet.transform.position, NEAR_SCAN_DISTANCE);

        // 막힘 상태 표시
        if (stuckTimer > 0)
        {
            Gizmos.color = Color.Lerp(Color.yellow, Color.red, stuckTimer / STUCK_THRESHOLD);
            Gizmos.DrawWireSphere(pet.transform.position, 1f + stuckTimer * 0.5f);
        }

        #if UNITY_EDITOR
        // 상태 정보 표시
        string debugInfo = $"{pet.petName}\n";
        if (targetSpot != null)
        {
            float distance = Vector3.Distance(pet.transform.position, targetSpot.transform.position);
            debugInfo += $"타겟: {distance:F1}m\n";
        }
        else if (isWandering)
        {
            debugInfo += "배회 중\n";
        }
        if (stuckTimer > 0)
        {
            debugInfo += $"막힘: {stuckTimer:F1}초";
        }
        UnityEditor.Handles.Label(pet.transform.position + Vector3.up * 2f, debugInfo);
        #endif
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