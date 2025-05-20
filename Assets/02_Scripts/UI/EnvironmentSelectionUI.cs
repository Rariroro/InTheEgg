using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class EnvironmentSelectionUI : MonoBehaviour
{
    [Header("환경 UI 요소")]
    public GameObject togglePrefab;          // 토글 버튼 프리팹
    public Transform toggleContainer;         // 토글 버튼들이 배치될 부모 오브젝트
    public int columns = 3;                   // 그리드 열 수
    public float toggleSpacing = 100f;        // 토글 간격
    public TMP_Text selectedEnvironmentText;  // 선택된 환경 수 표시 텍스트

    private List<Toggle> environmentToggles = new List<Toggle>();

    void Start()
    {
        // EnvironmentSelectionManager 초기화
        if (EnvironmentSelectionManager.Instance == null)
        {
            GameObject managerObj = new GameObject("EnvironmentSelectionManager");
            managerObj.AddComponent<EnvironmentSelectionManager>();
        }

        // 이전에 선택된 환경 초기화
        EnvironmentSelectionManager.Instance.ClearSelectedEnvironments();

        // 토글 버튼 생성
        CreateEnvironmentToggles();

        // 선택된 환경 텍스트 업데이트
        UpdateSelectedCountText();
    }

    void CreateEnvironmentToggles()
    {
        if (togglePrefab == null || toggleContainer == null)
        {
            Debug.LogError("토글 프리팹 또는 컨테이너가 할당되지 않았습니다!");
            return;
        }

        // 환경 ID 예시: "env_forest", "env_desert", "env_snow"
        string[] environmentIds = { "env_forest", "env_desert", "env_snow" };

        for (int i = 0; i < environmentIds.Length; i++)
        {
            // 토글 인스턴스 생성
            GameObject toggleObj = Instantiate(togglePrefab, toggleContainer);
            
            // 위치 조정 (그리드 레이아웃)
            int row = (i) / columns;
            int col = (i) % columns;
            RectTransform rectTransform = toggleObj.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(col * toggleSpacing, -row * toggleSpacing);

            // 토글 설정
            Toggle toggle = toggleObj.GetComponent<Toggle>();
            
            // 환경 ID 설정
            string environmentId = environmentIds[i];
            
            // 토글 라벨 텍스트 설정
            TMP_Text labelText = toggleObj.GetComponentInChildren<TMP_Text>();
            if (labelText != null)
            {
                labelText.text = environmentId;
            }

            // 토글 이벤트 등록 (다중 선택 방식)
            string id = environmentId; // 클로저에서 사용할 변수
            toggle.onValueChanged.AddListener((isOn) => OnToggleValueChanged(id, isOn));
            
            environmentToggles.Add(toggle);
        }
    }

    void OnToggleValueChanged(string environmentId, bool isOn)
    {
        if (isOn)
        {
            EnvironmentSelectionManager.Instance.AddEnvironment(environmentId);
        }
        else
        {
            EnvironmentSelectionManager.Instance.RemoveEnvironment(environmentId);
        }
        
        UpdateSelectedCountText();
    }

    void UpdateSelectedCountText()
    {
        if (selectedEnvironmentText != null)
        {
            int count = EnvironmentSelectionManager.Instance.selectedEnvironmentIds.Count;
            selectedEnvironmentText.text = $"선택된 환경: {count}개";
        }
    }
}