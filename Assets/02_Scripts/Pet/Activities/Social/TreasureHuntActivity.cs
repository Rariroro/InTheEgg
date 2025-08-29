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
    private GameObject carriedTreasure;
    private float searchTimer = 0f;
    private const float SEARCH_INTERVAL = 0.5f; // 0.5초마다 새 타겟 검색
    
    // 보물찾기 속도 배율
    private const float SPEED_MULTIPLIER = 3f;
    private const float ANGULAR_SPEED_MULTIPLIER = 3f;
    private const float ACCELERATION_MULTIPLIER = 3f;
    
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
        searchTimer = 0f;
        
        // 속도 설정
        if (agent != null && agent.enabled)
        {
            agent.speed = pet.Movement.walkSpeed * SPEED_MULTIPLIER;
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
            // 목표 지점 도착 체크
            if (!agent.pathPending && agent.remainingDistance <= 2f)
            {
                // 이 지점에 보물이 있는지 확인
                if (targetSpot.HasTreasure && targetSpot.TryOccupy(pet))
                {
                    OnTreasureFound();
                }
                else
                {
                    // 보물이 없거나 다른 펫이 차지했으면 다음 타겟 찾기
                    FindNewTarget();
                }
            }
        }
        else if (targetSpot == null)
        {
            // 타겟이 없으면 새로 찾기
            FindNewTarget();
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
        }
        
        // 가장 가까운 사용 가능한 보물 스팟 찾기
        targetSpot = TreasureHuntManager.Instance.FindNearestAvailableSpot(pet.transform.position);
        
        if (targetSpot != null)
        {
            // 새 타겟으로 이동
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.SetDestination(targetSpot.transform.position);
                agent.isStopped = false;
            }
            
            Debug.Log($"{pet.petName}: 새 보물 타겟 설정 - {targetSpot.name}");
        }
        else
        {
            // 찾을 보물이 없으면 랜덤 위치로 탐색
            WanderRandomly();
        }
    }
    
    /// <summary>
    /// 현재 타겟 유효성 체크
    /// </summary>
    private void CheckCurrentTarget()
    {
        if (targetSpot == null || !targetSpot.HasTreasure || 
            (targetSpot.IsOccupied && !targetSpot.TryOccupy(pet)))
        {
            FindNewTarget();
        }
    }
    
    /// <summary>
    /// 랜덤하게 배회하며 탐색
    /// </summary>
    private void WanderRandomly()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        
        // 랜덤 위치 생성
        Vector3 randomDirection = Random.insideUnitSphere * 10f;
        randomDirection += pet.transform.position;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, 10f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            agent.isStopped = false;
        }
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
            
            // 보물을 펫의 입 위치로 이동 (머리 위로 조정 가능)
            Transform mouthBone = FindMouthBone();
            if (mouthBone != null)
            {
                carriedTreasure.transform.SetParent(mouthBone);
                carriedTreasure.transform.localPosition = Vector3.forward * 0.3f;
                carriedTreasure.transform.localScale = Vector3.one * 0.5f;
            }
            else
            {
                // 입 본이 없으면 펫 위에 띄우기
                carriedTreasure.transform.SetParent(pet.transform);
                carriedTreasure.transform.localPosition = Vector3.up * 1.5f;
                carriedTreasure.transform.localScale = Vector3.one * 0.5f;
            }
        }
        
        // 대기 위치로 이동
        if (targetSpot != null && agent != null && agent.enabled)
        {
            agent.SetDestination(targetSpot.WaitingPosition);
            agent.isStopped = false;
        }
        
        // 기쁨 표현
        pet.ShowEmotion(EmotionType.Happy);
        
        // 점프 애니메이션 시작
        pet.StartCoroutine(CelebrationJump());
        
        Debug.Log($"{pet.petName}: 보물 발견! 대기 위치로 이동 중...");
    }
    
    /// <summary>
    /// 보물을 찾은 후 대기 처리
    /// </summary>
    private void HandleTreasureFound()
    {
        if (agent == null || !agent.enabled) return;
        
        // 대기 위치 도착 체크
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            agent.isStopped = true;
            
            // 카메라 바라보기
            if (Camera.main != null)
            {
                Vector3 directionToCamera = Camera.main.transform.position - pet.transform.position;
                directionToCamera.y = 0;
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                pet.transform.rotation = Quaternion.Slerp(pet.transform.rotation, targetRotation, 
                    pet.Movement.rotationSmoothness * Time.deltaTime);
            }
        }
    }
    
    /// <summary>
    /// 보물 발견 축하 점프
    /// </summary>
    private IEnumerator CelebrationJump()
    {
        while (hasFoundTreasure)
        {
            // 점프 애니메이션
            if (pet.animator)
            {
                pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Jump);
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