using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using LegendaryPet;

public class LegendaryPetSelectionUI : MonoBehaviour
{
    [Header("UI 요소")]
    public GameObject togglePrefab;          // 토글 버튼 프리팹
    public Transform toggleContainer;         // 토글 버튼들이 배치될 부모 오브젝트
    public TMP_Text selectedLegendsText;      // 선택된 레전드 펫 수 표시 텍스트
    public Button selectAllLegendsButton;     // 모든 레전드 펫 선택 버튼
    public int columns = 5;                   // 그리드 열 수 (5개씩 2줄)
    public float toggleSpacing = 120f;        // 토글 간격

    private List<Toggle> legendToggles = new List<Toggle>();
    private List<Toggle> firstAppearanceToggles = new List<Toggle>(); // 최초 등장 토글 리스트

    void Start()
    {
        // LegendaryPetSelectionManager 초기화
        if (LegendaryPetSelectionManager.Instance == null)
        {
            GameObject managerObj = new GameObject("LegendaryPetSelectionManager");
            managerObj.AddComponent<LegendaryPetSelectionManager>();
        }

        // 이전에 선택된 레전드 펫 목록 초기화
        LegendaryPetSelectionManager.Instance.ClearSelectedLegendaryPets();

        // 토글 버튼 생성
        CreateLegendaryPetToggles();

        // 모든 레전드 펫 선택 버튼 이벤트 설정
        if (selectAllLegendsButton != null)
        {
            selectAllLegendsButton.onClick.AddListener(OnSelectAllLegendsClicked);
        }

        // 선택된 레전드 펫 수 텍스트 업데이트
        UpdateSelectedCountText();
    }

    // 모든 레전드 펫 선택 버튼 클릭 메서드
    void OnSelectAllLegendsClicked()
    {
        // 모든 레전드 펫 토글을 켜기
        for (int i = 0; i < legendToggles.Count; i++)
        {
            if (legendToggles[i] != null && !legendToggles[i].isOn)
            {
                legendToggles[i].isOn = true;
            }
        }
        
        // 잠시 대기 후 모든 최초등장 토글 켜기
        StartCoroutine(EnableAllFirstAppearanceToggles());
    }
    
    // 모든 최초등장 토글을 켜는 코루틴
    private IEnumerator EnableAllFirstAppearanceToggles()
    {
        // 펫 토글들이 처리될 시간을 주기 위해 약간 대기
        yield return new WaitForSeconds(0.1f);
        
        // 모든 최초등장 토글 켜기
        for (int i = 0; i < firstAppearanceToggles.Count; i++)
        {
            if (firstAppearanceToggles[i] != null && firstAppearanceToggles[i].interactable && !firstAppearanceToggles[i].isOn)
            {
                firstAppearanceToggles[i].isOn = true;
            }
        }
        
        // 선택된 레전드 펫 수 텍스트 업데이트
        UpdateSelectedCountText();
    }

