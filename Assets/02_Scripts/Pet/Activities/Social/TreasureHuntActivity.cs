using UnityEngine;
using UnityEngine.AI;
using System.Collections;

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
    
    // 보물찾기 속도 배율
    private const float SEARCH_SPEED_MULTIPLIER = 2f;
    private const float FOUND_SPEED_MULTIPLIER = 3f;
    private const float ANGULAR_SPEED_MULTIPLIER = 3f;
    private const float ACCELERATION_MULTIPLIER = 3f;
    
    // 탐색 거리 설정
    private const float SEARCH_DISTANCE = 70f;
    
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
        Debug.Log($"[TreasureHunt] {pet.petName}: 보물찾기 시작!");
        
        // 상태 설정
        pet.State.TrySetStatus(PetStatus.TreasureHunting);
        isSearching = true;
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
        
        // 보물찾기가 종료되면 즉시 Stop 호출하여 완전히 중단
        if (!pet.State.IsTreasureHuntActive)
        {
            Debug.Log($"[TreasureHunt] {pet.petName}: 보물찾기 종료 감지, 활동 완전 중단");
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
        
        // agent 목적지 유실 체크 및 복구
        if (agent != null && agent.enabled && !agent.isStopped)
        {
            // 목적지가 없거나 경로가 없는 경우
            if (!agent.hasPath || agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
            {
                // 탐색 중이고 타겟이 있으면 재설정
                if (targetSpot != null)
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
        
        // 주기적으로 타겟 재검색
        searchTimer += Time.deltaTime;
        if (searchTimer >= SEARCH_INTERVAL)
        {
            searchTimer = 0f;
            CheckCurrentTarget();
        }
        
        // 현재 타겟으로 이동
        if (targetSpot != null && agent != null && agent.enabled)
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
                Debug.Log($"[TreasureHunt] {pet.petName}: 경로 재계산 중 (경로 없음)");
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
                
                // 이미 전환 중이면 중복 실행 방지
                if (isTransitioningToFound)
                {
                    return;
                }
                
                Debug.Log($"[TreasureHunt] {pet.petName}: 보물 획득 조건 충족 (거리: {agent.remainingDistance:F1}m)");
                
                // 이 지점에 보물이 있는지 확인
                if (targetSpot.HasTreasure)
                {
                    // 보물 획득 시도
                    if (targetSpot.TryCollect(pet))
                    {
                        // 성공: 보물 획득 후 TreasureFound 상태로 전환
                        Debug.Log($"[TreasureHunt] {pet.petName}: 보물 획득 성공! TreasureFound 상태로 전환");
                        approachStartTime = 0f;  // 리셋
                        isTransitioningToFound = true;  // 전환 시작
                        isSearching = false;  // Update 즉시 중단
                        
                        // 먹는 애니메이션 잠시 재생 후 상태 전환
                        pet.StartCoroutine(TransitionToFoundState());
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
        else if (targetSpot == null)  // 타겟이 없으면 배회
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
        Debug.Log($"[TreasureHunt] {pet.petName}: 보물찾기 종료");
        
        // 플래그 리셋
        isTransitioningToFound = false;
        
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
        
        // 애니메이션 정상화
        pet.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
        
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
                Debug.Log($"{pet.petName}: 대체 경로로 이동 - 목표: {hit.position}");
            }
        }
    }
    
    /// <summary>
    /// 보물을 찾은 후 TreasureFound 상태로 전환
    /// </summary>
    private IEnumerator TransitionToFoundState()
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
        
        // 보물 시각적 처리를 먼저 수행 (상태 전환 전에!)
        if (targetSpot != null && targetSpot.CurrentTreasure != null)
        {
            GameObject treasure = targetSpot.CurrentTreasure;
            
            // 1순위: 펫에 설정된 treasureHoldPoint 사용
            if (pet.treasureHoldPoint != null)
            {
                treasure.transform.SetParent(pet.treasureHoldPoint);
                treasure.transform.localPosition = Vector3.zero;
                treasure.transform.localRotation = Quaternion.identity;
            }
            // 2순위: 본 찾기
            else
            {
                Transform mouthBone = FindMouthBone();
                if (mouthBone != null)
                {
                    treasure.transform.SetParent(mouthBone);
                    treasure.transform.localPosition = Vector3.forward * 0.3f;
                }
                else
                {
                    // 3순위: 펫 위에 띄우기
                    treasure.transform.SetParent(pet.transform);
                    treasure.transform.localPosition = Vector3.up * 1.5f;
                }
            }
            
            // TreasureController의 StartCarrying 호출
            TreasureController treasureController = treasure.GetComponent<TreasureController>();
            if (treasureController != null)
            {
                treasureController.StartCarrying(pet);
                // IsCarried는 읽기 전용이므로 StartCarrying에서 설정됨
                Debug.Log($"[TreasureHunt] {pet.petName}: StartCarrying 호출 완료 - IsCarried: {treasureController.IsCarried}, CarryingPet: {treasureController.CarryingPet?.petName}");
            }
            
            // 매니저에 보물 찾음 알림
            if (TreasureHuntManager.Instance != null)
            {
                TreasureHuntManager.Instance.OnPetFoundTreasure(targetSpot, pet);
            }
            
            // targetSpot은 유지 - TreasureFoundActivity가 사용할 수 있도록
        }
        
        // 기쁨 표현
        pet.ShowEmotion(EmotionType.Happy);
        
        // 이제 보물이 부착되었으므로 상태 전환
        Debug.Log($"[TreasureHunt] {pet.petName}: 보물 부착 완료, TreasureFound 상태로 전환");
        pet.State.TrySetStatus(PetStatus.TreasureFound);
        
        // AI에게 즉시 재평가 요청 - 새로운 Activity로 전환하도록
        if (pet.AI != null)
        {
            pet.AI.InterruptAndResetAI();
        }
        
        Debug.Log($"[TreasureHunt] {pet.petName}: TreasureFound 상태로 전환 완료!");
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