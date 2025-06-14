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

    // 현재 활성화된 감정 말풍선
    private EmotionBubble activeBubble;

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

        // ... 나머지 코드는 동일


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

        // PetInteractionManager에 이 펫 등록
        if (PetInteractionManager.Instance != null)
        {
            // 약간의 지연 후 등록 (매니저가 완전히 초기화된 후)
            StartCoroutine(RegisterToPetManager());
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

    // PetController.cs의 Update 메서드 수정
    // Update 메서드 수정
    private void Update()
    {
        feedingController.UpdateFeeding();
        sleepingController.UpdateSleeping();

        // 모이기 중이거나 상호작용 중이 아닐 때만 움직임 업데이트
        if (!isGathering && !isInteracting)
        {
            movementController.UpdateMovement();
        }

        // 모이기 중이 아닐 때만 상호작용 처리
        if (!isGathering)
        {
            interactionController.HandleInput();
        }
        // [수정 1] 중앙 회전 처리 메서드를 여기서 호출합니다.
        HandleRotation();
        // 모이기 애니메이션 오버라이드 중이 아닐 때만 일반 애니메이션 업데이트
        if (!isGatheringAnimationOverride)
        {
            animationController.UpdateAnimation();
        }

        // ★ 물에 있을 때는 Y 오프셋 적용, 아니면 원위치
        if (petModelTransform != null)
        {
            Vector3 targetLocalPos = new Vector3(0, waterDepthOffset, 0);
            petModelTransform.localPosition = targetLocalPos;
            petModelTransform.localRotation = Quaternion.identity;
        }
    }
    // [수정 2] 아래 메서드를 클래스 내부에 새로 추가합니다.
    /// <summary>
    /// 펫의 회전을 중앙에서 관리합니다.
    /// NavMeshAgent의 이동 방향(velocity)에 맞춰 펫을 부드럽게 회전시킵니다.
    /// </summary>
    private void HandleRotation()
    {
        // 도착해서 멈췄거나(isGathered), 플레이어가 직접 조작 중일 때는(isInteracting) 자동 회전을 하지 않습니다.
        if (isGathered || isInteracting)
        {
            return;
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

        // 이동 방향 벡터를 가져옵니다.
        Vector3 moveDirection = agent.velocity.normalized;

        // 이동 방향이 있을 경우에만 회전합니다 (제자리에서 회전하는 것 방지).
        if (moveDirection.magnitude > 0.1f)
        {
            // 이동 방향을 바라보는 회전값(Quaternion)을 계산합니다.
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

            // 현재 각도에서 목표 각도로 부드럽게 회전시킵니다.
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
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
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PetController] {petName}: StopMovement 실패 - {e.Message}");
            }
        }
    }

    public void ResumeMovement()
    {
        // ★ 모이기 중일 때는 움직임 재개하지 않음
        if (isGathering)
        {
            return;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            try
            {
                agent.isStopped = false;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PetController] {petName}: ResumeMovement 실패 - {e.Message}");
            }
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
}