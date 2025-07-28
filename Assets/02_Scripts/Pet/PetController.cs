// PetController.cs 수정 버전
// 공통 열거형은 별도 static 클래스에 모아둡니다.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class PetAIProperties
{
    public enum Personality { Shy, Brave, Lazy, Playful }

    // public enum DietType { Carnivore, Herbivore, Omnivore } // << 기존 DietType 주석 처리 또는 삭제

    // ▼▼▼▼▼ [새로운 부분] Flags 열거형으로 식성 재정의 ▼▼▼▼▼
    [Flags] // 여러 값을 가질 수 있도록 Flags 특성 추가
    public enum DietaryFlags
    {
        None = 0, // 아무것도 먹지 않음
        SeedsAndGrains = 1 << 0, // 씨앗 및 곡물 (값: 1)
        FruitsAndVegetables = 1 << 1, // 과일 및 채소 (값: 2)
        Grass = 1 << 2, // 풀(초목) (값: 4)
        Honey = 1 << 3, // 꿀 (값: 8)
        Meat = 1 << 4, // 고기(육류) (값: 16)
        Fish = 1 << 5, // 생선(어류) (값: 32)

        // (선택) 조합 예시
        Omnivore_General = SeedsAndGrains | FruitsAndVegetables | Meat | Fish, // 일반적인 잡식
        Herbivore_General = FruitsAndVegetables | Grass | SeedsAndGrains // 일반적인 초식
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    public enum Habitat { Water, Forest, Field, Fence, Tree }
}

// PetController는 각 기능별 컴포넌트를 초기화하고 업데이트를 관리합니다.
public partial class PetController : MonoBehaviour
{
    [Header("Pet Movement & Animation Settings")]
    public float speed = 3.5f;
    public float angularSpeed = 120f;
    public float acceleration = 8f;
    public float stoppingDistance = 0.5f;
    public float rotationSpeed = 5f;
    public float smoothTime = 0.3f;

    [Header("Pet Properties")]
    public PetAIProperties.Personality personality = PetAIProperties.Personality.Shy;
    // ▼▼▼▼▼ [새로운 부분] 새로운 식성 변수 추가 ▼▼▼▼▼
    [Tooltip("펫이 먹는 음식의 종류를 중복 선택할 수 있습니다.")]
    public PetAIProperties.DietaryFlags diet = PetAIProperties.DietaryFlags.None;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
    public PetAIProperties.Habitat habitat = PetAIProperties.Habitat.Forest;


    // ▼▼▼ [수정된 부분] 이 아래에 새로운 변수를 추가합니다. ▼▼▼
    [Tooltip("서식지가 'Tree'인 펫이 평소에 나무에 오를 확률 (0.0 ~ 1.0 사이 값)")]
    [Range(0f, 1f)]
    public float treeClimbChance = 0.1f; // 기본값 10%
                                         // ▲▲▲ [수정된 부분] 여기까지 추가합니다. ▲▲▲
                                         // ▼▼▼▼▼ [이 부분 추가] 펫마다 다른 물 깊이를 설정하기 위한 변수 ▼▼▼▼▼
    [Tooltip("펫이 물에 잠기는 깊이를 설정합니다. 값이 클수록 더 깊이 잠깁니다.")]
    [Range(0f, 5f)]
    public float waterSinkDepth = 1.0f;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    // 욕구 관련 프로퍼티 - PetNeeds로 완전 위임
    public float affection
    {
        get => petNeeds != null ? petNeeds.Affection : 50f;
        set { if (petNeeds != null) petNeeds.SetAffection(value); }
    }
    public float hunger 
    {
        get => petNeeds != null ? petNeeds.Hunger : 50f;
        set { if (petNeeds != null) petNeeds.SetHunger(value); }
    }
    public float sleepiness
    {
        get => petNeeds != null ? petNeeds.Sleepiness : 30f;
        set { if (petNeeds != null) petNeeds.SetSleepiness(value); }
    }

    [Header("Pet Information")]
    public string petName = "Buddy";
    public DateTime birthday = default;
  // ▼▼▼ [수정] 이 부분을 추가합니다 ▼▼▼
    [Tooltip("감정 표현(이모티콘, 파티클)이 생성될 기준 위치입니다. 비워두면 자식 중 'EmotionOrigin'을 자동으로 찾습니다.")]
    public Transform emotionOrigin;
    // ▲▲▲ 여기까지 추가 ▲▲▲
    // 공용 컴포넌트
    [HideInInspector] public NavMeshAgent agent;
    [HideInInspector] public Animator animator;
    [HideInInspector] public Transform petModelTransform;
    public bool isGathered => petState.IsGathered;
    public bool isGathering => petState.IsGathering;
    public int gatherCommandVersion => petState.GatherCommandVersion;
    [HideInInspector] public bool isGatheringAnimationOverride = false; // 이것만 별도 유지 (애니메이션 전용)

    [HideInInspector] public float baseSpeed;
    [HideInInspector] public float baseAngularSpeed;
    [HideInInspector] public float baseAcceleration;
    [HideInInspector] public float baseStoppingDistance;

    // 각 기능별 컨트롤러 참조
    private PetMovementController movementController;
    private PetAnimationController animationController;
    private PetInputController inputController;
    public PetFeedingController feedingController;
    public PetSleepingController sleepingController; // 추가: 수면 컨트롤러
    private PetWaterBehaviorController waterBehaviorController; // ★ 추가
    private PetTreeClimbingController treeClimbingController;
    
    // AI 시스템
    private PetAI petAI;
    
    // 현재 활성화된 감정 말풍선
    private EmotionBubble activeBubble;
private GameObject activeParticle; // <<< 파티클 오브젝트를 추적하기 위한 변수 추가

    // 졸음 이모티콘 표시 - PetNeeds로 이동됨

    [Header("Pet Type")]
    [SerializeField] private PetType petType = PetType.Dog; // 기본값 설정
    [SerializeField] private bool manuallySetPetType = false; // 수동 설정 여부 체크 필드 추가

    // ★ [Phase 4] 호환성을 위한 프로퍼티 (기존 플래그를 PetState로 리다이렉트)
    public bool isInteracting => petState.IsInteracting;
    public PetController interactionPartner => petState.InteractionPartner;
    public bool isInWater => petState.IsInWater;
    public float waterDepthOffset 
    { 
        get => petState.WaterDepthOffset;
        set => petState.SetWaterDepthOffset(value);
    }
    public bool isClimbingTree => petState.IsClimbingTree;
    public Transform currentTree => petState.CurrentTree;
    [HideInInspector] public float climbHeight = 5f; // 나무 올라가는 높이 (이것만 유지)
    public bool isSelected => petState.IsSelected;
    public bool isHolding => petState.IsHolding;
    public bool isAnimationLocked => petState.IsAnimationLocked;
    public bool isActionLocked => petState.IsActionLocked;
    public Vector3 gatherTargetPosition => petState.GatherTargetPosition;
    public BasePetInteraction currentInteractionLogic => petState.CurrentInteractionLogic;
    public bool isAttractedToEnvironment => petState.IsAttractedToEnvironment;
    public Vector3 environmentTargetPosition => petState.EnvironmentTargetPosition;
    public bool isBeingAttackedByBees => petState.IsBeingAttacked;
    public Vector3 beeAttackSource => petState.BeeAttackSource;
    public float beeAttackStartTime => petState.BeeAttackStartTime;

    // Pet Needs Settings - PetNeeds로 이동됨
    public float hungerIncreaseRate
    {
        get => petNeeds != null ? petNeeds.HungerIncreaseRate : 0.2f;
        set { if (petNeeds != null) petNeeds.HungerIncreaseRate = value; }
    }
    public float sleepinessIncreaseRate
    {
        get => petNeeds != null ? petNeeds.SleepinessIncreaseRate : 0.1f;
        set { if (petNeeds != null) petNeeds.SleepinessIncreaseRate = value; }
    }
    
    // Affection Settings - PetNeeds로 이동됨
    public float hungerThresholdForAffectionDecrease
    {
        get => petNeeds != null ? petNeeds.HungerThresholdForAffectionDecrease : 80f;
        set { if (petNeeds != null) petNeeds.HungerThresholdForAffectionDecrease = value; }
    }
    public float affectionDecreaseRateWhenHungry
    {
        get => petNeeds != null ? petNeeds.AffectionDecreaseRateWhenHungry : 0.5f;
        set { if (petNeeds != null) petNeeds.AffectionDecreaseRateWhenHungry = value; }
    }
    public float lowAffectionThreshold
    {
        get => petNeeds != null ? petNeeds.LowAffectionThreshold : 20f;
        set { if (petNeeds != null) petNeeds.LowAffectionThreshold = value; }
    }
    [SerializeField] private float highAffectionThreshold = 80f;
    
    [Header("Food Affection Settings")] // 음식 관련 친밀도 설정
    [Tooltip("드롭된 음식 아이템을 먹었을 때 친밀도 증가 최소값")]
    [SerializeField] private float droppedFoodAffectionMin = 5f;
    [Tooltip("드롭된 음식 아이템을 먹었을 때 친밀도 증가 최대값")]
    [SerializeField] private float droppedFoodAffectionMax = 10f;
    [Tooltip("환경 음식(FeedingArea)을 먹었을 때 친밀도 증가 최소값")]
    [SerializeField] private float environmentFoodAffectionMin = 3f;
    [Tooltip("환경 음식(FeedingArea)을 먹었을 때 친밀도 증가 최대값")]
    [SerializeField] private float environmentFoodAffectionMax = 7f;

    public bool isExhausted => petState.IsExhausted;
    
    // ★★★ [Phase 4] 통합된 상태 관리 시스템 ★★★
    [Header("State Management")]
    [SerializeField] private PetState petState = new PetState();
    
    [Header("Needs Management")]
    private PetNeeds petNeeds;
    
    /// <summary>
    /// 외부에서 상태를 읽기 위한 프로퍼티
    /// </summary>
    public PetState State => petState;
    
    /// <summary>
    /// 외부에서 욕구를 읽기 위한 프로퍼티
    /// </summary>
    public PetNeeds Needs => petNeeds;
    
    /// <summary>
    /// 외부에서 AI 시스템에 접근하기 위한 프로퍼티
    /// </summary>
    public PetAI AI => petAI;
    
    // ... 다른 변수들 ...
    // 펫 타입 프로퍼티 - 외부에서 접근 가능하도록
    public PetType PetType
    {
        get { return petType; }
        set
        {
            petType = value;
            manuallySetPetType = true; // 값이 설정되면 수동 설정됨으로 표시
        }
    }
    // PetController.cs의 Awake() 메서드에서 NavMeshAgent 초기화 부분 수정
    private void Awake()
    {
        birthday = DateTime.Now;
  // ▼▼▼ [수정] 이 부분을 Awake() 메서드 상단에 추가합니다. ▼▼▼

        // 1. EmotionOrigin 자동 탐색
        // 인스펙터에서 수동으로 emotionOrigin을 할당하지 않은 경우에만 자동 탐색을 시도합니다.
        if (emotionOrigin == null)
        {
            // petModelTransform이 있으면 그 자식들 안에서 먼저 찾고, 없으면 전체 자식에서 찾습니다.
            Transform rootToSearch = petModelTransform != null ? petModelTransform : transform;
            emotionOrigin = FindDeepChild(rootToSearch, "EmotionOrigin");

            if (emotionOrigin != null)
            {
                // Debug.Log($"{petName}: 자식 오브젝트에서 'EmotionOrigin'을 자동으로 찾아 할당했습니다.");
            }
        }
        // ▲▲▲ 여기까지 추가 ▲▲▲
        // NavMeshAgent 초기화
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = speed;
            agent.angularSpeed = angularSpeed;
            agent.acceleration = acceleration;
            agent.stoppingDistance = stoppingDistance;

            // ★ 추가 설정 - 회전 관련 설정 개선
            agent.updateRotation = false;  // 펫 모델의 회전은 직접 제어
            agent.updatePosition = true;   // 위치는 NavMeshAgent가 제어
            agent.updateUpAxis = false;    // Y축 회전만 필요

            // 기본 값 저장
            baseSpeed = speed;
            baseAngularSpeed = angularSpeed;
            baseAcceleration = acceleration;
            baseStoppingDistance = stoppingDistance;
        }
        
        // Rigidbody 확인 및 추가 (Trigger 충돌 감지를 위해 필요)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Debug.Log($"[PetController] {petName}에 Rigidbody 자동 추가됨 (Trigger 충돌 감지용)");
        }

        // // petModelTransform: 첫 번째 자식을 우선 사용, 없으면 Renderer가 있는 오브젝트 사용
        // if (transform.childCount > 0)
        // {
        //     petModelTransform = transform.GetChild(0);
        // }
        // if (petModelTransform == null)
        // {
        //     Renderer renderer = GetComponentInChildren<Renderer>();
        //     if (renderer != null)
        //         petModelTransform = renderer.transform;
        // }
        // if (petModelTransform == null)
        // {
        //     // Debug.LogWarning("Pet model not found. The pet may not display correctly.");
        // }

        // // Animator 컴포넌트 획득
        // if (petModelTransform != null)
        // {
        //     animator = petModelTransform.GetComponent<Animator>();
        //     if (animator == null)
        //     {
        //         // Debug.LogWarning("Animator component not found on the pet model.");
        //     }
        // }
   // [새로운 안정적인 코드]
        // 1. 자식 오브젝트 중에서 Animator 컴포넌트를 직접 찾습니다.
        animator = GetComponentInChildren<Animator>();

        // 2. Animator를 성공적으로 찾았다면,
        if (animator != null)
        {
            // Animator가 붙어있는 트랜스폼을 petModelTransform으로 확정합니다.
            petModelTransform = animator.transform;
        }
        // 3. Animator를 찾지 못했을 경우에 대한 예외 처리
        else
        {
            Debug.LogWarning($"[PetController] {this.gameObject.name}에서 Animator 컴포넌트를 찾을 수 없습니다! 애니메이션이 작동하지 않습니다.");
            // Animator가 없다면, 모델이라도 찾으려는 시도를 할 수 있습니다. (선택사항)
            if (transform.childCount > 0)
            {
                petModelTransform = transform.GetChild(0);
            }
        }
        // 인스펙터에서 수동으로 설정하지 않았을 경우에만 자동 감지 실행
        if (!manuallySetPetType)
        {
            SetPetTypeFromName();
        }

        // ★ waterBehaviorController를 movementController보다 먼저 초기화
        waterBehaviorController = gameObject.AddComponent<PetWaterBehaviorController>();
        waterBehaviorController.Init(this);
        movementController = gameObject.AddComponent<PetMovementController>();
        movementController.Init(this);
        animationController = gameObject.AddComponent<PetAnimationController>();
        animationController.Init(this);
        inputController = gameObject.AddComponent<PetInputController>();
        inputController.Init(this);
        feedingController = gameObject.AddComponent<PetFeedingController>();
        feedingController.Init(this);
        sleepingController = gameObject.AddComponent<PetSleepingController>();
        sleepingController.Init(this);
        treeClimbingController = gameObject.AddComponent<PetTreeClimbingController>();
        treeClimbingController.Init(this);

        
        // ★ [Phase 3] PetAI 초기화 (Activity 시스템 포함)
        petAI = gameObject.AddComponent<PetAI>();
        petAI.Init(this);
        
        // ★ [Phase 4] 욕구 시스템 초기화 - 자체 Update 처리
        petNeeds = gameObject.AddComponent<PetNeeds>();
        petNeeds.Init(this);
        
        // 욕구 변화 이벤트 구독
        petNeeds.OnEmotionRequired += (emotionType) => ShowEmotion(emotionType, 3f);


        // 초기화 순서 변경 - NavMesh 위치 확인 후 컨트롤러 초기화
        StartCoroutine(EnsureNavMeshPlacement());

        // PetInteractionManager에 이 펫 등록
        if (PetInteractionManager.Instance != null)
        {
            // 약간의 지연 후 등록 (매니저가 완전히 초기화된 후)
            StartCoroutine(RegisterToPetManager());
        }
    }
    // 기존 Update 메서드를 완전히 대체합니다.
    private void Update()
    {
        
        // 1. 최우선 순위 처리: 플레이어의 직접적인 조작 (들기)
        // isHolding은 물리적인 상태이므로 최상단에서 제어하는 것이 좋습니다.
        inputController?.HandleInput();
        
        // ★ [Phase 1] 새로운 상태 체크와 기존 플래그 체크 병행
        // 선택된 상태는 PlayerControl이지만 AI 업데이트와 Action 실행이 필요함
        if ((petState.IsPlayerControlled && !isSelected) || isActionLocked) return;
        // ★ [Phase 4] PetNeeds가 자체적으로 Update 처리함
        // 2. 환경 상태 업데이트 (매 프레임)
        waterBehaviorController?.CheckWaterArea();

        // 3. AI 의사결정 (PetAI에서 처리)
        // PetAI가 자체적으로 Update에서 처리함
        
        // 4. 현재 행동 실행 및 시각적 표현 업데이트 (매 프레임)
        // PetAI가 현재 활동을 업데이트함

        // isGatheringAnimationOverride와 같은 복잡한 플래그 대신
        // 각 Action의 OnUpdate에서 애니메이션을 직접 제어하는 것이 더 좋습니다.
        // 하지만 현재 구조를 유지한다면 그대로 둬도 괜찮습니다.
        if (!isGatheringAnimationOverride)
        {
            animationController?.UpdateAnimation();
        }
        // HandleRotation();

        // if (petModelTransform != null)
        // {
        //     Vector3 targetLocalPos = new Vector3(0, waterDepthOffset, 0);
        //     petModelTransform.localPosition = Vector3.Lerp(petModelTransform.localPosition, targetLocalPos, Time.deltaTime * 5f);
        // }
    }
    
    // ★ [Phase 4] UpdateNeeds와 관련 이벤트 핸들러 제거 - PetNeeds가 자체 처리
    
    // ▼▼▼ [수정] 이 헬퍼 메서드를 PetController 클래스 내부에 추가합니다. ▼▼▼
    /// <summary>
    /// 지정된 부모 Transform 아래에서 특정 이름을 가진 자식을 재귀적으로 탐색하여 반환합니다.
    /// </summary>
    /// <param name="parent">검색을 시작할 부모 Transform</param>
    /// <param name="childName">찾고자 하는 자식의 이름</param>
    /// <returns>찾은 자식의 Transform. 없으면 null을 반환합니다.</returns>
    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }
            
            Transform result = FindDeepChild(child, childName);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }
    // ▲▲▲ 여기까지 추가 ▲▲▲
    
    public void BeginInteraction(PetController partner, BasePetInteraction interactionLogic)
    {
        // ★ [Phase 4] PetState를 통한 상태 설정
        petState.StartInteraction(partner);
        petState.SetInteractionLogic(interactionLogic);
    }

    public void InterruptCurrentActionFor(InteractionType type)
    {
        // PetAI의 Activity 시스템에서 처리
        if (petAI != null)
        {
            petAI.InterruptAndResetAI();
        }
        Debug.Log($"{petName}의 현재 활동이 '{type}'으로 인해 중단됩니다.");
    }
    // ★ 물 속도 조정을 위한 public 메소드 추가
    public void AdjustSpeedForWater()
    {
        if (waterBehaviorController != null)
        {
            waterBehaviorController.AdjustSpeedForWater();
        }
    }
    // [수정 2] 아래 메서드를 클래스 내부에 새로 추가합니다.
    /// <summary>
    /// 펫의 회전을 중앙에서 관리합니다.
    /// NavMeshAgent의 이동 방향(velocity)에 맞춰 펫을 부드럽게 회전시킵니다.
    /// </summary>
    public void HandleRotation()
    {
        // 선택된 상태에서는 자동 회전하지 않음
        if (isGathered  || isSelected)
        {
            return;
        }

        // ★★★ 수정: 상호작용 중이면서 NavMeshAgent가 자동 회전을 담당하는 경우 ★★★
        if (isInteracting && agent != null && agent.enabled && agent.updateRotation)
        {
            // NavMeshAgent가 회전을 담당하므로 여기서는 처리하지 않음
            return;
        }
        if (isInteracting)
        {
            // NavMeshAgent가 실제로 이동 중이라면 회전 허용
            if (agent != null && agent.enabled && agent.isOnNavMesh &&
                agent.velocity.magnitude > 0.1f && !agent.isStopped)
            {
                // 이동 중이므로 회전 허용 (아래 로직 계속 진행)
            }
            else
            {
                // 이동하지 않는 상호작용이면 회전하지 않음
                return;
            }
        }
        // ★ NavMeshAgent 상태 체크 추가
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }
        // NavMeshAgent가 멈춰있거나, 경로가 없으면 회전하지 않습니다.
        if (agent.isStopped || !agent.hasPath || agent.remainingDistance < 0.1f)
        {
            return;
        }

       // ★★★ 수정: NavMeshAgent가 자동 회전을 담당하지 않을 때만 수동으로 회전 ★★★
    if (!agent.updateRotation)
    {
        Vector3 moveDirection = agent.velocity.normalized;

        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            if (petModelTransform != null)
            {
                petModelTransform.rotation = transform.rotation;
            }
        }
    }
    }
    // PetController.cs에 추가
    public void SetRandomDestination()
    {
        movementController?.SetRandomDestination();
    }
    private IEnumerator RegisterToPetManager()
    {
        // 프레임 하나 대기
        yield return null;

        if (PetInteractionManager.Instance != null)
        {
            PetInteractionManager.Instance.RegisterPet(this);
        }
    }

    private void OnDestroy()
    {
        // PetInteractionManager에서 이 펫 제거
        if (PetInteractionManager.Instance != null)
        {
            PetInteractionManager.Instance.UnregisterPet(this);
        }
    }
    
    // ★ [Phase 4] OnValidate 제거 - PetNeeds가 직접 관리

    private IEnumerator EnsureNavMeshPlacement()
    {
        yield return new WaitForSeconds(0.2f);

        // NavMeshAgent가 존재하는지 확인
        if (agent == null)
        {
            Debug.LogWarning($"[PetController] {petName}: NavMeshAgent가 없습니다.");
            yield break;
        }

        // NavMesh에 없는 경우 배치 시도
        if (!agent.isOnNavMesh)
        {
            // Debug.Log($"[PetController] {petName}: NavMesh 위에 배치 시도 중...");

            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
            {
                // Agent를 일시적으로 비활성화하고 위치 조정
                bool wasEnabled = agent.enabled;
                agent.enabled = false;
                transform.position = hit.position;
                yield return new WaitForSeconds(0.1f);

                // Agent 다시 활성화
                agent.enabled = wasEnabled;
                yield return new WaitForSeconds(0.1f);

                // Debug.Log($"[PetController] {petName}: NavMesh 위치로 이동 완료 - {hit.position}");
            }
            else
            {
                Debug.LogWarning($"[PetController] {petName}: 적절한 NavMesh 위치를 찾을 수 없습니다.");
            }
        }

        // NavMeshAgent가 활성화되고 NavMesh 위에 있는지 최종 확인
        if (agent.enabled && agent.isOnNavMesh)
        {
            // Debug.Log($"[PetController] {petName}: NavMeshAgent 준비 완료");

            // 이제 안전하게 컨트롤러들을 초기화
            if (movementController != null)
                movementController.Init(this);
            if (animationController != null)
                animationController.Init(this);
            if (inputController != null)
                inputController.Init(this);
            if (feedingController != null)
                feedingController.Init(this);
        }
        else
        {
            Debug.LogError($"[PetController] {petName}: NavMeshAgent 초기화 실패. 컨트롤러들을 초기화하지 않습니다.");
        }
    }

    // 펫 이름에서 타입 유추하는 메서드 (개선된 버전)
    private void SetPetTypeFromName()
    {
        string name = gameObject.name.ToLower();
        // Debug.Log($"[PetController] 펫 이름에서 타입 유추 시작: {name}");

        bool typeFound = false;
        foreach (PetType type in Enum.GetValues(typeof(PetType)))
        {
            string typeName = type.ToString().ToLower();
            if (name.Contains(typeName))
            {
                petType = type;
                typeFound = true;
                // Debug.Log($"[PetController] 펫 타입 감지됨: {petType} (이름에서 '{typeName}' 문자열 발견)");
                break;
            }
        }

        if (!typeFound)
        {
            // 추가 이름 매핑 로직 (수동 매핑)
            if (name.Contains("lion")) petType = PetType.Lion;
            else if (name.Contains("tiger")) petType = PetType.Tiger;
            else if (name.Contains("turtle")) petType = PetType.Turtle;
            else if (name.Contains("rabbit")) petType = PetType.Rabbit;
            else if (name.Contains("cat")) petType = PetType.Cat;
            else if (name.Contains("dog")) petType = PetType.Dog;
            else
            {
                Debug.LogWarning($"[PetController] 펫 이름 '{name}'에서 타입을 감지할 수 없습니다. 기본값 {petType}을(를) 사용합니다.");
            }
        }
    }


    // ★ 외부에서 이동을 제어하기 위한 메서드들 개선
    public void StopMovement()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            try
            {
                agent.isStopped = true;
                agent.ResetPath();  // ★ 추가: 경로 완전히 초기화
                agent.velocity = Vector3.zero;  // ★ 추가: 속도도 0으로
                agent.updateRotation = false;  // ★ 추가: 자동 회전 중지
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PetController] {petName}: StopMovement 실패 - {e.Message}");
            }
        }
    }

    public void ResumeMovement()
    {
        if (isGathering) return;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            try
            {
                agent.updateRotation = true;  // ★ 추가: 자동 회전 재개
                agent.isStopped = false;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PetController] {petName}: ResumeMovement 실패 - {e.Message}");
            }
        }
    }

   // ▼▼▼ ShowEmotion 메서드를 아래와 같이 수정합니다. ▼▼▼
