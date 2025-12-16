using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 펫 상호작용 시 토스트 알림을 트리거하는 핸들러
/// BasePetInteraction과 ToastNotificationManager를 연결
/// </summary>
public class InteractionNotificationHandler : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static InteractionNotificationHandler Instance { get; private set; }

    [Header("알림 설정")]
    [SerializeField] private bool enableInteractionNotifications = true;
    [SerializeField] private bool debugMode = false;

    [Header("필터링")]
    [SerializeField] private List<InteractionType> ignoredInteractionTypes = new List<InteractionType>();

    [Header("상호작용 수 관리")]
    [SerializeField] private int activeInteractionCount = 0;
    [SerializeField] private int maxInteractionsForDisplay = 5;
    [SerializeField] private bool showAggregateNotification = true;

    // 이벤트
    public static event System.Action<PetController, PetController, InteractionType> OnInteractionStarted;
    public static event System.Action<PetController, PetController> OnInteractionEnded;
    public static event System.Action<int> OnActiveInteractionCountChanged;

    private void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        // 이벤트 구독
        OnInteractionStarted += HandleInteractionStarted;
        OnInteractionEnded += HandleInteractionEnded;
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제
        OnInteractionStarted -= HandleInteractionStarted;
        OnInteractionEnded -= HandleInteractionEnded;
    }

    /// <summary>
    /// 상호작용이 시작될 때 호출 (BasePetInteraction에서 호출)
    /// </summary>
    public static void NotifyInteractionStarted(PetController pet1, PetController pet2, InteractionType type)
    {
        OnInteractionStarted?.Invoke(pet1, pet2, type);
    }

    /// <summary>
    /// 상호작용이 종료될 때 호출 (BasePetInteraction에서 호출)
    /// </summary>
    public static void NotifyInteractionEnded(PetController pet1, PetController pet2)
    {
        OnInteractionEnded?.Invoke(pet1, pet2);
    }

    /// <summary>
    /// 상호작용 시작 핸들러
    /// </summary>
    private void HandleInteractionStarted(PetController pet1, PetController pet2, InteractionType type)
    {
        if (!enableInteractionNotifications) return;

        // 무시 리스트 체크
        if (ignoredInteractionTypes.Contains(type))
        {
            // if (debugMode)
                // Debug.Log($"[InteractionNotificationHandler] 무시된 상호작용 타입: {type}");
            return;
        }

        // 활성 상호작용 수 체크 (maxInteractionsForDisplay 이하일 때만 개별 토스트 표시)
        if (activeInteractionCount <= maxInteractionsForDisplay)
        {
            // 토스트 알림 표시
            if (ToastNotificationManager.Instance != null)
            {
                ToastNotificationManager.Instance.ShowInteractionToast(pet1, pet2, type);

                // if (debugMode)
                // {
                //     string interactionName = InteractionToastFormatter.GetInteractionName(type);
                //     Debug.Log($"[InteractionNotificationHandler] {pet1.name} ↔ {pet2.name} | {interactionName}");
                // }
            }
            else
            {
                Debug.LogWarning("[InteractionNotificationHandler] ToastNotificationManager를 찾을 수 없습니다!");
            }
        }
        else if (showAggregateNotification && activeInteractionCount == maxInteractionsForDisplay + 1)
        {
            // 초과 시 집계 알림 표시 (처음 초과할 때만)
            ShowAggregateNotification();
        }
    }

    /// <summary>
    /// 상호작용 종료 핸들러
    /// </summary>
    private void HandleInteractionEnded(PetController pet1, PetController pet2)
    {
        if (!enableInteractionNotifications) return;

        // 토스트 알림 제거
        if (ToastNotificationManager.Instance != null)
        {
            ToastNotificationManager.Instance.DismissInteractionToast(pet1, pet2);

            if (debugMode)
                Debug.Log($"[InteractionNotificationHandler] 토스트 제거 요청: {pet1?.name} ↔ {pet2?.name}");
        }
    }

    #region 편의 메서드

    /// <summary>
    /// 특정 상호작용 타입 무시 설정
    /// </summary>
    public void SetIgnoreInteractionType(InteractionType type, bool ignore)
    {
        if (ignore && !ignoredInteractionTypes.Contains(type))
        {
            ignoredInteractionTypes.Add(type);
        }
        else if (!ignore)
        {
            ignoredInteractionTypes.Remove(type);
        }
    }

    /// <summary>
    /// 알림 활성화/비활성화
    /// </summary>
    public void SetNotificationsEnabled(bool enabled)
    {
        enableInteractionNotifications = enabled;
    }

    /// <summary>
    /// 활성 상호작용 수 설정
    /// </summary>
    public void SetActiveInteractionCount(int count)
    {
        int previousCount = activeInteractionCount;
        activeInteractionCount = count;

        // if (debugMode)
            // Debug.Log($"[InteractionNotificationHandler] 활성 상호작용 수: {activeInteractionCount}");

        // 상호작용 수 변경 이벤트 발생
        OnActiveInteractionCountChanged?.Invoke(activeInteractionCount);

        // 집계 알림 업데이트
        if (showAggregateNotification)
        {
            if (activeInteractionCount > maxInteractionsForDisplay &&
                previousCount <= maxInteractionsForDisplay)
            {
                // 최대 표시 수를 초과하기 시작할 때
                ShowAggregateNotification();
            }
            else if (activeInteractionCount <= maxInteractionsForDisplay &&
                     previousCount > maxInteractionsForDisplay)
            {
                // 최대 표시 수 이하로 돌아올 때
                HideAggregateNotification();
            }
            else if (activeInteractionCount > maxInteractionsForDisplay)
            {
                // 이미 초과 상태에서 수가 변경될 때
                UpdateAggregateNotification();
            }
        }
    }

    /// <summary>
    /// 집계 알림 표시
    /// </summary>
    private void ShowAggregateNotification()
    {
        if (ToastNotificationManager.Instance != null)
        {
            int extraCount = activeInteractionCount - maxInteractionsForDisplay;
            string message = $"추가 {extraCount}개의 상호작용이 진행 중입니다...";

            ToastNotificationManager.Instance.ShowSystemToast(message);

            // if (debugMode)
                // Debug.Log($"[InteractionNotificationHandler] 집계 알림: {message}");
        }
    }

    /// <summary>
    /// 집계 알림 업데이트
    /// </summary>
    private void UpdateAggregateNotification()
    {
        // 기존 집계 알림을 업데이트하거나 새로 표시
        ShowAggregateNotification();
    }

    /// <summary>
    /// 집계 알림 숨기기
    /// </summary>
    private void HideAggregateNotification()
    {
        // ToastNotificationManager에서 시스템 토스트 제거는 자동으로 처리됨
        // if (debugMode)
            // Debug.Log("[InteractionNotificationHandler] 집계 알림 해제");
    }

    #endregion

    #region 테스트

    [ContextMenu("테스트 - 상호작용 알림")]
    private void TestInteractionNotification()
    {
        // 테스트용 더미 펫 생성
        GameObject dummy1 = new GameObject("TestPet1");
        GameObject dummy2 = new GameObject("TestPet2");

        PetController pet1 = dummy1.AddComponent<PetController>();
        PetController pet2 = dummy2.AddComponent<PetController>();

        // 상호작용 알림 테스트
        NotifyInteractionStarted(pet1, pet2, InteractionType.ChaseAndRun);

        // 정리
        Destroy(dummy1, 1f);
        Destroy(dummy2, 1f);
    }

    #endregion
}