using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class PetSelectionUI : MonoBehaviour
{
    [Header("UI 요소")]
    public GameObject togglePrefab;          // 토글 버튼 프리팹
    public Transform toggleContainer;         // 토글 버튼들이 배치될 부모 오브젝트
    public Button startButton;                // 게임 시작 버튼
    public TMP_Text selectedPetsText;         // 선택된 펫 수 표시 텍스트
                                              // ★ 새로 추가된 버튼들
    public Button selectAllPetsButton;       // 모든 펫 선택 버튼
    public int columns = 4;                   // 그리드 열 수
    public float toggleSpacing = 100f;        // 토글 간격

    private List<Toggle> petToggles = new List<Toggle>();
    private List<Toggle> firstAppearanceToggles = new List<Toggle>(); // 최초 등장 토글 리스트 (새로 추가)

    void Start()
    {
        // PetSelectionManager 초기화
        if (PetSelectionManager.Instance == null)
        {
            GameObject managerObj = new GameObject("PetSelectionManager");
            managerObj.AddComponent<PetSelectionManager>();
        }

        // 이전에 선택된 펫 목록 초기화
        PetSelectionManager.Instance.ClearSelectedPets();

        // 토글 버튼 생성
        CreatePetToggles();

        // 시작 버튼 이벤트 설정
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
            startButton.interactable = false; // 초기에는 비활성화
        }
        // ★ 새로 추가된 버튼 이벤트 설정
        if (selectAllPetsButton != null)
        {
            selectAllPetsButton.onClick.AddListener(OnSelectAllPetsClicked);
        }


        // 선택된 펫 수 텍스트 업데이트
        UpdateSelectedCountText();
    }
    // ★ 모든 펫 선택 버튼 클릭 메서드
    void OnSelectAllPetsClicked()
    {
        // Debug.Log("모든 펫 선택 버튼 클릭됨");

        // 모든 펫 토글을 켜기
        for (int i = 0; i < petToggles.Count; i++)
        {
            if (petToggles[i] != null && !petToggles[i].isOn)
            {
                petToggles[i].isOn = true; // 이렇게 하면 OnPetToggleValueChanged가 자동 호출됨
            }
        }

        // Debug.Log($"총 {petToggles.Count}개 펫이 선택되었습니다.");
    }


    void CreatePetToggles()
    {
        if (togglePrefab == null || toggleContainer == null)
        {
            Debug.LogError("토글 프리팹 또는 컨테이너가 할당되지 않았습니다!");
            return;
        }

        // 60개의 펫 토글 생성 (그리드 형태로)
        for (int i = 1; i <= 60; i++)
        {
            // 메인 컨테이너 생성 (펫 토글 + 최초 등장 토글을 담을 컨테이너)
            GameObject containerObj = new GameObject($"PetContainer_{i:D03}");
            containerObj.transform.SetParent(toggleContainer);

            RectTransform containerRect = containerObj.AddComponent<RectTransform>();

            // 컨테이너 위치 설정 (그리드 레이아웃)
            int row = (i - 1) / columns;
            int col = (i - 1) % columns;
            containerRect.anchoredPosition = new Vector2(col * toggleSpacing, -row * toggleSpacing);
            containerRect.sizeDelta = new Vector2(toggleSpacing * 0.9f, toggleSpacing * 0.9f);

            // 펫 선택 토글 생성
            GameObject petToggleObj = Instantiate(togglePrefab, containerObj.transform);
            RectTransform petToggleRect = petToggleObj.GetComponent<RectTransform>();
            petToggleRect.anchoredPosition = new Vector2(0, 10); // 위쪽에 배치
            petToggleRect.sizeDelta = new Vector2(80, 40);

            Toggle petToggle = petToggleObj.GetComponent<Toggle>();

            // 펫 ID 설정
            string petId = $"pet_{i:D03}";

            // 펫 토글 라벨 텍스트 설정
            TMP_Text petLabelText = petToggleObj.GetComponentInChildren<TMP_Text>();
            if (petLabelText != null)
            {
                petLabelText.text = petId;
                petLabelText.fontSize = 25; // 폰트 크기 조정
            }

            // 최초 등장 토글 생성
            GameObject firstAppearanceToggleObj = Instantiate(togglePrefab, containerObj.transform);
            RectTransform firstAppearanceRect = firstAppearanceToggleObj.GetComponent<RectTransform>();
            firstAppearanceRect.anchoredPosition = new Vector2(0, -20); // 아래쪽에 배치
            firstAppearanceRect.sizeDelta = new Vector2(80, 30);

            Toggle firstAppearanceToggle = firstAppearanceToggleObj.GetComponent<Toggle>();
            firstAppearanceToggle.interactable = false; // 처음에는 비활성화

            // 최초 등장 토글 라벨 텍스트 설정
            TMP_Text firstAppearanceLabelText = firstAppearanceToggleObj.GetComponentInChildren<TMP_Text>();
            if (firstAppearanceLabelText != null)
            {
                firstAppearanceLabelText.text = "Initial";
                firstAppearanceLabelText.fontSize = 25; // 작은 폰트 크기
            }

            // 펫 토글 이벤트 등록
            string id = petId; // 클로저에서 사용할 변수
            petToggle.onValueChanged.AddListener((isOn) => OnPetToggleValueChanged(id, isOn, firstAppearanceToggle));

            // 최초 등장 토글 이벤트 등록
            firstAppearanceToggle.onValueChanged.AddListener((isOn) => OnFirstAppearanceToggleValueChanged(id, isOn));

            petToggles.Add(petToggle);
            firstAppearanceToggles.Add(firstAppearanceToggle);
        }
    }

    void OnPetToggleValueChanged(string petId, bool isOn, Toggle firstAppearanceToggle)
    {
        if (isOn)
        {
            PetSelectionManager.Instance.AddPet(petId);
            firstAppearanceToggle.interactable = true; // 최초 등장 토글 활성화
        }
        else
        {
            PetSelectionManager.Instance.RemovePet(petId);
            firstAppearanceToggle.interactable = false; // 최초 등장 토글 비활성화
            firstAppearanceToggle.isOn = false; // 최초 등장 토글 해제
        }

        UpdateSelectedCountText();

        // 최소 하나 이상 선택되어 있는지 확인하여 시작 버튼 활성화/비활성화
        if (startButton != null)
        {
            startButton.interactable = PetSelectionManager.Instance.selectedPetIds.Count > 0;
        }
    }

    void OnFirstAppearanceToggleValueChanged(string petId, bool isOn)
    {
        PetSelectionManager.Instance.SetPetFirstAppearance(petId, isOn);
    }

    void UpdateSelectedCountText()
    {
        if (selectedPetsText != null)
        {
            int count = PetSelectionManager.Instance.selectedPetIds.Count;
            int firstAppearanceCount = PetSelectionManager.Instance.firstAppearancePetIds.Count;
            selectedPetsText.text = $"선택된 펫: {count}개 (최초등장: {firstAppearanceCount}개)";
        }
    }

    void OnStartButtonClicked()
    {
        PetSelectionManager.Instance.LoadGameScene();
    }
}