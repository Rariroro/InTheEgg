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

    [Tooltip("조건 안내 텍스트")]
    public TMP_Text conditionText;
    
    [Tooltip("보물 개수 표시 텍스트")]
    public TMP_Text treasureCountText;
    
    [Tooltip("남은 시간 표시 텍스트")]
    public TMP_Text timerText;
    
    [Header("설정")]
    [Tooltip("필요한 최소 친밀도")]
    public float requiredAffection = 75f;
    
    [Header("버튼 스프라이트")]
    [Tooltip("조건 만족 시 스프라이트")]
    public Sprite normalSprite;

    [Tooltip("조건 불만족 시 스프라이트")]
    public Sprite inactiveSprite;

    [Tooltip("보물찾기 진행 중 스프라이트")]
    public Sprite progressSprite;
    
    private bool isHuntActive = false;
    private float checkInterval = 5f; // 5초마다 조건 체크 (성능 개선)
    private float checkTimer = 0f;
    private bool shouldShowConditionText = false; // 조건 텍스트 표시 여부
    private Image buttonImage; // 버튼의 Image 컴포넌트
    private bool lastCanActivate = false; // 이전 활성화 상태 캐싱
    private Sprite currentSprite = null; // 현재 설정된 스프라이트 캐싱
    
    private void Start()
    {
        // 버튼 이벤트 연결
        if (treasureHuntButton != null)
        {
            treasureHuntButton.onClick.AddListener(OnButtonClick);

            // 자식 "Button" 오브젝트의 Image 컴포넌트 가져오기
            Transform buttonChild = treasureHuntButton.transform.Find("Button");
            if (buttonChild != null)
            {
                buttonImage = buttonChild.GetComponent<Image>();
                if (buttonImage == null)
                {
                    Debug.LogWarning("[TreasureHunt] 자식 'Button' 오브젝트에 Image 컴포넌트가 없습니다!");
                }
                else
                {
        // Debug.Log($"[TreasureHunt] buttonImage 찾음: {buttonImage.gameObject.name}, 현재 스프라이트: {(buttonImage.sprite != null ? buttonImage.sprite.name : "null")}");
                }
            }
            else
            {
                Debug.LogWarning("[TreasureHunt] 자식 'Button' 오브젝트를 찾을 수 없습니다!");
            }

            // 할당된 스프라이트 확인
        // Debug.Log($"[TreasureHunt] normalSprite: {(normalSprite != null ? normalSprite.name : "null")}");
        // Debug.Log($"[TreasureHunt] inactiveSprite: {(inactiveSprite != null ? inactiveSprite.name : "null")}");
        // Debug.Log($"[TreasureHunt] progressSprite: {(progressSprite != null ? progressSprite.name : "null")}");
        }
        else
        {
            Debug.LogWarning("[TreasureHunt] 버튼이 할당되지 않았습니다!");
        }

        // 조건 텍스트 초기화
        if (conditionText != null)
        {
            conditionText.gameObject.SetActive(false);
        }

        // 보물 개수와 타이머 텍스트 초기화
        if (treasureCountText != null)
        {
            treasureCountText.gameObject.SetActive(false);
        }
        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }

        // 매니저 이벤트 연결
        if (TreasureHuntManager.Instance != null)
        {
            TreasureHuntManager.Instance.OnTreasureHuntStarted += OnHuntStarted;
            TreasureHuntManager.Instance.OnTreasureHuntEnded += OnHuntEnded;
            TreasureHuntManager.Instance.OnCoinsCollected += OnCoinsCollected;
            TreasureHuntManager.Instance.OnTreasureFound += OnTreasureFound;
            TreasureHuntManager.Instance.OnTimeUpdate += OnTimeUpdate;
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
        // Debug.Log("[TreasureHuntButton] 버튼 클릭!");
        
        if (TreasureHuntManager.Instance == null)
        {
            Debug.LogError("TreasureHuntManager를 찾을 수 없습니다!");
            return;
        }
        
        if (isHuntActive)
        {
            // 진행 중에는 버튼 동작 안 함 (시작 버튼만 제공)
        // Debug.Log("[TreasureHuntButton] 보물찾기가 이미 진행 중입니다.");
            return;
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
            
        // Debug.Log($"[TreasureHuntButton] 조건 체크: {qualifiedPets}마리 참여 가능");
            
            if (qualifiedPets > 0)
            {
                // 조건 만족 - 보물찾기 시작
        // Debug.Log("[TreasureHuntButton] 조건 만족 - 보물찾기 시작");
                TreasureHuntManager.Instance.StartTreasureHunt();
            }
            else
            {
                // 조건 불만족 - 조건 텍스트 잠시 표시
        // Debug.Log("[TreasureHuntButton] 조건 불만족 - 조건 텍스트 표시");
                ShowConditionTextTemporarily();
            }
        }
    }
    
    /// <summary>
    /// 버튼 활성화 조건 체크
    /// </summary>
    private void CheckButtonAvailability()
    {
        if (buttonImage == null)
        {
            // Debug.LogWarning("[TreasureHunt] CheckButtonAvailability - buttonImage가 null입니다!");
            return;
        }

        // 보물찾기 진행 중이면 항상 활성화
        if (isHuntActive)
        {
            // 이미 진행 중 스프라이트가 설정되어 있으면 변경하지 않음
            if (currentSprite != progressSprite)
            {
                // Debug.Log("[TreasureHunt] 보물찾기 진행 중 - progressSprite 설정");
                SetButtonSprite(progressSprite);
            }
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

        // 상태가 변경된 경우에만 업데이트
        if (canActivate != lastCanActivate)
        {
        // Debug.Log($"[TreasureHunt] 조건 변경 감지: {qualifiedPets}/{allPets.Length}마리, canActivate: {canActivate}");
            lastCanActivate = canActivate;

            // 버튼 스프라이트 업데이트
            SetButtonSprite(canActivate ? normalSprite : inactiveSprite);
        }

        // 조건 안내는 shouldShowConditionText가 true일 때만 표시
        if (shouldShowConditionText)
        {
            UpdateConditionText(qualifiedPets, allPets.Length);
        }
    }
    
    /// <summary>
    /// 버튼 스프라이트 설정
    /// </summary>
    private void SetButtonSprite(Sprite sprite)
    {
        if (buttonImage != null && sprite != null)
        {
            // 이미 같은 스프라이트면 변경하지 않음
            if (currentSprite == sprite)
            {
                return;
            }

            // Debug.Log($"[TreasureHunt] 스프라이트 변경: {sprite.name}");
            buttonImage.sprite = sprite;
            currentSprite = sprite;
        }
        else if (sprite == null || buttonImage == null)
        {
            // Debug.LogWarning($"[TreasureHunt] 스프라이트 변경 실패 - buttonImage: {buttonImage != null}, sprite: {sprite != null}");
        }
    }

    /// <summary>
    /// 버튼 상태 설정 (활성화/비활성화)
    /// </summary>
    private void SetButtonInteractable(bool isInteractable)
    {
        if (treasureHuntButton != null)
        {
            treasureHuntButton.interactable = isInteractable;
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
        // Debug.Log("[TreasureHuntButton] ShowConditionTextTemporarily 호출");
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
        
        // Debug.Log($"[TreasureHuntButton] 조건 텍스트 표시: {qualifiedPets}/{allPets.Length}마리");
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
        shouldShowConditionText = false; // 조건 텍스트 숨김
        SetButtonSprite(progressSprite);
        SetButtonInteractable(false);  // 진행 중에는 버튼 비활성화

        // 조건 텍스트 숨기기
        if (conditionText != null)
        {
            conditionText.gameObject.SetActive(false);
        }

        // 보물 개수와 타이머 표시
        if (treasureCountText != null)
        {
            treasureCountText.gameObject.SetActive(true);
            UpdateTreasureCountDisplay(0, TreasureHuntManager.Instance.TotalTreasureCount);
        }
        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
            UpdateTimerDisplay(TreasureHuntManager.Instance.RemainingTime);
        }

        // 시작 효과
        if (buttonImage != null)
        {
            // 버튼 애니메이션 (선택사항)
            Animator buttonAnimator = buttonImage.GetComponent<Animator>();
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
        shouldShowConditionText = false;
        CheckButtonAvailability();

        // 모든 UI 요소 숨기기
        if (conditionText != null)
        {
            conditionText.gameObject.SetActive(false);
        }
        if (treasureCountText != null)
        {
            treasureCountText.gameObject.SetActive(false);
        }
        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }

        // 종료 효과
        if (buttonImage != null)
        {
            Animator buttonAnimator = buttonImage.GetComponent<Animator>();
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
        if (buttonImage != null)
        {
            StartCoroutine(FlashButton());
        }
    }

    /// <summary>
    /// 버튼 플래시 효과
    /// </summary>
    private System.Collections.IEnumerator FlashButton()
    {
        if (buttonImage == null) yield break;

        Color originalColor = buttonImage.color;
        buttonImage.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        buttonImage.color = originalColor;
    }
    
    /// <summary>
    /// 보물 발견 이벤트 처리
    /// </summary>
    private void OnTreasureFound(int found, int total)
    {
        UpdateTreasureCountDisplay(found, total);
        
        // 보물 찾을 때마다 짧은 플래시 효과
        if (treasureCountText != null)
        {
            StartCoroutine(FlashText(treasureCountText));
        }
    }
    
    /// <summary>
    /// 시간 업데이트 이벤트 처리
    /// </summary>
    private void OnTimeUpdate(float remainingTime)
    {
        UpdateTimerDisplay(remainingTime);
        
        // 10초 이하일 때 빨간색으로 표시
        if (timerText != null && remainingTime <= 10f)
        {
            timerText.color = Color.red;
        }
    }
    
    /// <summary>
    /// 보물 개수 표시 업데이트
    /// </summary>
    private void UpdateTreasureCountDisplay(int found, int total)
    {
        if (treasureCountText != null)
        {
            treasureCountText.text = $"보물: {found}/{total} 찾음";
            
            // 모두 찾았을 때 특별한 색상
            if (found >= total)
            {
                treasureCountText.color = Color.yellow;
            }
            else
            {
                treasureCountText.color = Color.white;
            }
        }
    }
    
    /// <summary>
    /// 타이머 표시 업데이트
    /// </summary>
    private void UpdateTimerDisplay(float seconds)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            timerText.text = $"남은 시간: {minutes:00}:{secs:00}";
        }
    }
    
    /// <summary>
    /// 텍스트 플래시 효과
    /// </summary>
    private System.Collections.IEnumerator FlashText(TMP_Text text)
    {
        if (text == null) yield break;
        
        Color originalColor = text.color;
        text.color = Color.green;
        yield return new WaitForSeconds(0.2f);
        text.color = originalColor;
    }
    
    private void OnDestroy()
    {
        // 이벤트 연결 해제
        if (TreasureHuntManager.Instance != null)
        {
            TreasureHuntManager.Instance.OnTreasureHuntStarted -= OnHuntStarted;
            TreasureHuntManager.Instance.OnTreasureHuntEnded -= OnHuntEnded;
            TreasureHuntManager.Instance.OnCoinsCollected -= OnCoinsCollected;
            TreasureHuntManager.Instance.OnTreasureFound -= OnTreasureFound;
            TreasureHuntManager.Instance.OnTimeUpdate -= OnTimeUpdate;
        }

        if (treasureHuntButton != null)
        {
            treasureHuntButton.onClick.RemoveListener(OnButtonClick);
        }
    }
}