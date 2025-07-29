// PetController.cs 수정 버전
// 공통 열거형은 별도 static 클래스에 모아둡니다.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// PetController는 각 기능별 컴포넌트를 초기화하고 업데이트를 관리합니다.
public partial class PetController : MonoBehaviour
{
    [Header("Pet Settings")]
    [SerializeField] private PetProfile profile = new PetProfile();
    [SerializeField] private MovementSettings movement = new MovementSettings();
    
    // 공개 프로퍼티로 외부 접근 허용
    public PetProfile Profile => profile;
    public MovementSettings Movement => movement;
    
    // === 기존 코드와의 호환성을 위한 프로퍼티들 ===
    // 자주 사용되는 것들만 유지
    public string petName 
    { 
        get => profile.name; 
        set => profile.name = value;
    }
    public PetTraits.Personality personality => profile.personality;
    public PetTraits.DietaryFlags diet => profile.diet;
    public PetTraits.Habitat habitat => profile.habitat;
    public float treeClimbChance => profile.treeClimbChance;
    public float waterSinkDepth => profile.waterSinkDepth;
    // 공용 컴포넌트
    [HideInInspector] public NavMeshAgent agent;
    [HideInInspector] public Animator animator;
    [HideInInspector] public Transform petModelTransform;
    [HideInInspector] public bool isGatheringAnimationOverride = false; // 이것만 별도 유지 (애니메이션 전용)

    // baseSpeed 등은 Movement 프로퍼티를 통해 접근
    public float baseSpeed => movement.walkSpeed;
    public float baseAngularSpeed => movement.angularSpeed;
    public float baseAcceleration => movement.acceleration;
    public float baseStoppingDistance => movement.stoppingDistance;

    // 각 기능별 컨트롤러 참조
    private PetMovementController movementController;
    private PetAnimationController animationController;
    private PetInputController inputController;
    public PetFeedingController feedingController;
    public PetSleepingController sleepingController; // 추가: 수면 컨트롤러
    private PetWaterBehaviorController waterBehaviorController; // ★ 추가
    private PetTreeClimbingController treeClimbingController;
    private PetEmotionController emotionController; // 감정 표현 컨트롤러
    
    // AI 시스템
    private PetAI petAI;
    

    // 졸음 이모티콘 표시 - PetNeeds로 이동됨

    [Header("Pet Type")]
    [SerializeField] private PetType petType = PetType.Dog; // 기본값 설정
    [SerializeField] private bool manuallySetPetType = false; // 수동 설정 여부 체크 필드 추가

    // ★ 필수 프로퍼티들만 유지 (나머지는 State 프로퍼티로 직접 접근)
    public bool isExhausted => petState.IsExhausted;
    public bool isGathered => petState.IsGathered;
    public bool isAnimationLocked => petState.IsAnimationLocked;
    public PetController interactionPartner => petState.InteractionPartner;
    public Transform currentTree => petState.CurrentTree;
    
    [HideInInspector] public float climbHeight = 5f;

    // 욕구 설정은 Needs 프로퍼티를 통해 직접 접근
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
        // 새로운 구조 초기화
        if (profile == null) profile = new PetProfile();
        if (movement == null) movement = new MovementSettings();
        
        // 프로필 초기화
        profile.birthday = DateTime.Now;
        // NavMeshAgent 초기화
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = movement.walkSpeed;
            agent.angularSpeed = movement.angularSpeed;
            agent.acceleration = movement.acceleration;
            agent.stoppingDistance = movement.stoppingDistance;

