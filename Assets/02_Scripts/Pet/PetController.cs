// PetController.cs
using System;
using System.Collections;
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
            PetDebug.LogDebug($"{petName}에 Rigidbody 자동 추가됨 (Trigger 충돌 감지용)", this);
        }

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
        
        if (profile.type == PetType.Dog && gameObject.name.ToLower() != "dog")
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
        
        // PetAI 초기화
        petAI = gameObject.AddComponent<PetAI>();
        petAI.Init(this);
        petNeeds ??= new PetNeeds();
        petNeeds.Init(this);
        
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
        PetDebug.Log($"{petName}의 현재 활동이 '{type}'으로 인해 중단됩니다.", this);
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
        if (petState != null)
        {
            petState.OnStatusChanged -= OnPetStatusChanged;
        }
        
        if (petNeeds != null)
        {
            petNeeds.OnEmotionRequired -= OnEmotionRequired;
            petNeeds.OnNeedCritical -= OnNeedCritical;
        }
        
        if (PetInteractionManager.Instance != null)
        {
            PetInteractionManager.Instance.UnregisterPet(this);
        }
    }
    
    private IEnumerator EnsureNavMeshPlacement()
    {
        yield return new WaitForSeconds(NAVMESH_PLACEMENT_WAIT);
        
        if (agent == null)
        {
            PetDebug.LogWarning($"{petName}: NavMeshAgent가 없습니다.", this);
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
                PetDebug.LogWarning($"{petName}: 적절한 NavMesh 위치를 찾을 수 없습니다.", this);
            }
        }
        
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            PetDebug.LogError($"{petName}: NavMeshAgent 초기화 실패. 컨트롤러들을 초기화하지 않습니다.", this);
        }
    }

    private void SetPetTypeFromName()
    {
        string name = gameObject.name.ToLower();
        bool typeFound = false;
        foreach (PetType type in Enum.GetValues(typeof(PetType)))
        {
            string typeName = type.ToString().ToLower();
            if (name.Contains(typeName))
            {
                profile.type = type;
                typeFound = true;
                break;
            }
        }

        if (!typeFound)
        {
            if (name.Contains("lion")) profile.type = PetType.Lion;
            else if (name.Contains("tiger")) profile.type = PetType.Tiger;
            else if (name.Contains("turtle")) profile.type = PetType.Turtle;
            else if (name.Contains("rabbit")) profile.type = PetType.Rabbit;
            else if (name.Contains("cat")) profile.type = PetType.Cat;
            else if (name.Contains("dog")) profile.type = PetType.Dog;
            else
            {
                PetDebug.LogWarning($"펫 이름 '{name}'에서 타입을 감지할 수 없습니다. 기본값 {profile.type}을(를) 사용합니다.", this);
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
                PetDebug.Log($"{petName}이(가) 배고파합니다!", this);
                break;
            case PetNeeds.NeedType.Sleepiness:
                PetDebug.Log($"{petName}이(가) 졸려합니다!", this);
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