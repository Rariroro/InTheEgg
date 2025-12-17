using System;
using UnityEngine;

/// <summary>
/// 펫의 현재 상태를 나타내는 열거형
/// 한 번에 하나의 상태만 가질 수 있음
/// </summary>
public enum PetStatus
{
    Idle,                  // 기본 상태 (아무것도 하지 않음)
    PlayerControl,         // 플레이어가 직접 제어 중 (들기, 선택 등)
    Interacting,           // 다른 펫과 상호작용 중
    Environmental,         // 환경과 상호작용 중 (나무 오르기, 물 속 등)
    Emergency,             // 긴급 상태 (탈진, 벌 공격 등)
    GatheringInProgress,   // 모이기 명령 수행 중 (이동 중)
    GatheredWaiting,       // 모인 후 대기 중
    TreasureHunting,       // 보물 찾는 중
    TreasureFound          // 보물 발견 후 대기
}

/// <summary>
/// 펫의 상태를 관리하는 클래스
/// 복잡한 플래그들을 체계적으로 관리
/// </summary>
[Serializable]
public class PetState
{
    [SerializeField] private PetStatus currentStatus = PetStatus.Idle;
    
    // 상태별 세부 정보
    [SerializeField] private bool isHolding;           // PlayerControl 상태의 세부 정보
    [SerializeField] private bool isSelected;          // PlayerControl 상태의 세부 정보
    [SerializeField] private bool isClimbingTree;     // Environmental 상태의 세부 정보
    [SerializeField] private bool isInWater;          // Environmental 상태의 세부 정보
    [SerializeField] private bool isExhausted;        // Emergency 상태의 세부 정보
    [SerializeField] private bool isBeingAttacked;    // Emergency 상태의 세부 정보
    
    // 추가 플래그들
    [SerializeField] private bool isAnimationLocked;   // 특별 애니메이션 재생 중
    [SerializeField] private bool isActionLocked;      // 행동이 잠긴 상태
    [SerializeField] private bool isAttractedToEnvironment; // 환경 오브젝트에 끌림
    
    // 상호작용 관련
    [SerializeField] private PetController interactionPartner;
    [SerializeField] private Transform currentTree;
    [SerializeField] private Vector3 gatherTargetPosition;
    [SerializeField] private Vector3 environmentTargetPosition;
    [SerializeField] private Vector3 beeAttackSource;
    [SerializeField] private float beeAttackStartTime;
    [SerializeField] private BasePetInteraction currentInteractionLogic;
    [SerializeField] private int gatherCommandVersion;
    [SerializeField] private float waterDepthOffset;
    
    // 보물찾기 관련
    [SerializeField] private bool isTreasureHuntActive;
    [SerializeField] private Vector3 treasureTargetPosition;

    // 상호작용 타임아웃 감지용
    private float interactionStartTime;
    private const float INTERACTION_TIMEOUT = 180f; // 180초 (긴 상호작용 허용)

    // PetController 참조 (로그용)
    private PetController ownerPet;

    // 이벤트
    public event Action<PetStatus, PetStatus> OnStatusChanged; // (이전 상태, 새 상태)
    
    /// <summary>
    /// 현재 상태
    /// </summary>
    public PetStatus CurrentStatus => currentStatus;
    
    /// <summary>
    /// 새로운 활동을 시작할 수 있는지 여부
    /// </summary>
    public bool CanChangeActivity => currentStatus == PetStatus.Idle;
    
    /// <summary>
    /// 플레이어가 제어 중인지
    /// </summary>
    public bool IsPlayerControlled => currentStatus == PetStatus.PlayerControl;
    
    /// <summary>
    /// 상호작용 중인지
    /// </summary>
    public bool IsInteracting => currentStatus == PetStatus.Interacting;
    
    /// <summary>
    /// 긴급 상태인지
    /// </summary>
    public bool IsInEmergency => currentStatus == PetStatus.Emergency;
    
