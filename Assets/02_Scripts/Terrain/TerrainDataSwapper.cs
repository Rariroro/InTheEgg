using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]  // 에디터와 런타임 모두에서 스크립트 실행
public class TerrainDataSwapper : MonoBehaviour
{
    // 교체할 TerrainData를 인스펙터에서 할당
    public TerrainData newTerrainData;

    // 값이 변경될 때마다 호출 (에디터에서 인스펙터 값을 수정할 때 자동 호출)
    void OnValidate()
    {
        SwapTerrainData();
    }

    void SwapTerrainData()
    {
        Terrain terrain = GetComponent<Terrain>();
        if (terrain != null && newTerrainData != null)
        {
            terrain.terrainData = newTerrainData;
            
            // 에디터에서 변경 사항을 기록하여 씬에 저장되도록 함
            #if UNITY_EDITOR
            EditorUtility.SetDirty(terrain);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            #endif
        }
    }
}
