using System.Collections.Generic;
using UnityEngine;

public class EnvironmentSelectionManager : MonoBehaviour
{
    // 싱글톤 패턴 구현
    public static EnvironmentSelectionManager Instance { get; private set; }

    // 선택된 환경 ID 목록 (다중 선택 지원)
    public List<string> selectedEnvironmentIds = new List<string>();

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

    // 환경 ID 추가 메서드
    public void AddEnvironment(string environmentId)
    {
        if (!selectedEnvironmentIds.Contains(environmentId))
        {
            selectedEnvironmentIds.Add(environmentId);
            Debug.Log($"환경 추가: {environmentId}, 현재 선택된 환경: {selectedEnvironmentIds.Count}개");
        }
    }

    // 환경 ID 제거 메서드
    public void RemoveEnvironment(string environmentId)
    {
        if (selectedEnvironmentIds.Contains(environmentId))
        {
            selectedEnvironmentIds.Remove(environmentId);
            Debug.Log($"환경 제거: {environmentId}, 현재 선택된 환경: {selectedEnvironmentIds.Count}개");
        }
    }

    // 선택된 모든 환경 초기화
    public void ClearSelectedEnvironments()
    {
        selectedEnvironmentIds.Clear();
        Debug.Log("선택된 모든 환경 초기화");
    }
}