using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 0.05f;
    public float zoomSpeed = 2f; // 줌 속도 감소
    public float minZoom = 50f; // 가장 축소됐을 때의 FOV (더 넓은 시야)
    public float maxZoom = 20f; // 가장 확대됐을 때의 FOV (더 좁은 시야)
    public float mouseDragSpeed = 0.5f;
  [Header("카메라 이동 제한")]
    public bool limitCameraMovement = true;
    public float minX = -500f;  // 최소 X 좌표
    public float maxX = 500f;   // 최대 X 좌표
    public float minZ = -500f;  // 최소 Z 좌표
    public float maxZ = 500f;   // 최대 Z 좌표

    [Header("카메라 애니메이션 잠금")]
    private bool isCameraAnimating = false;

    // 카메라 애니메이션 상태 속성 (읽기 전용)
    public bool IsCameraAnimating => isCameraAnimating;

    private Camera childCamera;
    private Vector3 lastPanPosition;
    private int fingerId = -1;
    private float initialPinchDistance;
    private Vector3 initialCameraPosition;
    private bool isDragging = false;
    private Vector3 lastMousePosition;

    void Start()
    {
        childCamera = GetComponentInChildren<Camera>();
        if (childCamera == null)
        {
            Debug.LogError("No camera found in children. Please add a camera as a child of this object.");
        }
        else
        {
            childCamera.orthographic = false;
            initialCameraPosition = childCamera.transform.localPosition;
            childCamera.fieldOfView = minZoom; // 시작할 때 FOV를 50으로 설정 (가장 축소된 상태)
            // Debug.Log($"Initial FOV set to: {childCamera.fieldOfView}");

        }
    }

    void Update()
    {
        if (Application.isMobilePlatform)
        {
            HandleMobileInput();
        }
        else
        {
            HandleEditorInput();
        }

         // 카메라 위치 제한 적용
        if (limitCameraMovement)
        {
            ClampCameraPosition();
        }
    }

    void HandleMobileInput()
    {
        // 펫이 들려있는지 확인
        bool isPetHolding = CheckIfAnyPetIsHolding();

        if (Input.touchCount == 1)
        {
            // 펫을 들고 있지 않을 때만 카메라 이동
            if (!isPetHolding)
            {
                PanCamera();
            }
        }
        else if (Input.touchCount == 2)
        {
            // 2손가락 줌은 펫 홀드와 상관없이 작동
            ZoomCamera();
        }
    }

    void HandleEditorInput()
    {
        // 펫이 들려있는지 확인
        bool isPetHolding = CheckIfAnyPetIsHolding();
        
        if (Input.GetMouseButtonDown(0))
        {
            // Debug.Log("HandleEditorInput :1");
            
            // 보물을 클릭했는지 확인
            if (CheckIfClickedOnTreasure())
            {
                // 보물을 클릭한 경우 카메라 드래그 시작하지 않음
                isDragging = false;
        // Debug.Log("[CameraController] 보물 클릭 감지 - 카메라 드래그 취소");
                return;
            }
            // 펫이 들려있으면 카메라 드래그 시작하지 않음
            else if (!isPetHolding)
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            // Debug.Log("HandleEditorInput :2");

            isDragging = false;
        }

        // 펫이 들려있는 동안 isDragging을 false로 유지
        if (isPetHolding && isDragging)
        {
            isDragging = false;
        }

        if (isDragging)
        {
            // Debug.Log("HandleEditorInput :3");

            Vector3 delta = Input.mousePosition - lastMousePosition;
            transform.Translate(-delta.x * mouseDragSpeed * Time.deltaTime, 0, -delta.y * mouseDragSpeed * Time.deltaTime);
            lastMousePosition = Input.mousePosition;
        }
        // Debug.Log("HandleEditorInput :4");

        // 마우스 스크롤 줌 처리
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {

            ZoomCameraAmount(-scroll * zoomSpeed);
        }

        // 키보드 이동 처리
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        if (horizontal != 0 || vertical != 0)
        {
            Vector3 keyboardMove = new Vector3(horizontal, 0, vertical);
            transform.Translate(keyboardMove * (moveSpeed*10) , Space.World);
        }
    }

    void PanCamera()
    {
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            // 터치 시작 시 보물을 터치했는지 확인
            if (CheckIfClickedOnTreasure())
            {
        // Debug.Log("[CameraController] 모바일: 보물 터치 감지 - 카메라 이동 취소");
                return;
            }

            lastPanPosition = touch.position;
            fingerId = touch.fingerId;
        }
        else if (touch.phase == TouchPhase.Moved && touch.fingerId == fingerId)
        {
            Vector3 delta = (Vector3)touch.position - (Vector3)lastPanPosition;
            transform.Translate(-delta.x * moveSpeed, 0, -delta.y * moveSpeed);
            lastPanPosition = touch.position;
        }
    }
  // 새로 추가된 메서드
    void ClampCameraPosition()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        transform.position = pos;
    }
    void ZoomCamera()
    {
        Touch touch1 = Input.GetTouch(0);
        Touch touch2 = Input.GetTouch(1);

        if (touch1.phase == TouchPhase.Began || touch2.phase == TouchPhase.Began)
        {
            initialPinchDistance = Vector2.Distance(touch1.position, touch2.position);
        }
        else if (touch1.phase == TouchPhase.Moved || touch2.phase == TouchPhase.Moved)
        {
            float currentPinchDistance = Vector2.Distance(touch1.position, touch2.position);
            float pinchDelta = currentPinchDistance - initialPinchDistance;

            ZoomCameraAmount(-pinchDelta * zoomSpeed * 0.01f); // 부호 변경

            initialPinchDistance = currentPinchDistance;
        }
    }


    void ZoomCameraAmount(float amount)
    {

        if (childCamera != null)
        {
            // Debug.Log("ZoomCameraAmount :1");
            float fov = childCamera.fieldOfView;
            fov += amount;
            fov = Mathf.Clamp(fov, maxZoom, minZoom);
            childCamera.fieldOfView = fov;
            // Debug.Log($"Current FOV: {childCamera.fieldOfView}");
        }
    }
    
    // 펫이 들려있는지 확인하는 헬퍼 메서드
    bool CheckIfAnyPetIsHolding()
    {
        // 모든 펫 오브젝트 찾기
        GameObject[] pets = GameObject.FindGameObjectsWithTag("Pet");
        foreach (var pet in pets)
        {
            var petController = pet.GetComponent<PetController>();
            if (petController != null && petController.State.IsHolding)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 마우스 클릭 위치에 보물이 있는지 확인
    /// </summary>
    bool CheckIfClickedOnTreasure()
    {
        if (childCamera == null) return false;

        // 마우스 위치에서 레이 발사
        Ray ray = childCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // DroppedItem 레이어만 검사
        int droppedItemLayer = LayerMask.NameToLayer("DroppedItem");
        int layerMask = 1 << droppedItemLayer;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
        {
            // TreasureController가 있는지 확인
            TreasureController treasure = hit.collider.GetComponent<TreasureController>();
            if (treasure != null)
            {
        // Debug.Log($"[CameraController] 보물 감지: {hit.collider.name}");
                // 보물의 수집 메서드 직접 호출
                treasure.OnTreasureClicked();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 카메라 애니메이션 시작 시 잠금 (다른 시스템의 카메라 조작 차단)
    /// </summary>
    public void LockCamera()
    {
        isCameraAnimating = true;
    }

    /// <summary>
    /// 카메라 애니메이션 종료 시 잠금 해제
    /// </summary>
    public void UnlockCamera()
    {
        isCameraAnimating = false;
    }

    /// <summary>
    /// 지정된 위치로 카메라를 부드럽게 이동 (Y축 고정)
    /// </summary>
    /// <param name="targetPosition">목표 위치 (월드 좌표)</param>
    /// <param name="duration">이동 시간 (초)</param>
    public void MoveCameraToPosition(Vector3 targetPosition, float duration = 1.0f)
    {
        StartCoroutine(MoveCameraToPositionCoroutine(targetPosition, duration, false));
    }

    /// <summary>
    /// 지정된 위치로 카메라를 부드럽게 이동 (Y축 포함 옵션)
    /// </summary>
    /// <param name="targetPosition">목표 위치 (월드 좌표)</param>
    /// <param name="duration">이동 시간 (초)</param>
    /// <param name="includeHeight">Y축 높이 포함 여부 (true: 높이 따라감, false: 현재 높이 유지)</param>
    public void MoveCameraToPosition(Vector3 targetPosition, float duration, bool includeHeight)
    {
        StartCoroutine(MoveCameraToPositionCoroutine(targetPosition, duration, includeHeight));
    }

    /// <summary>
    /// 지정된 위치로 카메라를 이동하면서 FOV도 조정 (토스트 알림용)
    /// </summary>
    /// <param name="targetPosition">목표 위치 (월드 좌표, Y는 무시됨)</param>
    /// <param name="duration">이동 시간 (초)</param>
    /// <param name="targetFOV">목표 FOV 값</param>
    public void MoveCameraToPositionWithZoom(Vector3 targetPosition, float duration, float targetFOV)
    {
        StartCoroutine(MoveCameraWithZoomCoroutine(targetPosition, duration, targetFOV));
    }

    /// <summary>
    /// 카메라 이동 코루틴
    /// </summary>
    /// <param name="targetPosition">목표 위치</param>
    /// <param name="duration">이동 시간</param>
    /// <param name="includeHeight">Y축 높이 포함 여부</param>
    private IEnumerator MoveCameraToPositionCoroutine(Vector3 targetPosition, float duration, bool includeHeight)
    {
        // 카메라 잠금 (사용자 입력 차단)
        LockCamera();

        // 시작 위치
        Vector3 startPosition = transform.position;

        // 목표 위치 조정
        Vector3 adjustedTarget;
        if (includeHeight)
        {
            // Y축 포함 전체 이동 (펫 높이 따라감)
            adjustedTarget = targetPosition;
        }
        else
        {
            // 기존 방식: 현재 Y 높이 유지
            adjustedTarget = new Vector3(targetPosition.x, startPosition.y, targetPosition.z);
        }

        // 위치 제한 적용
        if (limitCameraMovement)
        {
            adjustedTarget.x = Mathf.Clamp(adjustedTarget.x, minX, maxX);
            adjustedTarget.z = Mathf.Clamp(adjustedTarget.z, minZ, maxZ);
            // Y축도 제한 (너무 높거나 낮지 않도록)
            if (includeHeight)
            {
                adjustedTarget.y = Mathf.Clamp(adjustedTarget.y, 10f, 100f); // 적절한 높이 범위
            }
        }

        // 이동 거리에 따라 duration 자동 조정 (너무 멀면 더 오래 걸림)
        float distance = Vector3.Distance(startPosition, adjustedTarget);
        float adjustedDuration = Mathf.Max(duration, distance / 100f); // 100 유닛당 1초

        // 부드러운 이동
        float elapsed = 0f;
        while (elapsed < adjustedDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / adjustedDuration;

            // EaseInOut 커브 적용
            float smoothT = t < 0.5f
                ? 2f * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            transform.position = Vector3.Lerp(startPosition, adjustedTarget, smoothT);
            yield return null;
        }

        // 최종 위치로 정확히 설정
        transform.position = adjustedTarget;

        // 카메라 잠금 해제
        UnlockCamera();
    }

    /// <summary>
    /// 카메라 이동 + FOV 조정 코루틴 (Y축 고정)
    /// </summary>
    /// <param name="targetPosition">목표 위치</param>
    /// <param name="duration">이동 시간</param>
    /// <param name="targetFOV">목표 FOV</param>
    private IEnumerator MoveCameraWithZoomCoroutine(Vector3 targetPosition, float duration, float targetFOV)
    {
        // 카메라 잠금 (사용자 입력 차단)
        LockCamera();

        // 시작 위치 및 FOV
        Vector3 startPosition = transform.position;
        float startFOV = childCamera != null ? childCamera.fieldOfView : minZoom;

        // 목표 위치 조정 (Y는 현재 높이 유지)
        Vector3 adjustedTarget = new Vector3(targetPosition.x, startPosition.y, targetPosition.z);

        // 위치 제한 적용
        if (limitCameraMovement)
        {
            adjustedTarget.x = Mathf.Clamp(adjustedTarget.x, minX, maxX);
            adjustedTarget.z = Mathf.Clamp(adjustedTarget.z, minZ, maxZ);
        }

        // FOV 제한 적용
        targetFOV = Mathf.Clamp(targetFOV, maxZoom, minZoom);

        // 이동 거리에 따라 duration 자동 조정 (너무 멀면 더 오래 걸림)
        float distance = Vector3.Distance(startPosition, adjustedTarget);
        float adjustedDuration = Mathf.Max(duration, distance / 100f); // 100 유닛당 1초

        // 부드러운 이동 + FOV 변경
        float elapsed = 0f;
        while (elapsed < adjustedDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / adjustedDuration;

            // EaseInOut 커브 적용
            float smoothT = t < 0.5f
                ? 2f * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            // 위치 이동
            transform.position = Vector3.Lerp(startPosition, adjustedTarget, smoothT);

            // FOV 변경
            if (childCamera != null)
            {
                childCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, smoothT);
            }

            yield return null;
        }

        // 최종 위치 및 FOV로 정확히 설정
        transform.position = adjustedTarget;
        if (childCamera != null)
        {
            childCamera.fieldOfView = targetFOV;
        }

        // 카메라 잠금 해제
        UnlockCamera();
    }
}