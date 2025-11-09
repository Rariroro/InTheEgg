using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 보물찾기 테스트용 디버그 버튼
/// Editor 모드에서만 동작하는 수동 종료 버튼
/// </summary>
public class TreasureHuntDebugButton : MonoBehaviour
{
    [Header("UI 참조")]
    [Tooltip("수동 종료 버튼")]
    public Button forceEndButton;
    
    [Tooltip("완전 초기화 버튼")]
    public Button forceClearButton;
    
    [Tooltip("버튼 텍스트")]
    public TMP_Text forceEndButtonText;
    public TMP_Text forceClearButtonText;
    
    [Header("디버그 설정")]
    [Tooltip("에디터 전용 모드")]
    public bool editorOnly = true;
    
    private void Awake()
    {
        // 빌드 버전에서는 자동으로 비활성화
#if !UNITY_EDITOR
        if (editorOnly)
        {
            gameObject.SetActive(false);
            return;
        }
#endif
    }
    
    private void Start()
    {
        // 수동 종료 버튼 설정
        if (forceEndButton != null)
        {
            forceEndButton.onClick.AddListener(OnForceEndClick);
            
            // 텍스트 설정
            if (forceEndButtonText != null)
            {
                forceEndButtonText.text = "[테스트] 보물찾기 종료";
            }
            else
            {
                TMP_Text text = forceEndButton.GetComponentInChildren<TMP_Text>();
                if (text != null)
                {
                    text.text = "[테스트] 보물찾기 종료";
                }
            }
            
            // 초기에는 비활성화
            forceEndButton.gameObject.SetActive(false);
        }
        
        // 완전 초기화 버튼 설정
        if (forceClearButton != null)
        {
            forceClearButton.onClick.AddListener(OnForceClearClick);
            
            // 텍스트 설정
            if (forceClearButtonText != null)
            {
                forceClearButtonText.text = "[테스트] 완전 초기화";
            }
            else
            {
                TMP_Text text = forceClearButton.GetComponentInChildren<TMP_Text>();
                if (text != null)
                {
                    text.text = "[테스트] 완전 초기화";
                }
            }
            
            // 초기에는 비활성화
            forceClearButton.gameObject.SetActive(false);
        }
        
        // 매니저 이벤트 연결
        if (TreasureHuntManager.Instance != null)
        {
            TreasureHuntManager.Instance.OnTreasureHuntStarted += OnHuntStarted;
            TreasureHuntManager.Instance.OnTreasureHuntEnded += OnHuntEnded;
        }
    }
    
    private void Update()
    {
#if UNITY_EDITOR
        // 디버그 키 조합: Ctrl+Shift+T로 버튼 토글
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.T))
        {
            ToggleDebugButtons();
        }
        
        // 보물찾기 진행 중일 때만 버튼 표시
        bool isActive = TreasureHuntManager.Instance != null && TreasureHuntManager.Instance.IsTreasureHuntActive;
        
        if (forceEndButton != null && forceEndButton.gameObject.activeSelf != isActive)
        {
            forceEndButton.gameObject.SetActive(isActive);
        }
        
        if (forceClearButton != null && forceClearButton.gameObject.activeSelf != isActive)
        {
            forceClearButton.gameObject.SetActive(isActive);
        }
