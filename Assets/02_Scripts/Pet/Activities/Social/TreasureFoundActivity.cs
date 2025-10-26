using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 보물을 찾은 펫이 보물을 운반하고 축하하는 활동
/// TreasureHuntActivity에서 분리된 전용 Activity
/// </summary>
public class TreasureFoundActivity : PetActivityAdapter
{
    private readonly NavMeshAgent agent;
    private readonly PetMovementController moveController;
    
    // 보물 관련
    private TreasureSpot targetSpot;
    private GameObject carriedTreasure;
    private GameObject droppedTreasureObject;
    
    // 상태 플래그
    private bool hasDroppedTreasure = false;
    private bool isMovingToWaitingPoint = false;
    private bool isCelebrating = false;
    private Coroutine celebrationCoroutine;
    
    // 경로 막힘 감지용
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private const float STUCK_THRESHOLD = 3f;
    private const float STUCK_DISTANCE = 0.5f;

    // 속도 설정
    private const float CARRY_SPEED_MULTIPLIER = 3f;
    private const float ANGULAR_SPEED_MULTIPLIER = 3f;
    private const float ACCELERATION_MULTIPLIER = 3f;

    // 도착 판정 거리
    private const float ARRIVAL_DISTANCE = 0.5f;

    // 애니메이션 시간
    private const float EAT_ANIMATION_DURATION = 0.3f;
    private const float DROP_ANIMATION_DURATION = 0.8f;
    private const float BOUNCE_DURATION = 0.2f;

    // 보물 드롭 설정
    private const float TREASURE_DROP_DISTANCE = 3.5f;
    private const float TREASURE_HEIGHT_OFFSET = 1f;
    private const float BOUNCE_HEIGHT = 0.2f;
    private const float INITIAL_VELOCITY_Y = 2f;
    private const float GRAVITY = 9.8f;

    // 대체 위치 검색 설정
    private const float ALTERNATIVE_SEARCH_RADIUS = 3f;
    private const int ALTERNATIVE_SEARCH_ATTEMPTS = 8;

    // 축하 점프 설정
    private const float CELEBRATION_JUMP_DURATION = 2f;
    private const float CELEBRATION_WAIT_TIME = 2f;
    
    public override string Name => "TreasureFound";
    public override bool IsInterruptible => false; // 보물 수집까지 중단 불가
    
