using UnityEngine;

/// <summary>
/// 전역 디버그 설정을 관리하는 클래스입니다.
/// 빌드 환경(에디터, 개발 빌드, 릴리즈)에 따라 로그 표시 수준을 자동으로 조정합니다.
/// </summary>
public static class DebugConfig
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ConfigureLogs()
    {
        // 릴리즈 빌드(에디터 아님 && 개발 빌드 아님)일 때 적용
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        // 기본 설정: 에러(Error), 예외(Exception)와 경고(Warning)는 남기고, 일반 로그만 숨김
        // 개발 단계에서 중요 이슈(누락된 리소스 등)를 놓치지 않기 위해 Warning을 포함합니다.
        
        Debug.unityLogger.filterLogType = LogType.Warning; 
        
        // 최종 출시 때는 아래 코드로 변경하여 에러만 남기는 것을 권장합니다:
        // Debug.unityLogger.filterLogType = LogType.Error;
        
        // 완전히 끄려면:
        // Debug.unityLogger.logEnabled = false;
#endif
        
        // 설정 확인용 (에디터/개발빌드에서만 보임, 혹은 릴리즈에서도 Warning 이상이면 보임?)
        // filterLogType 설정 이전에 호출되면 보일 수 있음. 
        // 여기서는 설정이 적용된 후이므로 조건에 맞으면 출력됨.
        // 다만 릴리즈 빌드에서는 일반 Log가 차단되므로 아래 로그는 안 보일 것임.
        Debug.Log($"[DebugConfig] Log Configuration Complete. Filter: {Debug.unityLogger.filterLogType}");
    }
}
