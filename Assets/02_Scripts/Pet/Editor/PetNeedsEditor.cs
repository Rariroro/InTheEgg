using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PetNeeds))]
public class PetNeedsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        PetNeeds petNeeds = (PetNeeds)target;
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("기본값으로 리셋"))
        {
            Undo.RecordObject(petNeeds, "Reset Pet Needs Rates");
            
            // 기본값으로 리셋
            petNeeds.HungerIncreaseRate = 0.5f;
            petNeeds.SleepinessIncreaseRate = 0.3f;
            petNeeds.AffectionDecreaseRateWhenHungry = 0.2f;
            
            EditorUtility.SetDirty(petNeeds);
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "기본 증가율:\n" +
            "• 배고픔: 0.5/초 (200초에 0→100)\n" +
            "• 졸림: 0.3/초 (333초에 0→100)\n" +
            "• 친밀도 감소: 0.2/초 (배고플 때)", 
            MessageType.Info
        );
    }
}

// 모든 펫의 욕구 증가율을 한번에 리셋하는 메뉴
public static class PetNeedsEditorMenu
{
    [MenuItem("Tools/Pet/모든 펫 욕구 증가율 리셋")]
    public static void ResetAllPetNeedsRates()
    {
        PetNeeds[] allPetNeeds = GameObject.FindObjectsOfType<PetNeeds>();
        
        if (allPetNeeds.Length == 0)
        {
            EditorUtility.DisplayDialog("알림", "씬에 PetNeeds 컴포넌트를 가진 펫이 없습니다.", "확인");
            return;
        }
        
        foreach (var petNeeds in allPetNeeds)
        {
            Undo.RecordObject(petNeeds, "Reset All Pet Needs Rates");
            
            petNeeds.HungerIncreaseRate = 0.5f;
            petNeeds.SleepinessIncreaseRate = 0.3f;
            petNeeds.AffectionDecreaseRateWhenHungry = 0.2f;
            
            EditorUtility.SetDirty(petNeeds);
        }
        
        Debug.Log($"{allPetNeeds.Length}개의 펫의 욕구 증가율이 기본값으로 리셋되었습니다.");
    }
}