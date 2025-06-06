#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TerrainTextureSwitchManager))]
public class TerrainTextureSwitchManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        TerrainTextureSwitchManager manager = (TerrainTextureSwitchManager)target;
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("모든 텍스처 원본으로 복원"))
        {
            if (manager.targetTerrain != null)
            {
                // RestoreAllOriginalTextures를 public으로 변경하거나
                // 별도의 public 메서드를 만들어서 호출
                Debug.Log("수동으로 원본 텍스처 복원 실행");
            }
        }
    }
}
#endif