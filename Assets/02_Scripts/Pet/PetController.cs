// PetController.cs
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public partial class PetController : MonoBehaviour
{
    // 게임 설정 상수
    private const float NAVMESH_PLACEMENT_WAIT = 0.2f;
    private const float NAVMESH_REPOSITION_WAIT = 0.1f;
    private const float NAVMESH_SAMPLE_DISTANCE = 10f;
    
    [Header("Pet Settings")]
    [SerializeField] private PetProfile profile = new PetProfile();
    [SerializeField] private MovementSettings movement = new MovementSettings();
    
    public PetProfile Profile => profile;
    public MovementSettings Movement => movement;
    
    // 호환성 프로퍼티 (많은 곳에서 사용하므로 유지)
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
    [HideInInspector] public NavMeshAgent agent;
    [HideInInspector] public Animator animator;
    [HideInInspector] public Transform petModelTransform;
    [HideInInspector] public bool isGatheringAnimationOverride = false;

    // 이동 속도 관련
    public float baseSpeed => movement.walkSpeed;
    public float baseAngularSpeed => movement.angularSpeed;
    public float baseAcceleration => movement.acceleration;
    public float baseStoppingDistance => movement.stoppingDistance;

    // 컨트롤러 참조 (직접 접근 가능)
    public PetMovementController movementController;
    public PetAnimationController animationController;
    public PetInputController inputController;
    public PetFeedingController feedingController;
    public PetSleepingController sleepingController;
    public PetWaterBehaviorController waterBehaviorController;
    public PetTreeClimbingController treeClimbingController;
    public PetEmotionController emotionController;
    public PetInteractionDetector interactionDetector;
    
    private PetAI petAI;

    // 상태 관련 호환성 프로퍼티
    public bool isAnimationLocked => petState.IsAnimationLocked;
    public Transform currentTree => petState.CurrentTree;
    public float climbHeight => movement.climbHeight;

    // 상태 및 욕구 관리
    [Header("State Management")]
    [SerializeField] private PetState petState = new PetState();
    
    [Header("Needs Management")]
    [SerializeField] private PetNeeds petNeeds = new PetNeeds();
    
    [Header("Treasure Settings")]
    [Tooltip("보물을 물고 갈 위치 (입 위치) - Unity 에디터에서 설정")]
    public Transform treasureHoldPoint;

    [Header("Ride Settings")]
    [Tooltip("다른 펫이 올라탈 수 있는 위치 - Unity 에디터에서 설정")]
    public Transform ridePoint;
    public PetState State => petState;
    public PetNeeds Needs => petNeeds;
    public PetAI AI => petAI;
    public PetType PetType 
    { 
        get => profile.type; 
        set => profile.type = value; 
    }
    
    private void Awake()
    {
        profile ??= new PetProfile();
        movement ??= new MovementSettings();
        profile.birthday = DateTime.Now;
        
        // NavMeshAgent 초기화
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = movement.walkSpeed;
            agent.angularSpeed = movement.angularSpeed;
            agent.acceleration = movement.acceleration;
            agent.stoppingDistance = movement.stoppingDistance;

            agent.updateRotation = true;
            agent.updatePosition = true;
            agent.updateUpAxis = false;
        }
        
        // Rigidbody 확인 및 추가
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Debug.Log($"[Pet] {petName}에 Rigidbody 자동 추가됨 (Trigger 충돌 감지용)", this);
        }

        // Animator와 모델 Transform 찾기
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            petModelTransform = animator.transform;
        }
        else
        {
            Debug.LogWarning($"[Pet Warning] {this.gameObject.name}에서 Animator 컴포넌트를 찾을 수 없습니다! 애니메이션이 작동하지 않습니다.", this);
            if (transform.childCount > 0)
            {
                petModelTransform = transform.GetChild(0);
            }
        }

        // TreasureHoldPoint 자동 검색 (인스펙터에서 할당되지 않은 경우)
        if (treasureHoldPoint == null)
        {
            treasureHoldPoint = transform.Find("TreasureHoldPoint");
            if (treasureHoldPoint == null)
            {
                Debug.LogWarning($"[PetController] {petName}: TreasureHoldPoint를 찾을 수 없습니다. 보물찾기 기능이 제한될 수 있습니다.");
            }
        }

        // RidePoint 자동 검색 (인스펙터에서 할당되지 않은 경우)
        if (ridePoint == null)
        {
            ridePoint = transform.Find("RidePoint");
            // RidePoint는 선택사항이므로 경고 메시지 출력하지 않음
        }
        
        // 펫 타입과 이름이 일치하지 않거나 기본 이름인 경우 업데이트
        if (profile.name != profile.type.ToString() || profile.name == "Buddy")
        {
            SetPetTypeFromName();
        }

        // 컨트롤러 초기화
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
        emotionController.Init(this);
        interactionDetector = gameObject.AddComponent<PetInteractionDetector>();
        interactionDetector.Init(this);
        
        // PetAI 초기화
        petAI = gameObject.AddComponent<PetAI>();
        petAI.Init(this);
        petNeeds ??= new PetNeeds();
        petNeeds.Init(this);

        // PetState 초기화
        petState ??= new PetState();
        petState.Initialize(this);
        
        // 이벤트 구독
        petNeeds.OnEmotionRequired += OnEmotionRequired;
        petNeeds.OnNeedCritical += OnNeedCritical;
        petState.OnStatusChanged += OnPetStatusChanged;
        StartCoroutine(EnsureNavMeshPlacement());
        
        // PetInteractionManager 등록
        if (PetInteractionManager.Instance != null)
        {
            StartCoroutine(RegisterToPetManager());
        }
    }
    
    private void Update()
    {
        petNeeds?.UpdateNeeds();

        // 상호작용 타임아웃 체크
        petState?.CheckInteractionTimeout();
    }
    [System.Obsolete("Use State.StartInteraction() directly")]
    public void BeginInteraction(PetController partner, BasePetInteraction interactionLogic)
    {
        petState.StartInteraction(partner);
        petState.SetInteractionLogic(interactionLogic);
    }

    public void InterruptCurrentActionFor(InteractionType type)
    {
        if (petAI != null)
        {
            petAI.InterruptAndResetAI();
        }
        Debug.Log($"[Pet] {petName}의 현재 활동이 '{type}'으로 인해 중단됩니다.", this);
    }
    
    private IEnumerator RegisterToPetManager()
    {
        yield return null;
        
        if (PetInteractionManager.Instance != null)
        {
            PetInteractionManager.Instance.RegisterPet(this);
        }
    }

    private void OnDestroy()
    {
        // 이벤트 리스너 먼저 해제 (메모리 누수 방지)
        if (petState != null)
        {
            petState.OnStatusChanged -= OnPetStatusChanged;
        }

        if (petNeeds != null)
        {
            petNeeds.OnEmotionRequired -= OnEmotionRequired;
            petNeeds.OnNeedCritical -= OnNeedCritical;
        }

        // 코루틴 정리
        StopAllCoroutines();

        // InteractionManager에서 등록 해제
        if (PetInteractionManager.Instance != null)
        {
            PetInteractionManager.Instance.UnregisterPet(this);
        }

        // NavMeshAgent 정리 (안전하게)
        if (agent != null)
        {
            try
            {
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
                agent.enabled = false;
            }
            catch
            {
                // NavMeshAgent가 이미 파괴되었거나 접근할 수 없는 경우 무시
            }
        }
    }
    
    private IEnumerator EnsureNavMeshPlacement()
    {
        yield return new WaitForSeconds(NAVMESH_PLACEMENT_WAIT);
        
        if (agent == null)
        {
            Debug.LogWarning($"[Pet Warning] {petName}: NavMeshAgent가 없습니다.", this);
            yield break;
        }

        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas))
            {
                bool wasEnabled = agent.enabled;
                agent.enabled = false;
                transform.position = hit.position;
                yield return new WaitForSeconds(NAVMESH_REPOSITION_WAIT);
                agent.enabled = wasEnabled;
                yield return new WaitForSeconds(NAVMESH_REPOSITION_WAIT);
            }
            else
            {
                Debug.LogWarning($"[Pet Warning] {petName}: 적절한 NavMesh 위치를 찾을 수 없습니다.", this);
            }
        }
        
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            Debug.LogError($"[Pet Error] {petName}: NavMeshAgent 초기화 실패. 컨트롤러들을 초기화하지 않습니다.", this);
        }
    }

    private void SetPetTypeFromName()
    {
        string name = gameObject.name.ToLower();
        bool typeFound = false;
        
        // RedPanda를 Panda보다 먼저 체크 (더 구체적인 이름을 먼저 확인)
        if (name.Contains("redpanda"))
        {
            profile.type = PetType.RedPanda;
            profile.name = "RedPanda";
            typeFound = true;
        }
        else
        {
            // enum을 이름 길이 역순으로 정렬 (긴 이름 먼저 체크)
            // 예: "Chicken" (7글자) → "Chick" (5글자)보다 먼저 체크됨
            var sortedTypes = Enum.GetValues(typeof(PetType))
                .Cast<PetType>()
                .OrderByDescending(t => t.ToString().Length);

            foreach (PetType type in sortedTypes)
            {
                string typeName = type.ToString().ToLower();
                if (name.Contains(typeName))
                {
                    profile.type = type;
                    profile.name = type.ToString();  // 펫 이름도 타입명으로 설정
                    typeFound = true;
                    break;
                }
            }
        }

        if (!typeFound)
        {
            if (name.Contains("lion")) 
            { 
                profile.type = PetType.Lion; 
                profile.name = "Lion";
            }
            else if (name.Contains("tiger")) 
            { 
                profile.type = PetType.Tiger; 
                profile.name = "Tiger";
            }
            else if (name.Contains("turtle")) 
            { 
                profile.type = PetType.Turtle; 
                profile.name = "Turtle";
            }
            else if (name.Contains("rabbit")) 
            { 
                profile.type = PetType.Rabbit; 
                profile.name = "Rabbit";
            }
            else if (name.Contains("cat")) 
            { 
                profile.type = PetType.Cat; 
                profile.name = "Cat";
            }
            else if (name.Contains("dog")) 
            { 
                profile.type = PetType.Dog; 
                profile.name = "Dog";
            }
            else
            {
                Debug.LogWarning($"[Pet Warning] 펫 이름 '{name}'에서 타입을 감지할 수 없습니다. 기본값 {profile.type}을(를) 사용합니다.", this);
            }
        }
    }

    // 컨트롤러 간 상호 호출을 위한 위임 메서드 (컨트롤러 내부에서만 사용)
    public void HandleRotation() => movementController?.HandleRotation();
    public void StopMovement() => movementController?.StopMovement();
    public void ResumeMovement() => movementController?.ResumeMovement();
    public void SetRandomDestination() => movementController?.SetRandomDestination();
    public void ShowEmotion(EmotionType emotion, float duration = 10f) => emotionController?.ShowEmotion(emotion, duration);
    public void HideEmotion() => emotionController?.HideEmotion();
    public void AdjustSpeedForWater() => waterBehaviorController?.AdjustSpeedForWater();

    /// <summary>
    /// 긴급 상황 시 펫 상태 강제 복구
    /// </summary>
    public void ForceResetState()
    {
        Debug.LogWarning($"[PetController] {petName} - ForceResetState 호출");

        // 1. 진행 중인 상호작용 강제 종료
        if (petState.IsInteracting)
        {
            petState.ForceEndInteraction();

            if (petState.InteractionLogic != null)
            {
                petState.InteractionLogic.StopAllCoroutines();
            }
        }

        // 2. 상태 초기화
        petState.ForceIdle();

        // 3. 감정 숨기기
        HideEmotion();

        // 4. 애니메이션 정리
        if (animationController != null)
        {
            animationController.StopAllCoroutines();
            animationController.StopContinuousAnimation();
            animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
        }

        // 5. NavMeshAgent 강제 복구
        if (agent != null)
        {
            // 비활성화되었다면 활성화
            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            // NavMesh 위에 없다면 위치 보정
            if (agent.enabled && !agent.isOnNavMesh)
            {
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(transform.position, out navHit, 10f, NavMesh.AllAreas))
                {
                    agent.Warp(navHit.position);
                }
            }

            // Agent 상태 초기화
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
                agent.speed = baseSpeed;
                agent.acceleration = baseAcceleration;
                agent.angularSpeed = baseAngularSpeed;
                agent.updateRotation = true;
                agent.updatePosition = true;
            }
        }

        // 6. 이동 재개
        ResumeMovement();

        // 7. AI 즉시 재평가
        if (petAI != null)
        {
            petAI.InterruptAndResetAI();
        }

        Debug.Log($"[PetController] {petName} - ForceResetState 완료");
    }

    // 이벤트 핸들러
    private void OnPetStatusChanged(PetStatus oldStatus, PetStatus newStatus)
    {
        emotionController?.OnStatusChanged(newStatus);
    }
    private void OnEmotionRequired(EmotionType emotionType)
    {
        emotionController?.OnEmotionRequired(emotionType);
    }
    private void OnNeedCritical(PetNeeds.NeedType needType)
    {
        switch (needType)
        {
            case PetNeeds.NeedType.Hunger:
                Debug.Log($"[Pet] {petName}이(가) 배고파합니다!", this);
                break;
            case PetNeeds.NeedType.Sleepiness:
                Debug.Log($"[Pet] {petName}이(가) 졸려합니다!", this);
                break;
        }
    }
    
    // 친밀도 관련 프로퍼티
    public float GetHighAffectionThreshold() => petNeeds?.HighAffectionThreshold ?? 80f;
    public float GetDroppedFoodAffectionMin() => petNeeds?.DroppedFoodAffectionMin ?? 5f;
    public float GetDroppedFoodAffectionMax() => petNeeds?.DroppedFoodAffectionMax ?? 10f;
    public float GetEnvironmentFoodAffectionMin() => petNeeds?.EnvironmentFoodAffectionMin ?? 3f;
    public float GetEnvironmentFoodAffectionMax() => petNeeds?.EnvironmentFoodAffectionMax ?? 7f;
}