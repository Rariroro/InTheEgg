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
    private const float STUCK_THRESHOLD = 3f;  // 3초 동안 움직이지 못하면 막힌 것으로 판단
    private const float STUCK_DISTANCE = 0.5f;  // 이동 거리가 0.5 미만이면 막힌 것으로 판단
    
    // 속도 설정
    private const float CARRY_SPEED_MULTIPLIER = 3f;
    private const float ANGULAR_SPEED_MULTIPLIER = 3f;
    private const float ACCELERATION_MULTIPLIER = 3f;
    
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
        Debug.Log($"[TreasureFound] {pet.petName}: 보물 운반 활동 시작!");
        
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
                Debug.Log($"[TreasureFound] {pet.petName}: NavMesh 우선순위 설정 - Priority: 10, ObstacleAvoidance: None");
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
        Debug.Log($"[TreasureFound] {pet.petName}: 보물 운반 활동 종료 (완전 초기화)");
        
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
            Debug.Log($"[TreasureFound] {pet.petName}: NavMesh 설정 복구 완료");
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
                Debug.Log($"[TreasureFound] {pet.petName}: Stop() - 내려놓은 보물 유지: {droppedTreasureObject.name}");
            }
            else
            {
                Debug.Log($"[TreasureFound] {pet.petName}: Stop() - 내려놓은 보물이 이미 삭제됨");
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
        Debug.Log($"[TreasureFound] {pet.petName}: 보물 찾기 시작");
        
        // 펫의 모든 자식에서 TreasureController 찾기
        TreasureController[] treasures = pet.GetComponentsInChildren<TreasureController>();
        Debug.Log($"[TreasureFound] {pet.petName}: 찾은 TreasureController 수: {treasures.Length}");
        
        foreach (var tc in treasures)
        {
            if (tc != null)
            {
                Debug.Log($"[TreasureFound] {pet.petName}: 보물 발견 - IsCarried: {tc.IsCarried}, CarryingPet: {tc.CarryingPet?.petName}");
                
                // IsCarried 체크를 제거하고 CarryingPet으로 확인
                if (tc.CarryingPet == pet)
                {
                    carriedTreasure = tc.gameObject;
                    Debug.Log($"[TreasureFound] {pet.petName}: 내 보물 확인! 위치: {carriedTreasure.transform.parent?.name}");
                    
                    // TreasureSpot 찾기
                    TreasureSpot[] spots = Object.FindObjectsOfType<TreasureSpot>();
                    foreach (var spot in spots)
                    {
                        if (spot.CurrentTreasure == carriedTreasure)
                        {
                            targetSpot = spot;
                            Debug.Log($"[TreasureFound] {pet.petName}: TreasureSpot 찾음: {spot.name}");
                            break;
                        }
                    }
                    return;
                }
            }
        }
        
        Debug.Log($"[TreasureFound] {pet.petName}: 보물을 찾지 못함!");
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
        
        Debug.Log($"[TreasureFound] {pet.petName}: 대기 위치로 이동 시작 - {waitingPos}");
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
            Debug.Log($"[TreasureFound] {pet.petName}: 물 속에서 대기 중...");
            return;
        }
        
        // 경로 막힘 감지
        float distance = Vector3.Distance(pet.transform.position, lastPosition);
        if (distance < STUCK_DISTANCE)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= STUCK_THRESHOLD)
            {
                Debug.Log($"[TreasureFound] {pet.petName}: 경로가 막혔습니다. 대체 위치 찾기...");
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
        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
        {
            Debug.Log($"[TreasureFound] {pet.petName}: 대기 위치 도착!");
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
        float searchRadius = 3f;
        
        // 현재 위치 주변에서 유효한 NavMesh 위치 찾기
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 testPosition = currentTarget + direction * searchRadius;
            
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(testPosition, out hit, searchRadius, UnityEngine.AI.NavMesh.AllAreas))
            {
                // 유효한 경로가 있는지 확인
                UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                {
                    Debug.Log($"[TreasureFound] {pet.petName}: 대체 위치 발견 - {hit.position}");
                    agent.SetDestination(hit.position);
                    lastPosition = pet.transform.position;
                    return;
                }
            }
        }
        
        // 대체 위치를 찾지 못한 경우, 현재 위치에서 보물 내려놓기
        Debug.Log($"[TreasureFound] {pet.petName}: 대체 위치를 찾지 못했습니다. 현재 위치에서 보물을 내려놓습니다.");
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
        
        yield return new WaitForSeconds(0.3f);
        
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
        
        // 목표 위치 (펫 앞쪽 지면 - 더 멀리)
        Vector3 targetPos = pet.transform.position + pet.transform.forward * 3.5f;
        
        // Raycast로 정확한 지면 높이 찾기
        RaycastHit hit;
        Vector3 rayStart = targetPos + Vector3.up * 5f;
        int groundLayer = LayerMask.NameToLayer("Ground");
        LayerMask layerMask = groundLayer != -1 ? (1 << groundLayer) | (1 << 0) : ~0;
        
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 10f, layerMask))
        {
            targetPos.y = hit.point.y + 0.3f;  // 지면 위 0.3f (보물 크기 고려)
        }
        else
        {
            targetPos.y = pet.transform.position.y;
        }
        
        // 애니메이션 파라미터
        float duration = 0.8f;
        float elapsed = 0f;
        float gravity = 9.8f;
        
        // 초기 속도 계산 (포물선 운동)
        float initialVelocityY = 2f;  // 살짝 위로 던지는 효과
        
        // 애니메이션 루프
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // XZ 평면 이동 (선형 보간 + 약간의 곡선)
            float easedT = Mathf.SmoothStep(0, 1, t);
            Vector3 pos = Vector3.Lerp(startPos, targetPos, easedT);
            
            // Y축 이동 (중력 시뮬레이션)
            float timeInAir = elapsed;
            float currentY = startPos.y + (initialVelocityY * timeInAir) - (0.5f * gravity * timeInAir * timeInAir);
            
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
        
        // 바운스 효과 추가
        yield return BounceEffect(treasureToAnimate, targetPos);
        
        // 최종 처리
        droppedTreasureObject = treasureToAnimate;
        carriedTreasure = null;
        
        // 수집 가능하게 설정
        TreasureController treasureController = droppedTreasureObject?.GetComponent<TreasureController>();
        if (treasureController != null)
        {
            treasureController.EnableCollection();
        }
        
        Debug.Log($"[TreasureFound] {pet.petName}: 보물을 자연스럽게 떨어뜨렸습니다!");
    }
    
    /// <summary>
    /// 바운스 효과
    /// </summary>
    private IEnumerator BounceEffect(GameObject treasure, Vector3 groundPos)
    {
        if (treasure == null) yield break;
        
        float bounceHeight = 0.2f;
        float bounceDuration = 0.2f;
        float elapsed = 0f;
        
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bounceDuration;
            
            // 바운스 커브 (위로 올라갔다가 내려옴)
            float bounce = Mathf.Sin(t * Mathf.PI) * bounceHeight;
            
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
    /// 물리 기반으로 보물을 자연스럽게 떨어뜨리기 (백업)
    /// </summary>
    private void DropTreasureWithPhysics()
    {
        if (carriedTreasure == null) return;
        
        GameObject treasureToAnimate = carriedTreasure;
        
        // 부모 해제
        treasureToAnimate.transform.SetParent(null);
        
        // 현재 위치 유지 (펫 입 위치)
        // 위치는 이미 펫에 부착된 상태이므로 그대로 유지
        
        // Rigidbody 컴포넌트 확인 및 추가
        Rigidbody rb = treasureToAnimate.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = treasureToAnimate.AddComponent<Rigidbody>();
        }
        
        // 물리 설정
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = 0.5f;  // 가벼운 보물
        rb.linearDamping = 0.5f;  // 공기 저항
        rb.angularDamping = 0.5f;  // 회전 저항
        
        // 콜라이더가 없으면 추가
        if (treasureToAnimate.GetComponent<Collider>() == null)
        {
            treasureToAnimate.AddComponent<BoxCollider>();
        }
        
        // 약간의 앞쪽과 아래쪽 힘 추가 (자연스럽게 떨어뜨리는 효과)
        Vector3 dropForce = pet.transform.forward * 1.5f + Vector3.down * 0.5f;
        rb.AddForce(dropForce, ForceMode.Impulse);
        
        // 약간의 회전 추가 (더 자연스러운 효과)
        rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        
        droppedTreasureObject = treasureToAnimate;
        carriedTreasure = null;
        
        // 1초 후 물리 비활성화 및 수집 가능 설정
        pet.StartCoroutine(FinalizeDroppedTreasure(treasureToAnimate, rb));
        
        Debug.Log($"[TreasureFound] {pet.petName}: 보물을 떨어뜨립니다! (물리 기반)");
    }
    
    /// <summary>
    /// 떨어진 보물 마무리 처리
    /// </summary>
    private IEnumerator FinalizeDroppedTreasure(GameObject treasure, Rigidbody rb)
    {
        // 보물이 땅에 안착할 때까지 대기
        yield return new WaitForSeconds(1.5f);
        
        if (treasure != null && rb != null)
        {
            // 물리 비활성화 (더 이상 움직이지 않도록)
            rb.isKinematic = true;
            rb.useGravity = false;
            
            // 수집 가능하게 설정
            TreasureController treasureController = treasure.GetComponent<TreasureController>();
            if (treasureController != null)
            {
                treasureController.EnableCollection();
            }
            
            Debug.Log($"[TreasureFound] 보물이 땅에 안착했습니다!");
        }
    }
    
    /// <summary>
    /// 보물을 즉시 바닥에 내려놓기 (코루틴 없이) - 백업용
    /// </summary>
    private void DropTreasureImmediate()
    {
        if (carriedTreasure == null) return;
        
        GameObject treasureToAnimate = carriedTreasure;
        
        // 보물 위치 설정 (펫 앞쪽 1.5f 거리)
        Vector3 dropPosition = pet.transform.position + pet.transform.forward * 1.5f;
        
        // Raycast로 지면 높이 찾기
        RaycastHit hit;
        Vector3 rayStart = dropPosition + Vector3.up * 5f;
        
        // Ground 레이어가 있으면 우선 사용, 없으면 Default 레이어 사용
        int groundLayer = LayerMask.NameToLayer("Ground");
        LayerMask layerMask = groundLayer != -1 ? 
            (1 << groundLayer) | (1 << 0) :  // Ground + Default
            ~0;  // 모든 레이어
        
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 10f, layerMask))
        {
            dropPosition.y = hit.point.y + 0.5f;  // 지면 위 0.5f
            Debug.Log($"[TreasureFound] 지면 감지됨: {hit.collider.name}, 높이: {hit.point.y}");
        }
        else
        {
            // 지면을 찾지 못한 경우 펫과 같은 높이 사용
            dropPosition.y = pet.transform.position.y;
            Debug.Log($"[TreasureFound] 지면 미감지, 펫 높이 사용: {dropPosition.y}");
        }
        
        // 부모 해제
        treasureToAnimate.transform.SetParent(null);
        
        // 위치와 회전 설정
        treasureToAnimate.transform.position = dropPosition;
        treasureToAnimate.transform.rotation = Quaternion.identity;
        
        droppedTreasureObject = treasureToAnimate;
        carriedTreasure = null;
        
        // 수집 가능하게 설정
        TreasureController treasureController = droppedTreasureObject.GetComponent<TreasureController>();
        if (treasureController != null)
        {
            treasureController.EnableCollection();
        }
        
        Debug.Log($"[TreasureFound] {pet.petName}: 보물 내려놓기 완료! (즉시 처리)");
    }
    
    /// <summary>
    /// 보물을 바닥에 내려놓기 (기존 애니메이션 버전 - 사용 안 함)
    /// </summary>
    private void DropTreasure()
    {
        if (carriedTreasure == null) return;
        
        Vector3 startPos = carriedTreasure.transform.position;
        Vector3 endPos = pet.transform.position + pet.transform.forward * 0.7f;
        endPos.y = pet.transform.position.y + 1f;
        
        pet.StartCoroutine(DropTreasureAnimation(startPos, endPos));
    }
    
    /// <summary>
    /// 보물 내려놓기 애니메이션
    /// </summary>
    private IEnumerator DropTreasureAnimation(Vector3 from, Vector3 to)
    {
        GameObject treasureToAnimate = carriedTreasure;
        if (treasureToAnimate == null) yield break;
        
        float duration = 0.3f;
        float elapsed = 0f;
        
        treasureToAnimate.transform.SetParent(null);
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            Vector3 pos = Vector3.Lerp(from, to, t);
            float heightCurve = 1f - (t - 0.5f) * (t - 0.5f) * 4f;
            pos.y = Mathf.Lerp(from.y, to.y, t) + heightCurve * 0.2f;
            
            if (treasureToAnimate == null) yield break;
            treasureToAnimate.transform.position = pos;
            yield return null;
        }
        
        if (treasureToAnimate == null) yield break;
        
        treasureToAnimate.transform.position = to;
        treasureToAnimate.transform.rotation = Quaternion.identity;
        
        droppedTreasureObject = treasureToAnimate;
        carriedTreasure = null;
        
        // 수집 가능하게 설정
        TreasureController treasureController = droppedTreasureObject.GetComponent<TreasureController>();
        if (treasureController != null)
        {
            treasureController.EnableCollection();
        }
        
        Debug.Log($"[TreasureFound] {pet.petName}: 보물 내려놓기 완료!");
    }
    
    /// <summary>
    /// 축하 점프
    /// </summary>
    private IEnumerator CelebrationJump()
    {
        var animController = pet.GetComponent<PetAnimationController>();
        
        Debug.Log($"[TreasureFound] {pet.petName}: 축하 점프 시작!");
        
        // 보물이 수집될 때까지 계속 점프 (보물찾기 종료 여부와 무관)
        while (droppedTreasureObject != null)
        {
            
            // 점프 애니메이션
            if (animController != null)
            {
                yield return pet.StartCoroutine(animController.PlayAnimationWithCustomDuration(
                    PetAnimationController.PetAnimationType.Jump, 
                    2f,
                    true,
                    false
                ));
            }
            else if (pet.animator)
            {
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Jump);
                yield return new WaitForSeconds(2f);
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Idle);
            }
            
            // 감정 표현
            if (Random.value < 0.3f)
            {
                pet.ShowEmotion(EmotionType.Love);
            }
            
            yield return new WaitForSeconds(2f);
        }
        
        Debug.Log($"[TreasureFound] {pet.petName}: 축하 점프 종료");
        
        // 보물이 수집된 경우에만 Idle로 전환
        if (droppedTreasureObject == null)
        {
            Debug.Log($"[TreasureFound] {pet.petName}: 보물 수집 완료, 일상으로 복귀");
            isCelebrating = false;
            hasDroppedTreasure = false;
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