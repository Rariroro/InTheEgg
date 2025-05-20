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
    public int columns = 4;                   // 그리드 열 수
    public float toggleSpacing = 100f;        // 토글 간격

    private List<Toggle> petToggles = new List<Toggle>();

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

        // 선택된 펫 수 텍스트 업데이트
        UpdateSelectedCountText();
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
            // 토글 인스턴스 생성
            GameObject toggleObj = Instantiate(togglePrefab, toggleContainer);
            
            // 위치 조정 (그리드 레이아웃)
            int row = (i - 1) / columns;
            int col = (i - 1) % columns;
            RectTransform rectTransform = toggleObj.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(col * toggleSpacing, -row * toggleSpacing);

            // 토글 설정
            Toggle toggle = toggleObj.GetComponent<Toggle>();
            
            // 펫 ID 설정 (pet_001, pet_002, ...)
            string petId = $"pet_{i:D03}";
            
            // 토글 라벨 텍스트 설정
            TMP_Text labelText = toggleObj.GetComponentInChildren<TMP_Text>();
            if (labelText != null)
            {
                labelText.text = petId;
            }

            // 토글 이벤트 등록
            string id = petId; // 클로저에서 사용할 변수
            toggle.onValueChanged.AddListener((isOn) => OnToggleValueChanged(id, isOn));
            
            petToggles.Add(toggle);
        }
    }

    void OnToggleValueChanged(string petId, bool isOn)
    {
        if (isOn)
        {
            PetSelectionManager.Instance.AddPet(petId);
        }
        else
        {
            PetSelectionManager.Instance.RemovePet(petId);
        }
        
        UpdateSelectedCountText();
        
        // 최소 하나 이상 선택되어 있는지 확인하여 시작 버튼 활성화/비활성화
        if (startButton != null)
        {
            startButton.interactable = PetSelectionManager.Instance.selectedPetIds.Count > 0;
        }
    }

    void UpdateSelectedCountText()
    {
        if (selectedPetsText != null)
        {
            int count = PetSelectionManager.Instance.selectedPetIds.Count;
            selectedPetsText.text = $"선택된 펫: {count}개";
        }
    }

    void OnStartButtonClicked()
    {
        PetSelectionManager.Instance.LoadGameScene();
    }
}