#endif
    }
    
    /// <summary>
    /// 수동 종료 버튼 클릭 (보물과 펫 유지)
    /// </summary>
    private void OnForceEndClick()
    {
        // Debug.Log("[DEBUG] 보물찾기 수동 종료 (보물과 펫 유지)");
        
        if (TreasureHuntManager.Instance != null && TreasureHuntManager.Instance.IsTreasureHuntActive)
        {
            // false = 일반 종료 (펫과 보물 유지)
            TreasureHuntManager.Instance.EndTreasureHunt(false);
            
            ShowDebugMessage("보물찾기가 종료되었습니다. (펫과 보물은 유지)");
        }
    }
    
    /// <summary>
    /// 완전 초기화 버튼 클릭 (모든 것 정리)
    /// </summary>
    private void OnForceClearClick()
    {
        // Debug.Log("[DEBUG] 보물찾기 완전 초기화");
        
        if (TreasureHuntManager.Instance != null && TreasureHuntManager.Instance.IsTreasureHuntActive)
        {
            // true = 강제 종료 (모든 것 정리)
            TreasureHuntManager.Instance.EndTreasureHunt(true);
            
            ShowDebugMessage("보물찾기가 완전히 초기화되었습니다.");
        }
    }
    
    /// <summary>
    /// 디버그 버튼 표시 토글
    /// </summary>
    private void ToggleDebugButtons()
    {
        if (forceEndButton != null)
        {
            bool newState = !forceEndButton.gameObject.activeSelf;
            forceEndButton.gameObject.SetActive(newState);
        // Debug.Log($"[DEBUG] 수동 종료 버튼: {(newState ? "표시" : "숨김")}");
        }
        
        if (forceClearButton != null)
        {
            bool newState = !forceClearButton.gameObject.activeSelf;
            forceClearButton.gameObject.SetActive(newState);
        // Debug.Log($"[DEBUG] 완전 초기화 버튼: {(newState ? "표시" : "숨김")}");
        }
    }
    
    /// <summary>
    /// 보물찾기 시작 시
    /// </summary>
    private void OnHuntStarted()
    {
#if UNITY_EDITOR
        // Debug.Log("[DEBUG] 보물찾기 시작 감지 - 디버그 버튼 활성화");
        
        // 자동으로 버튼 표시
        if (forceEndButton != null)
        {
            forceEndButton.gameObject.SetActive(true);
        }
        if (forceClearButton != null)
        {
            forceClearButton.gameObject.SetActive(true);
        }
#endif
    }
    
    /// <summary>
    /// 보물찾기 종료 시
    /// </summary>
    private void OnHuntEnded()
    {
#if UNITY_EDITOR
        // Debug.Log("[DEBUG] 보물찾기 종료 감지 - 디버그 버튼 비활성화");
        
        // 자동으로 버튼 숨김
        if (forceEndButton != null)
        {
            forceEndButton.gameObject.SetActive(false);
        }
        if (forceClearButton != null)
        {
            forceClearButton.gameObject.SetActive(false);
        }
#endif
    }
    
    /// <summary>
    /// 디버그 메시지 표시
    /// </summary>
    private void ShowDebugMessage(string message)
    {
#if UNITY_EDITOR
        // Debug.Log($"[DEBUG] {message}");
        
        // UI 피드백 (옵션)
        if (TreasureHuntManager.Instance != null && TreasureHuntManager.Instance.feedbackText != null)
        {
            var feedbackText = TreasureHuntManager.Instance.feedbackText;
            feedbackText.text = $"[DEBUG] {message}";
            feedbackText.color = Color.cyan;
            feedbackText.gameObject.SetActive(true);
            
            // 3초 후 숨기기
            StartCoroutine(HideMessageAfterDelay(feedbackText.gameObject, 3f));
        }
#endif
    }
    
    private System.Collections.IEnumerator HideMessageAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
        {
            obj.SetActive(false);
        }
    }
    
    private void OnDestroy()
    {
        // 이벤트 연결 해제
        if (TreasureHuntManager.Instance != null)
        {
            TreasureHuntManager.Instance.OnTreasureHuntStarted -= OnHuntStarted;
            TreasureHuntManager.Instance.OnTreasureHuntEnded -= OnHuntEnded;
        }
        
        if (forceEndButton != null)
        {
            forceEndButton.onClick.RemoveListener(OnForceEndClick);
        }
        
        if (forceClearButton != null)
        {
            forceClearButton.onClick.RemoveListener(OnForceClearClick);
        }
    }
}