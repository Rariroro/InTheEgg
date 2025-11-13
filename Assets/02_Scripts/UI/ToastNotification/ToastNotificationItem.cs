using UnityEngine;
using System;

/// <summary>
/// 토스트 알림 개별 아이템의 데이터 구조
/// </summary>
[System.Serializable]
public class ToastNotificationItem
{
    // 기본 정보
    public string id;                          // 고유 ID (중복 체크용)
    public DateTime timestamp;                 // 생성 시간
    public NotificationType type;              // 알림 타입

    // 펫 정보
    public PetInfo pet1;                       // 첫 번째 펫
    public PetInfo pet2;                       // 두 번째 펫 (상호작용 시)

    // 상호작용 정보
    public InteractionType interactionType;    // 상호작용 타입
    public string customMessage;               // 커스텀 메시지 (선택)

    // 위치 정보
    public Vector3 worldPosition;              // 발생 위치

    // 표시 옵션
    public float displayDuration;              // 표시 시간 (기본값 override)
    public bool isClickable;                   // 클릭 가능 여부
    public bool isPersistent;                  // 지속 표시 여부

    // 콜백
    public Action onClick;                     // 클릭 시 실행할 액션
    public Action onDismiss;                   // 사라질 때 실행할 액션

    // UI 참조 (상호작용 종료 시 토스트 제거용)
    [System.NonSerialized]
    public ToastNotificationUI toastUI;        // 생성된 토스트 UI 참조
    public float displayStartTime;             // 표시 시작 시간

    /// <summary>
    /// 펫 정보 구조체
    /// </summary>
    [System.Serializable]
    public struct PetInfo
    {
        public string name;                    // 펫 이름
        public Sprite icon;                    // 펫 아이콘
        public PetType petType;                // 펫 타입
        public bool isLegendary;               // 레전드 펫 여부
        public bool isFavorite;                // 즐겨찾기 여부
        public GameObject petObject;           // 펫 게임오브젝트 참조

        public PetInfo(PetController pet)
        {
            if (pet != null)
            {
                name = pet.Profile?.name ?? pet.name;
                icon = null;  // PetProfile에 icon 속성 없음
                petType = pet.Profile?.type ?? PetType.Cat;

                // 타입 문자열로 컴포넌트 확인 (컴파일 안전)
                var legComp = pet.gameObject.GetComponent("LegendaryPetController");
                isLegendary = legComp != null;

                isFavorite = CheckIfFavorite(pet);
                petObject = pet.gameObject;
            }
            else
            {
                name = "Unknown";
                icon = null;
                petType = PetType.Cat;
                isLegendary = false;
                isFavorite = false;
                petObject = null;
            }
        }

        private static bool CheckIfFavorite(PetController pet)
        {
            // TODO: 즐겨찾기 시스템 구현 시 연동
            // FavoritePetManager.Instance?.IsFavorite(pet) ?? false;
            return false;
        }
    }

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public ToastNotificationItem()
    {
        id = Guid.NewGuid().ToString();
        timestamp = DateTime.Now;
        type = NotificationType.PetInteraction;
        isClickable = true;
        isPersistent = false;
        displayDuration = 3f;
    }

    /// <summary>
    /// 펫 상호작용 토스트 생성
    /// </summary>
    public static ToastNotificationItem CreateInteractionToast(
        PetController pet1,
        PetController pet2,
        InteractionType interactionType)
    {
        var toast = new ToastNotificationItem
        {
            type = NotificationType.PetInteraction,
            pet1 = new PetInfo(pet1),
            pet2 = new PetInfo(pet2),
            interactionType = interactionType,
            worldPosition = (pet1.transform.position + pet2.transform.position) / 2f,
            isPersistent = true  // 상호작용이 끝날 때까지 지속
        };

        // 고유 ID 생성 (펫 조합 기반)
        toast.id = GenerateInteractionId(pet1, pet2, interactionType);

        // 클릭 액션 설정 (실시간 펫 위치로 이동)
        toast.onClick = () => FocusOnPets(toast.pet1.petObject, toast.pet2.petObject);

        return toast;
    }