            // ★ 수정: 회전 제어 방식 통일
            // 기본적으로 NavMeshAgent가 회전을 제어하도록 설정
            // 특별한 경우(선택, 상호작용 등)에만 수동 제어
            agent.updateRotation = true;   // NavMeshAgent가 회전 제어
            agent.updatePosition = true;   // 위치는 NavMeshAgent가 제어
            agent.updateUpAxis = false;    // Y축 회전만 필요
        }
        
        // Rigidbody 확인 및 추가 (Trigger 충돌 감지를 위해 필요)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            PetDebug.LogDebug($"{petName}에 Rigidbody 자동 추가됨 (Trigger 충돌 감지용)", this);
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
        // Animator와 모델 Transform 찾기
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            petModelTransform = animator.transform;
        }
        else
        {
            PetDebug.LogWarning($"{this.gameObject.name}에서 Animator 컴포넌트를 찾을 수 없습니다! 애니메이션이 작동하지 않습니다.", this);
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

        // 컨트롤러들 초기화 - 의존성 순서대로
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
        emotionController = gameObject.AddComponent<PetEmotionController>();
        emotionController.Initialize(petModelTransform);

        
        // ★ [Phase 3] PetAI 초기화 (Activity 시스템 포함)
        petAI = gameObject.AddComponent<PetAI>();
        petAI.Init(this);
        
        // ★ [Phase 4] 욕구 시스템 초기화 - 자체 Update 처리
        petNeeds = gameObject.AddComponent<PetNeeds>();
        petNeeds.Init(this);
        
        // 욕구 변화 이벤트 구독
        petNeeds.OnEmotionRequired += OnEmotionRequired;
        petNeeds.OnNeedCritical += OnNeedCritical;


        // 상태 변경 이벤트 구독
        petState.OnStatusChanged += OnPetStatusChanged;
        
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
        if ((petState.IsPlayerControlled && !petState.IsSelected) || petState.IsActionLocked) return;
        // 2. 환경 상태 업데이트 (매 프레임)
        if (waterBehaviorController != null) waterBehaviorController.CheckWaterArea();

        // 3. 애니메이션 업데이트 (특별한 애니메이션이 재생 중이 아닐 때만)
        if (!isGatheringAnimationOverride && animationController != null)
        {
            animationController.UpdateAnimation();
        }
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
    
    
    // ★ [Phase 1] 상호작용 시작 - PetState 메서드 직접 사용 권장
    // 호환성을 위해 유지하되, 향후 제거 예정
    [System.Obsolete("Use State.StartInteraction() directly")]
    public void BeginInteraction(PetController partner, BasePetInteraction interactionLogic)
    {
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
        PetDebug.Log($"{petName}의 현재 활동이 '{type}'으로 인해 중단됩니다.", this);
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
    /// 특별한 상태(선택, 모이기 등)에서만 수동 회전을 처리합니다.
    /// </summary>
    public void HandleRotation()
    {
        // NavMeshAgent가 없거나 비활성화된 경우 처리하지 않음
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        // 선택된 상태나 모인 상태에서는 수동 회전 제어
        if (petState.IsSelected || petState.IsGathered)
        {
            // NavMeshAgent의 자동 회전 비활성화
            if (agent.updateRotation)
            {
                agent.updateRotation = false;
            }
            
            // 선택된 상태에서는 회전하지 않음 (플레이어가 제어)
            return;
        }
        
        // 상호작용 중이지만 이동하지 않는 경우
        if (petState.IsInteracting && agent.velocity.magnitude < 0.1f)
        {
            // NavMeshAgent의 자동 회전 비활성화
            if (agent.updateRotation)
            {
                agent.updateRotation = false;
            }
            return;
        }
        
        // 일반적인 이동 상태에서는 NavMeshAgent가 자동으로 회전 처리
        if (!agent.updateRotation)
        {
            agent.updateRotation = true;
        }
        
        // 펫 모델이 본체와 동기화되도록 보장
        if (petModelTransform != null && petModelTransform.rotation != transform.rotation)
        {
            petModelTransform.rotation = transform.rotation;
        }
    }
    // PetController.cs에 추가
    public void SetRandomDestination()
    {
        if (movementController != null) movementController.SetRandomDestination();
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
        // 이벤트 구독 해제
        if (petState != null)
        {
            petState.OnStatusChanged -= OnPetStatusChanged;
        }
        
        if (petNeeds != null)
        {
            petNeeds.OnEmotionRequired -= OnEmotionRequired;
            petNeeds.OnNeedCritical -= OnNeedCritical;
        }
        
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
            PetDebug.LogWarning($"{petName}: NavMeshAgent가 없습니다.", this);
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
                PetDebug.LogWarning($"{petName}: 적절한 NavMesh 위치를 찾을 수 없습니다.", this);
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
            PetDebug.LogError($"{petName}: NavMeshAgent 초기화 실패. 컨트롤러들을 초기화하지 않습니다.", this);
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
                PetDebug.LogWarning($"펫 이름 '{name}'에서 타입을 감지할 수 없습니다. 기본값 {petType}을(를) 사용합니다.", this);
            }
        }
    }


    // NavMeshAgent 제어 메서드들
    public void StopMovement()
    {
        if (!IsNavMeshAgentValid()) return;
        
        try
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            // 회전 제어는 HandleRotation()에서 통합 관리
        }
        catch (System.Exception e)
        {
            PetDebug.LogWarning($"{petName}: StopMovement 실패 - {e.Message}", this);
        }
    }

    public void ResumeMovement()
    {
        if (petState.IsGathering || !IsNavMeshAgentValid()) return;

        try
        {
            agent.isStopped = false;
            // 회전 제어는 HandleRotation()에서 통합 관리
        }
        catch (System.Exception e)
        {
            PetDebug.LogWarning($"{petName}: ResumeMovement 실패 - {e.Message}", this);
        }
    }
    
    // NavMeshAgent 유효성 검사 헬퍼 메서드
    private bool IsNavMeshAgentValid()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh;
    }

    // 감정 표현 메서드 - EmotionController로 위임
    public void ShowEmotion(EmotionType emotion, float duration = 10f)
    {
        if (emotionController != null)
            emotionController.ShowEmotion(emotion, duration);
    }

    public void HideEmotion()
    {
        if (emotionController != null)
            emotionController.HideEmotion();
    }

    // 이벤트 핸들러
    private void OnPetStatusChanged(PetStatus oldStatus, PetStatus newStatus)
    {
        // 상태 변경에 따른 추가 처리
        if (emotionController != null)
            emotionController.OnStatusChanged(newStatus);
    }
    
    private void OnEmotionRequired(EmotionType emotionType)
    {
        if (emotionController != null)
            emotionController.OnEmotionRequired(emotionType);
    }
    
    private void OnNeedCritical(PetNeeds.NeedType needType)
    {
        // 욕구가 임계치에 도달했을 때 처리
        switch (needType)
        {
            case PetNeeds.NeedType.Hunger:
                PetDebug.Log($"{petName}이(가) 배고파합니다!", this);
                break;
            case PetNeeds.NeedType.Sleepiness:
                PetDebug.Log($"{petName}이(가) 졸려합니다!", this);
                break;
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