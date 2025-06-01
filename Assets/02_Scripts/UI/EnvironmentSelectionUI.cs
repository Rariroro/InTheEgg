using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// EnvironmentSelectionUI 클래스는 플레이어가 게임에서 사용할 환경(배경/맵)을 선택할 수 있는 UI를 관리합니다.
/// 토글(toggle) 버튼을 통해 여러 환경을 다중 선택할 수 있으며, 선택된 환경 개수를 화면에 표시합니다.
/// </summary>
public class EnvironmentSelectionUI : MonoBehaviour
{
    [Header("환경 UI 요소")]
    public GameObject togglePrefab;          // 토글 버튼 프리팹 (UI에서 클릭 가능한 버튼)
    public Transform toggleContainer;        // 토글 버튼들이 배치될 부모 오브젝트 (Grid 형식으로 토글을 배치할 컨테이너)
    public int columns = 3;                  // 그리드 형태의 열 수 (한 행에 몇 개의 토글을 배치할지)
    public float toggleSpacing = 100f;       // 토글 간격 (x, y 좌표를 계산할 때 사용할 거리)
    public TMP_Text selectedEnvironmentText;  // 선택된 환경 개수를 표시할 텍스트 (예: "선택된 환경: 2개")

    // UI 토글들을 관리하기 위한 리스트. 생성된 토글 인스턴스를 모두 저장.
    private List<Toggle> environmentToggles = new List<Toggle>();

    /// <summary>
    /// Start() 메서드는 이 스크립트가 활성화될 때 호출됩니다.
    /// EnvironmentSelectionManager 싱글톤을 초기화하고, 기존에 선택된 환경을 초기화한 뒤,
    /// 토글 버튼을 생성하고 초깃값으로 선택된 환경 개수를 업데이트합니다.
    /// </summary>
    void Start()
    {
        // EnvironmentSelectionManager 인스턴스가 존재하는지 확인
        if (EnvironmentSelectionManager.Instance == null)
        {
            // 인스턴스가 없으면 빈 GameObject를 생성하고 매니저 컴포넌트를 추가하여 싱글톤 초기화
            GameObject managerObj = new GameObject("EnvironmentSelectionManager");
            managerObj.AddComponent<EnvironmentSelectionManager>();
        }

        // 이전에 선택된 환경이 남아있을 수 있으므로 초기화
        EnvironmentSelectionManager.Instance.ClearSelectedEnvironments();

        // UI에 표시할 토글 버튼을 동적으로 생성
        CreateEnvironmentToggles();

        // 선택된 환경 개수를 텍스트에 반영(초기 값: 0)
        UpdateSelectedCountText();
    }

    /// <summary>
    /// CreateEnvironmentToggles() 메서드는 토글 버튼 프리팹을 복제하여
    /// 미리 정의된 환경 ID 목록에 따라 토글을 생성하고, 그리드 형태로 배치합니다.
    /// </summary>
    void CreateEnvironmentToggles()
    {
        // togglePrefab 또는 toggleContainer가 할당되지 않았으면 에러 로그 출력 후 종료
        if (togglePrefab == null || toggleContainer == null)
        {
            Debug.LogError("토글 프리팹 또는 컨테이너가 할당되지 않았습니다!");
            return;
        }

         // 새로운 환경 ID 목록 (13가지)
    string[] environmentIds = { 
        "env_foodstore",     // 음식가게
        "env_orchard",       // 과수원
        "env_berryfield",    // 산딸기밭
        "env_honeypot",      // 꿀통
        "env_sunflower",     // 해바라기
        "env_cucumber",      // 오이밭
        "env_ricefield",     // 논
        "env_watermelon",    // 수박밭
        "env_cornfield",     // 옥수수밭
        "env_forest",        // 숲
        "env_pond",          // 물웅덩이
        "env_flowers",       // 꽃
        "env_fence"          // 초식동물용울타리
    };

        // 환경 ID 배열 길이만큼 반복하여 토글 생성
        for (int i = 0; i < environmentIds.Length; i++)
        {
            // togglePrefab을 toggleContainer의 자식으로 생성
            GameObject toggleObj = Instantiate(togglePrefab, toggleContainer);

            // 그리드 배치를 위해 행(row)과 열(column) 계산
            int row = i / columns;           // i가 0~2일 때 row=0, i가 3~5일 때 row=1 등
            int col = i % columns;           // i값을 열 개수로 나눈 나머지가 열 인덱스

            // 생성된 토글 오브젝트의 RectTransform을 가져와 위치 조정
            RectTransform rectTransform = toggleObj.GetComponent<RectTransform>();
            // anchoredPosition은 로컬 좌표계에서의 위치. col*toggleSpacing 만큼 오른쪽, row*toggleSpacing 만큼 아래로 이동
            rectTransform.anchoredPosition = new Vector2(col * toggleSpacing, -row * toggleSpacing);

            // 실제 Toggle 컴포넌트를 가져와 설정
            Toggle toggle = toggleObj.GetComponent<Toggle>();

            // 현재 인덱스에 해당하는 환경 ID를 변수에 저장
            string environmentId = environmentIds[i];

            // 토글 버튼 자식 객체들 중 TextMeshPro 텍스트 컴포넌트를 찾아 라벨 텍스트 설정
            TMP_Text labelText = toggleObj.GetComponentInChildren<TMP_Text>();
            if (labelText != null)
            {
                labelText.text = environmentId;  // 예: "env_forest", "env_desert" 등
            }

            // 로컬 변수 id를 사용하여 람다식 내부에서 참조하도록 함(클로저 이슈 방지)
            string id = environmentId;
            // 토글 값이 변경될 때 호출될 이벤트 리스너 등록 (다중 선택 가능)
            toggle.onValueChanged.AddListener((isOn) => OnToggleValueChanged(id, isOn));

            // 생성된 토글을 리스트에 추가하여 추후 관리
            environmentToggles.Add(toggle);
        }
    }

    /// <summary>
    /// OnToggleValueChanged() 메서드는 특정 토글의 값이 변경될 때 호출된다.
    /// isOn이 true이면 EnvironmentSelectionManager에 환경 ID를 추가하고,
    /// false이면 제거한다. 이후 선택된 환경 개수를 갱신한다.
    /// </summary>
    /// <param name="environmentId">현재 토글에 연결된 환경 ID (예: "env_forest")</param>
    /// <param name="isOn">토글이 켜졌으면(true), 꺼졌으면(false)</param>
    void OnToggleValueChanged(string environmentId, bool isOn)
    {
        if (isOn)
        {
            // 토글이 켜진 경우 매니저에 환경 ID 추가
            EnvironmentSelectionManager.Instance.AddEnvironment(environmentId);
        }
        else
        {
            // 토글이 꺼진 경우 매니저에서 환경 ID 제거
            EnvironmentSelectionManager.Instance.RemoveEnvironment(environmentId);
        }

        // 선택된 환경 개수 텍스트 업데이트
        UpdateSelectedCountText();
    }

    /// <summary>
    /// UpdateSelectedCountText() 메서드는 EnvironmentSelectionManager에
    /// 저장된 선택된 환경 개수를 가져와 텍스트 UI에 반영한다.
    /// </summary>
    void UpdateSelectedCountText()
    {
        if (selectedEnvironmentText != null)
        {
            // 매니저에서 관리하는 selectedEnvironmentIds 리스트의 개수를 가져옴
            int count = EnvironmentSelectionManager.Instance.selectedEnvironmentIds.Count;
            // 예: "선택된 환경: 2개"
            selectedEnvironmentText.text = $"선택된 환경: {count}개";
        }
    }
}
