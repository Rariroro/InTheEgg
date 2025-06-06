using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 0.05f;
    public float zoomSpeed = 2f; // 줌 속도 감소
    public float minZoom = 50f; // 가장 축소됐을 때의 FOV (더 넓은 시야)
    public float maxZoom = 20f; // 가장 확대됐을 때의 FOV (더 좁은 시야)
    public float mouseDragSpeed = 0.5f;

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
            Debug.Log($"Initial FOV set to: {childCamera.fieldOfView}");

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
    }

    void HandleMobileInput()
    {
        if (Input.touchCount == 1)
        {
            PanCamera();
        }
        else if (Input.touchCount == 2)
        {
            ZoomCamera();
        }
    }

    void HandleEditorInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Debug.Log("HandleEditorInput :1");

            isDragging = true;
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            // Debug.Log("HandleEditorInput :2");

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
}