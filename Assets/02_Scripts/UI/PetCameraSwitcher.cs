using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PetCameraSwitcher : MonoBehaviour
{
    public static PetCameraSwitcher Instance { get; private set; }

    // UI 버튼과 텍스트 컴포넌트 (인스펙터에서 할당)
    public Button petCameraButton;
    public TMP_Text petCameraButtonText;

    // 기존에 사용한 피드백 텍스트 변수 (PetGatheringController와 동일한 역할)
    public TMP_Text feedbackText;

    // 펫 카메라 모드 활성화 플래그 (펫 터치 후 실제 전환)
    [HideInInspector] public bool petCameraModeActivated = false;

    private Camera mainCamera;
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private bool isInPetCameraMode = false;

    // 카메라 회전 관련 변수
    private float petCameraRotationSpeed = 50f;
    private float currentYaw = 0f;
    private float currentPitch = 0f;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
        
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            originalParent = mainCamera.transform.parent;
            originalLocalPosition = mainCamera.transform.localPosition;
            originalLocalRotation = mainCamera.transform.localRotation;
        }
    }

    void Update()
    {
        if (isInPetCameraMode)
        {
            // 터치 입력이 있을 경우
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Moved)
                {
                    float touchRotationSpeed = petCameraRotationSpeed * 0.1f;
                    currentYaw += touch.deltaPosition.x * touchRotationSpeed * Time.deltaTime;
                    currentPitch -= touch.deltaPosition.y * touchRotationSpeed * Time.deltaTime;
                    currentPitch = Mathf.Clamp(currentPitch, -40f, 40f);
                    currentYaw = Mathf.Clamp(currentYaw, -40f, 40f);
                }
            }
            else // 키보드 입력 처리 (에디터나 PC에서)
            {
                float horizontalInput = Input.GetAxis("Horizontal");
                float verticalInput = Input.GetAxis("Vertical");

                currentYaw += horizontalInput * petCameraRotationSpeed * Time.deltaTime;
                currentPitch -= verticalInput * petCameraRotationSpeed * Time.deltaTime;
                currentPitch = Mathf.Clamp(currentPitch, -40f, 40f);
                currentYaw = Mathf.Clamp(currentYaw, -40f, 40f);
            }
            
            mainCamera.transform.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        }
    }

    // 펫 카메라 모드 활성화
    public void ActivatePetCameraMode()
    {
        petCameraModeActivated = true;
    }

    // 펫 카메라 모드 비활성화
    public void DeactivatePetCameraMode()
    {
        petCameraModeActivated = false;
    }

    // 펫의 CameraPoint로 카메라 전환 (가로 모드)
    public void SwitchToPetCamera(Transform petCameraPoint)
    {
        if (mainCamera == null || petCameraPoint == null)
            return;

        Screen.orientation = ScreenOrientation.LandscapeLeft;

        CameraController camController = mainCamera.GetComponent<CameraController>();
        if (camController != null)
            camController.enabled = false;
        
        mainCamera.transform.SetParent(petCameraPoint);
        mainCamera.transform.localPosition = Vector3.zero;
        mainCamera.transform.localRotation = Quaternion.identity;
        
        isInPetCameraMode = true;
        petCameraModeActivated = false;

        if (petCameraButtonText != null)
            petCameraButtonText.text = "Camera";
              // 피드백 텍스트 숨김
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
    }

    // 기존 쿼터뷰 카메라로 복귀 (세로 모드)
    public void SwitchBackToMainCamera()
    {
        if (mainCamera == null)
            return;
        
        Screen.orientation = ScreenOrientation.Portrait;
        
        mainCamera.transform.SetParent(originalParent);
        mainCamera.transform.localPosition = originalLocalPosition;
        mainCamera.transform.localRotation = originalLocalRotation;
        
        CameraController camController = mainCamera.GetComponent<CameraController>();
        if (camController != null)
            camController.enabled = true;
        
        isInPetCameraMode = false;
        petCameraModeActivated = false;

        if (petCameraButtonText != null)
            petCameraButtonText.text = "Pet Camera";
    }

    // 피드백 텍스트를 일정 시간 후 숨김 처리하는 코루틴
    private IEnumerator HideFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);
    }

    // 모드 토글 함수 (UI 버튼 OnClick 이벤트에 연결)
    public void ToggleCameraMode()
    {
        if (IsInPetCameraMode())
        {
            // 이미 펫 카메라 모드인 경우 기존 쿼터뷰로 복귀
            SwitchBackToMainCamera();
        }
        else if (petCameraModeActivated)
        {
            // 펫 카메라 모드 대기 상태인 경우 취소
            DeactivatePetCameraMode();
            
            // 피드백 텍스트 숨김
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
            
            // 버튼 텍스트 원래대로 복원
            if (petCameraButtonText != null)
                petCameraButtonText.text = "Pet Camera";
        }
        else
        {
            // 펫 카메라 모드로 전환 대기 상태 활성화
            ActivatePetCameraMode();

            // 피드백 텍스트에 "원하는 펫을 선택하세요" 메시지 표시
            if (feedbackText != null)
            {
                feedbackText.text = "원하는 펫을 선택하세요";
                feedbackText.gameObject.SetActive(true);
                // 자동으로 메시지를 숨기지 않음 (사용자가 취소하거나 펫을 선택할 때까지 표시)
            }
            
            // 버튼 텍스트를 "취소"로 변경
            if (petCameraButtonText != null)
                petCameraButtonText.text = "취소";
        }
    }
    public bool IsInPetCameraMode()
    {
        return isInPetCameraMode;
    }
}