    public TreasureFoundActivity(PetController petController, PetMovementController movementController) : base(petController)
    {
        agent = pet.agent;
        moveController = movementController;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 보물을 찾은 상태이기만 하면 됨 (보물찾기 종료 여부와 완전히 무관)
        if (state.CurrentStatus != PetStatus.TreasureFound) return false;
        if (state.IsHolding || state.IsSelected) return false;
        
        return true;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs)) return 0f;
        
        // 보물을 찾은 상태는 최고 우선순위
        return 25.0f;
    }
    
    public override void Start()
    {

        // 모든 상태 변수 명시적 초기화
        targetSpot = null;
        carriedTreasure = null;
        droppedTreasureObject = null;
        hasDroppedTreasure = false;
        isMovingToWaitingPoint = false;
        isCelebrating = false;
        celebrationCoroutine = null;
        lastPosition = pet.transform.position;
        stuckTimer = 0f;

        // 보물 감정 표시 (지속)
        pet.ShowEmotion(EmotionType.Tresure);

        // 보물 정보 가져오기
        FindCarriedTreasure();
        
        if (carriedTreasure != null)
        {
            // 속도 설정 및 충돌 회피 설정 (NavMesh 상태 확인)
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.speed = pet.Movement.walkSpeed * CARRY_SPEED_MULTIPLIER;
                agent.angularSpeed = pet.Movement.angularSpeed * ANGULAR_SPEED_MULTIPLIER;
                agent.acceleration = pet.Movement.acceleration * ACCELERATION_MULTIPLIER;
                
                // 다른 펫들을 통과할 수 있도록 설정
                agent.avoidancePriority = 10;  // 높은 우선순위 (기본값 50)
                agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
                agent.radius = agent.radius * 0.5f;  // 충돌 반경 축소
            }
            
            // 대기 위치로 이동 시작
            MoveToWaitingPoint();
        }
        else
        {
            Debug.LogWarning($"[TreasureFound] {pet.petName}: 보물을 찾을 수 없음!");
            // 보물이 없으면 Idle로 전환
            pet.State.TrySetStatus(PetStatus.Idle);
        }
    }
    
    public override void Update()
    {
        // 보물찾기 종료 여부와 무관하게 계속 진행
        // 보물이 수집될 때까지 축하 행동 유지
        
        // 축하 점프 중이면 카메라만 바라보기
        if (isCelebrating && hasDroppedTreasure)
        {
            LookAtCamera();
            return;
        }
        
        // 대기 위치로 이동 중
        if (isMovingToWaitingPoint && !hasDroppedTreasure)
        {
            CheckWaitingPointArrival();
        }
        
        // 회전 처리
        if (moveController != null && !hasDroppedTreasure)
        {
            moveController.HandleRotation();
        }
    }
    
    public override void Stop()
    {
        
        // 축하 코루틴 정리
        if (celebrationCoroutine != null)
        {
            pet.StopCoroutine(celebrationCoroutine);
            celebrationCoroutine = null;
        }
        
        // NavMesh 설정 원래대로 복구 (NavMesh 상태 확인)
        if (agent != null && agent.enabled)
        {
            // NavMesh에 있는 경우에만 상태 변경
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
            }
            
            // 모든 NavMesh 설정 원래대로 복구
            agent.speed = pet.Movement.walkSpeed;
            agent.angularSpeed = pet.Movement.angularSpeed;
            agent.acceleration = pet.Movement.acceleration;
            
            // 충돌 회피 설정 원래대로 복구
            agent.avoidancePriority = 50;  // 기본값으로 복구
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.radius = 0.5f;  // 기본 반경으로 복구 (일반적인 펫 크기)
        }
        
        // 애니메이션 정상화
        pet.GetComponent<PetAnimationController>()?.StopContinuousAnimation();
        
        // 들고 있던 보물 정리
        if (carriedTreasure != null && !hasDroppedTreasure)
        {
            carriedTreasure.transform.SetParent(null);
            carriedTreasure = null;
        }
        
        // 타겟 해제
        if (targetSpot != null)
        {
            targetSpot.Release(pet);
            targetSpot = null;
        }
        
        // 상태 초기화 - 내려놓은 보물은 유지
        if (!hasDroppedTreasure)
        {
            // 아직 내려놓지 않은 경우만 초기화
            droppedTreasureObject = null;
        }
        else
        {
            // 이미 내려놓은 보물은 유지
            if (droppedTreasureObject != null)
            {
            }
            else
            {
            }
        }
        
        hasDroppedTreasure = false;
        isMovingToWaitingPoint = false;
        isCelebrating = false;
    }
    
    /// <summary>
    /// 현재 들고 있는 보물 찾기
    /// </summary>
    private void FindCarriedTreasure()
    {

        // 펫의 모든 자식에서 TreasureController 찾기
        TreasureController[] treasures = pet.GetComponentsInChildren<TreasureController>();

        foreach (var tc in treasures)
        {
            if (tc != null)
            {

                // IsCarried 체크를 제거하고 CarryingPet으로 확인
                if (tc.CarryingPet == pet)
                {
                    carriedTreasure = tc.gameObject;

                    // TreasureSpot 찾기 - 두 가지 방법 시도
                    TreasureSpot[] spots = Object.FindObjectsOfType<TreasureSpot>();
                    foreach (var spot in spots)
                    {
                        // 방법 1: CurrentTreasure로 매칭 (기존 방식)
                        if (spot.CurrentTreasure == carriedTreasure)
                        {
                            targetSpot = spot;
                            break;
                        }
                        // 방법 2: 펫이 점유한 스팟 확인 (백업)
                        else if (spot.IsOccupied && !spot.HasTreasure)
                        {
                            // 이 펫이 점유한 스팟인지 확인 (occupyingPet은 private이므로 간접 확인)
                            // HasTreasure가 false이고 IsOccupied가 true면 이 펫이 가져간 것
                            targetSpot = spot;
                            break;
                        }
                    }

                    // targetSpot을 못 찾은 경우 가장 가까운 스팟 사용
                    if (targetSpot == null && spots.Length > 0)
                    {
                        float minDistance = float.MaxValue;
                        foreach (var spot in spots)
                        {
                            float distance = Vector3.Distance(pet.transform.position, spot.transform.position);
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                targetSpot = spot;
                            }
                        }
                    }
                    return;
                }
            }
        }

    }
    
    /// <summary>
    /// 대기 위치로 이동 시작
    /// </summary>
    private void MoveToWaitingPoint()
    {
        if (targetSpot == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        
        isMovingToWaitingPoint = true;
        Vector3 waitingPos = targetSpot.WaitingPosition;
        
        agent.SetDestination(waitingPos);
        agent.isStopped = false;
        
        // 달리기 애니메이션
        if (pet.animator)
        {
            pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Run);
        }
        
    }
    
    
    /// <summary>
    /// 대기 위치 도착 체크
    /// </summary>
    private void CheckWaitingPointArrival()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        // 물 속에 있으면 대기
        if (pet.State.IsInWater)
        {
            return;
        }
        
        // 경로 막힘 감지
        float distance = Vector3.Distance(pet.transform.position, lastPosition);
        if (distance < STUCK_DISTANCE)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= STUCK_THRESHOLD)
            {
                FindAlternativeWaitingPoint();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
            lastPosition = pet.transform.position;
        }

        // 도착 체크
        if (!agent.pathPending && agent.remainingDistance <= ARRIVAL_DISTANCE)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            isMovingToWaitingPoint = false;

            // 보물 내려놓기 시퀀스 시작
            pet.StartCoroutine(DropTreasureSequence());
        }
    }
    
    /// <summary>
    /// 경로가 막혔을 때 대체 위치 찾기
    /// </summary>
    private void FindAlternativeWaitingPoint()
    {
        if (targetSpot == null || agent == null || !agent.isOnNavMesh) return;

        Vector3 currentTarget = targetSpot.WaitingPosition;

        // 현재 위치 주변에서 유효한 NavMesh 위치 찾기
        for (int i = 0; i < ALTERNATIVE_SEARCH_ATTEMPTS; i++)
        {
            float angle = i * 45f;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 testPosition = currentTarget + direction * ALTERNATIVE_SEARCH_RADIUS;

            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(testPosition, out hit, ALTERNATIVE_SEARCH_RADIUS, UnityEngine.AI.NavMesh.AllAreas))
            {
                // 유효한 경로가 있는지 확인
                UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                {
                    agent.SetDestination(hit.position);
                    lastPosition = pet.transform.position;
                    return;
                }
            }
        }
        
        // 대체 위치를 찾지 못한 경우, 현재 위치에서 보물 내려놓기
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        isMovingToWaitingPoint = false;
        pet.StartCoroutine(DropTreasureSequence());
    }
    
    /// <summary>
    /// 보물을 내려놓는 시퀀스
    /// </summary>
    private IEnumerator DropTreasureSequence()
    {
        // 먼저 플래그 설정하여 보물 보호
        hasDroppedTreasure = true;
        
        // 먹는 애니메이션 (내려놓기 모션)
        if (pet.animator)
        {
            pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Eat);
        }

        yield return new WaitForSeconds(EAT_ANIMATION_DURATION);
        
        // 애니메이션 커브로 보물 떨어뜨리기
        yield return DropTreasureAnimationImproved();
        
        // 축하 점프 시작
        isCelebrating = true;
        celebrationCoroutine = pet.StartCoroutine(CelebrationJump());
    }
    
    /// <summary>
    /// 개선된 애니메이션 커브로 보물 떨어뜨리기
    /// </summary>
    private IEnumerator DropTreasureAnimationImproved()
    {
        if (carriedTreasure == null) yield break;
        
        GameObject treasureToAnimate = carriedTreasure;
        
        // 부모 해제
        treasureToAnimate.transform.SetParent(null);
        
        // 시작 위치 (현재 보물 위치 - 펫 입)
        Vector3 startPos = treasureToAnimate.transform.position;

        // 목표 위치 (펫 앞쪽 지면)
        Vector3 targetPos = pet.transform.position + pet.transform.forward * TREASURE_DROP_DISTANCE;
        
        // RaycastAll로 모든 충돌체 감지 후 펫 제외
        Vector3 rayStart = targetPos + Vector3.up * 5f;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 10f);
        
        bool groundFound = false;
        foreach (var hit in hits)
        {
            // 펫이 아닌 경우만 지면으로 인식
            if (hit.collider != null && hit.collider.GetComponent<PetController>() == null)
            {
                targetPos.y = hit.point.y + TREASURE_HEIGHT_OFFSET;
                groundFound = true;
                break;
            }
        }
        
        // 지면을 찾지 못한 경우 Terrain 레이어만 다시 검사
        if (!groundFound)
        {
            int terrainLayer = LayerMask.NameToLayer("Terrain");
            if (terrainLayer != -1)
            {
                LayerMask terrainOnlyMask = 1 << terrainLayer;
                RaycastHit hit;
                if (Physics.Raycast(rayStart, Vector3.down, out hit, 10f, terrainOnlyMask))
                {
                    targetPos.y = hit.point.y + 0.3f;
                }
                else
                {
                    // 최후의 수단으로 펫과 같은 높이 사용
                    targetPos.y = pet.transform.position.y;
                }
            }
            else
            {
                targetPos.y = pet.transform.position.y;
            }
        }
        
        // 애니메이션 루프
        float elapsed = 0f;
        while (elapsed < DROP_ANIMATION_DURATION)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / DROP_ANIMATION_DURATION;

            // XZ 평면 이동 (선형 보간 + 약간의 곡선)
            float easedT = Mathf.SmoothStep(0, 1, t);
            Vector3 pos = Vector3.Lerp(startPos, targetPos, easedT);

            // Y축 이동 (중력 시뮬레이션)
            float timeInAir = elapsed;
            float currentY = startPos.y + (INITIAL_VELOCITY_Y * timeInAir) - (0.5f * GRAVITY * timeInAir * timeInAir);
            
            // 지면 아래로 가지 않도록 제한
            currentY = Mathf.Max(currentY, targetPos.y);
            pos.y = currentY;
            
            // 위치 적용
            if (treasureToAnimate == null) yield break;
            treasureToAnimate.transform.position = pos;
            
            yield return null;
        }
        
        // 최종 위치 설정
        if (treasureToAnimate == null) yield break;
        treasureToAnimate.transform.position = targetPos;

        // 바운스 효과 추가 (보물이 여전히 존재하는 경우에만)
        if (treasureToAnimate != null)
        {
            yield return BounceEffect(treasureToAnimate, targetPos);
        }

        // 최종 처리 (보물이 여전히 존재하는 경우에만)
        if (treasureToAnimate != null)
        {
            droppedTreasureObject = treasureToAnimate;
            carriedTreasure = null;

            // 수집 가능하게 설정
            TreasureController treasureController = treasureToAnimate.GetComponent<TreasureController>();
            if (treasureController != null)
            {
                treasureController.EnableCollection();
            }
        }
        else
        {
            // 보물이 이미 수집되어 사라진 경우
            droppedTreasureObject = null;
            carriedTreasure = null;
        }
        
    }
    
    /// <summary>
    /// 바운스 효과
    /// </summary>
    private IEnumerator BounceEffect(GameObject treasure, Vector3 groundPos)
    {
        if (treasure == null) yield break;

        float elapsed = 0f;

        while (elapsed < BOUNCE_DURATION)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / BOUNCE_DURATION;
            
            // 바운스 커브 (위로 올라갔다가 내려옴)
            float bounce = Mathf.Sin(t * Mathf.PI) * BOUNCE_HEIGHT;
            
            if (treasure == null) yield break;
            treasure.transform.position = groundPos + Vector3.up * bounce;
            
            yield return null;
        }
        
        // 최종 위치 고정
        if (treasure != null)
        {
            treasure.transform.position = groundPos;
        }
    }
    
    
    /// <summary>
    /// 축하 점프
    /// </summary>
    private IEnumerator CelebrationJump()
    {
        var animController = pet.GetComponent<PetAnimationController>();
        
        
        // 보물이 수집될 때까지 계속 점프 (보물찾기 종료 여부와 무관)
        while (droppedTreasureObject != null)
        {
            
            // 점프 애니메이션
            if (animController != null)
            {
                yield return pet.StartCoroutine(animController.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Jump,
                    CELEBRATION_JUMP_DURATION,
                    true,
                    false
                ));
            }
            else if (pet.animator)
            {
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Jump);
                yield return new WaitForSeconds(CELEBRATION_JUMP_DURATION);
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Idle);
            }

            // 보물 감정이 계속 유지됨 (Love 감정 제거)

            yield return new WaitForSeconds(CELEBRATION_WAIT_TIME);
        }


        // 보물이 수집된 경우에만 Idle로 전환
        if (droppedTreasureObject == null)
        {
            isCelebrating = false;
            hasDroppedTreasure = false;

            // 보물 감정 중단
            if (pet.emotionController != null)
            {
                pet.emotionController.StopTreasureEmotion();
            }

            pet.State.TrySetStatus(PetStatus.Idle);

            // AI 재평가
            if (pet.AI != null)
            {
                pet.AI.InterruptAndResetAI();
            }
        }
    }
    
    /// <summary>
    /// 카메라 바라보기
    /// </summary>
    private void LookAtCamera()
    {
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