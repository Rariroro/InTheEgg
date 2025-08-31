using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

/// <summary>
/// 보물찾기 UI 버튼을 제어하는 컴포넌트
/// 친밀도가 높은 펫이 있을 때만 활성화됨
/// </summary>
public class TreasureHuntButton : MonoBehaviour
{
    [Header("UI 참조")]
    [Tooltip("보물찾기 버튼")]
    public Button treasureHuntButton;
    
    [Tooltip("버튼 텍스트")]
    public TMP_Text buttonText;
    
    [Tooltip("조건 안내 텍스트")]
    public TMP_Text conditionText;
    
    [Header("설정")]
    [Tooltip("필요한 최소 친밀도")]
    public float requiredAffection = 75f;
    
    [Header("색상")]
    public Color activeColor = Color.yellow;
    public Color inactiveColor = Color.gray;
    public Color progressColor = Color.green;
    
    private bool isHuntActive = false;
    private float checkInterval = 1f; // 1초마다 조건 체크
    private float checkTimer = 0f;
    private bool shouldShowConditionText = false; // 조건 텍스트 표시 여부
    
    private void Start()
    {
        // 버튼 이벤트 연결
        if (treasureHuntButton != null)
        {
            treasureHuntButton.onClick.AddListener(OnButtonClick);
        }
        else
        {
            Debug.LogWarning("TreasureHuntButton: 버튼이 할당되지 않았습니다!");
        }
        
        // 초기 텍스트 설정
        UpdateButtonText("보물찾기 시작");
        
        // 조건 텍스트 초기화
        if (conditionText != null)
        {
            conditionText.gameObject.SetActive(false);
        }
        
        // 매니저 이벤트 연결
        if (TreasureHuntManager.Instance != null)
        {
            TreasureHuntManager.Instance.OnTreasureHuntStarted += OnHuntStarted;
            TreasureHuntManager.Instance.OnTreasureHuntEnded += OnHuntEnded;
            TreasureHuntManager.Instance.OnCoinsCollected += OnCoinsCollected;
        }
        
        // 초기 상태 체크
        CheckButtonAvailability();
    }
    
