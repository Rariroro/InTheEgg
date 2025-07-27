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
public class PetController : MonoBehaviour
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

    [Range(0, 100)]
    public float affection;
    [Range(0, 100)]
    public float hunger;
    [Range(0, 100)]
    public float sleepiness;

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
    [HideInInspector] public bool isGathered = false;
    [HideInInspector] public bool isGathering = false; // 추가: 모이기 중인지 확인하는 플래그
    [HideInInspector] public int gatherCommandVersion = 0; // 추가: 모으기 명령 버전 추적
    [HideInInspector] public bool isGatheringAnimationOverride = false; // 추가: 모이기 애니메이션 오버라이드 플래그
    // [HideInInspector] public bool isGatheringRotationOverride = false; // ★ 추가: 모이기 방향 오버라이드 플래그

    [HideInInspector] public float baseSpeed;
    [HideInInspector] public float baseAngularSpeed;
    [HideInInspector] public float baseAcceleration;
    [HideInInspector] public float baseStoppingDistance;

    // 각 기능별 컨트롤러 참조
    private PetMovementController movementController;
    private PetAnimationController animationController;
    private PetInteractionController interactionController;
    private PetFeedingController feedingController;
    private PetSleepingController sleepingController; // 추가: 수면 컨트롤러
    private PetWaterBehaviorController waterBehaviorController; // ★ 추가
    private PetTreeClimbingController treeClimbingController;
    // 현재 활성화된 감정 말풍선
    private EmotionBubble activeBubble;
