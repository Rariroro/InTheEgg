using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class SceneHierarchyExporter : EditorWindow
{
    private bool includeComponents = true;
    private bool includeProperties = true;
    private bool includeInactive = true;
    private string outputPath;

    [MenuItem("Tools/Export Scene Hierarchy")]
    public static void ShowWindow()
    {
        GetWindow<SceneHierarchyExporter>("Scene Exporter");
    }

    private void OnEnable()
    {
        outputPath = Application.dataPath + "/../";
    }

    private void OnGUI()
    {
        GUILayout.Label("Scene Hierarchy Exporter", EditorStyles.boldLabel);
        
        includeComponents = EditorGUILayout.Toggle("Include Components", includeComponents);
        includeProperties = EditorGUILayout.Toggle("Include Properties", includeProperties);
        includeInactive = EditorGUILayout.Toggle("Include Inactive Objects", includeInactive);
        
        // 현재 씬 이름을 기본 파일명으로 표시
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        EditorGUILayout.LabelField("Output File Name:", $"{currentSceneName}_export.txt (자동 생성)");
        
        if (GUILayout.Button("Browse Output Path"))
        {
            string path = EditorUtility.SaveFolderPanel("Choose Output Directory", outputPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                outputPath = path;
            }
        }
        
        EditorGUILayout.LabelField("Output Path:", outputPath);
        
        if (GUILayout.Button("Export Scene Hierarchy"))
        {
            ExportSceneHierarchy();
        }
    }

    private void ExportSceneHierarchy()
    {
        StringBuilder sb = new StringBuilder();
        
        // 현재 씬 이름 가져오기
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // 씬 이름을 파일명에 포함
        string outputFileName = $"{sceneName}_export.txt";
        
        // Add basic scene info
        sb.AppendLine("# Scene Hierarchy Export");
        sb.AppendLine($"Scene Name: {sceneName}");
        sb.AppendLine($"Export Date: {System.DateTime.Now}");
        sb.AppendLine("--------------------------------------------\n");
        
        // Get all root GameObjects in the scene
        List<GameObject> rootObjects = new List<GameObject>();
        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        activeScene.GetRootGameObjects(rootObjects);
        
        // Process each root object and its children
        foreach (GameObject rootObj in rootObjects)
        {
            if (!includeInactive && !rootObj.activeSelf)
                continue;
            
            ProcessGameObject(rootObj, 0, sb);
        }
        
        // Write to file
        string fullPath = Path.Combine(outputPath, outputFileName);
        File.WriteAllText(fullPath, sb.ToString());
        
        Debug.Log($"Scene hierarchy exported to: {fullPath}");
        EditorUtility.RevealInFinder(fullPath);
    }

    private void ProcessGameObject(GameObject obj, int depth, StringBuilder sb)
    {
        // Skip inactive objects if option is not enabled
        if (!includeInactive && !obj.activeSelf)
            return;

        // Indent according to depth
        string indent = new string(' ', depth * 2);
        
        // Add object info
        sb.AppendLine($"{indent}GameObject: {obj.name} (Active: {obj.activeSelf})");
        
        // Add component info if enabled
        if (includeComponents)
        {
            Component[] components = obj.GetComponents<Component>();
            
            foreach (Component component in components)
            {
                if (component == null)
                    continue;
                
                sb.AppendLine($"{indent}  Component: {component.GetType().Name}");
                
                // Add properties if enabled
                if (includeProperties)
                {
                    try
                    {
                        AddComponentProperties(component, depth + 2, sb);
                    }
                    catch (System.Exception e)
                    {
                        sb.AppendLine($"{indent}    Error getting properties: {e.Message}");
                    }
                }
            }
        }
        
        sb.AppendLine();
        
        // Process children
        foreach (Transform child in obj.transform)
        {
            ProcessGameObject(child.gameObject, depth + 1, sb);
        }
    }

    private void AddComponentProperties(Component component, int depth, StringBuilder sb)
    {
        if (component == null)
            return;
            
        string indent = new string(' ', depth * 2);
        
        // Use SerializedObject to get properties
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.GetIterator();
        
        if (property.NextVisible(true))
        {
            do
            {
                try
                {
                    // Skip script reference and arrays/lists that might be too verbose
                    if (property.name == "m_Script" || property.isArray && property.arraySize > 10)
                        continue;
                        
                    string propertyValue = GetPropertyValueAsString(property);
                    sb.AppendLine($"{indent}  {property.name}: {propertyValue}");
                }
                catch (System.Exception)
                {
                    // Skip properties that cause errors
                    sb.AppendLine($"{indent}  {property.name}: [Error reading value]");
                }
                
            } while (property.NextVisible(false));
        }
    }

    private string GetPropertyValueAsString(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.intValue.ToString();
            case SerializedPropertyType.Boolean:
                return property.boolValue.ToString();
            case SerializedPropertyType.Float:
                return property.floatValue.ToString("F2");
            case SerializedPropertyType.String:
                return property.stringValue;
            case SerializedPropertyType.Vector2:
                return property.vector2Value.ToString();
            case SerializedPropertyType.Vector3:
                return property.vector3Value.ToString();
            case SerializedPropertyType.Vector4:
                return property.vector4Value.ToString();
            case SerializedPropertyType.Quaternion:
                return property.quaternionValue.ToString();
            case SerializedPropertyType.Color:
                return property.colorValue.ToString();
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue != null ? property.objectReferenceValue.name : "null";
            case SerializedPropertyType.Enum:
                // 유효한 인덱스인지 확인
                if (property.enumValueIndex >= 0 && property.enumValueIndex < property.enumNames.Length)
                {
                    return property.enumNames[property.enumValueIndex];
                }
                else
                {
                    return $"[Invalid enum index: {property.enumValueIndex}]";
                }
            default:
                return "(Not representable as string)";
        }
    }
}