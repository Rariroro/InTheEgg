using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PetSelectionManager : MonoBehaviour
{
    // 싱글톤 패턴 구현
    public static PetSelectionManager Instance { get; private set; }

    // 선택된 펫 ID 목록
    public List<string> selectedPetIds = new List<string>();

    private void Awake()
    {
        // 싱글톤 처리
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 펫 ID 추가 메서드
    public void AddPet(string petId)
    {
        if (!selectedPetIds.Contains(petId))
        {
            selectedPetIds.Add(petId);
            Debug.Log($"펫 추가: {petId}, 현재 선택된 펫: {selectedPetIds.Count}개");
        }
    }

    // 펫 ID 제거 메서드
    public void RemovePet(string petId)
    {
        if (selectedPetIds.Contains(petId))
        {
            selectedPetIds.Remove(petId);
            Debug.Log($"펫 제거: {petId}, 현재 선택된 펫: {selectedPetIds.Count}개");
        }
    }

    // 선택된 모든 펫 초기화
    public void ClearSelectedPets()
    {
        selectedPetIds.Clear();
        Debug.Log("선택된 모든 펫 초기화");
    }

    // 두 번째 씬으로 이동 (수정된 버전)
// 두 번째 씬으로 이동 (다중 환경 선택 지원)
public void LoadGameScene()
{
    // 최소 한 개의 펫이 선택되었는지 확인
    if (selectedPetIds.Count > 0)
    {
        // 환경 선택 여부 로깅 (선택사항)
        if (EnvironmentSelectionManager.Instance != null)
        {
            int environmentCount = EnvironmentSelectionManager.Instance.selectedEnvironmentIds.Count;
            
            if (environmentCount == 0)
            {
                // 경고 메시지는 선택사항 (환경을 선택하지 않아도 괜찮음)
                Debug.Log("환경이 선택되지 않았습니다. 기본 지형만 사용합니다.");
            }
            else
            {
                Debug.Log($"선택된 환경 수: {environmentCount}");
            }
        }
        
        // 씬 로드
        SceneManager.LoadScene("PetVillge");
    }
    else
    {
        Debug.LogWarning("최소 하나 이상의 펫을 선택해야 합니다!");
    }
}
}