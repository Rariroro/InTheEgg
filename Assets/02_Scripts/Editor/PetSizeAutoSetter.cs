using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 모든 펫 프리펩의 CapsuleCollider radius를 기반으로 크기를 자동 설정하는 에디터 도구
/// </summary>
public class PetSizeAutoSetter : EditorWindow
{
    private Vector2 scrollPos;
    private List<string> updateLog = new List<string>();
    private Dictionary<string, PetTraits.Size> previewResults = new Dictionary<string, PetTraits.Size>();
    private bool showPreview = false;
    
    [MenuItem("Tools/Auto Set Pet Sizes from Collider")]
    public static void ShowWindow()
    {
        var window = GetWindow<PetSizeAutoSetter>("Pet Size Auto Setter");
        window.minSize = new Vector2(500, 400);
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("펫 크기 자동 설정 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // 크기 기준 설명
        EditorGUILayout.HelpBox(
            "CapsuleCollider radius 기준:\n" +
            "• Small (소형): radius < 1.5\n" +
            "• Medium (중형): 1.5 ≤ radius < 3.0\n" +
            "• Large (대형): radius ≥ 3.0", 
            MessageType.Info);
        
        EditorGUILayout.Space();
        
        // 미리보기 버튼
        if (GUILayout.Button("미리보기", GUILayout.Height(25)))
        {
            PreviewPetSizes();
        }
        
        // 미리보기 결과 표시
        if (showPreview && previewResults.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("미리보기 결과:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
            
            foreach (var kvp in previewResults)
            {
                string sizeText = kvp.Value.ToString();
                Color textColor = kvp.Value == PetTraits.Size.Small ? Color.green :
                                 kvp.Value == PetTraits.Size.Medium ? Color.yellow :
                                 Color.red;
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(200));
                
                GUI.color = textColor;
                EditorGUILayout.LabelField(sizeText, EditorStyles.boldLabel, GUILayout.Width(80));
                GUI.color = Color.white;
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.Space();
        
        // 실제 적용 버튼
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("모든 펫 크기 자동 설정 적용", GUILayout.Height(35)))
        {
            if (EditorUtility.DisplayDialog("확인", 
                "모든 펫 프리펩의 크기를 CapsuleCollider radius 기준으로 자동 설정하시겠습니까?", 
                "예", "아니오"))
            {
                ApplyPetSizes();
            }
        }
        GUI.backgroundColor = Color.white;
        
        // 업데이트 로그 표시
        if (updateLog.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("업데이트 로그:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Vector2 logScrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
            
            foreach (var log in updateLog)
            {
                EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            
            if (GUILayout.Button("로그 지우기"))
            {
                updateLog.Clear();
                previewResults.Clear();
                showPreview = false;
            }
        }
    }
    
    private void PreviewPetSizes()
    {
        previewResults.Clear();
        updateLog.Clear();
        
        string prefabPath = "Assets/03_Prefabs/Pet";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabPath });
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab == null) continue;
            
            PetController petController = prefab.GetComponent<PetController>();
            if (petController == null) continue;
            
            CapsuleCollider collider = prefab.GetComponentInChildren<CapsuleCollider>();
            if (collider == null)
            {
                updateLog.Add($"[경고] {prefab.name} - CapsuleCollider 없음");
                continue;
            }
            
            float radius = collider.radius;
            PetTraits.Size detectedSize = DetectSize(radius);
            
            previewResults[prefab.name] = detectedSize;
            updateLog.Add($"{prefab.name}: radius={radius:F2} → {detectedSize}");
        }
        
        showPreview = true;
        updateLog.Add($"\n총 {previewResults.Count}개 펫 분석 완료");
    }
    
    private void ApplyPetSizes()
    {
        updateLog.Clear();
        updateLog.Add("=== 펫 크기 자동 설정 시작 ===");
        
        string prefabPath = "Assets/03_Prefabs/Pet";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabPath });
        
        int successCount = 0;
        int failCount = 0;
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab == null) continue;
            
            PetController petController = prefab.GetComponent<PetController>();
            if (petController == null)
            {
                updateLog.Add($"[스킵] {prefab.name} - PetController 없음");
                continue;
            }
            
            CapsuleCollider collider = prefab.GetComponentInChildren<CapsuleCollider>();
            if (collider == null)
            {
                updateLog.Add($"[실패] {prefab.name} - CapsuleCollider 없음");
                failCount++;
                continue;
            }
            
            float radius = collider.radius;
            PetTraits.Size detectedSize = DetectSize(radius);
            
            // 프리펩 수정
            using (var prefabScope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                GameObject prefabRoot = prefabScope.prefabContentsRoot;
                PetController controller = prefabRoot.GetComponent<PetController>();
                
                if (controller != null)
                {
                    // SerializedObject를 사용하여 private 필드 수정
                    SerializedObject serializedController = new SerializedObject(controller);
                    SerializedProperty profileProp = serializedController.FindProperty("profile");
                    
                    if (profileProp != null)
                    {
                        SerializedProperty sizeProp = profileProp.FindPropertyRelative("size");
                        if (sizeProp != null)
                        {
                            sizeProp.enumValueIndex = (int)detectedSize;
                            serializedController.ApplyModifiedProperties();
                            
                            updateLog.Add($"[성공] {prefab.name}: radius={radius:F2} → {detectedSize}");
                            successCount++;
                        }
                        else
                        {
                            updateLog.Add($"[실패] {prefab.name} - size 필드를 찾을 수 없음");
                            failCount++;
                        }
                    }
                    else
                    {
                        updateLog.Add($"[실패] {prefab.name} - profile 필드를 찾을 수 없음");
                        failCount++;
                    }
                }
            }
        }
        
        updateLog.Add($"\n=== 업데이트 완료 ===");
        updateLog.Add($"성공: {successCount}개, 실패: {failCount}개");
        
        // 크기별 통계
        var sizeStats = new Dictionary<PetTraits.Size, int>();
        foreach (var kvp in previewResults)
        {
            if (!sizeStats.ContainsKey(kvp.Value))
                sizeStats[kvp.Value] = 0;
            sizeStats[kvp.Value]++;
        }
        
        updateLog.Add($"\n=== 크기별 분포 ===");
        foreach (var stat in sizeStats)
        {
            updateLog.Add($"{stat.Key}: {stat.Value}개");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("완료", 
            $"펫 크기 자동 설정이 완료되었습니다.\n성공: {successCount}개\n실패: {failCount}개", 
            "확인");
    }
    
    private PetTraits.Size DetectSize(float radius)
    {
        if (radius < 1.5f)
            return PetTraits.Size.Small;
        else if (radius < 3.0f)
            return PetTraits.Size.Medium;
        else
            return PetTraits.Size.Large;
    }
}