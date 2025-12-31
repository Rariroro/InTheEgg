using UnityEngine;

/// <summary>
/// 게임 로딩 완료 알림 관리
/// Flutter로 로딩 화면이 이동되어, 이제 완료 신호만 전송
/// </summary>
public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    private bool isLoadingComplete = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// 로딩 완료
    /// </summary>
    public void OnLoadingComplete()
    {
        if (isLoadingComplete) return;
        isLoadingComplete = true;

        Debug.Log("[LoadingManager] 로딩 완료");
    }

    /// <summary>
    /// 로딩이 완료되었는지 확인
    /// </summary>
    public bool IsLoadingComplete => isLoadingComplete;

    /// <summary>
    /// 새 세션을 위해 상태 리셋
    /// </summary>
    public void ResetForNewSession()
    {
        isLoadingComplete = false;
        Debug.Log("[LoadingManager] 상태 리셋");
    }
}
