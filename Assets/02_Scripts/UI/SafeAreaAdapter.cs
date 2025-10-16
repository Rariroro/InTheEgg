// SafeAreaAdapter.cs
using UnityEngine;

/// <summary>
/// iOS의 Dynamic Island, 노치, 화면 모서리를 피하기 위해 RectTransform을 자동 조정합니다.
///
/// 사용 방법:
/// 1. Canvas 아래에 "SafeArea" 빈 오브젝트 생성
/// 2. 이 스크립트를 SafeArea 오브젝트에 추가
/// 3. 모든 UI 요소를 SafeArea 안에 배치
///
/// 참고: Unity 2017.2.1 이상에서 Screen.safeArea API 지원
/// </summary>
public class SafeAreaAdapter : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // RectTransform이 없으면 추가
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
        }

        // Anchors를 stretch로 설정 (0,0 ~ 1,1)
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
    }

    void Start()
    {
        // 초기 Safe Area 적용
        ApplySafeArea();
    }

    void Update()
    {
        // Safe Area나 화면 크기가 변경되었는지 확인 (화면 회전 등)
        Vector2Int currentScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (lastSafeArea != Screen.safeArea || lastScreenSize != currentScreenSize)
        {
            ApplySafeArea();
        }
    }

    /// <summary>
    /// Screen.safeArea를 기준으로 RectTransform의 offsetMin/offsetMax 조정
    /// </summary>
    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;

        // 화면 크기에 대한 Safe Area의 비율 계산
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // Anchor를 Safe Area 기준으로 설정
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;

        // Offset은 0으로 (Anchor에 딱 맞춤)
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        // 마지막 값 저장
        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        #if UNITY_EDITOR || DEBUG
        Debug.Log($"[SafeAreaAdapter] Safe Area 적용: {safeArea} (Screen: {Screen.width}x{Screen.height})");
        #endif
    }

    /// <summary>
    /// 런타임에서 강제로 Safe Area 다시 적용
    /// </summary>
    public void RefreshSafeArea()
    {
        ApplySafeArea();
    }
}
