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
    private float searchTimer = 0f;
    private const float SEARCH_INTERVAL = 0.5f; // 0.5초마다 새 타겟 검색
    
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
    private const float NEAR_SEARCH_DISTANCE = 50f;    // 근거리 탐색
    private const float FAR_SEARCH_DISTANCE = 100f;    // 중거리 탐색
    
    public override string Name => "TreasureHunt";
    public override bool IsInterruptible => false; // 보물찾기는 중단 불가
    
    public TreasureHuntActivity(PetController petController, PetMovementController movementController) : base(petController)
    {
        agent = pet.agent;
        moveController = movementController;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 보물찾기 모드가 활성화되어 있고, 친밀도가 충분할 때만
        if (!state.IsTreasureHuntActive) return false;
        if (needs.Affection < 75f) return false;
        
        // 이미 다른 중요한 상태에 있으면 불가
        if (state.IsHolding || state.IsSelected) return false;
        if (state.CurrentStatus == PetStatus.Emergency) return false;
        
        return state.CurrentStatus == PetStatus.TreasureHunting || 
               state.CurrentStatus == PetStatus.Idle;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 보물찾기는 높은 우선순위 (모이기보다는 낮음)
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
            
            // 목표 지점 도착 체크
            if (!agent.pathPending && agent.remainingDistance <= 2f)
            {
                // 이 지점에 보물이 있는지 확인
                if (targetSpot.HasTreasure)
                {
                    // 보물 획득 시도
                    if (targetSpot.TryCollect(pet))
                    {
                        // 성공: 보물 줍기
                        pet.StartCoroutine(PickupTreasureSequence());
                    }
                    else
                    {
                        // 실패: 다른 펫이 먼저 가져감
                        Debug.Log($"{pet.petName}: 아쉽게도 다른 펫이 먼저 보물을 가져갔습니다.");
                        pet.ShowEmotion(EmotionType.Sad);
                        
                        // 잠시 실망 표현 후 다른 보물 찾기
                        pet.StartCoroutine(DisappointmentAndSearch());
                    }
                }
                else
                {
                    // 보물이 이미 없음
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
        Debug.Log($"[TreasureHunt] {pet.petName}: 보물찾기 종료");
        
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
        
        // 애니메이션 정상화
        pet.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
        
        isSearching = false;
        hasFoundTreasure = false;
        hasDroppedTreasure = false;
        isWandering = false;
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
        
        // 1단계: 근거리 탐색 (50m)
        targetSpot = TreasureHuntManager.Instance.FindNearestAvailableSpot(
            pet.transform.position, NEAR_SEARCH_DISTANCE);
        
        // 2단계: 중거리 탐색 (100m)
        if (targetSpot == null)
        {
            targetSpot = TreasureHuntManager.Instance.FindNearestAvailableSpot(
                pet.transform.position, FAR_SEARCH_DISTANCE);
        }
        
        if (targetSpot != null)
        {
            // 보물 발견! 속도 증가
            if (agent != null && agent.enabled)
            {
                agent.speed = pet.Movement.walkSpeed * FOUND_SPEED_MULTIPLIER;
                agent.SetDestination(targetSpot.transform.position);
                agent.isStopped = false;
            }
            
            // 이 보물을 목표로 설정 (경쟁 추적)
            targetSpot.AddCompetingPet(pet);
            
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
        if (agent == null || !agent.enabled) return;
        
        // 대기 위치가 변경되었는지 확인하고 재설정
        if (targetSpot != null)
        {
            Vector3 expectedDestination = targetSpot.WaitingPosition;
            if (Vector3.Distance(agent.destination, expectedDestination) > 1f)
            {
                Debug.Log($"{pet.petName}: 경로 이탈 감지! 대기 위치로 재설정");
                agent.SetDestination(expectedDestination);
                return;  // 아직 도착 안 함
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
            
            // 카메라 바라보기
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
        while (hasFoundTreasure && hasDroppedTreasure)
        {
            // 점프 애니메이션
            if (pet.animator)
            {
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Jump);
            }
            
            // 점프 중 행복 표현
            if (Random.value < 0.3f) // 30% 확률로 감정 표현
            {
                pet.ShowEmotion(EmotionType.Love);
            }
            
            yield return new WaitForSeconds(1f);
            
            // Idle 애니메이션
            if (pet.animator)
            {
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Idle);
            }
            
            yield return new WaitForSeconds(2f);
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
        float duration = 0.3f;
        float elapsed = 0f;

        // 부모 해제 (월드 좌표로 전환)
        carriedTreasure.transform.SetParent(null);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 포물선 움직임 (자연스럽게 떨어지는 효과)
            Vector3 pos = Vector3.Lerp(from, to, t);
            // 시작 높이에서 최종 높이로 포물선 형태로 이동
            float heightCurve = 1f - (t - 0.5f) * (t - 0.5f) * 4f; // 중간에 살짝 올라갔다가 내려옴
            pos.y = Mathf.Lerp(from.y, to.y, t) + heightCurve * 0.2f;

            carriedTreasure.transform.position = pos;
            yield return null;
        }

        // 최종 위치 설정
        carriedTreasure.transform.position = to;
        carriedTreasure.transform.rotation = Quaternion.identity;

        // TreasureController의 EnableCollection 호출
        TreasureController treasureController = carriedTreasure.GetComponent<TreasureController>();
        if (treasureController != null)
        {
            Debug.Log($"[TreasureHuntActivity] {pet.petName}: EnableCollection 호출");
            treasureController.EnableCollection();
        }

        Debug.Log($"[TreasureHuntActivity] {pet.petName}: 보물을 내려놓고 대기 중! 위치: {carriedTreasure.transform.position}");
    }
    
    /// <summary>
    /// 실망 표현 후 재탐색
    /// </summary>
    private IEnumerator DisappointmentAndSearch()
    {
        // 실망 애니메이션 (예: 고개 숙이기)
        if (pet.animator)
        {
            pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Idle);
        }
        
        // 잠시 멈춤
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
        }
        
        yield return new WaitForSeconds(1.5f);
        
        // 다시 이동 시작
        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
        }
        
        // 새로운 보물 찾기
        FindNewTarget();
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