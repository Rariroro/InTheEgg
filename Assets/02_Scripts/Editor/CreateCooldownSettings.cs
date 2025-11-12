using UnityEditor;
using UnityEngine;

/// <summary>
/// CooldownSettings 에셋 파일 생성 도우미
/// 이 스크립트를 Unity 에디터에서 실행하면 CooldownSettings.asset 파일이 생성됨
/// </summary>
public static class CreateCooldownSettingsAsset
{
    [MenuItem("InTheEgg/Create Cooldown Settings Asset")]
    public static void CreateAsset()
    {
        // CooldownSettings ScriptableObject 인스턴스 생성
        CooldownSettings asset = ScriptableObject.CreateInstance<CooldownSettings>();

        // 에셋 파일로 저장
        string path = "Assets/09_GameDatas/CooldownSettings.asset";

        // 디렉토리가 없으면 생성
        string directory = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 생성된 에셋 선택
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;

        // Debug.Log($"CooldownSettings 에셋이 생성되었습니다: {path}");
    }

    [MenuItem("InTheEgg/Create Cooldown Settings in Resources")]
    public static void CreateResourceAsset()
    {
        // Resources 폴더에도 생성 (런타임 로드용)
        CooldownSettings asset = ScriptableObject.CreateInstance<CooldownSettings>();

        string resourcesPath = "Assets/Resources";
        if (!System.IO.Directory.Exists(resourcesPath))
        {
            System.IO.Directory.CreateDirectory(resourcesPath);
        }

        string path = "Assets/Resources/CooldownSettings.asset";
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;

        // Debug.Log($"CooldownSettings 에셋이 Resources 폴더에 생성되었습니다: {path}");
    }
}