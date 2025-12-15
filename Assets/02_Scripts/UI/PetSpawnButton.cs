using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [DEPRECATED] 이 클래스는 더 이상 사용되지 않습니다.
/// 펫 스폰이 자동화되어 버튼 방식이 폐기되었습니다.
/// PetManager가 Flutter 모드와 PetChoice 모드 모두에서 자동으로 펫을 스폰합니다.
/// 씬에서 이 컴포넌트가 붙은 오브젝트를 비활성화하거나 삭제하세요.
/// </summary>
[System.Obsolete("PetSpawnButton is deprecated. Pet spawning is now automatic in PetManager.")]
public class PetSpawnButton : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    public Button spawnButton;
    public TMP_Text buttonText;

    [Header("텍스트 설정")]
    public string defaultButtonText = "펫 스폰";
    public string completedButtonText = "스폰 완료";

    private void Start()
    {
        // 이 컴포넌트는 deprecated됨 - 자동으로 비활성화
        Debug.LogWarning("[PetSpawnButton] 이 컴포넌트는 더 이상 사용되지 않습니다. 펫 스폰이 자동화되었습니다. 이 오브젝트를 삭제하세요.");

        if (spawnButton != null)
        {
            spawnButton.interactable = false;
        }

        if (buttonText != null)
        {
            buttonText.text = "자동 스폰";
        }

        // 오브젝트 비활성화
        gameObject.SetActive(false);
    }
}