    private void Update()
    {
        // 주기적으로 버튼 활성화 조건 체크
        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            CheckButtonAvailability();
        }
    }
    
    /// <summary>
    /// 버튼 클릭 처리
    /// </summary>
    private void OnButtonClick()
    {
        Debug.Log("[TreasureHuntButton] 버튼 클릭!");
        
        if (TreasureHuntManager.Instance == null)
        {
            Debug.LogError("TreasureHuntManager를 찾을 수 없습니다!");
            return;
        }
        
        if (isHuntActive)
        {
            // 보물찾기 종료
            Debug.Log("[TreasureHuntButton] 보물찾기 종료");
            TreasureHuntManager.Instance.EndTreasureHunt();
            shouldShowConditionText = false; // 종료 시 조건 텍스트 숨김
            if (conditionText != null)
            {
                conditionText.gameObject.SetActive(false);
            }
        }
        else
        {
            // 친밀도 조건 체크
            PetController[] allPets = FindObjectsByType<PetController>(FindObjectsSortMode.None);
            int qualifiedPets = 0;
            
            foreach (var pet in allPets)
            {
                if (pet != null && pet.Needs != null && pet.Needs.Affection >= requiredAffection)
                {
                    qualifiedPets++;
                }
            }
            
            Debug.Log($"[TreasureHuntButton] 조건 체크: {qualifiedPets}마리 참여 가능");
            
            if (qualifiedPets > 0)
            {
                // 조건 만족 - 보물찾기 시작
                Debug.Log("[TreasureHuntButton] 조건 만족 - 보물찾기 시작");
                TreasureHuntManager.Instance.StartTreasureHunt();
            }
            else
            {
                // 조건 불만족 - 조건 텍스트 잠시 표시
                Debug.Log("[TreasureHuntButton] 조건 불만족 - 조건 텍스트 표시");
                ShowConditionTextTemporarily();
            }
        }
    }
    
    /// <summary>
    /// 버튼 활성화 조건 체크
    /// </summary>
    private void CheckButtonAvailability()
    {
        if (treasureHuntButton == null) return;
        
        // 보물찾기 진행 중이면 항상 활성화
        if (isHuntActive)
        {
            SetButtonState(true, progressColor);
            return;
        }
        
        // 친밀도 조건 체크
        PetController[] allPets = FindObjectsByType<PetController>(FindObjectsSortMode.None);
        int qualifiedPets = 0;
        
        foreach (var pet in allPets)
        {
            if (pet != null && pet.Needs != null && pet.Needs.Affection >= requiredAffection)
            {
                qualifiedPets++;
            }
        }
        
        bool canActivate = qualifiedPets > 0;
        
        // 버튼 상태 업데이트 - 항상 활성화 상태로 유지하고 색상만 변경
        SetButtonState(true, canActivate ? activeColor : inactiveColor);
        
        // 조건 안내는 shouldShowConditionText가 true일 때만 표시
        if (shouldShowConditionText)
        {
            UpdateConditionText(qualifiedPets, allPets.Length);
        }
    }
    
    /// <summary>
    /// 버튼 상태 설정
    /// </summary>
    private void SetButtonState(bool isInteractable, Color color)
    {
        if (treasureHuntButton != null)
        {
            treasureHuntButton.interactable = isInteractable;
            
            // 버튼 색상 변경
            ColorBlock colors = treasureHuntButton.colors;
            colors.normalColor = color;
            treasureHuntButton.colors = colors;
        }
    }
    
    /// <summary>
    /// 버튼 텍스트 업데이트
    /// </summary>
    private void UpdateButtonText(string text)
    {
        if (buttonText != null)
        {
            buttonText.text = text;
        }
        else if (treasureHuntButton != null)
        {
            // 버튼의 자식에서 텍스트 컴포넌트 찾기
            TMP_Text childText = treasureHuntButton.GetComponentInChildren<TMP_Text>();
            if (childText != null)
            {
                childText.text = text;
            }
        }
    }
    
    /// <summary>
    /// 조건 안내 텍스트 업데이트
    /// </summary>
    private void UpdateConditionText(int qualified, int total)
    {
        if (conditionText == null) return;
        
        if (isHuntActive)
        {
            conditionText.gameObject.SetActive(true);
            conditionText.text = $"<color=green>보물찾기 진행 중...</color>";
        }
        else if (qualified == 0)
        {
            conditionText.gameObject.SetActive(true);
            conditionText.text = $"<color=red>친밀도 {requiredAffection} 이상 필요</color>\n현재: {qualified}/{total}마리";
        }
        else
        {
            // 조건을 만족할 때는 텍스트를 숨김 (버튼을 눌렀을 때만 보여줌)
            conditionText.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 조건 텍스트를 잠시 표시했다가 숨기기
    /// </summary>
    private void ShowConditionTextTemporarily()
    {
        Debug.Log("[TreasureHuntButton] ShowConditionTextTemporarily 호출");
        shouldShowConditionText = true;
        
        // 현재 조건 체크 및 표시
        PetController[] allPets = FindObjectsByType<PetController>(FindObjectsSortMode.None);
        int qualifiedPets = 0;
        
        foreach (var pet in allPets)
        {
            if (pet != null && pet.Needs != null && pet.Needs.Affection >= requiredAffection)
            {
                qualifiedPets++;
            }
        }
        
        Debug.Log($"[TreasureHuntButton] 조건 텍스트 표시: {qualifiedPets}/{allPets.Length}마리");
        UpdateConditionText(qualifiedPets, allPets.Length);
        
        // 3초 후 자동으로 숨기기
        StartCoroutine(HideConditionTextAfterDelay(3f));
    }
    
    /// <summary>
    /// 지연 후 조건 텍스트 숨기기
    /// </summary>
    private System.Collections.IEnumerator HideConditionTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        shouldShowConditionText = false;
        if (conditionText != null && !isHuntActive)
        {
            conditionText.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 보물찾기 시작 이벤트 처리
    /// </summary>
    private void OnHuntStarted()
    {
        isHuntActive = true;
        shouldShowConditionText = true; // 진행 중 표시를 위해
        UpdateButtonText("보물찾기 종료");
        SetButtonState(true, progressColor);
        
        // 진행 중 표시
        if (conditionText != null)
        {
            conditionText.gameObject.SetActive(true);
            conditionText.text = $"<color=green>보물찾기 진행 중...</color>";
        }
        
        // 시작 효과
        if (treasureHuntButton != null)
        {
            // 버튼 애니메이션 (선택사항)
            Animator buttonAnimator = treasureHuntButton.GetComponent<Animator>();
            if (buttonAnimator != null)
            {
                buttonAnimator.SetTrigger("Start");
            }
        }
    }
    
    /// <summary>
    /// 보물찾기 종료 이벤트 처리
    /// </summary>
    private void OnHuntEnded()
    {
        isHuntActive = false;
        shouldShowConditionText = false; // 종료 시 조건 텍스트 숨김
        UpdateButtonText("보물찾기 시작");
        CheckButtonAvailability();
        
        // 조건 텍스트 숨기기
        if (conditionText != null)
        {
            conditionText.gameObject.SetActive(false);
        }
        
        // 종료 효과
        if (treasureHuntButton != null)
        {
            Animator buttonAnimator = treasureHuntButton.GetComponent<Animator>();
            if (buttonAnimator != null)
            {
                buttonAnimator.SetTrigger("End");
            }
        }
    }
    
    /// <summary>
    /// 코인 획득 이벤트 처리
    /// </summary>
    private void OnCoinsCollected(int coins)
    {
        // 버튼 짧은 플래시 효과
        if (treasureHuntButton != null)
        {
            StartCoroutine(FlashButton());
        }
    }
    
    /// <summary>
    /// 버튼 플래시 효과
    /// </summary>
    private System.Collections.IEnumerator FlashButton()
    {
        if (treasureHuntButton == null) yield break;
        
        ColorBlock originalColors = treasureHuntButton.colors;
        ColorBlock flashColors = originalColors;
        flashColors.normalColor = Color.white;
        
        treasureHuntButton.colors = flashColors;
        yield return new WaitForSeconds(0.1f);
        treasureHuntButton.colors = originalColors;
    }
    
    private void OnDestroy()
    {
        // 이벤트 연결 해제
        if (TreasureHuntManager.Instance != null)
        {
            TreasureHuntManager.Instance.OnTreasureHuntStarted -= OnHuntStarted;
            TreasureHuntManager.Instance.OnTreasureHuntEnded -= OnHuntEnded;
            TreasureHuntManager.Instance.OnCoinsCollected -= OnCoinsCollected;
        }
        
        if (treasureHuntButton != null)
        {
            treasureHuntButton.onClick.RemoveListener(OnButtonClick);
        }
    }
}