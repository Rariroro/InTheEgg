using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PetSpawnButton : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    public Button spawnButton;
    public TMP_Text buttonText;

    [Header("텍스트 설정")]
    public string defaultButtonText = "펫 스폰";
    public string completedButtonText = "스폰 완료";

    private PetManager petManager;

    private void Start()
    {
        // PetManager 찾기
        petManager = FindObjectOfType<PetManager>();

        if (petManager == null)
        {
            Debug.LogError("[PetSpawnButton] PetManager를 찾을 수 없습니다!");
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
        if (petManager != null)
        {
            UpdateButtonState();
        }
    }

    private void OnSpawnButtonClicked()
    {
        if (petManager != null && petManager.CanSpawnNextPet())
        {
            petManager.SpawnNextPet();
            UpdateButtonState();
        }
    }

    private void UpdateButtonState()
    {
        if (petManager == null || spawnButton == null) return;

        int totalPets = petManager.GetTotalPetCount();
        int spawnedPets = petManager.GetCurrentSpawnIndex();

        // 버튼 활성화 상태 설정
        bool canSpawn = petManager.CanSpawnNextPet();
        spawnButton.interactable = canSpawn;

        // 버튼 텍스트 업데이트
        if (buttonText != null)
        {
            if (spawnedPets >= totalPets)
            {
                buttonText.text = completedButtonText;
            }
            else
            {
                buttonText.text = $"{defaultButtonText} ({spawnedPets}/{totalPets})";
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
