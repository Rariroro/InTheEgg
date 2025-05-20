// PetController.cs 수정 버전
// 공통 열거형은 별도 static 클래스에 모아둡니다.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public static class PetAIProperties
{
    public enum Personality { Shy, Brave, Lazy, Playful }
    public enum DietType { Carnivore, Herbivore, Omnivore }
    public enum Habitat { Water, Forest, Field }
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
    public PetAIProperties.DietType dietType = PetAIProperties.DietType.Omnivore;
    public PetAIProperties.Habitat habitat = PetAIProperties.Habitat.Forest;
    [Range(0, 100)]
    public float affection;
    [Range(0, 100)]
    public float hunger;
    [Range(0, 100)]
    public float sleepiness;

    [Header("Pet Information")]
    public string petName = "Buddy";
    public DateTime birthday = default;

    // 공용 컴포넌트
    [HideInInspector] public NavMeshAgent agent;
    [HideInInspector] public Animator animator;
    [HideInInspector] public Transform petModelTransform;
    [HideInInspector] public bool isGathered = false;
    [HideInInspector] public int gatherCommandVersion = 0; // 추가: 모으기 명령 버전 추적

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

     // 현재 활성화된 감정 말풍선
    private EmotionBubble activeBubble;

    [Header("Pet Type")]
    [SerializeField] private PetType petType = PetType.Dog; // 기본값 설정
    [SerializeField] private bool manuallySetPetType = false; // 수동 설정 여부 체크 필드 추가

    // 상호작용 관련 변수
    [HideInInspector] public bool isInteracting = false;
    [HideInInspector] public PetController interactionPartner = null;

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

    private void Awake()
    {
        birthday = DateTime.Now;

        // NavMeshAgent 초기화
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = speed;
            agent.angularSpeed = angularSpeed;
            agent.acceleration = acceleration;
            agent.stoppingDistance = stoppingDistance;
            // 기본 값 저장
            baseSpeed = speed;
            baseAngularSpeed = angularSpeed;
            baseAcceleration = acceleration;
            baseStoppingDistance = stoppingDistance;
        }

        // petModelTransform: 첫 번째 자식을 우선 사용, 없으면 Renderer가 있는 오브젝트 사용
        if (transform.childCount > 0)
        {
            petModelTransform = transform.GetChild(0);
        }
        if (petModelTransform == null)
        {
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
                petModelTransform = renderer.transform;
        }
        if (petModelTransform == null)
        {
            // Debug.LogWarning("Pet model not found. The pet may not display correctly.");
        }

        // Animator 컴포넌트 획득
        if (petModelTransform != null)
        {
            animator = petModelTransform.GetComponent<Animator>();
            if (animator == null)
            {
                // Debug.LogWarning("Animator component not found on the pet model.");
            }
        }

        // 인스펙터에서 수동으로 설정하지 않았을 경우에만 자동 감지 실행
        if (!manuallySetPetType)
        {
            SetPetTypeFromName();
        }

        // 디버그 로그에 펫 타입 출력
        // Debug.Log($"[PetController] {gameObject.name} - 펫 타입: {petType}");

        // 각 기능별 컨트롤러 추가 및 초기화
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
        // 초기화 순서 변경 - NavMesh 위치 확인 후 컨트롤러 초기화
        StartCoroutine(EnsureNavMeshPlacement());
    }
    private IEnumerator EnsureNavMeshPlacement()
    {
        yield return new WaitForSeconds(0.2f);

        // NavMesh에 없는 경우 배치 시도
        if (agent != null && !agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                yield return new WaitForSeconds(0.1f);
            }
        }

        // 컨트롤러 초기화
        movementController.Init(this);
        animationController.Init(this);
        interactionController.Init(this);
        feedingController.Init(this);
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
                // Debug.LogWarning($"[PetController] 펫 이름 '{name}'에서 타입을 감지할 수 없습니다. 기본값 {petType}을(를) 사용합니다.");
            }
        }
    }

    // PetController.cs의 Update 메서드 수정
    private void Update()
    {
        feedingController.UpdateFeeding();
    sleepingController.UpdateSleeping(); // 추가: 수면 상태 업데이트

        // 상호작용 중이 아닐 때만 움직임 업데이트
        if (!isInteracting)
        {
            movementController.UpdateMovement();
        }

        interactionController.HandleInput();
        animationController.UpdateAnimation();
    }

    // 외부에서 이동을 제어하기 위한 메서드들
    public void StopMovement()
    {
        if (agent != null) agent.isStopped = true;
    }

    public void ResumeMovement()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }
      // 감정 표현 메서드
    public void ShowEmotion(EmotionType emotion, float duration = 10f)
    {
        // 기존 말풍선 처리
        if (activeBubble != null && activeBubble.gameObject.activeSelf)
        {
            // 이미 활성화된 말풍선이 있으면 비활성화
            EmotionManager.Instance.ReturnBubbleToPool(activeBubble);
            activeBubble = null;
        }
        
        // 새 말풍선 표시
        if (EmotionManager.Instance != null)
        {
            activeBubble = EmotionManager.Instance.ShowPetEmotion(this, emotion, duration);
        }
    }
    
    // 감정 말풍선 숨기기
    public void HideEmotion()
    {
        if (activeBubble != null)
        {
            EmotionManager.Instance.ReturnBubbleToPool(activeBubble);
            activeBubble = null;
        }
    }
    
    // 상호작용에 따른 감정 표현 (PetInteractionController에서 호출)
    // public void ShowInteractionEmotion(InteractionType interactionType, bool isInitiator = true)
    // {
    //     switch (interactionType)
    //     {
    //         case InteractionType.Fight:
    //             ShowEmotion(isInitiator ? EmotionType.Angry : EmotionType.Scared);
    //             break;
                
    //         case InteractionType.WalkTogether:
    //             ShowEmotion(EmotionType.Happy);
    //             break;
                
    //         case InteractionType.RestTogether:
    //             ShowEmotion(EmotionType.Sleepy);
    //             break;
                
    //         case InteractionType.Race:
    //             ShowEmotion(EmotionType.Surprised);
    //             break;
                
    //         case InteractionType.ChaseAndRun:
    //             ShowEmotion(isInitiator ? EmotionType.Angry : EmotionType.Scared);
    //             break;
                
    //         case InteractionType.SleepTogether:
    //             ShowEmotion(EmotionType.Sleepy);
    //             break;
                
    //         case InteractionType.RideAndWalk:
    //             ShowEmotion(isInitiator ? EmotionType.Happy : EmotionType.Surprised);
    //             break;
                
    //         default:
    //             ShowEmotion(EmotionType.Confused);
    //             break;
    //     }
    // }
    
    // 상호작용 결과에 따른 감정 표현
        // public void ShowResultEmotion(bool isWinner)
        // {
        //     ShowEmotion(isWinner ? EmotionType.Victory : EmotionType.Defeat);
        // }
}