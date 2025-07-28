using System.Collections;
using UnityEngine;

/// <summary>
/// 펫의 입력 처리와 사용자 상호작용만을 담당하는 클래스
/// PetController에서 입력 관련 로직을 분리
/// </summary>
public class PetInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float touchResponseTime = 0.1f;
    [SerializeField] private float holdDetectionTime = 0.5f;
    [SerializeField] private float interactionCooldown = 1f;
    
    [Header("Camera Look Settings")]
    [SerializeField] private float lookAtSpeed = 5f;
    [SerializeField] private float lookDuration = 3f;
    
    private PetController petController;
    private PetState petState;
    private PetEffects petEffects;
    private PetMovement petMovement;
    
    // 상호작용 상태
    private bool isTouched = false;
    private bool isHolding = false;
    private float touchStartTime = 0f;
    private float lastInteractionTime = -10f;
    private Coroutine lookAtCameraCoroutine;
    private bool isInitialized = false;
    
    // 이벤트
    public event System.Action OnTouched;
    public event System.Action OnHoldStarted;
    public event System.Action OnHoldEnded;
    public event System.Action OnSelected;
    public event System.Action OnDeselected;
    
    // 프로퍼티
    public bool IsTouched => isTouched;
    public bool IsHolding => isHolding;
    public bool CanInteract => Time.time - lastInteractionTime >= interactionCooldown;
    
    /// <summary>
    /// PetInteractor 초기화
    /// </summary>
    public void Init(PetController controller, PetState state, PetEffects effects, PetMovement movement)
    {
        petController = controller;
        petState = state;
        petEffects = effects;
        petMovement = movement;
        
        isInitialized = true;
        Debug.Log($"[PetInteractor] {petController.petName}: 상호작용 시스템 초기화 완료");
    }
    
    /// <summary>
    /// 터치 입력 처리
    /// </summary>
    public void HandleTouch()
    {
        if (!isInitialized || !CanInteract)
            return;
            
        isTouched = true;
        touchStartTime = Time.time;
        lastInteractionTime = Time.time;
        
        // 즉시 선택 상태로 전환
        HandleSelection();
        
        // 터치 이벤트 발생
        OnTouched?.Invoke();
        
        Debug.Log($"[PetInteractor] {petController.petName}: 터치됨");
    }
    
    /// <summary>
    /// 터치 해제 처리
    /// </summary>
    public void HandleTouchRelease()
    {
        if (!isInitialized)
            return;
            
        if (isHolding)
        {
            EndHold();
        }
        
        isTouched = false;
        touchStartTime = 0f;
    }
    
    /// <summary>
    /// 선택 처리
    /// </summary>
    public void HandleSelection()
    {
        if (!isInitialized)
            return;
            
        // 상태를 PlayerControl로 변경
        petState.TrySetStatus(PetStatus.PlayerControl);
        
        // 이동 중지
        petMovement?.Stop();
        
        // 카메라 보기 시작
        StartLookingAtCamera();
        
        // 이름 표시
        petEffects?.ShowName(true);
        
        OnSelected?.Invoke();
        
        Debug.Log($"[PetInteractor] {petController.petName}: 선택됨");
    }
    
    /// <summary>
    /// 선택 해제 처리
    /// </summary>
    public void HandleDeselection()
    {
        if (!isInitialized)
            return;
            
        // 상태를 Idle로 변경
        petState.TrySetStatus(PetStatus.Idle);
        
        // 카메라 보기 중지
        StopLookingAtCamera();
        
        // 이름 숨김
        petEffects?.ShowName(false);
        
        OnDeselected?.Invoke();
        
        Debug.Log($"[PetInteractor] {petController.petName}: 선택 해제됨");
    }
    
    /// <summary>
    /// 홀드 시작
    /// </summary>
    private void StartHold()
    {
        if (isHolding)
            return;
            
        isHolding = true;
        OnHoldStarted?.Invoke();
        
        Debug.Log($"[PetInteractor] {petController.petName}: 홀드 시작");
    }
    
    /// <summary>
    /// 홀드 종료
    /// </summary>
    private void EndHold()
    {
        if (!isHolding)
            return;
            
        isHolding = false;
        OnHoldEnded?.Invoke();
        
        Debug.Log($"[PetInteractor] {petController.petName}: 홀드 종료");
    }
    
    /// <summary>
    /// 드래그 처리
    /// </summary>
    public void HandleDrag(Vector3 screenPosition)
    {
        if (!isInitialized || !isHolding)
            return;
            
        // 화면 좌표를 월드 좌표로 변환하여 펫 이동
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("Ground")))
        {
            Vector3 targetPosition = hit.point;
            targetPosition.y = transform.position.y; // 높이 유지
            
            // 직접 이동 (드래그 중에는 NavMesh 사용 안 함)
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
        }
    }
    
    /// <summary>
    /// 카메라 바라보기 시작
    /// </summary>
    private void StartLookingAtCamera()
    {
        if (lookAtCameraCoroutine != null)
        {
            StopCoroutine(lookAtCameraCoroutine);
        }
        
        lookAtCameraCoroutine = StartCoroutine(LookAtCameraCoroutine());
    }
    
    /// <summary>
    /// 카메라 바라보기 중지
    /// </summary>
    private void StopLookingAtCamera()
    {
        if (lookAtCameraCoroutine != null)
        {
            StopCoroutine(lookAtCameraCoroutine);
            lookAtCameraCoroutine = null;
        }
        
        // NavMeshAgent 회전 다시 활성화
        petMovement?.SetAutoRotation(true);
    }
    
    /// <summary>
    /// 카메라를 바라보는 코루틴
    /// </summary>
    private IEnumerator LookAtCameraCoroutine()
    {
        // NavMeshAgent 회전 비활성화
        petMovement?.SetAutoRotation(false);
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[PetInteractor] 메인 카메라를 찾을 수 없습니다!");
            yield break;
        }
        
        float elapsedTime = 0f;
        
        while (petState.CurrentStatus == PetStatus.PlayerControl)
        {
            // 카메라 방향 계산
            Vector3 lookDirection = mainCamera.transform.position - transform.position;
            lookDirection.y = 0; // 수평 회전만
            
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookAtSpeed);
            }
            
            elapsedTime += Time.deltaTime;
            
            // 지정된 시간이 지나면 자동으로 선택 해제
            if (elapsedTime >= lookDuration)
            {
                HandleDeselection();
                break;
            }
            
            yield return null;
        }
        
        lookAtCameraCoroutine = null;
    }
    
    /// <summary>
    /// 매 프레임 업데이트
    /// </summary>
    private void Update()
    {
        if (!isInitialized)
            return;
            
        // 터치 홀드 감지
        if (isTouched && !isHolding)
        {
            if (Time.time - touchStartTime >= holdDetectionTime)
            {
                StartHold();
            }
        }
    }
    
    /// <summary>
    /// 상호작용 가능한 오브젝트인지 확인
    /// </summary>
    public bool CanInteractWith(GameObject target)
    {
        if (!CanInteract)
            return false;
            
        // 다른 펫인지 확인
        PetController otherPet = target.GetComponent<PetController>();
        if (otherPet != null && otherPet != petController)
        {
            return otherPet != null && !otherPet.isAnimationLocked;
        }
        
        // 아이템인지 확인
        if (target.layer == LayerMask.NameToLayer("Item"))
        {
            return true;
        }
        
        // 환경 오브젝트인지 확인
        if (target.layer == LayerMask.NameToLayer("Environment"))
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 특정 오브젝트와 상호작용 시작
    /// </summary>
    public void InteractWith(GameObject target)
    {
        if (!CanInteractWith(target))
            return;
            
        lastInteractionTime = Time.time;
        
        // 상호작용 타입에 따라 처리
        PetController otherPet = target.GetComponent<PetController>();
        if (otherPet != null)
        {
            Debug.Log($"[PetInteractor] {petController.petName}: {otherPet.petName}와 상호작용 시작");
            // PetInteractionController가 처리
        }
        else if (target.layer == LayerMask.NameToLayer("Item"))
        {
            Debug.Log($"[PetInteractor] {petController.petName}: 아이템과 상호작용");
        }
        else if (target.layer == LayerMask.NameToLayer("Environment"))
        {
            Debug.Log($"[PetInteractor] {petController.petName}: 환경과 상호작용");
        }
    }
    
    private void OnDestroy()
    {
        if (lookAtCameraCoroutine != null)
        {
            StopCoroutine(lookAtCameraCoroutine);
        }
    }
}