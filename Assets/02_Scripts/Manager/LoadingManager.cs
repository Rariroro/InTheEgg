using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// 게임 로딩 화면 관리
/// Canvas를 통해 로딩 중에는 게임 화면을 가리고, 로딩 완료 시 페이드 아웃
/// </summary>
public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    [Header("UI 참조")]
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private Image progressBar;

    [Header("설정")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float minLoadingTime = 2f; // 최소 로딩 시간 (너무 빠르면 어색)

    [Header("로딩 팁")]
    [SerializeField] private string[] loadingTips = new string[]
    {
        "펫을 쓰다듬으면 친밀도가 올라가요!",
        "배고픈 펫에게 음식을 줘보세요.",
        "펫들은 서로 놀기도 해요.",
        "Egg를 터치하면 새 펫이 나와요!",
        "펫이 졸려하면 쉬게 해주세요."
    };

    private float loadingStartTime;
    private bool isLoadingComplete = false;
    private int totalPets = 0;
    private int loadedPets = 0;

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

    private void Start()
    {
        // 로딩 시작
        loadingStartTime = Time.time;

        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
        }

        // 랜덤 팁 표시
        ShowRandomTip();

        // 진행률 초기화
        UpdateProgress(0, 1);
    }

    /// <summary>
    /// 로딩할 총 펫 수 설정
    /// </summary>
    public void SetTotalPets(int total)
    {
        totalPets = total;
        loadedPets = 0;
        UpdateProgressUI();
    }

    /// <summary>
    /// 펫 로딩 진행률 업데이트
    /// </summary>
    public void OnPetLoaded()
    {
        loadedPets++;
        UpdateProgressUI();
    }

    /// <summary>
    /// 진행률 직접 설정 (0~1)
    /// </summary>
    public void UpdateProgress(int current, int total)
    {
        loadedPets = current;
        totalPets = total;
        UpdateProgressUI();
    }

    private void UpdateProgressUI()
    {
        if (progressText != null)
        {
            if (totalPets > 0)
            {
                progressText.text = $"펫 준비 중... {loadedPets}/{totalPets}";
            }
            else
            {
                progressText.text = "로딩 중...";
            }
        }

        if (progressBar != null && totalPets > 0)
        {
            progressBar.fillAmount = (float)loadedPets / totalPets;
        }
    }

    /// <summary>
    /// 랜덤 팁 표시
    /// </summary>
    private void ShowRandomTip()
    {
        if (tipText != null && loadingTips != null && loadingTips.Length > 0)
        {
            tipText.text = loadingTips[Random.Range(0, loadingTips.Length)];
        }
    }

    /// <summary>
    /// 로딩 완료 - 페이드 아웃 후 로딩 화면 숨김
    /// </summary>
    public void OnLoadingComplete()
    {
        if (isLoadingComplete) return;
        isLoadingComplete = true;

        StartCoroutine(CompleteLoadingCoroutine());
    }

    private IEnumerator CompleteLoadingCoroutine()
    {
        // 최소 로딩 시간 보장 (너무 빠르면 어색함)
        float elapsed = Time.time - loadingStartTime;
        if (elapsed < minLoadingTime)
        {
            yield return new WaitForSeconds(minLoadingTime - elapsed);
        }

        // 진행률 100% 표시
        if (progressText != null)
        {
            progressText.text = "완료!";
        }
        if (progressBar != null)
        {
            progressBar.fillAmount = 1f;
        }

        yield return new WaitForSeconds(0.3f);

        // 페이드 아웃
        yield return StartCoroutine(FadeOut());

        // 로딩 화면 비활성화
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(false);
        }

        Debug.Log("[LoadingManager] 로딩 완료, 게임 시작");
    }

    private IEnumerator FadeOut()
    {
        if (backgroundImage == null)
        {
            yield break;
        }

        Color startColor = backgroundImage.color;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            backgroundImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        backgroundImage.color = new Color(startColor.r, startColor.g, startColor.b, 0f);
    }

    /// <summary>
    /// 로딩 화면 즉시 숨기기 (테스트용)
    /// </summary>
    public void HideImmediately()
    {
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(false);
        }
        isLoadingComplete = true;
    }

    /// <summary>
    /// 로딩이 완료되었는지 확인
    /// </summary>
    public bool IsLoadingComplete => isLoadingComplete;
}
