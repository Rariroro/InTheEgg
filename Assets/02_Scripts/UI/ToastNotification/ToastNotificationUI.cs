using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

/// <summary>
/// 개별 토스트 알림 UI를 제어하는 컴포넌트
/// </summary>
public class ToastNotificationUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI 요소")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectTransform;

    [Header("콘텐츠")]
    [SerializeField] private TextMeshProUGUI mainText;
    [SerializeField] private Image interactionIcon;
    [SerializeField] private Image backgroundImage;

    [Header("애니메이션")]
    [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // 내부 상태
    private ToastNotificationItem data;
    private ToastNotificationSettings settings;
    private Coroutine displayCoroutine;
    private Coroutine fadeCoroutine;
    private bool isHovered = false;
    private bool isDismissed = false;
    private float remainingTime;
    private Vector2 targetPosition;
    private Vector2 startPosition;

    private void Awake()
    {
        // 컴포넌트 자동 찾기
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        // 초기 상태
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// 토스트 초기화 및 표시
    /// </summary>
    public void Initialize(ToastNotificationItem item, ToastNotificationSettings settings, Vector2 position)
    {
        this.data = item;
        this.settings = settings;
        this.targetPosition = position;

        // 오브젝트 활성화 확인
        if (!gameObject.activeSelf)
        {
            Debug.LogWarning("[ToastNotificationUI] Initialize() 호출되었지만 GameObject가 비활성화 상태입니다. 활성화합니다.");
            gameObject.SetActive(true);
        }

        // UI 내용 설정
        SetupContent();

        // 위치 설정
        SetupPosition();

        // 표시 시작
        Show();
    }

    /// <summary>
    /// UI 콘텐츠 설정
    /// </summary>
    private void SetupContent()
    {
        if (data == null) return;

        // 텍스트 설정
        if (mainText != null)
        {
            mainText.text = GetFormattedText();

            // 우선순위에 따른 텍스트 색상
            mainText.color = GetPriorityColor(data.priorityLevel);
        }

        // 상호작용 아이콘 설정
        if (interactionIcon != null)
        {
            if (data.type == NotificationType.PetInteraction)
            {
                var iconMapping = GetInteractionIcon(data.interactionType);

                if (iconMapping != null && iconMapping.icon != null)
                {
                    interactionIcon.gameObject.SetActive(true);
                    interactionIcon.sprite = iconMapping.icon;
                    interactionIcon.color = Color.white; // 원본 아이콘 색상 유지
                }
                else
                {
                    interactionIcon.gameObject.SetActive(false);
                }
            }
            else
            {
                interactionIcon.gameObject.SetActive(false);
            }
        }

        // 배경 색상 설정
        if (backgroundImage != null)
        {
            backgroundImage.color = GetBackgroundColor(data.priorityLevel);
        }
    }

    /// <summary>
    /// 텍스트 포맷팅
    /// </summary>
    private string GetFormattedText()
    {
        switch (data.type)
        {
            case NotificationType.PetInteraction:
                string pet1Name = TruncateName(data.pet1.name, 6);
                string pet2Name = TruncateName(data.pet2.name, 6);
                string interactionName = InteractionToastFormatter.GetInteractionName(data.interactionType);
                return $"{pet1Name} ↔ {pet2Name} | {interactionName}";

            case NotificationType.System:
            case NotificationType.Achievement:
            case NotificationType.Warning:
                return data.customMessage;

            default:
                return "알림";
        }
    }

    /// <summary>
    /// 이름 자르기 (너무 길면)
    /// </summary>
    private string TruncateName(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxLength) return name;
        return name.Substring(0, maxLength - 1) + "…";
    }

    /// <summary>
    /// 위치 설정
    /// </summary>
    private void SetupPosition()
    {
        if (settings == null || rectTransform == null) return;

        // 슬라이드 애니메이션 사용 시 시작 위치 설정
        if (settings.useSlideAnimation)
        {
            startPosition = targetPosition;

            // 오른쪽에서 슬라이드인
            if (settings.position == ToastPosition.TopRight ||
                settings.position == ToastPosition.MiddleRight ||
                settings.position == ToastPosition.BottomRight)
            {
                startPosition.x += settings.slideDistance;
            }
            // 왼쪽에서 슬라이드인
            else if (settings.position == ToastPosition.TopLeft ||
                     settings.position == ToastPosition.MiddleLeft ||
                     settings.position == ToastPosition.BottomLeft)
            {
                startPosition.x -= settings.slideDistance;
            }
            // 위/아래에서 슬라이드인
            else
            {
                startPosition.y += settings.slideDistance;
            }

            rectTransform.anchoredPosition = startPosition;
        }
        else
        {
            rectTransform.anchoredPosition = targetPosition;
        }
    }

    /// <summary>
    /// 토스트 표시
    /// </summary>
    public void Show()
    {
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
        }

        displayCoroutine = StartCoroutine(DisplayRoutine());
    }

    /// <summary>
    /// 표시 루틴
    /// </summary>
    private IEnumerator DisplayRoutine()
    {
        isDismissed = false;

        // 페이드 인 & 슬라이드
        yield return StartCoroutine(FadeIn());

        // 표시 유지
        remainingTime = data?.displayDuration ?? settings?.displayDuration ?? 3f;

        while (remainingTime > 0 && !isDismissed)
        {
            // 호버 중이면 시간 정지
            if (!isHovered || !settings.pauseOnHover)
            {
                remainingTime -= Time.deltaTime;
            }

            yield return null;
        }

        // 페이드 아웃
        if (!isDismissed)
        {
            yield return StartCoroutine(FadeOut());
        }

        // 제거
        OnDismissed();
    }

    /// <summary>
    /// 페이드 인 애니메이션
    /// </summary>
    private IEnumerator FadeIn()
    {
        float duration = settings?.fadeInDuration ?? 0.3f;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 알파 페이드
            if (canvasGroup != null)
            {
                canvasGroup.alpha = fadeInCurve.Evaluate(t);
            }

            // 슬라이드
            if (settings != null && settings.useSlideAnimation && rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    slideCurve.Evaluate(t)
                );
            }

            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1;

        if (rectTransform != null)
            rectTransform.anchoredPosition = targetPosition;
    }

    /// <summary>
    /// 페이드 아웃 애니메이션
    /// </summary>
    private IEnumerator FadeOut()
    {
        float duration = settings?.fadeOutDuration ?? 0.5f;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 알파 페이드
            if (canvasGroup != null)
            {
                canvasGroup.alpha = fadeOutCurve.Evaluate(t);
            }

            // 스케일 다운 (선택적)
            if (rectTransform != null)
            {
                float scale = Mathf.Lerp(1f, 0.95f, t);
                rectTransform.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0;
    }

    /// <summary>
    /// 즉시 제거
    /// </summary>
    public void Dismiss()
    {
        if (isDismissed) return;

        isDismissed = true;

        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(QuickFadeOut());
    }

    /// <summary>
    /// 빠른 페이드 아웃
    /// </summary>
    private IEnumerator QuickFadeOut()
    {
        float duration = 0.2f;
        float startAlpha = canvasGroup?.alpha ?? 1f;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, t);
            }

            yield return null;
        }

        OnDismissed();
    }

    /// <summary>
    /// 제거 완료
    /// </summary>
    private void OnDismissed()
    {
        // 콜백 실행
        data?.onDismiss?.Invoke();

        // 매니저에 알림
        ToastNotificationManager.Instance?.HandleToastDismissed(this);

        // 오브젝트 풀로 반환 또는 삭제
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 위치 업데이트 (스택 재정렬용)
    /// </summary>
    public void UpdatePosition(Vector2 newPosition, float duration = 0.3f)
    {
        if (rectTransform == null) return;

        StopCoroutine(nameof(MoveToPosition));
        StartCoroutine(MoveToPosition(newPosition, duration));
    }

    private IEnumerator MoveToPosition(Vector2 newPosition, float duration)
    {
        Vector2 startPos = rectTransform.anchoredPosition;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rectTransform.anchoredPosition = Vector2.Lerp(
                startPos,
                newPosition,
                slideCurve.Evaluate(t)
            );

            yield return null;
        }

        rectTransform.anchoredPosition = newPosition;
        targetPosition = newPosition;
    }

    #region UI 이벤트

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

        // 호버 효과
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        // 스케일 업
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one * 1.05f;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        // 스케일 복원
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (data != null && data.isClickable)
        {
            // 클릭 콜백 실행
            data.onClick?.Invoke();

            // 클릭 후 제거 (선택적)
            if (!data.isPersistent)
            {
                Dismiss();
            }
        }
    }

    #endregion

    #region 헬퍼 메서드

    private Color GetPriorityColor(NotificationPriority priority)
    {
        switch (priority)
        {
            case NotificationPriority.Critical:
                return new Color(1f, 0.8f, 0f); // 금색
            case NotificationPriority.High:
                return new Color(0.5f, 0.8f, 1f); // 하늘색
            case NotificationPriority.Medium:
                return Color.white;
            case NotificationPriority.Low:
                return new Color(0.8f, 0.8f, 0.8f);
            default:
                return new Color(0.6f, 0.6f, 0.6f);
        }
    }

    private Color GetBackgroundColor(NotificationPriority priority)
    {
        Color baseColor = new Color(0, 0, 0, 0.7f);

        switch (priority)
        {
            case NotificationPriority.Critical:
                return new Color(0.2f, 0.15f, 0, 0.8f); // 어두운 금색
            case NotificationPriority.High:
                return new Color(0, 0.1f, 0.2f, 0.8f); // 어두운 파랑
            default:
                return baseColor;
        }
    }

    private InteractionIconMapping GetInteractionIcon(InteractionType type)
    {
        if (settings == null || settings.interactionIcons == null) return null;

        return settings.interactionIcons.Find(x => x.interactionType == type);
    }

    #endregion
}