    // 세부 상태 접근자
    public bool IsHolding => isHolding;
    public bool IsSelected => isSelected;
    public bool IsClimbingTree => isClimbingTree;
    public bool IsInWater => isInWater;
    public bool IsExhausted => isExhausted;
    public bool IsBeingAttacked => isBeingAttacked;
    public bool IsGathering => currentStatus == PetStatus.GatheringInProgress;
    public bool IsGathered => currentStatus == PetStatus.GatheredWaiting;
    public bool IsAnimationLocked => isAnimationLocked;
    public bool IsActionLocked => isActionLocked;
    public bool IsAttractedToEnvironment => isAttractedToEnvironment;
    
    // 보물찾기 관련 접근자
    public bool IsTreasureHuntActive => isTreasureHuntActive;
    public bool IsTreasureHunting => currentStatus == PetStatus.TreasureHunting;
    public bool HasFoundTreasure => currentStatus == PetStatus.TreasureFound;
    public Vector3 TreasureTargetPosition => treasureTargetPosition;
    
    // 상호작용 관련 접근자
    public PetController InteractionPartner => interactionPartner;
    public Transform CurrentTree => currentTree;
    public Vector3 GatherTargetPosition => gatherTargetPosition;
    public Vector3 EnvironmentTargetPosition => environmentTargetPosition;
    public Vector3 BeeAttackSource => beeAttackSource;
    public float BeeAttackStartTime => beeAttackStartTime;
    public BasePetInteraction CurrentInteractionLogic => currentInteractionLogic;
    public int GatherCommandVersion => gatherCommandVersion;
    public float WaterDepthOffset => waterDepthOffset;
    
    // 추가 프로퍼티들
    public bool IsBeingAttackedByBees => isBeingAttacked; // BeeHazardZone 호환성
    public BasePetInteraction InteractionLogic => currentInteractionLogic; // PetInputController 호환성
    
    /// <summary>
    /// 상태 전환 시도
    /// </summary>
    /// <summary>
    /// PetState 초기화 (PetController 참조 설정)
    /// </summary>
    public void Initialize(PetController pet)
    {
        ownerPet = pet;
    }

    public bool TrySetStatus(PetStatus newStatus)
    {
        if (!CanTransition(currentStatus, newStatus))
        {
            Debug.LogWarning($"[Pet Warning] {currentStatus}에서 {newStatus}로 전환할 수 없습니다.");
            return false;
        }

        var previousStatus = currentStatus;
        currentStatus = newStatus;
        OnStatusChanged?.Invoke(previousStatus, newStatus);

        Debug.Log($"[Pet State] {ownerPet?.petName ?? "Pet"}: {previousStatus} → {newStatus}");
        return true;
    }
    
    /// <summary>
    /// 상태 전환이 가능한지 확인
    /// </summary>
    private bool CanTransition(PetStatus from, PetStatus to)
    {
        // Emergency 상태는 항상 전환 가능 (최우선순위)
        if (to == PetStatus.Emergency) return true;
        
        // Emergency 상태에서는 Idle 또는 PlayerControl로 전환 가능
        if (from == PetStatus.Emergency) return to == PetStatus.Idle || to == PetStatus.PlayerControl;
        
        // PlayerControl은 언제든 전환 가능 (플레이어 우선)
        if (to == PetStatus.PlayerControl) return true;
        
        // Idle로는 언제든 전환 가능 (기본 상태)
        if (to == PetStatus.Idle) return true;
        
        // PlayerControl에서는 Idle, Environmental로 전환 가능
        if (from == PetStatus.PlayerControl) return to == PetStatus.Idle || to == PetStatus.Environmental;
        
        // Interacting 상태는 Idle, Environmental에서 진입 가능
        if (to == PetStatus.Interacting)
        {
            return from == PetStatus.Idle || from == PetStatus.Environmental;
        }
        
        // GatheringInProgress 상태는 Idle, Environmental, Interacting에서 진입 가능
        if (to == PetStatus.GatheringInProgress)
        {
            return from == PetStatus.Idle || from == PetStatus.Environmental || from == PetStatus.Interacting;
        }
        
        // GatheredWaiting 상태는 GatheringInProgress에서만 진입 가능
        if (to == PetStatus.GatheredWaiting)
        {
            return from == PetStatus.GatheringInProgress;
        }
        
        // GatheringInProgress와 GatheredWaiting에서는 Idle로만 전환 가능
        if (from == PetStatus.GatheringInProgress || from == PetStatus.GatheredWaiting)
        {
            return to == PetStatus.Idle;
        }
        
        // TreasureHunting 상태는 Idle에서 진입 가능
        if (to == PetStatus.TreasureHunting)
        {
            return from == PetStatus.Idle || from == PetStatus.Environmental;
        }
        
        // TreasureFound 상태는 TreasureHunting에서만 진입 가능
        if (to == PetStatus.TreasureFound)
        {
            return from == PetStatus.TreasureHunting;
        }
        
        // TreasureHunting과 TreasureFound에서는 Idle로만 전환 가능
        if (from == PetStatus.TreasureHunting || from == PetStatus.TreasureFound)
        {
            return to == PetStatus.Idle || to == PetStatus.PlayerControl;
        }
        
        // Environmental 상태는 Idle, Interacting에서 진입 가능
        if (to == PetStatus.Environmental)
        {
            return from == PetStatus.Idle || from == PetStatus.Interacting;
        }
        
        // 그 외의 경우는 전환 불가
        return false;
    }
    
