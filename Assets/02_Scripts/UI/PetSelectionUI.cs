using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// PetSelectionUI 클래스는 게임 시작 전에 플레이어가 사용할 펫을 선택할 수 있는 UI를 관리합니다.
/// 토글(toggle) 버튼을 통해 최대 60개의 펫 중에서 선택하고, 선택된 펫 개수를 표시하며,
/// 최소 한 개 이상의 펫이 선택되면 시작 버튼을 활성화합니다.
/// </summary>
public class PetSelectionUI : MonoBehaviour
{
    [Header("UI 요소")]
    public GameObject togglePrefab;          // 토글 버튼 프리팹 (UI에서 클릭 가능한 버튼 역할)
    public Transform toggleContainer;        // 토글 버튼들이 배치될 부모 오브젝트 (Grid 형태로 위치를 잡을 부모)
    public Button startButton;               // 게임 시작 버튼 (선택 완료 후 누르는 버튼)
    public TMP_Text selectedPetsText;        // 선택된 펫 수를 표시할 텍스트 (예: "선택된 펫: 3개")
    public int columns = 4;                  // 그리드 형태의 열 수 (한 줄에 토글이 몇 개 들어가는지)
    public float toggleSpacing = 100f;       // 토글 간격 (x, y 좌표를 결정할 때 사용하는 거리)

    // 내부에서 관리할 토글 버튼 리스트. PetSelectionManager와 연동하여 선택된 펫 ID를 관리
    private List<Toggle> petToggles = new List<Toggle>();

    /// <summary>
    /// Start() 메서드는 게임 오브젝트가 활성화될 때 처음 한 번 실행됩니다.
    /// 여기서 PetSelectionManager를 초기화하고, UI 토글을 생성하며, 시작 버튼 이벤트를 설정합니다.
    /// </summary>
    void Start()
    {
        // PetSelectionManager 인스턴스가 없으면 새로 생성
        if (PetSelectionManager.Instance == null)
        {
            // PetSelectionManager를 관리할 빈 게임오브젝트를 생성하고 컴포넌트로 추가
            GameObject managerObj = new GameObject("PetSelectionManager");
            managerObj.AddComponent<PetSelectionManager>();
        }

        // 이전에 선택된 펫 목록이 남아있을 수 있으므로 초기화
        PetSelectionManager.Instance.ClearSelectedPets();

        // 토글 버튼을 동적으로 생성하여 화면에 배치
        CreatePetToggles();

        // 시작 버튼이 할당되어 있으면 클릭 이벤트를 등록하고, 초기에는 비활성화 상태로 설정
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
            startButton.interactable = false; // 펫이 하나도 선택되지 않았으므로 시작 버튼 비활성화
        }

