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
    
    // 속도 설정
    private const float CARRY_SPEED_MULTIPLIER = 3f;
    private const float ANGULAR_SPEED_MULTIPLIER = 3f;
    private const float ACCELERATION_MULTIPLIER = 3f;
    
    public override string Name => "TreasureFound";
    public override bool IsInterruptible => true; // 항상 중단 가능 (보물찾기 종료 시 즉시 중단되도록)
    
    public TreasureFoundActivity(PetController petController, PetMovementController movementController) : base(petController)
    {
        agent = pet.agent;
        moveController = movementController;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 보물을 찾은 상태이기만 하면 됨 (보물찾기 종료 여부와 무관)
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
        
        // 초기화
        hasDroppedTreasure = false;
        isMovingToWaitingPoint = false;
        isCelebrating = false;
        
        // 보물 정보 가져오기
        FindCarriedTreasure();
        
        if (carriedTreasure != null)
        {
            // 속도 설정
            if (agent != null && agent.enabled)
            {
                agent.speed = pet.Movement.walkSpeed * CARRY_SPEED_MULTIPLIER;
                agent.angularSpeed = pet.Movement.angularSpeed * ANGULAR_SPEED_MULTIPLIER;
                agent.acceleration = pet.Movement.acceleration * ACCELERATION_MULTIPLIER;
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
        // 보물찾기가 종료되면 즉시 중단
        if (!TreasureHuntManager.Instance?.IsTreasureHuntActive ?? true)
        {
            Debug.Log($"[TreasureFound] {pet.petName}: 보물찾기 종료 감지, 축하 중단");
            Stop();
            pet.State.TrySetStatus(PetStatus.Idle);
            
            // AI 재평가
            if (pet.AI != null)
            {
                pet.AI.InterruptAndResetAI();
            }
            return;
        }
        
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
        Debug.Log($"[TreasureFound] {pet.petName}: 보물 운반 활동 종료");
        
        // 축하 코루틴 정리
        if (celebrationCoroutine != null)
        {
            pet.StopCoroutine(celebrationCoroutine);
            celebrationCoroutine = null;
        }
        
        // 속도 원래대로
        if (agent != null && agent.enabled)
        {
            agent.speed = pet.Movement.walkSpeed;
            agent.angularSpeed = pet.Movement.angularSpeed;
            agent.acceleration = pet.Movement.acceleration;
            agent.isStopped = false;
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
        if (targetSpot == null || agent == null || !agent.enabled) return;
        
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
        if (agent == null || !agent.enabled) return;
        
        // 물 속에 있으면 대기
        if (pet.State.IsInWater)
        {
            Debug.Log($"[TreasureFound] {pet.petName}: 물 속에서 대기 중...");
            return;
        }
        
        // 도착 체크
        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
        {
            Debug.Log($"[TreasureFound] {pet.petName}: 대기 위치 도착!");
            agent.isStopped = true;
            isMovingToWaitingPoint = false;
            
            // 보물 내려놓기 시퀀스 시작
            pet.StartCoroutine(DropTreasureSequence());
        }
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
        
        yield return new WaitForSeconds(0.5f);
        
        // 보물 내려놓기 - 즉시 실행
        DropTreasureImmediate();
        
        // 축하 점프 시작
        isCelebrating = true;
        celebrationCoroutine = pet.StartCoroutine(CelebrationJump());
    }
    
    /// <summary>
    /// 보물을 즉시 바닥에 내려놓기 (코루틴 없이)
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
        
        // 보물이 수집될 때까지 또는 보물찾기가 종료될 때까지 점프
        while (droppedTreasureObject != null && 
               (TreasureHuntManager.Instance?.IsTreasureHuntActive ?? false))
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
        
        // 보물찾기가 종료되어 중단된 경우 - 보물 유지
        if (!TreasureHuntManager.Instance?.IsTreasureHuntActive ?? true)
        {
            Debug.Log($"[TreasureFound] {pet.petName}: 보물찾기 종료로 인한 중단, 보물 유지");
            isCelebrating = false;
            // hasDroppedTreasure와 droppedTreasureObject는 유지하여 보물 보호
            pet.State.TrySetStatus(PetStatus.Idle);
        }
        // 보물이 수집된 경우
        else if (droppedTreasureObject == null)
        {
            Debug.Log($"[TreasureFound] {pet.petName}: 보물 수집 완료, 일상으로 복귀");
            isCelebrating = false;
            hasDroppedTreasure = false;
            pet.State.TrySetStatus(PetStatus.Idle);
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