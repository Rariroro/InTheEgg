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
    [SerializeField] private bool enableActivityNotifications = true;
    [SerializeField] private bool debugMode = false;

    [Header("필터링")]
    [SerializeField] private List<InteractionType> ignoredInteractionTypes = new List<InteractionType>();
    [SerializeField] private List<string> ignoredActivityNames = new List<string>();

    // 이벤트
    public static event System.Action<PetController, PetController, InteractionType> OnInteractionStarted;
    public static event System.Action<PetController, string> OnActivityStarted;

    private void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
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
        OnActivityStarted += HandleActivityStarted;
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제
        OnInteractionStarted -= HandleInteractionStarted;
        OnActivityStarted -= HandleActivityStarted;
    }

    /// <summary>
    /// 상호작용이 시작될 때 호출 (BasePetInteraction에서 호출)
    /// </summary>
    public static void NotifyInteractionStarted(PetController pet1, PetController pet2, InteractionType type)
    {
        OnInteractionStarted?.Invoke(pet1, pet2, type);
    }

    /// <summary>
    /// 활동이 시작될 때 호출 (Activity에서 호출)
    /// </summary>
    public static void NotifyActivityStarted(PetController pet, string activityName)
    {
        OnActivityStarted?.Invoke(pet, activityName);
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
            if (debugMode)
                Debug.Log($"[InteractionNotificationHandler] 무시된 상호작용 타입: {type}");
            return;
        }

        // 토스트 알림 표시
        if (ToastNotificationManager.Instance != null)
        {
            ToastNotificationManager.Instance.ShowInteractionToast(pet1, pet2, type);

            if (debugMode)
            {
                string message = InteractionToastFormatter.FormatInteractionMessage(
                    pet1.name,
                    pet2.name,
                    type,
                    includeEmoji: true
                );
                Debug.Log($"[InteractionNotificationHandler] {message}");
            }
        }
        else
        {
            Debug.LogWarning("[InteractionNotificationHandler] ToastNotificationManager를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 활동 시작 핸들러
    /// </summary>
    private void HandleActivityStarted(PetController pet, string activityName)
    {
        if (!enableActivityNotifications) return;

        // 무시 리스트 체크
        if (ignoredActivityNames.Contains(activityName))
        {
            if (debugMode)
                Debug.Log($"[InteractionNotificationHandler] 무시된 활동: {activityName}");
            return;
        }

        // 특정 활동만 알림 (선택적)
        if (ShouldNotifyActivity(activityName))
        {
            if (ToastNotificationManager.Instance != null)
            {
                ToastNotificationManager.Instance.ShowActivityToast(pet, activityName);

                if (debugMode)
                {
                    string message = InteractionToastFormatter.FormatActivityMessage(
                        pet.name,
                        activityName,
                        includeEmoji: true
                    );
                    Debug.Log($"[InteractionNotificationHandler] {message}");
                }
            }
        }
    }

    /// <summary>
    /// 활동 알림 여부 판단
    /// </summary>
    private bool ShouldNotifyActivity(string activityName)
    {
        // 중요한 활동만 알림
        switch (activityName)
        {
            case "TreasureFound":
            case "TreasureHunt":
            case "Diving":
            case "BeeEscape":
                return true;

            case "Eat":
            case "Sleep":
                // 선택적: 기본 활동은 알림 안함
                return false;

            default:
                // 기본값: 알림 표시
                return false;
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
    /// 특정 활동 무시 설정
    /// </summary>
    public void SetIgnoreActivity(string activityName, bool ignore)
    {
        if (ignore && !ignoredActivityNames.Contains(activityName))
        {
            ignoredActivityNames.Add(activityName);
        }
        else if (!ignore)
        {
            ignoredActivityNames.Remove(activityName);
        }
    }

    /// <summary>
    /// 알림 활성화/비활성화
    /// </summary>
    public void SetNotificationsEnabled(bool interactions, bool activities)
    {
        enableInteractionNotifications = interactions;
        enableActivityNotifications = activities;
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

    [ContextMenu("테스트 - 활동 알림")]
    private void TestActivityNotification()
    {
        // 테스트용 더미 펫 생성
        GameObject dummy = new GameObject("TestPet");
        PetController pet = dummy.AddComponent<PetController>();

        // 활동 알림 테스트
        NotifyActivityStarted(pet, "TreasureFound");

        // 정리
        Destroy(dummy, 1f);
    }

    #endregion
}