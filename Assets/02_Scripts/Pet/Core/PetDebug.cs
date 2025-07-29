using UnityEngine;

/// <summary>
/// 펫 시스템의 디버그 로그를 관리하는 정적 클래스
/// 조건부 컴파일을 사용하여 프로덕션 빌드에서는 로그를 제거합니다.
/// </summary>
public static class PetDebug
{
    // 로그 레벨 정의
    public enum LogLevel
    {
        None = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Debug = 4,
        Verbose = 5
    }
    
    // 현재 로그 레벨 (에디터에서 설정 가능)
    private static LogLevel currentLogLevel = LogLevel.Info;
    
    /// <summary>
    /// 현재 로그 레벨 설정/가져오기
    /// </summary>
    public static LogLevel CurrentLogLevel
    {
        get => currentLogLevel;
        set => currentLogLevel = value;
    }
    
    /// <summary>
    /// 정보 로그
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string message, Object context = null)
    {
        if (currentLogLevel >= LogLevel.Info)
        {
            Debug.Log($"[Pet] {message}", context);
        }
    }
    
    /// <summary>
    /// 디버그 로그 (상세 정보)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogDebug(string message, Object context = null)
    {
        if (currentLogLevel >= LogLevel.Debug)
        {
            Debug.Log($"[Pet Debug] {message}", context);
        }
    }
    
    /// <summary>
    /// 상세 로그 (매우 자세한 정보)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogVerbose(string message, Object context = null)
    {
        if (currentLogLevel >= LogLevel.Verbose)
        {
            Debug.Log($"[Pet Verbose] {message}", context);
        }
    }
    
    /// <summary>
    /// 경고 로그
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(string message, Object context = null)
    {
        if (currentLogLevel >= LogLevel.Warning)
        {
            Debug.LogWarning($"[Pet Warning] {message}", context);
        }
    }
    
    /// <summary>
    /// 에러 로그
    /// </summary>
    public static void LogError(string message, Object context = null)
    {
        if (currentLogLevel >= LogLevel.Error)
        {
            Debug.LogError($"[Pet Error] {message}", context);
        }
    }
    
    /// <summary>
    /// 상태 변경 로그 (특별 카테고리)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogStateChange(string petName, string oldState, string newState)
    {
        if (currentLogLevel >= LogLevel.Info)
        {
            Debug.Log($"[Pet State] {petName}: {oldState} → {newState}");
        }
    }
    
    /// <summary>
    /// 활동 변경 로그 (특별 카테고리)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogActivityChange(string petName, string oldActivity, string newActivity, float priority = 0f)
    {
        if (currentLogLevel >= LogLevel.Info)
        {
            if (priority > 0)
                Debug.Log($"[Pet Activity] {petName}: {oldActivity} → {newActivity} (Priority: {priority:F1})");
            else
                Debug.Log($"[Pet Activity] {petName}: {oldActivity} → {newActivity}");
        }
    }
    
    /// <summary>
    /// 욕구 변경 로그 (특별 카테고리)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogNeedChange(string petName, string needType, float oldValue, float newValue)
    {
        if (currentLogLevel >= LogLevel.Debug)
        {
            Debug.Log($"[Pet Needs] {petName}: {needType} {oldValue:F1} → {newValue:F1}");
        }
    }
}