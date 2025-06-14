using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Collections;

public class EnvironmentSelectionUI : MonoBehaviour
{
    [Header("환경 UI 요소")]
    public GameObject togglePrefab;          // 토글 버튼 프리팹
    public Transform toggleContainer;        // 토글 버튼들이 배치될 부모 오브젝트
    public int columns = 3;                  // 그리드 형태의 열 수
    public float toggleSpacing = 100f;       // 토글 간격
    public TMP_Text selectedEnvironmentText;  // 선택된 환경 개수를 표시할 텍스트
    public Button selectAllEnvironmentsButton; // 모든 환경 최초등장 선택 버튼


    // UI 토글들을 관리하기 위한 리스트
    private List<Toggle> environmentToggles = new List<Toggle>();
    private List<Toggle> firstAppearanceToggles = new List<Toggle>(); // 최초 등장 토글 리스트 (새로 추가)

    void Start()
    {
        // EnvironmentSelectionManager 인스턴스가 존재하는지 확인
        if (EnvironmentSelectionManager.Instance == null)
        {
            GameObject managerObj = new GameObject("EnvironmentSelectionManager");
            managerObj.AddComponent<EnvironmentSelectionManager>();
        }

        // 이전에 선택된 환경이 남아있을 수 있으므로 초기화
        EnvironmentSelectionManager.Instance.ClearSelectedEnvironments();

        // UI에 표시할 토글 버튼을 동적으로 생성
        CreateEnvironmentToggles();
  // ★ 모든 환경 최초등장 선택 버튼 이벤트 설정
    if (selectAllEnvironmentsButton != null)
    {
        selectAllEnvironmentsButton.onClick.AddListener(OnSelectAllEnvironmentsClicked);
    }
        // 선택된 환경 개수를 텍스트에 반영(초기 값: 0)
        UpdateSelectedCountText();
    }
// ★ 모든 환경 최초등장 선택 버튼 클릭 메서드
void OnSelectAllEnvironmentsClicked()
{
    // Debug.Log("모든 환경 최초등장 선택 버튼 클릭됨");
    
    // 모든 환경 토글 켜기
    for (int i = 0; i < environmentToggles.Count; i++)
    {
        if (environmentToggles[i] != null && !environmentToggles[i].isOn)
        {
            environmentToggles[i].isOn = true;
        }
    }
    
    // 잠시 대기 후 모든 최초등장 토글 켜기
    StartCoroutine(EnableAllFirstAppearanceToggles());
}

// ★ 모든 최초등장 토글을 켜는 코루틴
private IEnumerator EnableAllFirstAppearanceToggles()
{
    // 환경 토글들이 처리될 시간을 주기 위해 약간 대기
    yield return new WaitForSeconds(0.1f);
    
    // 모든 최초등장 토글 켜기
    for (int i = 0; i < firstAppearanceToggles.Count; i++)
    {
        if (firstAppearanceToggles[i] != null && firstAppearanceToggles[i].interactable && !firstAppearanceToggles[i].isOn)
        {
            firstAppearanceToggles[i].isOn = true;
        }
    }
    
    // Debug.Log($"총 {environmentToggles.Count}개 환경이 최초등장 상태로 선택되었습니다.");
    
    // 선택된 환경 개수 텍스트 업데이트
    UpdateSelectedCountText();
}
    void CreateEnvironmentToggles()
    {
        if (togglePrefab == null || toggleContainer == null)
        {
            Debug.LogError("토글 프리팹 또는 컨테이너가 할당되지 않았습니다!");
            return;
        }

        // 환경 ID 목록 (13가지)
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
            // 메인 컨테이너 생성 (환경 토글 + 최초 등장 토글을 담을 컨테이너)
            GameObject containerObj = new GameObject($"EnvironmentContainer_{i}");
            containerObj.transform.SetParent(toggleContainer);
            
            RectTransform containerRect = containerObj.AddComponent<RectTransform>();
            
            // 컨테이너 위치 설정 (그리드 레이아웃)
            int row = i / columns;
            int col = i % columns;
            containerRect.anchoredPosition = new Vector2(col * toggleSpacing, -row * toggleSpacing);
            containerRect.sizeDelta = new Vector2(toggleSpacing * 0.9f, toggleSpacing * 0.9f);

            // 환경 선택 토글 생성
            GameObject envToggleObj = Instantiate(togglePrefab, containerObj.transform);
            RectTransform envToggleRect = envToggleObj.GetComponent<RectTransform>();
            envToggleRect.anchoredPosition = new Vector2(0, 10); // 위쪽에 배치
            envToggleRect.sizeDelta = new Vector2(90, 40);

            Toggle envToggle = envToggleObj.GetComponent<Toggle>();
            
            // 현재 인덱스에 해당하는 환경 ID를 변수에 저장
            string environmentId = environmentIds[i];

            // 환경 토글 라벨 텍스트 설정
            TMP_Text envLabelText = envToggleObj.GetComponentInChildren<TMP_Text>();
            if (envLabelText != null)
            {
                envLabelText.text = environmentId;
                envLabelText.fontSize = 25; // 폰트 크기 조정
            }

            // 최초 등장 토글 생성
            GameObject firstAppearanceToggleObj = Instantiate(togglePrefab, containerObj.transform);
            RectTransform firstAppearanceRect = firstAppearanceToggleObj.GetComponent<RectTransform>();
            firstAppearanceRect.anchoredPosition = new Vector2(0, -50); // 아래쪽에 배치
            firstAppearanceRect.sizeDelta = new Vector2(90, 30);

            Toggle firstAppearanceToggle = firstAppearanceToggleObj.GetComponent<Toggle>();
            firstAppearanceToggle.interactable = false; // 처음에는 비활성화

            // 최초 등장 토글 라벨 텍스트 설정
            TMP_Text firstAppearanceLabelText = firstAppearanceToggleObj.GetComponentInChildren<TMP_Text>();
            if (firstAppearanceLabelText != null)
            {
                firstAppearanceLabelText.text = "Initial";
                firstAppearanceLabelText.fontSize = 25; // 작은 폰트 크기
            }

            // 환경 토글 이벤트 등록
            string id = environmentId; // 클로저에서 사용할 변수
            envToggle.onValueChanged.AddListener((isOn) => OnEnvironmentToggleValueChanged(id, isOn, firstAppearanceToggle));
            
            // 최초 등장 토글 이벤트 등록
            firstAppearanceToggle.onValueChanged.AddListener((isOn) => OnFirstAppearanceToggleValueChanged(id, isOn));

            // 생성된 토글을 리스트에 추가하여 추후 관리
            environmentToggles.Add(envToggle);
            firstAppearanceToggles.Add(firstAppearanceToggle);
        }
    }

    void OnEnvironmentToggleValueChanged(string environmentId, bool isOn, Toggle firstAppearanceToggle)
    {
        if (isOn)
        {
            // 토글이 켜진 경우 매니저에 환경 ID 추가
            EnvironmentSelectionManager.Instance.AddEnvironment(environmentId);
            firstAppearanceToggle.interactable = true; // 최초 등장 토글 활성화
        }
        else
        {
            // 토글이 꺼진 경우 매니저에서 환경 ID 제거
            EnvironmentSelectionManager.Instance.RemoveEnvironment(environmentId);
            firstAppearanceToggle.interactable = false; // 최초 등장 토글 비활성화
            firstAppearanceToggle.isOn = false; // 최초 등장 토글 해제
        }

        // 선택된 환경 개수 텍스트 업데이트
        UpdateSelectedCountText();
    }

    void OnFirstAppearanceToggleValueChanged(string environmentId, bool isOn)
    {
        EnvironmentSelectionManager.Instance.SetEnvironmentFirstAppearance(environmentId, isOn);
    }

    void UpdateSelectedCountText()
    {
        if (selectedEnvironmentText != null)
        {
            // 매니저에서 관리하는 selectedEnvironmentIds 리스트의 개수를 가져옴
            int count = EnvironmentSelectionManager.Instance.selectedEnvironmentIds.Count;
            int firstAppearanceCount = EnvironmentSelectionManager.Instance.firstAppearanceEnvironmentIds.Count;
            selectedEnvironmentText.text = $"선택된 환경: {count}개 (최초등장: {firstAppearanceCount}개)";
        }
    }
}