private GameObject activeParticle; // <<< 파티클 오브젝트를 추적하기 위한 변수 추가

 // ▼▼▼ [추가] 졸음 이모티콘 간헐적 표시를 위한 변수 ▼▼▼
    private float _sleepyEmotionTimer = 0f;
    private const float SLEEPY_EMOTION_INTERVAL = 10f; // 10초마다 졸음 표현을 시도합니다.
    private const float SLEEPY_EMOTION_CHANCE = 0.3f;  // 30% 확률로 졸음 이모티콘을 표시합니다.
    // ▲▲▲ [여기까지 추가] ▲▲▲

    [Header("Pet Type")]
    [SerializeField] private PetType petType = PetType.Dog; // 기본값 설정
    [SerializeField] private bool manuallySetPetType = false; // 수동 설정 여부 체크 필드 추가

    // 상호작용 관련 변수
    [HideInInspector] public bool isInteracting = false;
    [HideInInspector] public PetController interactionPartner = null;
    // PetController에 물 상태 플래그 추가
    [HideInInspector] public bool isInWater = false;
    [HideInInspector] public float waterDepthOffset = 0f;
    // PetController.cs에 추가
    [HideInInspector] public bool isClimbingTree = false;
    [HideInInspector] public Transform currentTree = null;
    [HideInInspector] public float climbHeight = 5f; // 나무 올라가는 높이
    [HideInInspector] public bool isSelected = false;
    [HideInInspector] public bool isHolding = false; // 들고 있는 상태 추적
    [HideInInspector] public bool isAnimationLocked = false; // 특별 애니메이션 재생으로 상호작용이 잠겼는지 확인
    [HideInInspector] public bool isActionLocked = false;
    [HideInInspector] public Vector3 gatherTargetPosition;
    [HideInInspector] public BasePetInteraction currentInteractionLogic;

    [HideInInspector] public bool isAttractedToEnvironment = false;
    [HideInInspector] public Vector3 environmentTargetPosition;

    // 벌 공격 관련 상태
    [HideInInspector] public bool isBeingAttackedByBees = false;
    [HideInInspector] public Vector3 beeAttackSource = Vector3.zero;
    [HideInInspector] public float beeAttackStartTime = 0f;

    [Header("Pet Needs Settings")] // 인스펙터에서 편하게 관리하기 위해 헤더 추가
    [Tooltip("초당 배고픔 증가량")]
    [SerializeField] private float hungerIncreaseRate = 0.2f;
    [Tooltip("초당 졸림 증가량")]
    [SerializeField] private float sleepinessIncreaseRate = 0.1f;
    
    [Header("Affection Settings")] // 친밀도 관련 설정
    [Tooltip("배고픔이 이 수치 이상일 때 친밀도가 감소하기 시작합니다")]
    [SerializeField] private float hungerThresholdForAffectionDecrease = 80f;
    [Tooltip("배고플 때 초당 친밀도 감소량 (최대 배고픔 상태에서)")]
    [SerializeField] private float affectionDecreaseRateWhenHungry = 0.5f;
    [Tooltip("친밀도가 이 수치 이하로 떨어지면 Sad 감정을 표현합니다")]
    [SerializeField] private float lowAffectionThreshold = 20f;
    [Tooltip("친밀도가 이 수치 이상이면 Love 감정을 표현합니다")]
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

    [Tooltip("펫이 현재 탈진 상태인지 여부를 나타냅니다.")]
    [HideInInspector] public bool isExhausted = false;
    
    // ★★★ [Phase 1 추가] 새로운 상태 관리 시스템 ★★★
    [Header("State Management (New)")]
    [SerializeField] private PetState petState = new PetState();
    
    /// <summary>
    /// 외부에서 상태를 읽기 위한 프로퍼티
    /// </summary>
    public PetState State => petState;
    
    // ... 다른 변수들 ...
    private float _aiUpdateTimer = 0f;
    private float _aiUpdateInterval = 0.5f; // 1초에 2번만 AI 의사결정을 하도록 설정 (조절 가능)
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
    // 행동 시스템 관련 변수
    private List<IPetAction> _allActions;
    private IPetAction _currentAction;
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
        interactionController = gameObject.AddComponent<PetInteractionController>();
        interactionController.Init(this);
        feedingController = gameObject.AddComponent<PetFeedingController>();
        feedingController.Init(this);
        sleepingController = gameObject.AddComponent<PetSleepingController>();
        sleepingController.Init(this);
        treeClimbingController = gameObject.AddComponent<PetTreeClimbingController>();
        treeClimbingController.Init(this);

        // 행동 리스트 초기화
        InitializeActions();


        // 초기화 순서 변경 - NavMesh 위치 확인 후 컨트롤러 초기화
        StartCoroutine(EnsureNavMeshPlacement());

        // PetInteractionManager에 이 펫 등록
        if (PetInteractionManager.Instance != null)
        {
            // 약간의 지연 후 등록 (매니저가 완전히 초기화된 후)
            StartCoroutine(RegisterToPetManager());
        }
    }
    /// <summary>
    /// 펫이 수행할 수 있는 모든 행동을 생성하고 리스트에 추가합니다.
    /// </summary>
    private void InitializeActions()
    {
        _allActions = new List<IPetAction>
    {
        // === 최우선순위: 긴급 상황 ===
        new BeeEscapeAction(this),               // 벌 공격 도망 [우선순위: 100.0]
        new ExhaustedAction(this),               // 탈진 [우선순위: 50.0]

        // === 최상위 우선순위: 외부 명령 ===
        new GatherAction(this),                  // 모이기 [우선순위: 20.0]

        
                new EnvironmentGatherAction(this),       // 환경 스폰 모이기 [우선순위: 15.0]
        new InteractWithPetAction(this),         // 펫 간 상호작용 [우선순위: 10.0]

        // ★★★ 추가: 플레이어 선택 행동 ★★★
        new SelectedAction(this),                // 플레이어 선택 [우선순위: 5.0]

        // === 중간 우선순위: 긴급한 욕구 ===
        new EatAction(this, feedingController),      // 식사 [우선순위: ~1.0]
        new SleepAction(this, sleepingController),   // 수면 [우선순위: ~1.0]

        // === 낮은 우선순위: 자율 행동 ===
        new ClimbTreeAction(this, treeClimbingController), // 나무 오르기 [우선순위: 0.3]

        // === 최하위 우선순위: 기본 행동 ===
        new WanderAction(this, movementController)   // 배회 [우선순위: 0.1]
    };

        // 기본 행동 설정
        _currentAction = _allActions.Find(a => a is WanderAction);
        _currentAction?.OnEnter();
        Debug.Log($"{petName}의 AI 시스템이 초기화되었습니다. 현재 행동: {_currentAction.GetType().Name}");
    }
    // 기존 Update 메서드를 완전히 대체합니다.
    private void Update()
    {
        // ★ [Phase 1] 상태 동기화 - 기존 플래그와 새 상태 시스템 연동
        SyncStateWithFlags();
        
        // 1. 최우선 순위 처리: 플레이어의 직접적인 조작 (들기)
        // isHolding은 물리적인 상태이므로 최상단에서 제어하는 것이 좋습니다.
        interactionController?.HandleInput();
        
        // ★ [Phase 1] 새로운 상태 체크와 기존 플래그 체크 병행
        if (petState.IsPlayerControlled || isActionLocked) return;
        // ★★★★★ 새로 추가된 부분 ★★★★★
        // 2. 욕구 상태 업데이트 (매 프레임)
        UpdateNeeds();
        // ★★★★★ 여기까지 추가 ★★★★★
        // 2. 환경 상태 업데이트 (매 프레임)
        waterBehaviorController?.CheckWaterArea();

        // 3. AI 의사결정 (주기적으로)
        _aiUpdateTimer += Time.deltaTime;
        if (_aiUpdateTimer >= _aiUpdateInterval)
        {
            UpdateAI();
            _aiUpdateTimer = 0f;
        }

        // 4. 현재 행동 실행 및 시각적 표현 업데이트 (매 프레임)
        _currentAction?.OnUpdate();

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

    /// <summary>
    /// AI의 핵심 의사결정 루프. 주기적으로 호출됩니다.
    /// </summary>
    public void UpdateAI()
    {
        // ★ [Phase 1] 새로운 상태 체크와 기존 플래그 체크 병행
        // PlayerControl 상태나 isActionLocked일 때는 AI 의사결정 중지
        if (petState.CurrentStatus == PetStatus.PlayerControl || isActionLocked) return;
        
        // 기존 플래그도 체크 (호환성)
        if (isHolding) return;

        IPetAction bestAction = null;
        float maxPriority = -1f;
        
        // 디버깅용: 선택된 상태일 때 모든 액션의 우선순위 출력
        if (isSelected && Debug.isDebugBuild)
        {
            Debug.Log($"[UpdateAI] {petName} - 모든 액션 우선순위 체크:");
        }

        foreach (var action in _allActions)
        {
            float currentPriority = action.GetPriority();
            
            // 디버깅용: 선택된 상태일 때 각 액션의 우선순위 출력
            if (isSelected && Debug.isDebugBuild && currentPriority > 0)
            {
                Debug.Log($"  - {action.GetType().Name}: {currentPriority}");
            }
            
            if (currentPriority > maxPriority)
            {
                maxPriority = currentPriority;
                bestAction = action;
            }
        }

        // ★★★ 수정: 행동 전환 로직 개선 ★★★
        if (bestAction != null && bestAction.GetType() != _currentAction?.GetType())
        {
            // ★ [Phase 1] 상태 전환 로깅 추가 (디버깅용)
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[AI] {petName} 행동 전환: {_currentAction?.GetType().Name} -> {bestAction.GetType().Name} " +
                         $"(우선순위: {maxPriority}, 상태: {petState.CurrentStatus})");
            }

            // 1. 이전 행동의 종료 처리
            _currentAction?.OnExit();

            // 2. 현재 행동을 새로운 행동으로 교체
            _currentAction = bestAction;

            // 3. 새로운 행동의 시작 처리
            _currentAction.OnEnter();
        }
    }
    /// <summary>
    /// 펫의 배고픔과 졸림 수치를 시간에 따라 지속적으로 업데이트합니다.
    /// </summary>
    private void UpdateNeeds()
    {
        // 펫이 먹고 있거나, 먹을 것을 찾아가는 중이 아닐 때만 배고픔 증가
        if (feedingController != null && !feedingController.IsEatingOrSeeking())
        {
            hunger = Mathf.Clamp(hunger + hungerIncreaseRate * Time.deltaTime, 0f, 100f);
            
            // 배고픔에 따른 친밀도 감소
            if (hunger >= hungerThresholdForAffectionDecrease) // 설정된 배고픔 임계값 이상일 때
            {
                // 초당 친밀도 감소 (배고픔이 심할수록 더 빠르게 감소)
                float affectionDecreaseRate = affectionDecreaseRateWhenHungry * (hunger / 100f);
                float previousAffection = affection;
                affection = Mathf.Clamp(affection - affectionDecreaseRate * Time.deltaTime, 0f, 100f);
                
                // 친밀도가 설정된 임계값 이하로 떨어지고, 이전에는 그보다 높았다면 Sad 감정 표현
                if (affection <= lowAffectionThreshold && previousAffection > lowAffectionThreshold)
                {
                    ShowEmotion(EmotionType.Sad, 3f);
                    Debug.Log($"[Affection] {petName}의 친밀도가 낮아졌습니다: {affection:F1}");
                }
            }
        }

        // 펫이 자고 있거나, 잠잘 곳을 찾아가는 중이 아닐 때만 졸림 증가
        if (sleepingController != null && !sleepingController.IsSleepingOrSeeking())
        {
            sleepiness = Mathf.Clamp(sleepiness + sleepinessIncreaseRate * Time.deltaTime, 0f, 100f);

            // ▼▼▼ [추가] 졸릴 때 간헐적으로 감정 표현 ▼▼▼
            if (sleepiness >= 70f)
            {
                _sleepyEmotionTimer += Time.deltaTime;
                if (_sleepyEmotionTimer >= SLEEPY_EMOTION_INTERVAL)
                {
                    if (UnityEngine.Random.value < SLEEPY_EMOTION_CHANCE)
                    {
                        ShowEmotion(EmotionType.Sleepy, 2f); // 2초간 '졸림' 표시
                    }
                    _sleepyEmotionTimer = 0f; // 타이머 초기화
                }
            }
            // ▲▲▲ [여기까지 추가] ▲▲▲
        }
    }

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
    /// <summary>
    /// 외부의 강력한 중단(Interrupt)에 의해 현재 진행 중인 행동을 강제로 무효화합니다.
    /// </summary>
    public void InvalidateCurrentAction()
    {
        if (_currentAction != null)
        {
            _currentAction.OnExit();
            _currentAction = null;
        }
    }
   // PetController.cs

    /// <summary>
    /// 플레이어의 직접적인 개입 등으로 AI의 현재 행동을 강제로 중단하고 초기화합니다.
    /// 다음 AI 업데이트 시 가장 적절한 행동을 처음부터 다시 선택하게 됩니다.
    /// </summary>
    public void InterruptAndResetAI()
    {
        if (_currentAction != null)
        {
            // 1. 현재 진행 중인 행동의 OnExit()를 호출하여 상태를 깨끗하게 정리합니다.
            //    (예: EatAction -> CancelFeeding() 호출, WanderAction -> 코루틴 중지 등)
            _currentAction.OnExit();
            // Debug.Log($"[AI Reset] 현재 행동 '{_currentAction.GetType().Name}'이 외부 요인에 의해 중단되었습니다.");
        }

        // 2. 현재 행동을 null로 설정하여 다음 UpdateAI()에서 반드시 새로운 행동을 찾도록 합니다.
        _currentAction = null;

        // 3. 만약을 대비해 모든 컨트롤러의 주요 상태를 한 번 더 초기화할 수 있습니다.
        //    (예: isGathering, isInteracting 플래그 등)
        //    이 부분은 각 Action의 OnExit에서 잘 처리되고 있다면 생략 가능합니다.
        GetComponent<PetAnimationController>()?.ForceStopAllAnimations();
    }
    
    public void BeginInteraction(PetController partner, BasePetInteraction interactionLogic)
    {
        // ★ [Phase 1] 기존 플래그 설정
        isInteracting = true;
        interactionPartner = partner;
        currentInteractionLogic = interactionLogic;
        
        // ★ [Phase 1] 새로운 상태 시스템에도 반영
        // SyncStateWithFlags가 다음 Update에서 자동으로 처리하지만
        // 즉시 반영을 위해 여기서도 호출
        petState.StartInteraction(partner);
        
        // 여기서 UpdateAI()를 강제 호출하지 않습니다.
        // 다음 AI 업데이트 주기 때 자연스럽게 InteractWithPetAction으로 전환될 것입니다.
    }

    public void InterruptCurrentActionFor(InteractionType type)
    {
        if (_currentAction != null)
        {
            Debug.Log($"{petName}의 현재 행동 '{_currentAction.GetType().Name}'이 '{type}'으로 인해 중단됩니다.");
            // isInteracting, isGathering 등의 플래그가 설정되면
            // 다음 UpdateAI에서 자동으로 새 Action으로 전환되므로 OnExit()만 호출해도 충분합니다.
            _currentAction.OnExit();

            // 특정 Action으로 즉시 전환해야 할 경우
            // IPetAction newAction = _allActions.Find(a => a is InteractWithPetAction);
            // _currentAction = newAction;
            // _currentAction.OnEnter();
        }
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
            if (interactionController != null)
                interactionController.Init(this);
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
    
    // 현재 실행 중인 Action 반환 (PetMovementController에서 사용)
    public IPetAction GetCurrentAction()
    {
        return _currentAction;
    }
    
    #region Phase 1 - State System Integration
    
    /// <summary>
    /// 기존 플래그들과 새로운 상태 시스템을 동기화
    /// 점진적 마이그레이션을 위한 임시 메서드
    /// </summary>
    private void SyncStateWithFlags()
    {
        // 플레이어 제어 상태 동기화
        if (isHolding || isSelected)
        {
            if (petState.CurrentStatus != PetStatus.PlayerControl)
            {
                petState.SetPlayerControl(isHolding, isSelected);
            }
            else
            {
                // 상태는 이미 PlayerControl이므로 세부 플래그만 업데이트
                petState.UpdateHoldingState(isHolding);
                petState.UpdateSelectedState(isSelected);
            }
        }
        
        // 긴급 상태 동기화
        else if (isExhausted || isBeingAttackedByBees)
        {
            if (petState.CurrentStatus != PetStatus.Emergency)
            {
                petState.SetEmergencyState(isExhausted, isBeingAttackedByBees);
            }
        }
        
        // 상호작용 상태 동기화
        else if (isInteracting && interactionPartner != null)
        {
            if (petState.CurrentStatus != PetStatus.Interacting)
            {
                petState.StartInteraction(interactionPartner);
            }
        }
        
        // 환경 상호작용 상태 동기화
        else if (isClimbingTree || isInWater)
        {
            if (petState.CurrentStatus != PetStatus.Environmental)
            {
                petState.SetEnvironmentalState(isClimbingTree, isInWater, currentTree);
            }
        }
        
        // 모이기 상태 동기화
        else if (isGathering)
        {
            if (petState.CurrentStatus != PetStatus.Gathering)
            {
                petState.SetGatheringState(gatherTargetPosition);
            }
        }
        
        // 모든 플래그가 false면 Idle 상태로
        else if (petState.CurrentStatus != PetStatus.Idle)
        {
            petState.TrySetStatus(PetStatus.Idle);
        }
    }
    
    /// <summary>
    /// 상태 시스템을 통해 플래그 업데이트 (역방향 동기화)
    /// Phase 2에서 사용될 예정
    /// </summary>
    private void UpdateFlagsFromState()
    {
        // TODO: Phase 2에서 구현
        // 새로운 상태 시스템의 값으로 기존 플래그들을 업데이트
    }
    
    #endregion

}