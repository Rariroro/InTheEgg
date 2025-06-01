using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PetSelectionManager 클래스는 플레이어가 선택한 펫 ID를 관리하고,
/// 게임 씬 전환 시 선택된 펫 목록을 유지하기 위한 싱글톤 매니저 역할을 수행한다.
/// </summary>
public class PetSelectionManager : MonoBehaviour
{
    // 싱글톤 인스턴스를 외부에서 읽기 전용으로 접근할 수 있도록 설정
    public static PetSelectionManager Instance { get; private set; }

    // 플레이어가 선택한 펫 ID 목록을 저장하는 리스트
    public List<string> selectedPetIds = new List<string>();

    /// <summary>
    /// Awake() 메서드는 현재 오브젝트가 생성될 때 가장 먼저 호출된다.
    /// 싱글톤 패턴을 구현하여 중복된 인스턴스가 존재하지 않도록 처리한다.
    /// </summary>
    private void Awake()
    {
        // Instance가 아직 할당되지 않은 경우, 현재 오브젝트를 인스턴스로 지정하고 파괴되지 않도록 설정
        if (Instance == null)
        {
            Instance = this;
            // 씬 전환 시에도 이 게임 오브젝트가 파괴되지 않도록 설정
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 이미 다른 인스턴스가 존재한다면, 중복 생성된 오브젝트를 즉시 파괴
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// AddPet 메서드는 특정 펫 ID를 선택 목록에 추가한다.
    /// 이미 리스트에 존재하지 않는 경우에만 추가하며, 추가 시 Debug.Log로 상태를 출력한다.
    /// </summary>
    /// <param name="petId">추가할 펫의 고유 ID (예: "pet_001")</param>
    public void AddPet(string petId)
    {
        // 중복 방지를 위해 해당 ID가 리스트에 없는 경우에만 추가
        if (!selectedPetIds.Contains(petId))
        {
            selectedPetIds.Add(petId);
            Debug.Log($"펫 추가: {petId}, 현재 선택된 펫: {selectedPetIds.Count}개");
        }
    }

    /// <summary>
    /// RemovePet 메서드는 특정 펫 ID를 선택 목록에서 제거한다.
    /// 리스트에 존재할 경우에만 제거하며, 제거 시 Debug.Log로 상태를 출력한다.
    /// </summary>
    /// <param name="petId">제거할 펫의 고유 ID</param>
    public void RemovePet(string petId)
    {
        // 리스트에 해당 ID가 존재하는 경우에만 제거
        if (selectedPetIds.Contains(petId))
        {
            selectedPetIds.Remove(petId);
            Debug.Log($"펫 제거: {petId}, 현재 선택된 펫: {selectedPetIds.Count}개");
        }
    }

    /// <summary>
    /// ClearSelectedPets 메서드는 선택된 펫 목록을 모두 초기화(비움)한다.
    /// 씬 전환 시 이전 선택 상태를 유지하지 않도록 호출한다.
    /// </summary>
    public void ClearSelectedPets()
    {
        selectedPetIds.Clear();
        Debug.Log("선택된 모든 펫 초기화");
    }

    /// <summary>
    /// LoadGameScene 메서드는 선택된 펫이 하나 이상 있을 때에만 실제 게임 씬으로 전환한다.
    /// 또한, 환경(Environment) 선택 매니저가 존재하면 선택된 환경 개수를 확인하고 로그를 출력한다.
    /// </summary>
    public void LoadGameScene()
    {
        // 최소 하나의 펫이 선택되었는지 확인
        if (selectedPetIds.Count > 0)
        {
            // EnvironmentSelectionManager가 관리 중인 환경 선택 목록이 있으면 개수를 확인
            if (EnvironmentSelectionManager.Instance != null)
            {
                int environmentCount = EnvironmentSelectionManager.Instance.selectedEnvironmentIds.Count;

                // 환경을 선택하지 않은 경우 경고 로그를 출력하되, 기본 지형만 사용하도록 허용
                if (environmentCount == 0)
                {
                    Debug.Log("환경이 선택되지 않았습니다. 기본 지형만 사용합니다.");
                }
                else
                {
                    Debug.Log($"선택된 환경 수: {environmentCount}");
                }
            }

            // PetVillage 씬을 로드 (씬 이름과 실제 빌드 설정에 등록된 이름이 동일해야 함)
            SceneManager.LoadScene("PetVillge");
        }
        else
        {
            // 펫을 하나도 선택하지 않은 상태에서 호출되면 경고 로그 출력
            Debug.LogWarning("최소 하나 이상의 펫을 선택해야 합니다!");
        }
    }
}
