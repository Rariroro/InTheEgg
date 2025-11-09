#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// PreferredZone 컴포넌트를 위한 커스텀 에디터
/// Zone Shape에 따라 관련 필드만 표시하여 더 깔끔한 Inspector를 제공합니다
/// </summary>
[CustomEditor(typeof(PreferredZone))]
public class PreferredZoneEditor : Editor
{
    private SerializedProperty zoneShapeProperty;
    private SerializedProperty habitatTypeProperty;
    private SerializedProperty zoneRadiusProperty;
    private SerializedProperty usePersonalityPreferenceProperty;
    private SerializedProperty preferredPersonalitiesProperty;
    private SerializedProperty behaviorDurationMultiplierProperty;
    
    void OnEnable()
    {
        // SerializedProperty 캐싱
        zoneShapeProperty = serializedObject.FindProperty("zoneShape");
        habitatTypeProperty = serializedObject.FindProperty("habitatType");
        zoneRadiusProperty = serializedObject.FindProperty("zoneRadius");
        usePersonalityPreferenceProperty = serializedObject.FindProperty("usePersonalityPreference");
        preferredPersonalitiesProperty = serializedObject.FindProperty("preferredPersonalities");
        behaviorDurationMultiplierProperty = serializedObject.FindProperty("behaviorDurationMultiplier");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        PreferredZone zone = (PreferredZone)target;
        
        // 타이틀
        EditorGUILayout.LabelField("구역 설정", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        // Zone Shape 선택
        EditorGUILayout.PropertyField(zoneShapeProperty, new GUIContent("구역 모양"));
        
        // Shape에 따른 조건부 표시
        if (zone.zoneShape == PreferredZone.ZoneShape.Sphere)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Sphere 설정", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(zoneRadiusProperty, new GUIContent("구역 반경"));
        }
        else if (zone.zoneShape == PreferredZone.ZoneShape.Box)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Box 설정", EditorStyles.miniBoldLabel);
            
            BoxCollider boxCollider = zone.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                EditorGUILayout.HelpBox("Box Collider가 필요합니다!", MessageType.Warning);
                
                if (GUILayout.Button("Box Collider 추가"))
                {
                    boxCollider = zone.gameObject.AddComponent<BoxCollider>();
                    boxCollider.isTrigger = true;
                    EditorUtility.SetDirty(zone.gameObject);
                }
            }
            else
            {
                if (!boxCollider.isTrigger)
                {
                    EditorGUILayout.HelpBox("Box Collider의 Is Trigger가 비활성화되어 있습니다.", MessageType.Info);
                    if (GUILayout.Button("Is Trigger 활성화"))
                    {
                        boxCollider.isTrigger = true;
                        EditorUtility.SetDirty(boxCollider);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"Box 크기: {boxCollider.size}", MessageType.None);
                }
            }
        }
        
        // 구분선
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(5);
        
        // 펫 속성 설정
        EditorGUILayout.LabelField("펫 선호도 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(habitatTypeProperty, new GUIContent("서식지 타입"));
        
        // 선택적 성격 설정
        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(usePersonalityPreferenceProperty, new GUIContent("성격 선호도 사용"));
        
        if (zone.usePersonalityPreference)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(preferredPersonalitiesProperty, new GUIContent("선호 성격"), true);
            EditorGUI.indentLevel--;
        }
        
        // 구분선
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(5);
        
        // 행동 설정
        EditorGUILayout.LabelField("행동 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(behaviorDurationMultiplierProperty, new GUIContent("행동 지속 시간 배율"));
        EditorGUILayout.HelpBox("구역 내에서 펫의 행동 지속 시간이 이 배율만큼 증가합니다.", MessageType.Info);
        
        // 디버그 정보
        EditorGUILayout.Space(10);
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("디버그 정보", EditorStyles.boldLabel);
            
            // 현재 구역 내 펫 수 표시 (실제 구현 시 추가 가능)
            EditorGUILayout.HelpBox("Play Mode에서 구역 내 펫 정보가 표시됩니다.", MessageType.None);
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}
#endif