    /// <summary>
    /// 시스템 알림 토스트 생성
    /// </summary>
    public static ToastNotificationItem CreateSystemToast(string message)
    {
        return new ToastNotificationItem
        {
            type = NotificationType.System,
            customMessage = message,
            isClickable = false
        };
    }

    /// <summary>
    /// 상호작용 ID 생성 (중복 체크용)
    /// </summary>
    private static string GenerateInteractionId(
        PetController pet1,
        PetController pet2,
        InteractionType type)
    {
        // 펫 이름을 정렬하여 순서 무관하게 만듦
        string name1 = pet1?.name ?? "";
        string name2 = pet2?.name ?? "";

        if (string.Compare(name1, name2) > 0)
        {
            (name1, name2) = (name2, name1);
        }

        return $"{name1}_{name2}_{type}";
    }

    /// <summary>
    /// 펫들의 실시간 위치로 카메라 포커스 (오프셋 적용)
    /// </summary>
    private static void FocusOnPets(GameObject petObject1, GameObject petObject2)
    {
        // 펫 오브젝트 유효성 체크
        if (petObject1 == null || petObject2 == null)
        {
            Debug.LogWarning($"[ToastNotification] 펫 오브젝트가 null입니다.");
            return;
        }

        // PetController 가져오기
        var pet1 = petObject1.GetComponent<PetController>();
        var pet2 = petObject2.GetComponent<PetController>();

        if (pet1 == null || pet2 == null)
        {
            Debug.LogWarning($"[ToastNotification] PetController를 찾을 수 없습니다.");
            return;
        }

        // 실시간 중간 위치 계산
        Vector3 currentPosition = (pet1.transform.position + pet2.transform.position) / 2f;

        // 쿼터뷰 카메라 오프셋 적용 (펫이 화면 중앙에 잘 보이도록)
        // 카메라가 대각선에서 바라보므로 약간 뒤로 이동
        Vector3 cameraOffset = new Vector3(-8f, 0f, -8f);
        Vector3 targetPosition = currentPosition + cameraOffset;

        FocusOnPosition(targetPosition);
    }

    /// <summary>
    /// 위치로 카메라 포커스
    /// </summary>
    private static void FocusOnPosition(Vector3 position)
    {
        // 펫 카메라 모드에서는 작동하지 않음
        var cameraSwitcher = GameObject.FindObjectOfType<PetCameraSwitcherButton>();
        if (cameraSwitcher != null && cameraSwitcher.IsInPetCameraMode())
        {
            Debug.Log($"[ToastNotification] 펫 카메라 모드에서는 이동할 수 없습니다.");
            return;
        }

        // CameraController를 통해 카메라 이동
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            var cameraController = mainCamera.GetComponentInParent<CameraController>();
            if (cameraController != null)
            {
                // 1초 동안 부드럽게 이동
                cameraController.MoveCameraToPosition(position, 1.0f);
                Debug.Log($"[ToastNotification] 카메라를 {position}로 이동 중...");
            }
            else
            {
                Debug.LogWarning($"[ToastNotification] CameraController를 찾을 수 없습니다.");
            }
        }
    }

    /// <summary>
    /// 펫으로 카메라 포커스
    /// </summary>
    private static void FocusOnPet(PetController pet)
    {
        if (pet == null) return;

        var cameraSwitcher = GameObject.FindObjectOfType<PetCameraSwitcherButton>();
        if (cameraSwitcher != null)
        {
            // 펫의 카메라 포인트 찾기
            Transform cameraPoint = pet.transform.Find("CameraPoint");
            if (cameraPoint == null)
            {
                // CameraPoint가 없으면 펫 위치 사용
                GameObject tempPoint = new GameObject("TempCameraPoint");
                tempPoint.transform.SetParent(pet.transform);
                tempPoint.transform.localPosition = new Vector3(0, 2, -3);
                cameraPoint = tempPoint.transform;
            }

            cameraSwitcher.SwitchToPetCamera(cameraPoint);
        }
    }
}

/// <summary>
/// 알림 타입
/// </summary>
public enum NotificationType
{
    PetInteraction,     // 펫 간 상호작용
    PetActivity,        // 펫 개별 활동
    System,             // 시스템 메시지
    Achievement,        // 업적 달성
    Warning            // 경고 메시지
}