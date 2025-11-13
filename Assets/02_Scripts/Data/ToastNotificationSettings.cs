using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 토스트 알림 시스템 설정을 관리하는 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "ToastNotificationSettings", menuName = "InTheEgg/Toast Notification Settings")]
public class ToastNotificationSettings : ScriptableObject
{
    [Header("표시 설정")]
    [Tooltip("동시에 표시할 수 있는 최대 상호작용 토스트 개수")]
    [Range(1, 20)]
    public int maxInteractionToasts = 10;

    [Tooltip("토스트가 표시되는 시간 (초) - Persistent 토스트는 무시")]
    [Range(1f, 10f)]
    public float displayDuration = 3f;

    [Tooltip("페이드 인 시간")]
    [Range(0.1f, 1f)]
    public float fadeInDuration = 0.3f;

    [Tooltip("페이드 아웃 시간")]
    [Range(0.1f, 1f)]
    public float fadeOutDuration = 0.5f;

    [Header("필터링 설정")]
    [Tooltip("동일한 펫 조합의 상호작용 무시 시간 (초)")]
    [Range(5f, 60f)]
    public float duplicateCooldown = 30f;

    [Header("UI 설정")]
    [Tooltip("토스트 표시 위치")]
    public ToastPosition position = ToastPosition.TopRight;

    [Tooltip("토스트 간 간격 (픽셀)")]
    [Range(10f, 150f)]
    public float toastSpacing = 70f;

    [Tooltip("화면 가장자리로부터의 여백")]
    public Vector2 screenMargin = new Vector2(20f, 20f);

    [Header("애니메이션")]
    [Tooltip("슬라이드 인 애니메이션 사용")]
    public bool useSlideAnimation = true;

    [Tooltip("슬라이드 거리")]
    [Range(50f, 300f)]
    public float slideDistance = 200f;

    [Tooltip("호버 시 일시정지")]
    public bool pauseOnHover = true;

    [Header("상호작용 타입별 아이콘")]
    [Tooltip("상호작용 타입별 아이콘 매핑")]
    public List<InteractionIconMapping> interactionIcons = new List<InteractionIconMapping>();

    [Header("사운드")]
    [Tooltip("알림 사운드 사용")]
    public bool useNotificationSound = true;

    [Tooltip("알림 사운드 클립")]
    public AudioClip notificationSound;

    [Tooltip("사운드 볼륨")]
    [Range(0f, 1f)]
    public float soundVolume = 0.5f;

    [Header("집계 모드")]
    [Tooltip("집계 모드 활성화 임계값 (이 개수 이상일 때 집계)")]
    [Range(5, 20)]
    public int aggregationThreshold = 10;

    [Tooltip("집계 윈도우 시간 (초)")]
    [Range(1f, 5f)]
    public float aggregationWindow = 2f;

    [Header("디버그")]
    [Tooltip("디버그 로그 표시")]
    public bool enableDebugLog = false;
}

/// <summary>
/// 토스트 표시 위치
/// </summary>
public enum ToastPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

/// <summary>
/// 상호작용 타입과 아이콘 매핑
/// 한글 이름은 InteractionToastFormatter에서 가져옵니다.
/// </summary>
[System.Serializable]
public class InteractionIconMapping
{
    public InteractionType interactionType;
    public Sprite icon;
    public Color iconTint = Color.white;  // 아이콘 색상
}