    void CreateLegendaryPetToggles()
    {
        if (togglePrefab == null || toggleContainer == null)
        {
            Debug.LogError("토글 프리팹 또는 컨테이너가 할당되지 않았습니다!");
            return;
        }

        // 레전드 펫 ID 리스트 생성
        List<string> legendaryPetIds = new List<string>();
        
        // 유니콘 5종류
        for (int i = 1; i <= 5; i++)
        {
            legendaryPetIds.Add($"unicorn{i}");
        }
        
        // 드래곤 5종류
        for (int i = 1; i <= 5; i++)
        {
            legendaryPetIds.Add($"dragon{i}");
        }

        // 10개의 레전드 펫 토글 생성 (2행 x 5열 그리드)
        for (int i = 0; i < legendaryPetIds.Count; i++)
        {
            // 메인 컨테이너 생성 (레전드 펫 토글 + 최초 등장 토글을 담을 컨테이너)
            GameObject containerObj = new GameObject($"LegendContainer_{legendaryPetIds[i]}");
            containerObj.transform.SetParent(toggleContainer);

            RectTransform containerRect = containerObj.AddComponent<RectTransform>();

            // 컨테이너 위치 설정 (그리드 레이아웃)
            int row = i / columns;
            int col = i % columns;
            containerRect.anchoredPosition = new Vector2(col * toggleSpacing, -row * toggleSpacing);
            containerRect.sizeDelta = new Vector2(toggleSpacing * 0.9f, toggleSpacing * 0.9f);

            // 레전드 펫 선택 토글 생성
            GameObject legendToggleObj = Instantiate(togglePrefab, containerObj.transform);
            RectTransform legendToggleRect = legendToggleObj.GetComponent<RectTransform>();
            legendToggleRect.anchoredPosition = new Vector2(0, 15); // 위쪽에 배치
            legendToggleRect.sizeDelta = new Vector2(100, 50);

            Toggle legendToggle = legendToggleObj.GetComponent<Toggle>();

            // 레전드 펫 ID
            string legendId = legendaryPetIds[i];

            // 레전드 펫 토글 라벨 텍스트 설정
            TMP_Text legendLabelText = legendToggleObj.GetComponentInChildren<TMP_Text>();
            if (legendLabelText != null)
            {
                // 더 읽기 쉬운 이름으로 표시
                string displayName = legendId.Replace("unicorn", "Unicorn").Replace("dragon", "Dragon");
                legendLabelText.text = displayName;
                legendLabelText.fontSize = 20;
            }

            // 최초 등장 토글 생성
            GameObject firstAppearanceToggleObj = Instantiate(togglePrefab, containerObj.transform);
            RectTransform firstAppearanceRect = firstAppearanceToggleObj.GetComponent<RectTransform>();
            firstAppearanceRect.anchoredPosition = new Vector2(0, -25); // 아래쪽에 배치
            firstAppearanceRect.sizeDelta = new Vector2(100, 35);

            Toggle firstAppearanceToggle = firstAppearanceToggleObj.GetComponent<Toggle>();
            firstAppearanceToggle.interactable = false; // 처음에는 비활성화

            // 최초 등장 토글 라벨 텍스트 설정
            TMP_Text firstAppearanceLabelText = firstAppearanceToggleObj.GetComponentInChildren<TMP_Text>();
            if (firstAppearanceLabelText != null)
            {
                firstAppearanceLabelText.text = "Initial";
                firstAppearanceLabelText.fontSize = 18;
            }

            // 레전드 펫 토글 이벤트 등록
            string id = legendId; // 클로저에서 사용할 변수
            legendToggle.onValueChanged.AddListener((isOn) => OnLegendToggleValueChanged(id, isOn, firstAppearanceToggle));

            // 최초 등장 토글 이벤트 등록
            firstAppearanceToggle.onValueChanged.AddListener((isOn) => OnFirstAppearanceToggleValueChanged(id, isOn));

            legendToggles.Add(legendToggle);
            firstAppearanceToggles.Add(firstAppearanceToggle);
        }
    }

    void OnLegendToggleValueChanged(string legendId, bool isOn, Toggle firstAppearanceToggle)
    {
        if (isOn)
        {
            LegendaryPetSelectionManager.Instance.AddLegendaryPet(legendId);
            firstAppearanceToggle.interactable = true; // 최초 등장 토글 활성화
        }
        else
        {
            LegendaryPetSelectionManager.Instance.RemoveLegendaryPet(legendId);
            firstAppearanceToggle.interactable = false; // 최초 등장 토글 비활성화
            firstAppearanceToggle.isOn = false; // 최초 등장 토글 해제
        }

        UpdateSelectedCountText();
    }

    void OnFirstAppearanceToggleValueChanged(string legendId, bool isOn)
    {
        LegendaryPetSelectionManager.Instance.SetLegendaryPetFirstAppearance(legendId, isOn);
        UpdateSelectedCountText();
    }

    void UpdateSelectedCountText()
    {
        if (selectedLegendsText != null)
        {
            int count = LegendaryPetSelectionManager.Instance.selectedLegendaryPetIds.Count;
            int firstAppearanceCount = LegendaryPetSelectionManager.Instance.firstAppearanceLegendaryIds.Count;
            selectedLegendsText.text = $"Selected Legends: {count} (Initial: {firstAppearanceCount})";
        }
    }
}