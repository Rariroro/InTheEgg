using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using LegendaryPet;

/// <summary>
/// 테스트용 씬 전환 버튼
/// </summary>
public class TestSceneButton : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("이동할 씬 이름")]
    public string targetSceneName = "Playground";

    [Tooltip("버튼 컴포넌트 (없으면 자동 검색)")]
    public Button button;

    private void Start()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
        else
        {
            Debug.LogWarning("[TestSceneButton] 버튼 컴포넌트를 찾을 수 없습니다!");
        }
    }

    private void OnButtonClick()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[TestSceneButton] 대상 씬 이름이 설정되지 않았습니다!");
            return;
        }

        Debug.Log($"[TestSceneButton] {targetSceneName} 씬으로 이동합니다.");

        // 씬 전환 전 정리 작업
        CleanupBeforeSceneChange();

        SceneManager.LoadScene(targetSceneName);
    }

    /// <summary>
    /// 씬 전환 전 모든 펫 관련 코루틴 및 상호작용 정리
    /// </summary>
    private void CleanupBeforeSceneChange()
    {
        // 모든 PetController의 코루틴 중지
        var petControllers = FindObjectsByType<PetController>(FindObjectsSortMode.None);
        foreach (var pet in petControllers)
        {
            if (pet != null)
            {
                pet.StopAllCoroutines();
            }
        }

        // 모든 BasePetInteraction의 코루틴 중지
        var interactions = FindObjectsByType<BasePetInteraction>(FindObjectsSortMode.None);
        foreach (var interaction in interactions)
        {
            if (interaction != null)
            {
                interaction.StopAllCoroutines();
            }
        }

        // 모든 PetAnimationController의 코루틴 중지
        var animControllers = FindObjectsByType<PetAnimationController>(FindObjectsSortMode.None);
        foreach (var animController in animControllers)
        {
            if (animController != null)
            {
                animController.StopAllCoroutines();
            }
        }

        // 레전드 펫도 정리
        var legendaryPets = FindObjectsByType<LegendaryPetController>(FindObjectsSortMode.None);
        foreach (var pet in legendaryPets)
        {
            if (pet != null)
            {
                pet.StopAllCoroutines();
            }
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
        }
    }
}
