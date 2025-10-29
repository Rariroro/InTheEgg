using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LegendaryPet;

public class LegendaryPetSpawnButton : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    public Button spawnButton;
    public TMP_Text buttonText;

    [Header("텍스트 설정")]
    public string defaultButtonText = "레전드펫 스폰";
    public string completedButtonText = "스폰 완료";

    private LegendaryPetManager legendaryPetManager;

    private void Start()
    {
        // LegendaryPetManager 찾기
        legendaryPetManager = LegendaryPetManager.Instance;

        if (legendaryPetManager == null)
        {
            Debug.LogError("[LegendaryPetSpawnButton] LegendaryPetManager를 찾을 수 없습니다!");
            if (spawnButton != null)
                spawnButton.interactable = false;
            return;
        }

        // 버튼 클릭 이벤트 등록
        if (spawnButton != null)
        {
            spawnButton.onClick.AddListener(OnSpawnButtonClicked);
        }

        // 초기 버튼 상태 업데이트
        UpdateButtonState();
    }

    private void Update()
    {
        // 매 프레임 버튼 상태 업데이트
        if (legendaryPetManager != null)
        {
            UpdateButtonState();
        }
    }

    private void OnSpawnButtonClicked()
    {
        if (legendaryPetManager != null && legendaryPetManager.CanSpawnNextLegendaryPet())
        {
            legendaryPetManager.SpawnNextLegendaryPet();
            UpdateButtonState();
        }
    }

    private void UpdateButtonState()
    {
        if (legendaryPetManager == null || spawnButton == null) return;

        int totalLegends = legendaryPetManager.GetTotalLegendaryPetCount();
        int spawnedLegends = legendaryPetManager.GetCurrentLegendSpawnIndex();

        // 버튼 활성화 상태 설정
        bool canSpawn = legendaryPetManager.CanSpawnNextLegendaryPet();
        spawnButton.interactable = canSpawn;

        // 버튼 텍스트 업데이트
        if (buttonText != null)
        {
            if (spawnedLegends >= totalLegends)
            {
                buttonText.text = completedButtonText;
            }
            else
            {
                buttonText.text = $"{defaultButtonText} ({spawnedLegends}/{totalLegends})";
            }
        }
    }

    private void OnDestroy()
    {
        // 버튼 이벤트 해제
        if (spawnButton != null)
        {
            spawnButton.onClick.RemoveListener(OnSpawnButtonClicked);
        }
    }
}