    /// <summary>
    /// Idle 상태로 강제 전환 (상태 초기화)
    /// </summary>
    public void ForceIdle()
    {
        currentStatus = PetStatus.Idle;
        ClearAllFlags();
        OnStatusChanged?.Invoke(currentStatus, PetStatus.Idle);
    }
    
    /// <summary>
    /// 모든 세부 플래그 초기화
    /// </summary>
    private void ClearAllFlags()
    {
        isHolding = false;
        isSelected = false;
        isClimbingTree = false;
        isInWater = false;
        isExhausted = false;
        isBeingAttacked = false;
        isAnimationLocked = false;
        isActionLocked = false;
        isAttractedToEnvironment = false;
        
        interactionPartner = null;
        currentTree = null;
        currentInteractionLogic = null;
        gatherTargetPosition = Vector3.zero;
        environmentTargetPosition = Vector3.zero;
        beeAttackSource = Vector3.zero;
        beeAttackStartTime = 0f;
        gatherCommandVersion = 0;
        waterDepthOffset = 0f;
    }
    
    #region 세부 상태 설정 메서드
    
    /// <summary>
    /// 플레이어 제어 상태 설정
    /// </summary>
    public void SetPlayerControl(bool holding, bool selected)
    {
        if (TrySetStatus(PetStatus.PlayerControl))
        {
            isHolding = holding;
            isSelected = selected;
        }
    }
    
    /// <summary>
    /// 들기 상태 업데이트
    /// </summary>
    public void UpdateHoldingState(bool holding)
    {
        if (currentStatus == PetStatus.PlayerControl)
        {
            isHolding = holding;
            if (!holding && !isSelected)
            {
                // 들기와 선택 모두 해제되면 Idle로 전환
                TrySetStatus(PetStatus.Idle);
            }
        }
    }
    
    /// <summary>
    /// 선택 상태 업데이트
    /// </summary>
    public void UpdateSelectedState(bool selected)
    {
        if (currentStatus == PetStatus.PlayerControl)
        {
            isSelected = selected;
            if (!selected && !isHolding)
            {
                // 들기와 선택 모두 해제되면 Idle로 전환
                TrySetStatus(PetStatus.Idle);
            }
        }
    }
    
    /// <summary>
    /// 상호작용 시작
    /// </summary>
    public void StartInteraction(PetController partner)
    {
        if (TrySetStatus(PetStatus.Interacting))
        {
            interactionPartner = partner;
            interactionStartTime = Time.time;
        }
    }

    /// <summary>
    /// 상호작용 종료
    /// </summary>
    public void EndInteraction()
    {
        if (currentStatus == PetStatus.Interacting)
        {
            interactionPartner = null;
            currentInteractionLogic = null;
            TrySetStatus(PetStatus.Idle);
        }
    }

