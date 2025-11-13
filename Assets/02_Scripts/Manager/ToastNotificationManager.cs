using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// 토스트 알림 시스템 중앙 관리자
/// </summary>
public class ToastNotificationManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static ToastNotificationManager Instance { get; private set; }

    [Header("설정")]
    [SerializeField] private ToastNotificationSettings settings;
    [SerializeField] private bool useDefaultSettingsIfNull = true;

    [Header("UI 프리팹")]
    [SerializeField] private GameObject toastPrefab;
    [SerializeField] private GameObject toastCanvasPrefab;

    [Header("UI 컨테이너")]
    [SerializeField] private Canvas toastCanvas;
    [SerializeField] private RectTransform toastContainer;

    [Header("오브젝트 풀")]
    [SerializeField] private int poolSize = 10;
    [SerializeField] private bool expandPoolIfNeeded = true;

    [Header("디버그")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool showAllNotifications = false; // 모든 알림 표시 (필터 무시)

    // 내부 상태
    private Queue<ToastNotificationItem> notificationQueue = new Queue<ToastNotificationItem>();
    private List<ToastNotificationUI> activeToasts = new List<ToastNotificationUI>();
    private List<ToastNotificationUI> activeInteractionToasts = new List<ToastNotificationUI>(); // 상호작용 토스트 별도 관리
    private Dictionary<string, DateTime> duplicateTracker = new Dictionary<string, DateTime>();
    private Queue<ToastNotificationUI> toastPool = new Queue<ToastNotificationUI>();

    // 집계 모드
    private List<ToastNotificationItem> aggregationBuffer = new List<ToastNotificationItem>();
    private float aggregationTimer = 0f;
    private bool isAggregating = false;

    // 상호작용 토스트 제한 (기본값, Settings에서 오버라이드 가능)
    private int MaxInteractionToasts => settings?.maxInteractionToasts ?? 5;

    // 이벤트
    public event Action<ToastNotificationItem> OnToastShown;
    public event Action<ToastNotificationItem> OnToastDismissed;
    public event Action<int> OnQueueSizeChanged;

    private void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        // 설정 로드
        if (settings == null && useDefaultSettingsIfNull)
        {
            settings = Resources.Load<ToastNotificationSettings>("ToastNotificationSettings");
            if (settings == null)
            {
                Debug.LogWarning("[ToastNotificationManager] Settings를 찾을 수 없습니다. 기본값을 사용합니다.");
                CreateDefaultSettings();
            }
        }

        // 캔버스 설정
        SetupCanvas();

        // 오브젝트 풀 초기화
        InitializePool();

        // 코루틴 시작
        StartCoroutine(ProcessQueue());
        StartCoroutine(CleanupTrackers());
    }

    private void CreateDefaultSettings()
    {
        // 런타임에 기본 설정 생성 (저장은 안됨)
        settings = ScriptableObject.CreateInstance<ToastNotificationSettings>();
        settings.maxInteractionToasts = 10;
        settings.displayDuration = 3f;
    }

    private void SetupCanvas()
    {
        // 캔버스가 없으면 생성
        if (toastCanvas == null)
        {
            if (toastCanvasPrefab != null)
            {
                GameObject canvasObj = Instantiate(toastCanvasPrefab);
                toastCanvas = canvasObj.GetComponent<Canvas>();
            }
            else
            {
                // 기본 캔버스 생성
                GameObject canvasObj = new GameObject("ToastNotificationCanvas");
                toastCanvas = canvasObj.AddComponent<Canvas>();
                toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                toastCanvas.sortingOrder = 100; // 상위 레이어

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObj.AddComponent<GraphicRaycaster>();
            }
        }

        // 토스트 컨테이너 생성
        if (toastContainer == null)
        {
            GameObject containerObj = new GameObject("ToastContainer");
            containerObj.transform.SetParent(toastCanvas.transform, false);
            toastContainer = containerObj.AddComponent<RectTransform>();

            // 위치 설정
            SetupContainerPosition();
        }
    }

    private void SetupContainerPosition()
    {
        if (toastContainer == null || settings == null) return;

        // 앵커와 피벗 설정
        switch (settings.position)
        {
            case ToastPosition.TopLeft:
                toastContainer.anchorMin = new Vector2(0, 1);
                toastContainer.anchorMax = new Vector2(0, 1);
                toastContainer.pivot = new Vector2(0, 1);
                break;
            case ToastPosition.TopCenter:
                toastContainer.anchorMin = new Vector2(0.5f, 1);
                toastContainer.anchorMax = new Vector2(0.5f, 1);
                toastContainer.pivot = new Vector2(0.5f, 1);
                break;
            case ToastPosition.TopRight:
                toastContainer.anchorMin = new Vector2(1, 1);
                toastContainer.anchorMax = new Vector2(1, 1);
                toastContainer.pivot = new Vector2(1, 1);
                break;
            case ToastPosition.BottomRight:
                toastContainer.anchorMin = new Vector2(1, 0);
                toastContainer.anchorMax = new Vector2(1, 0);
                toastContainer.pivot = new Vector2(1, 0);
                break;
            // ... 다른 위치들
        }

        // 마진 적용
        toastContainer.anchoredPosition = new Vector2(
            settings.position.ToString().Contains("Right") ? -settings.screenMargin.x : settings.screenMargin.x,
            settings.position.ToString().Contains("Top") ? -settings.screenMargin.y : settings.screenMargin.y
        );
    }

    private void InitializePool()
    {
        if (toastPrefab == null)
        {
            Debug.LogError("[ToastNotificationManager] Toast Prefab이 설정되지 않았습니다!");
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            CreatePooledToast();
        }
    }

    private ToastNotificationUI CreatePooledToast()
    {
        GameObject toastObj = Instantiate(toastPrefab, toastContainer);
        ToastNotificationUI toastUI = toastObj.GetComponent<ToastNotificationUI>();

        if (toastUI == null)
        {
            toastUI = toastObj.AddComponent<ToastNotificationUI>();
        }

        toastObj.SetActive(false);
        toastPool.Enqueue(toastUI);
        return toastUI;
    }

    #region Public API

    /// <summary>
    /// 펫 상호작용 알림 추가
    /// </summary>
    public void ShowInteractionToast(PetController pet1, PetController pet2, InteractionType interactionType)
    {
        if (pet1 == null || pet2 == null) return;

        // 상호작용 토스트 5개 제한 체크
        if (activeInteractionToasts.Count >= MaxInteractionToasts)
        {
            // 가장 오래된 상호작용 토스트 제거
            if (activeInteractionToasts.Count > 0)
            {
                var oldestToast = activeInteractionToasts[0];
                oldestToast.Dismiss();
                activeInteractionToasts.RemoveAt(0);

                // if (debugMode)
                    // Debug.Log($"[ToastNotificationManager] 상호작용 토스트 제한 도달. 가장 오래된 토스트 제거");
            }
        }

        var toast = ToastNotificationItem.CreateInteractionToast(pet1, pet2, interactionType);
        EnqueueNotification(toast);
    }

    /// <summary>
    /// 시스템 메시지 알림 추가
    /// </summary>
    public void ShowSystemToast(string message)
    {
        var toast = ToastNotificationItem.CreateSystemToast(message);
        EnqueueNotification(toast);
    }

    /// <summary>
    /// 커스텀 토스트 추가
    /// </summary>
    public void ShowCustomToast(ToastNotificationItem toast)
    {
        if (toast != null)
        {
            EnqueueNotification(toast);
        }
    }

    /// <summary>
    /// 상호작용 종료 시 해당 토스트 제거 (하이브리드 방식: 최소 표시 시간 보장)
    /// </summary>
    public void DismissInteractionToast(PetController pet1, PetController pet2)
    {
        if (pet1 == null || pet2 == null) return;

        const float MIN_DISPLAY_TIME = 2f; // 최소 2초 표시 보장

        // 펫 쌍에 해당하는 토스트 찾기
        for (int i = activeInteractionToasts.Count - 1; i >= 0; i--)
        {
            var toastUI = activeInteractionToasts[i];
            if (toastUI == null) continue;

            // 토스트의 원본 데이터 확인 필요 - ToastNotificationUI에서 item 참조 가능하도록 수정 필요
            // 임시로 펫 이름으로 비교 (ToastNotificationUI에 GetItem 메서드 추가 필요)
            var item = toastUI.GetNotificationItem();
            if (item == null) continue;

            // 펫 쌍 매칭 (순서 무관) - GameObject 참조로 비교
            bool isPet1Match = (item.pet1.petObject == pet1.gameObject || item.pet1.petObject == pet2.gameObject);
            bool isPet2Match = (item.pet2.petObject == pet1.gameObject || item.pet2.petObject == pet2.gameObject);

            if (isPet1Match && isPet2Match)
            {
                // 최소 표시 시간 체크
                float displayedTime = Time.time - item.displayStartTime;
                if (displayedTime >= MIN_DISPLAY_TIME)
                {
                    // 즉시 제거
                    toastUI.Dismiss();
                    activeInteractionToasts.RemoveAt(i);
                    if (debugMode)
                        Debug.Log($"[ToastNotificationManager] 상호작용 종료로 토스트 제거: {item.pet1.name} ↔ {item.pet2.name}");
                }
                else
                {
                    // 최소 시간 미달 - 나머지 시간만큼 대기 후 제거
                    float remainingTime = MIN_DISPLAY_TIME - displayedTime;
                    StartCoroutine(DismissAfterDelay(toastUI, remainingTime));
                    if (debugMode)
                        Debug.Log($"[ToastNotificationManager] 상호작용 종료 - {remainingTime:F1}초 후 토스트 제거 예약");
                }
                break; // 하나만 찾으면 종료
            }
        }
    }

    /// <summary>
    /// 지연 후 토스트 제거
    /// </summary>
    private IEnumerator DismissAfterDelay(ToastNotificationUI toastUI, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (toastUI != null && toastUI.gameObject.activeSelf)
        {
            toastUI.Dismiss();
            activeInteractionToasts.Remove(toastUI);
        }
    }

    #endregion

    #region Queue Management

    private void EnqueueNotification(ToastNotificationItem item)
    {
        // 중복 체크만 수행
        if (IsDuplicate(item))
        {
            // if (debugMode)
                // Debug.Log($"[ToastNotificationManager] 중복 필터링: {item.id}");
            return;
        }

        // 집계 모드 체크
        if (settings != null && notificationQueue.Count >= settings.aggregationThreshold)
        {
            StartAggregation(item);
            return;
        }

        // 큐에 추가
        notificationQueue.Enqueue(item);
        OnQueueSizeChanged?.Invoke(notificationQueue.Count);

        // if (debugMode)
            // Debug.Log($"[ToastNotificationManager] 알림 큐에 추가: {item.id}");
    }

    private bool IsDuplicate(ToastNotificationItem item)
    {
        if (settings == null) return false;

        if (duplicateTracker.TryGetValue(item.id, out DateTime lastTime))
        {
            if ((DateTime.Now - lastTime).TotalSeconds < settings.duplicateCooldown)
                return true;
        }

        // 트래커 업데이트
        duplicateTracker[item.id] = DateTime.Now;
        return false;
    }


    #endregion

    #region Display Management

    private IEnumerator ProcessQueue()
    {
        while (true)
        {
            // 큐에 항목이 있고, 동시 표시 제한을 넘지 않으면
            if (notificationQueue.Count > 0 &&
                activeInteractionToasts.Count < MaxInteractionToasts)
            {
                var item = notificationQueue.Dequeue();
                ShowToast(item);
                OnQueueSizeChanged?.Invoke(notificationQueue.Count);

                // 약간의 딜레이로 순차적 표시
                yield return new WaitForSeconds(0.1f);
            }

            yield return null;
        }
    }

    private void ShowToast(ToastNotificationItem item)
    {
        // 풀에서 토스트 가져오기
        ToastNotificationUI toastUI = GetFromPool();
        if (toastUI == null)
        {
            Debug.LogWarning("[ToastNotificationManager] 토스트 풀이 비었습니다!");
            return;
        }

        // 위치 계산
        Vector2 position = CalculateToastPosition(activeToasts.Count);

        // 초기화 및 표시
        toastUI.gameObject.SetActive(true);
        toastUI.Initialize(item, settings, position);

        // ToastNotificationItem에 UI 참조와 시작 시간 저장 (상호작용 종료 시 제거용)
        item.toastUI = toastUI;
        item.displayStartTime = Time.time;

        // 활성 리스트에 추가
        activeToasts.Add(toastUI);

        // 상호작용 토스트인 경우 별도 관리
        if (item.type == NotificationType.PetInteraction)
        {
            activeInteractionToasts.Add(toastUI);
        }

        // 이벤트 발생
        OnToastShown?.Invoke(item);

        // if (debugMode)
            // Debug.Log($"[ToastNotificationManager] 토스트 표시: {item.id}");
    }

    private Vector2 CalculateToastPosition(int index)
    {
        float spacing = settings?.toastSpacing ?? 70f;
        float yOffset = index * spacing;

        // 위치에 따라 y 방향 결정
        if (settings != null && settings.position.ToString().Contains("Bottom"))
        {
            yOffset = -yOffset; // 아래에서 위로
        }

        return new Vector2(0, -yOffset);
    }

    /// <summary>
    /// 토스트가 닫혔을 때 호출
    /// </summary>
    public void HandleToastDismissed(ToastNotificationUI toast)
    {
        if (toast == null) return;

        // 활성 리스트에서 제거
        int index = activeToasts.IndexOf(toast);
        if (index >= 0)
        {
            activeToasts.RemoveAt(index);

            // 나머지 토스트 위치 재조정
            UpdateToastPositions();
        }

        // 상호작용 토스트 리스트에서도 제거
        if (activeInteractionToasts.Contains(toast))
        {
            activeInteractionToasts.Remove(toast);
        }

        // 풀로 반환
        ReturnToPool(toast);
    }

    private void UpdateToastPositions()
    {
        for (int i = 0; i < activeToasts.Count; i++)
        {
            Vector2 newPosition = CalculateToastPosition(i);
            activeToasts[i].UpdatePosition(newPosition);
        }
    }

    #endregion

    #region Pool Management

    private ToastNotificationUI GetFromPool()
    {
        if (toastPool.Count > 0)
        {
            return toastPool.Dequeue();
        }
        else if (expandPoolIfNeeded)
        {
            return CreatePooledToast();
        }

        return null;
    }

    private void ReturnToPool(ToastNotificationUI toast)
    {
        if (toast == null) return;

        toast.gameObject.SetActive(false);
        toastPool.Enqueue(toast);
    }

    #endregion

    #region Aggregation

    private void StartAggregation(ToastNotificationItem item)
    {
        if (!isAggregating)
        {
            isAggregating = true;
            aggregationTimer = 0f;
            aggregationBuffer.Clear();
            StartCoroutine(AggregationRoutine());
        }

        aggregationBuffer.Add(item);
    }

    private IEnumerator AggregationRoutine()
    {
        float aggregationWindow = settings?.aggregationWindow ?? 2f;

        while (aggregationTimer < aggregationWindow)
        {
            aggregationTimer += Time.deltaTime;
            yield return null;
        }

        // 집계된 알림 생성
        if (aggregationBuffer.Count > 0)
        {
            ShowAggregatedToast();
        }

        isAggregating = false;
        aggregationBuffer.Clear();
    }

    private void ShowAggregatedToast()
    {
        // 상호작용 타입별로 그룹화
        var groups = aggregationBuffer
            .Where(x => x.type == NotificationType.PetInteraction)
            .GroupBy(x => x.interactionType);

        foreach (var group in groups)
        {
            string message = InteractionToastFormatter.FormatAggregatedMessage(
                group.Count(),
                group.Key
            );

            ShowSystemToast(message);
        }

        // 나머지 알림 요약
        int otherCount = aggregationBuffer.Count(x => x.type != NotificationType.PetInteraction);
        if (otherCount > 0)
        {
            ShowSystemToast($"{otherCount}개의 다른 활동 진행 중");
        }
    }

    #endregion

    #region Cleanup

    private IEnumerator CleanupTrackers()
    {
        while (true)
        {
            yield return new WaitForSeconds(60f); // 1분마다 정리

            // 오래된 중복 트래커 제거
            var expiredDuplicates = duplicateTracker
                .Where(x => (DateTime.Now - x.Value).TotalSeconds > (settings?.duplicateCooldown ?? 30))
                .Select(x => x.Key)
                .ToList();

            foreach (var key in expiredDuplicates)
            {
                duplicateTracker.Remove(key);
            }
        }
    }

    #endregion

    #region Editor Methods

    [ContextMenu("테스트 토스트 표시")]
    private void ShowTestToast()
    {
        ShowSystemToast("테스트 토스트 메시지입니다!");
    }

    [ContextMenu("큐 상태 출력")]
    private void PrintQueueStatus()
    {
        // Debug.Log($"[ToastNotificationManager] 큐 크기: {notificationQueue.Count}, 활성 토스트: {activeToasts.Count}");
    }

    #endregion
}