        // 선택된 펫 수를 화면에 표시 (초기 값은 0)
        UpdateSelectedCountText();
    }

    /// <summary>
    /// CreatePetToggles() 메서드는 토글 버튼 프리팹을 복제하여
    /// 최대 60개의 펫을 선택할 수 있는 UI 토글을 그리드 형태로 생성합니다.
    /// </summary>
    void CreatePetToggles()
    {
        // 토글 프리팹이나 컨테이너가 할당되지 않은 경우 에러 로그 출력 후 함수 종료
        if (togglePrefab == null || toggleContainer == null)
        {
            Debug.LogError("토글 프리팹 또는 컨테이너가 할당되지 않았습니다!");
            return;
        }

        // 1번부터 60번까지 총 60개의 토글 생성
        for (int i = 1; i <= 60; i++)
        {
            // togglePrefab을 toggleContainer의 자식으로 생성
            GameObject toggleObj = Instantiate(togglePrefab, toggleContainer);

            // Grid 형태로 배치하기 위해 행(row)과 열(column) 계산
            int row = (i - 1) / columns;     // i=1~4 => row=0; i=5~8 => row=1; ...
            int col = (i - 1) % columns;     // col은 0부터 columns-1까지 반복

            // 생성된 토글 오브젝트의 RectTransform 컴포넌트를 가져와 위치 조정
            RectTransform rectTransform = toggleObj.GetComponent<RectTransform>();
            // anchoredPosition은 로컬 좌표계에서 토글의 위치를 설정
            rectTransform.anchoredPosition = new Vector2(col * toggleSpacing, -row * toggleSpacing);
            // x축으로는 col * toggleSpacing만큼 오른쪽으로, y축으로는 row * toggleSpacing만큼 아래로 배치

            // 실제 토글 컴포넌트 가져오기
            Toggle toggle = toggleObj.GetComponent<Toggle>();

            // petId 를 "pet_001", "pet_002", ... "pet_060" 형태로 생성
            string petId = $"pet_{i:D3}";

            // 토글의 자식 오브젝트 중 TextMeshPro 텍스트 컴포넌트를 찾아서 라벨 텍스트 설정
            TMP_Text labelText = toggleObj.GetComponentInChildren<TMP_Text>();
            if (labelText != null)
            {
                labelText.text = petId;   // 토글 버튼 위에 표시될 텍스트를 petId로 설정
            }

            // 로컬 변수 id 에 petId를 할당하여 람다식(클로저)에서 안전하게 사용
            string id = petId;
            // 토글 값이 변경될 때마다 호출되는 이벤트에 리스너 등록
            toggle.onValueChanged.AddListener((isOn) => OnToggleValueChanged(id, isOn));

            // petToggles 리스트에 추가하여 관리
            petToggles.Add(toggle);
        }
    }

    /// <summary>
    /// OnToggleValueChanged() 메서드는 특정 토글의 상태가 변경될 때 호출됩니다.
    /// isOn이 true면 PetSelectionManager에 펫 ID를 추가하고, false면 제거합니다.
    /// 또한 선택된 펫 수를 표시하고, 시작 버튼 활성화 여부를 판단합니다.
    /// </summary>
    /// <param name="petId">선택된(또는 선택 해제된) 펫의 고유 ID</param>
    /// <param name="isOn">토글이 켜졌는지(false) 꺼졌는지(true) 상태</param>
    void OnToggleValueChanged(string petId, bool isOn)
    {
        if (isOn)
        {
            // 토글이 켜졌으므로 PetSelectionManager 에 petId 추가
            PetSelectionManager.Instance.AddPet(petId);
        }
        else
        {
            // 토글이 꺼졌으므로 PetSelectionManager 에서 petId 제거
            PetSelectionManager.Instance.RemovePet(petId);
        }

        // 화면에 표시되는 선택된 펫 개수를 업데이트
        UpdateSelectedCountText();

        // 최소 하나 이상 선택되어 있는지 확인하여 시작 버튼 활성화/비활성화
        if (startButton != null)
        {
            // selectedPetIds.Count > 0 이면 시작 버튼 활성화, 아니면 비활성화
            startButton.interactable = PetSelectionManager.Instance.selectedPetIds.Count > 0;
        }
    }

    /// <summary>
    /// 선택된 펫의 개수를 TMP_Text(selectedPetsText)에 반영하여 화면에 표시합니다.
    /// </summary>
    void UpdateSelectedCountText()
    {
        if (selectedPetsText != null)
        {
            int count = PetSelectionManager.Instance.selectedPetIds.Count;
            selectedPetsText.text = $"선택된 펫: {count}개";
        }
    }

    /// <summary>
    /// OnStartButtonClicked() 메서드는 시작 버튼을 클릭했을 때 호출됩니다.
    /// PetSelectionManager를 통해 실제 게임 씬을 로드합니다.
    /// </summary>
    void OnStartButtonClicked()
    {
        // PetSelectionManager에 구현된 LoadGameScene() 호출하여 게임 씬 전환
        PetSelectionManager.Instance.LoadGameScene();
    }
}