    /// <summary>
    /// 상호작용 강제 종료 (상태 검증 없이)
    /// </summary>
    public void ForceEndInteraction()
    {
        if (currentStatus == PetStatus.Interacting)
        {
            string petName = ownerPet != null ? ownerPet.petName : "Unknown";
            string partnerName = interactionPartner != null ? interactionPartner.petName : "Unknown";
            string interactionType = currentInteractionLogic != null ? currentInteractionLogic.InteractionName : "Unknown";

            Debug.LogWarning($"[PetState] {petName} ForceEndInteraction - {petName} & {partnerName}의 {interactionType} 상호작용 강제 종료");

            interactionPartner = null;
            currentInteractionLogic = null;
            currentStatus = PetStatus.Idle; // TrySetStatus 없이 직접 변경
        }
    }

    /// <summary>
    /// 상호작용 타임아웃 체크 (매 프레임 호출 권장)
    /// </summary>
    public void CheckInteractionTimeout()
    {
        if (currentStatus == PetStatus.Interacting)
        {
            if (Time.time - interactionStartTime > INTERACTION_TIMEOUT)
            {
                #if UNITY_EDITOR
                string petName = ownerPet != null ? ownerPet.petName : "Unknown";
                string partnerName = interactionPartner != null ? interactionPartner.petName : "Unknown";
                string interactionType = currentInteractionLogic != null ? currentInteractionLogic.InteractionName : "Unknown";

                Debug.LogWarning($"[PetState] {petName} & {partnerName}의 {interactionType} 상호작용이 {INTERACTION_TIMEOUT}초 경과하여 안전하게 종료합니다. (개발 모드 안전장치)");
                #endif
                ForceEndInteraction();
            }
        }
    }
    
    /// <summary>
    /// 환경 상호작용 설정
    /// </summary>
    public void SetEnvironmentalState(bool climbingTree = false, bool inWater = false, Transform tree = null)
    {
        if (TrySetStatus(PetStatus.Environmental))
        {
            isClimbingTree = climbingTree;
            isInWater = inWater;
            currentTree = tree;
        }
    }
    
    /// <summary>
    /// 긴급 상태 설정
    /// </summary>
    public void SetEmergencyState(bool exhausted = false, bool beingAttacked = false)
    {
        if (TrySetStatus(PetStatus.Emergency))
        {
            isExhausted = exhausted;
            isBeingAttacked = beingAttacked;
        }
    }
    
    /// <summary>
    /// 모이기 상태 설정
    /// </summary>
    public void SetGatheringState(Vector3 targetPosition, int commandVersion = 0)
    {
        if (TrySetStatus(PetStatus.GatheringInProgress))
        {
            gatherTargetPosition = targetPosition;
            gatherCommandVersion = commandVersion;
        }
    }
    
    /// <summary>
    /// 모이기 완료 상태 설정
    /// </summary>
    public void SetGatheredState()
    {
        if (currentStatus == PetStatus.GatheringInProgress)
        {
            TrySetStatus(PetStatus.GatheredWaiting);
        }
    }
    
    /// <summary>
    /// 애니메이션 잠금 상태 설정
    /// </summary>
    public void SetAnimationLocked(bool locked)
    {
        isAnimationLocked = locked;
    }
    
    /// <summary>
    /// 액션 잠금 상태 설정
    /// </summary>
    public void SetActionLocked(bool locked)
    {
        isActionLocked = locked;
    }
    
    /// <summary>
    /// 환경 끌림 상태 설정
    /// </summary>
    public void SetEnvironmentAttraction(bool attracted, Vector3 targetPosition = default)
    {
        isAttractedToEnvironment = attracted;
        if (attracted)
        {
            environmentTargetPosition = targetPosition;
        }
    }
    
