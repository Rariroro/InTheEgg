using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PetCameraSwitcherButton : MonoBehaviour
{
    public static PetCameraSwitcherButton Instance { get; private set; }

    // UI 버튼과 텍스트 컴포넌트 (인스펙터에서 할당)
    public Button petCameraButton;
    public TMP_Text petCameraButtonText;

    [Header("Button Visual Settings")]
    public Image iconImage;                    // Icon 오브젝트의 Image 컴포넌트
    public Image buttonBackgroundImage;        // Button 오브젝트의 Image 컴포넌트
    public Sprite petCameraIcon;              // Pet Camera 아이콘 (기본 상태)
    public Sprite cameraIcon;                 // Camera 아이콘 (펫 카메라 모드)
    public Sprite cancelIcon;                  // 취소 아이콘 (대기 상태)
    public Sprite normalButtonBackground;     // 일반 버튼 배경
    public Sprite activeButtonBackground;     // 활성화 버튼 배경
    public Sprite cancelButtonBackground;     // 취소 버튼 배경

    [Header("Fade Settings")]
    public Image fadePanel;                       // 화면 전환용 검은 패널 (Inspector에서 할당)
    private float fadeDuration = 0.2f;            // 페이드 인/아웃 시간 (0.15초 → 0.2초로 증가)

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

    // 카메라 상태 저장 변수
    private float savedMainCameraFOV = 50f;  // 기본 카메라의 FOV 저장
    private const float PET_CAMERA_DEFAULT_FOV = 60f;  // 펫 카메라 기본 FOV

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

        // 버튼 비주얼 초기화
        UpdateButtonVisual("petCamera");
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

    // 페이드 인 (검은 화면으로 전환)
    private IEnumerator FadeToBlack(float duration)
    {
        if (fadePanel == null) yield break;

        fadePanel.gameObject.SetActive(true);
        Color color = fadePanel.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 1f, elapsed / duration);
            fadePanel.color = color;
            yield return null;
        }

        color.a = 1f;
        fadePanel.color = color;
    }

    // 페이드 아웃 (검은 화면에서 밝아짐)
    private IEnumerator FadeFromBlack(float duration)
    {
        if (fadePanel == null) yield break;

        Color color = fadePanel.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsed / duration);
            fadePanel.color = color;
            yield return null;
        }

        color.a = 0f;
        fadePanel.color = color;
        fadePanel.gameObject.SetActive(false);
    }

    // 펫의 CameraPoint로 카메라 전환 (가로 모드)
    public void SwitchToPetCamera(Transform petCameraPoint)
    {
        if (mainCamera == null || petCameraPoint == null)
            return;

        StartCoroutine(SwitchToPetCameraWithFade(petCameraPoint));
    }

    private IEnumerator SwitchToPetCameraWithFade(Transform petCameraPoint)
    {
        // 1. 페이드 인 (검은 화면으로)
        yield return FadeToBlack(fadeDuration);

        // 2. 화면 회전 시작
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        // 3. 화면 회전 완료까지 동적 대기
        float maxWaitTime = 1.0f;
        float elapsedTime = 0f;
        while (Screen.orientation != ScreenOrientation.LandscapeLeft && elapsedTime < maxWaitTime)
        {
            yield return null;
            elapsedTime += Time.deltaTime;
        }

        // 4. 추가 안정화 대기
        yield return new WaitForSeconds(0.1f);

        // 5. 카메라 설정
        // 기본 카메라의 현재 FOV 저장
        savedMainCameraFOV = mainCamera.fieldOfView;

        CameraController camController = mainCamera.GetComponent<CameraController>();
        if (camController != null)
            camController.enabled = false;

        mainCamera.transform.SetParent(petCameraPoint);
        mainCamera.transform.localPosition = Vector3.zero;
        mainCamera.transform.localRotation = Quaternion.identity;

        // 펫 카메라용 기본 FOV로 설정
        mainCamera.fieldOfView = PET_CAMERA_DEFAULT_FOV;

        // 펫 카메라 회전 상태 초기화
        currentYaw = 0f;
        currentPitch = 0f;

        isInPetCameraMode = true;
        petCameraModeActivated = false;

        if (petCameraButtonText != null)
            petCameraButtonText.text = "Camera";

        UpdateButtonVisual("camera");

        // 피드백 텍스트 숨김
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }

        // 6. 페이드 아웃 (펫 시점 공개)
        yield return FadeFromBlack(fadeDuration);
    }

    // 기존 쿼터뷰 카메라로 복귀 (세로 모드)
    public void SwitchBackToMainCamera()
    {
        if (mainCamera == null)
            return;

        StartCoroutine(SwitchBackToMainCameraWithFade());
    }

    private IEnumerator SwitchBackToMainCameraWithFade()
    {
        // 1. 페이드 인 (검은 화면으로)
        yield return FadeToBlack(fadeDuration);

        // 2. 화면 회전 시작
        Screen.orientation = ScreenOrientation.Portrait;

        // 3. 화면 회전 완료까지 동적 대기
        float maxWaitTime = 1.0f;
        float elapsedTime = 0f;
        while (Screen.orientation != ScreenOrientation.Portrait && elapsedTime < maxWaitTime)
        {
            yield return null;
            elapsedTime += Time.deltaTime;
        }

        // 4. 추가 안정화 대기
        yield return new WaitForSeconds(0.1f);

        // 5. 카메라 설정 복원
        mainCamera.transform.SetParent(originalParent);
        mainCamera.transform.localPosition = originalLocalPosition;
        mainCamera.transform.localRotation = originalLocalRotation;

        // 저장해둔 기본 카메라 FOV 복원
        mainCamera.fieldOfView = savedMainCameraFOV;

        CameraController camController = mainCamera.GetComponent<CameraController>();
        if (camController != null)
            camController.enabled = true;

        isInPetCameraMode = false;
        petCameraModeActivated = false;

        if (petCameraButtonText != null)
            petCameraButtonText.text = "Pet Camera";

        UpdateButtonVisual("petCamera");

        // 6. 페이드 아웃 (메인 카메라 공개)
        yield return FadeFromBlack(fadeDuration);
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

            UpdateButtonVisual("petCamera");
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

            UpdateButtonVisual("cancel");
        }
    }
    public bool IsInPetCameraMode()
    {
        return isInPetCameraMode;
    }

    private void UpdateButtonVisual(string mode)
    {
        if (iconImage != null && buttonBackgroundImage != null)
        {
            switch (mode)
            {
                case "petCamera":  // 기본 상태
                    iconImage.sprite = petCameraIcon;
                    buttonBackgroundImage.sprite = normalButtonBackground;
                    break;
                case "camera":  // 펫 카메라 모드 활성화 시
                    iconImage.sprite = cameraIcon;
                    buttonBackgroundImage.sprite = activeButtonBackground;
                    break;
                case "cancel":  // 대기 상태
                    iconImage.sprite = cancelIcon;
                    buttonBackgroundImage.sprite = cancelButtonBackground;
                    break;
            }
        }
    }
}