public void ShowEmotion(EmotionType emotion, float duration = 10f)
{
    // 기존에 표시되던 감정 표현(말풍선 또는 파티클)을 먼저 제거합니다.
    HideEmotion();

    if (EmotionManager.Instance != null)
    {
        // EmotionManager로부터 생성된 오브젝트(말풍선 또는 파티클)를 받습니다.
        GameObject emotionObject = EmotionManager.Instance.ShowPetEmotion(this, emotion, duration);

        if (emotionObject != null)
        {
            // 반환된 오브젝트가 EmotionBubble 타입인지 확인하고, activeBubble에 할당합니다.
            if (emotionObject.TryGetComponent<EmotionBubble>(out EmotionBubble bubble))
            {
                activeBubble = bubble;
            }
            // 파티클인 경우 activeParticle에 할당합니다.
            else
            {
                activeParticle = emotionObject;
            }
        }
    }
}

// ▼▼▼ HideEmotion 메서드를 아래와 같이 수정합니다. ▼▼▼
public void HideEmotion()
{
    // 활성화된 말풍선이 있다면 풀에 반환합니다.
    if (activeBubble != null)
    {
        EmotionManager.Instance.ReturnBubbleToPool(activeBubble);
        activeBubble = null;
    }
    // 활성화된 파티클이 있다면 파괴합니다.
    if (activeParticle != null)
    {
        Destroy(activeParticle);
        activeParticle = null;
    }
}

    // 친밀도 임계값 getter
    public float GetHighAffectionThreshold()
    {
        return highAffectionThreshold;
    }
    
    // 음식 관련 친밀도 설정 getter
    public float GetDroppedFoodAffectionMin() { return droppedFoodAffectionMin; }
    public float GetDroppedFoodAffectionMax() { return droppedFoodAffectionMax; }
    public float GetEnvironmentFoodAffectionMin() { return environmentFoodAffectionMin; }
    public float GetEnvironmentFoodAffectionMax() { return environmentFoodAffectionMax; }
    

}