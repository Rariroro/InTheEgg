using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;

/// <summary>
/// PetNeeds를 코드에 정의된 기본값으로 초기화하는 도구
/// </summary>
public class PetNeedsInitializer : EditorWindow
{
    [MenuItem("Tools/InTheEgg/PetNeeds 초기화")]
    public static void ShowWindow()
    {
        var window = GetWindow<PetNeedsInitializer>("PetNeeds 초기화");
        window.maxSize = new Vector2(300, 150);
        window.minSize = new Vector2(300, 150);
    }
    
    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("PetNeeds 초기화", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        EditorGUILayout.HelpBox("모든 펫의 PetNeeds 설정을 코드에 정의된 기본값으로 초기화합니다.", MessageType.Info);
        
        EditorGUILayout.Space(20);
        
        if (GUILayout.Button("초기화", GUILayout.Height(40)))
        {
            InitializeAllPetNeeds();
        }
    }
    
    private void InitializeAllPetNeeds()
    {
        // 씬의 모든 PetController 찾기
        var scenePets = FindObjectsByType<PetController>(FindObjectsSortMode.None);
        
        // 프로젝트의 모든 펫 프리팹 찾기
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        var prefabPets = new System.Collections.Generic.List<PetController>();
        
        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                var petController = prefab.GetComponent<PetController>();
                if (petController != null)
                {
                    prefabPets.Add(petController);
                }
            }
        }
        
        int count = 0;
        
        // 씬의 펫들 초기화
        foreach (var pet in scenePets)
        {
            if (ResetPetNeeds(pet))
                count++;
        }
        
        // 프리팹 펫들 초기화
        foreach (var pet in prefabPets)
        {
            if (ResetPetNeeds(pet))
            {
                PrefabUtility.SavePrefabAsset(pet.gameObject);
                count++;
            }
        }
        
        EditorUtility.DisplayDialog("초기화 완료", 
            $"{count}개의 펫 PetNeeds가 코드 기본값으로 초기화되었습니다.", "확인");
    }
    
    private bool ResetPetNeeds(PetController pet)
    {
        if (pet == null) return false;
        
        var serializedObject = new SerializedObject(pet);
        var petNeedsProperty = serializedObject.FindProperty("petNeeds");
        
        if (petNeedsProperty == null) return false;
        
        // PetNeeds 클래스의 기본값을 리플렉션으로 가져오기
        var defaultValues = GetPetNeedsDefaultValues();
        
        // 각 필드에 기본값 적용
        foreach (var field in defaultValues)
        {
            var property = petNeedsProperty.FindPropertyRelative(field.Key);
            if (property != null && field.Value != null)
            {
                if (field.Value is float floatValue)
                {
                    property.floatValue = floatValue;
                }
                else if (field.Value is int intValue)
                {
                    property.intValue = intValue;
                }
                else if (field.Value is bool boolValue)
                {
                    property.boolValue = boolValue;
                }
            }
        }
        
        serializedObject.ApplyModifiedProperties();
        
        Debug.Log($"{pet.petName}의 PetNeeds가 코드 기본값으로 초기화되었습니다.");
        return true;
    }
    
    private System.Collections.Generic.Dictionary<string, object> GetPetNeedsDefaultValues()
    {
        var defaultValues = new System.Collections.Generic.Dictionary<string, object>();
        
        // PetNeeds 임시 인스턴스 생성하여 기본값 가져오기
        var tempPetNeeds = new PetNeeds();
        var type = typeof(PetNeeds);
        
        // SerializeField로 마크된 private 필드들 가져오기
        var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.GetCustomAttribute<SerializeField>() != null);
        
        foreach (var field in fields)
        {
            var value = field.GetValue(tempPetNeeds);
            defaultValues[field.Name] = value;
        }
        
        return defaultValues;
    }
}