using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 모든 펫 프리펩의 프로필을 일괄 업데이트하는 에디터 도구
/// </summary>
public class PetProfileUpdater : EditorWindow
{
    private PetDataDatabase database;
    private Vector2 scrollPos;
    private List<string> updateLog = new List<string>();
    
    [MenuItem("Tools/Update Pet Profiles")]
    public static void ShowWindow()
    {
        var window = GetWindow<PetProfileUpdater>("Pet Profile Updater");
        window.minSize = new Vector2(400, 300);
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("펫 프로필 일괄 업데이트", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // 데이터베이스 선택
        database = (PetDataDatabase)EditorGUILayout.ObjectField("Pet Data Database", database, typeof(PetDataDatabase), false);
        
        if (database == null)
        {
            EditorGUILayout.HelpBox("먼저 PetDataDatabase를 생성하거나 선택해주세요.\n" +
                                  "Project 창에서 우클릭 > Create > Pet > Pet Data Database", MessageType.Info);
            
            if (GUILayout.Button("새 데이터베이스 생성 및 초기화"))
            {
                CreateAndInitializeDatabase();
            }
            return;
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("모든 펫 프리펩 업데이트", GUILayout.Height(30)))
        {
            UpdateAllPetPrefabs();
        }
        
        // 업데이트 로그 표시
        if (updateLog.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("업데이트 로그:", EditorStyles.boldLabel);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            foreach (var log in updateLog)
            {
                EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button("로그 지우기"))
            {
                updateLog.Clear();
            }
        }
    }
    
    private void CreateAndInitializeDatabase()
    {
        // 데이터베이스 생성
        database = ScriptableObject.CreateInstance<PetDataDatabase>();
        database.InitializeDatabase();
        
        // 에셋으로 저장
        string path = "Assets/09_GameDatas/PetDataDatabase.asset";
        
        // 디렉토리가 없으면 생성
        if (!AssetDatabase.IsValidFolder("Assets/09_GameDatas"))
        {
            AssetDatabase.CreateFolder("Assets", "09_GameDatas");
        }
        
        AssetDatabase.CreateAsset(database, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        updateLog.Add($"새 데이터베이스 생성됨: {path}");
        Debug.Log($"PetDataDatabase가 생성되었습니다: {path}");
    }
    
    private void UpdateAllPetPrefabs()
    {
        if (database == null)
        {
            EditorUtility.DisplayDialog("오류", "PetDataDatabase가 선택되지 않았습니다.", "확인");
            return;
        }
        
        updateLog.Clear();
        updateLog.Add("=== 펫 프리펩 업데이트 시작 ===");
        
        // 펫 프리펩 폴더 경로
        string prefabPath = "Assets/03_Prefabs/Pet";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabPath });
        
        int successCount = 0;
        int failCount = 0;
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab == null) continue;
            
            // PetController 컴포넌트 확인
            PetController petController = prefab.GetComponent<PetController>();
            if (petController == null)
            {
                updateLog.Add($"[스킵] {prefab.name} - PetController 없음");
                continue;
            }
            
            // 프리펩 이름에서 PetType 추출
            string prefabName = prefab.name.TrimStart('_');
            
            // 특수 케이스 매핑
            Dictionary<string, string> nameMapping = new Dictionary<string, string>
            {
                { "Anteater_A", "Anteater" },
                { "Armadillo_B", "Armadillo" },
                { "BearA", "Bear" },
                { "Bison_A", "Bison" },
                { "BoarA", "Boar" },
                { "BuffaloB", "Buffalo" },
                { "BullA", "Bull" },
                { "Camel_B", "Camel" },
                { "ChameleonA", "Chameleon" },
                { "CrocodileA", "Crocodile" },
                { "DuckB", "Duck" },
                { "ElephantA", "Elephant" },
                { "Elk_A", "Elk" },
                { "GoatA", "Goat" },
                { "GorillaA", "Gorilla" },
                { "Iguana_A", "Iguana" },
                { "LeopardA", "Leopard" },
                { "Malayan_A", "Malayan" },
                { "MonkeyA", "Monkey" },
                { "MuleA", "Mule" },
                { "Otter_A", "Otter" },
                { "OwlC", "Owl" },
                { "Pangolin_A", "Pangolin" },
                { "PlatypusA", "Platypus" },
                { "Possum_A", "Possum" },
                { "SheepB", "Sheep" },
                { "Sloth_A", "Sloth" },
                { "SquirrelA", "Squirrel" },
                { "TigerA", "Tiger" },
                { "WolfB", "Wolf" }
            };
            
            // 매핑된 이름이 있으면 사용, 없으면 원래 이름 사용
            if (nameMapping.ContainsKey(prefabName))
            {
                prefabName = nameMapping[prefabName];
            }
            
            // PetType enum 파싱
            if (!System.Enum.TryParse<PetType>(prefabName, out PetType petType))
            {
                updateLog.Add($"[실패] {prefab.name} - PetType 파싱 실패 ({prefabName})");
                failCount++;
                continue;
            }
            
            // 데이터베이스에서 데이터 가져오기
            PetData petData = database.GetPetData(petType);
            if (petData == null)
            {
                updateLog.Add($"[실패] {prefab.name} - 데이터베이스에 없음");
                failCount++;
                continue;
            }
            
            // 프리펩 수정 시작
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
                        profileProp.FindPropertyRelative("type").enumValueIndex = (int)petData.petType;
                        profileProp.FindPropertyRelative("personality").enumValueIndex = (int)petData.personality;
                        profileProp.FindPropertyRelative("diet").intValue = (int)petData.diet;
                        profileProp.FindPropertyRelative("habitat").enumValueIndex = (int)petData.habitat;
                        
                        serializedController.ApplyModifiedProperties();
                        
                        updateLog.Add($"[성공] {prefab.name} - {petData.petType}, {petData.personality}, {petData.diet}, {petData.habitat}");
                        successCount++;
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
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("완료", 
            $"펫 프리펩 업데이트가 완료되었습니다.\n성공: {successCount}개\n실패: {failCount}개", 
            "확인");
    }
}