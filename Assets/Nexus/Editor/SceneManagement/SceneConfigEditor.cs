#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Custom editor for SceneConfig with automatic scene detection.
/// Place in Editor folder.
/// </summary>
[CustomEditor(typeof(SceneConfig))]
public class SceneConfigEditor : Editor
{
    private SceneConfig config;
    
    void OnEnable()
    {
        config = (SceneConfig)target;
    }
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Scene Tools", EditorStyles.boldLabel);
        
        // Scan scenes button
        if (GUILayout.Button("Scan Project for Scenes", GUILayout.Height(30)))
        {
            ScanForScenes();
        }
        
        // Add to build settings
        if (GUILayout.Button("Add All to Build Settings", GUILayout.Height(25)))
        {
            AddToBuildSettings();
        }
        
        // Clear list
        if (GUILayout.Button("Clear Scene List", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Clear List", 
                "Clear all scenes from the list?", "Yes", "Cancel"))
            {
                Undo.RecordObject(config, "Clear Scene List");
                config.scenes.Clear();
                EditorUtility.SetDirty(config);
            }
        }
        
        EditorGUILayout.Space(5);
        
        // Info box
        EditorGUILayout.HelpBox(
            $"Total Scenes: {config.scenes.Count}\n" +
            $"Main Scene: {config.mainSceneName}",
            MessageType.Info);
        
        // Validation warnings
        if (string.IsNullOrEmpty(config.mainSceneName))
        {
            EditorGUILayout.HelpBox("Main scene name is empty!", MessageType.Error);
        }
        
        if (config.scenes.Count == 0)
        {
            EditorGUILayout.HelpBox("No scenes configured. Use 'Scan Project' to add scenes.", MessageType.Warning);
        }
    }
    
    void ScanForScenes()
    {
        // Define the folder to scan
        string folderPath = "Assets/NexusUser/Scenes";
        
        // Check if folder exists
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("Folder Not Found", 
                $"The folder '{folderPath}' does not exist.\n\nCreate it or change the path in the script.", "OK");
            return;
        }
        
        // Find all scene files in the Scenes folder
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { folderPath });
        
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("No Scenes", 
                $"No scene files found in '{folderPath}'.", "OK");
            return;
        }
        
        Undo.RecordObject(config, "Scan for Scenes");
        config.scenes.Clear();
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string sceneName = Path.GetFileNameWithoutExtension(path);
            
            // Skip main scene
            if (sceneName == config.mainSceneName)
                continue;
            
            config.scenes.Add(new SceneConfig.SceneEntry
            {
                sceneName = sceneName,
                thumbnail = null,
                description = ""
            });
        }
        
        EditorUtility.SetDirty(config);
        
        EditorUtility.DisplayDialog("Scan Complete", 
            $"Found {config.scenes.Count} scenes in '{folderPath}'.\n\nAdd thumbnails in the Inspector.", "OK");
    }
    
    void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
        
        // Add main scene
        string mainPath = FindScenePath(config.mainSceneName);
        if (!string.IsNullOrEmpty(mainPath))
        {
            buildScenes.Add(new EditorBuildSettingsScene(mainPath, true));
        }
        
        // Add all sub scenes
        foreach (var entry in config.scenes)
        {
            string path = FindScenePath(entry.sceneName);
            if (!string.IsNullOrEmpty(path))
            {
                buildScenes.Add(new EditorBuildSettingsScene(path, true));
            }
        }
        
        EditorBuildSettings.scenes = buildScenes.ToArray();
        
        EditorUtility.DisplayDialog("Build Settings Updated", 
            $"{buildScenes.Count} scenes added to Build Settings.", "OK");
    }
    
    string FindScenePath(string sceneName)
    {
        string[] guids = AssetDatabase.FindAssets($"{sceneName} t:Scene");
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == sceneName)
            {
                return path;
            }
        }
        
        return null;
    }
}
#endif