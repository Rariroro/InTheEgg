using UnityEngine;
using System.Collections.Generic;

public class EnvironmentManager : MonoBehaviour
{
    // 환경 프리팹들 (인스펙터에서 할당)
    [Header("환경 프리팹")]
    public GameObject forestEnvironment;
    public GameObject desertEnvironment;
    public GameObject snowEnvironment;

    // 환경 ID와 프리팹 연결을 위한 딕셔너리
    private Dictionary<string, GameObject> environmentPrefabs = new Dictionary<string, GameObject>();

    private void Awake()
    {
        // 딕셔너리 초기화
        environmentPrefabs.Add("env_forest", forestEnvironment);
        environmentPrefabs.Add("env_desert", desertEnvironment);
        environmentPrefabs.Add("env_snow", snowEnvironment);
    }

    private void Start()
    {
        // EnvironmentSelectionManager가 존재하는지 확인
        if (EnvironmentSelectionManager.Instance != null)
        {
            // 선택된 환경만 스폰
            SpawnSelectedEnvironments();
        }
        else
        {
            Debug.LogWarning("EnvironmentSelectionManager가 없습니다.");
        }
    }

    // 선택된 모든 환경 스폰
    private void SpawnSelectedEnvironments()
    {
        List<string> selectedIds = EnvironmentSelectionManager.Instance.selectedEnvironmentIds;
        
        Debug.Log($"선택된 환경 수: {selectedIds.Count}");
        
        // 선택된 환경이 없어도 괜찮음 (하나도 선택하지 않은 경우)
        if (selectedIds.Count == 0)
        {
            Debug.Log("선택된 환경이 없습니다. 기본 지형만 사용합니다.");
            return;
        }
        
        // 모든 선택된 환경 스폰
        foreach (string environmentId in selectedIds)
        {
            SpawnEnvironment(environmentId);
        }
    }
    
    // 지정된 ID의 환경 스폰
    private void SpawnEnvironment(string environmentId)
    {
        // 환경 ID에 해당하는 프리팹 가져오기
        if (!environmentPrefabs.ContainsKey(environmentId))
        {
            Debug.LogError($"알 수 없는 환경 ID: {environmentId}");
            return;
        }
        
        GameObject prefab = environmentPrefabs[environmentId];
        
        // 프리팹 존재 여부 확인
        if (prefab == null)
        {
            Debug.LogError($"환경 프리팹이 할당되지 않았습니다: {environmentId}");
            return;
        }
        
        // 프리팹 그대로 인스턴스화 (프리팹의 위치/회전 사용)
        Instantiate(prefab);
        Debug.Log($"환경 생성 완료: {environmentId}");
    }
}