    /// <summary>
    /// 벌 공격 상태 설정
    /// </summary>
    public void SetBeeAttackState(bool attacked, Vector3 attackSource = default)
    {
        isBeingAttacked = attacked;
        if (attacked)
        {
            beeAttackSource = attackSource;
            beeAttackStartTime = Time.time;
            SetEmergencyState(beingAttacked: true);
        }
        else
        {
            // 벌 공격이 끝났을 때
            beeAttackSource = attackSource; // Vector3.zero로 설정될 수 있음
            
            // Emergency 상태에서 Idle로 전환 (탈진 상태가 아닌 경우에만)
            if (currentStatus == PetStatus.Emergency && !isExhausted)
            {
                TrySetStatus(PetStatus.Idle);
                Debug.Log($"[Pet] 벌 공격 종료 - Emergency → Idle 전환");
            }
        }
    }
    
    /// <summary>
    /// 물 깊이 오프셋 설정
    /// </summary>
    public void SetWaterDepthOffset(float offset)
    {
        waterDepthOffset = offset;
    }
    
    /// <summary>
    /// 현재 상호작용 로직 설정
    /// </summary>
    public void SetInteractionLogic(BasePetInteraction logic)
    {
        currentInteractionLogic = logic;
    }
    
    /// <summary>
    /// 환경 상태 업데이트 (물에 들어가거나 나올 때)
    /// </summary>
    public void UpdateWaterState(bool inWater)
    {
        if (inWater && !isInWater)
        {
            isInWater = true;
            if (currentStatus == PetStatus.Idle)
            {
                TrySetStatus(PetStatus.Environmental);
            }
        }
        else if (!inWater && isInWater)
        {
            isInWater = false;
            waterDepthOffset = 0f;
            if (currentStatus == PetStatus.Environmental && !isClimbingTree)
            {
                TrySetStatus(PetStatus.Idle);
            }
        }
    }
    
    /// <summary>
    /// 나무 오르기 상태 업데이트
    /// </summary>
    public void UpdateTreeClimbingState(bool climbing, Transform tree = null)
    {
        if (climbing && !isClimbingTree)
        {
            isClimbingTree = true;
            currentTree = tree;
            if (currentStatus == PetStatus.Idle)
            {
                TrySetStatus(PetStatus.Environmental);
            }
        }
        else if (!climbing && isClimbingTree)
        {
            isClimbingTree = false;
            currentTree = null;
            if (currentStatus == PetStatus.Environmental && !isInWater)
            {
                TrySetStatus(PetStatus.Idle);
            }
        }
    }
    
    /// <summary>
    /// 보물찾기 상태 설정
    /// </summary>
    public void SetTreasureHuntingState(bool active)
    {
        isTreasureHuntActive = active;
        if (active)
        {
            // 보물찾기 시작 시 상태를 Idle로 초기화하여 Activity가 시작할 수 있게 함
            if (currentStatus != PetStatus.PlayerControl && currentStatus != PetStatus.Emergency)
            {
                TrySetStatus(PetStatus.Idle);
            }
        }
        else
        {
            // 보물찾기 종료
            isTreasureHuntActive = false;
            treasureTargetPosition = Vector3.zero;
            
            // TreasureHunting 상태만 Idle로 전환 (TreasureFound는 유지)
            if (currentStatus == PetStatus.TreasureHunting)
            {
                TrySetStatus(PetStatus.Idle);
            }
            // TreasureFound 상태는 그대로 유지 - 유저가 보물을 수집할 때까지
        }
    }
    
    /// <summary>
    /// 보물 타겟 설정
    /// </summary>
    public void SetTreasureTarget(Vector3 position)
    {
        treasureTargetPosition = position;
    }
    
    
    #endregion
    
    /// <summary>
    /// 디버그용 상태 정보 문자열
    /// </summary>
    public override string ToString()
    {
        string details = currentStatus.ToString();
        
        switch (currentStatus)
        {
            case PetStatus.PlayerControl:
                details += $" (Holding: {isHolding}, Selected: {isSelected})";
                break;
            case PetStatus.Interacting:
                details += $" (Partner: {(interactionPartner != null ? interactionPartner.petName : "None")})";
                break;
            case PetStatus.Environmental:
                details += $" (Tree: {isClimbingTree}, Water: {isInWater})";
                break;
            case PetStatus.Emergency:
                details += $" (Exhausted: {isExhausted}, Attacked: {isBeingAttacked})";
                break;
        }
        
        return details;
    }
}