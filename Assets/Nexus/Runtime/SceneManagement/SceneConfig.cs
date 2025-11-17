using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Configuration for the dynamic scene system.
/// Create via: Right Click > Create > Scene System > Scene Config
/// </summary>
[CreateAssetMenu(fileName = "SceneConfig", menuName = "Scene System/Scene Config")]
public class SceneConfig : ScriptableObject
{
    [System.Serializable]
    public class SceneEntry
    {
        public string sceneName;
        public Sprite thumbnail;
        
        [TextArea(2, 3)]
        public string description;
    }
    
    [Header("Main Scene")]
    public string mainSceneName = "MainScene";
    
    [Header("Sub Scenes")]
    public List<SceneEntry> scenes = new List<SceneEntry>();
    
    [Header("Settings")]
    public bool autoLoadFirstScene = true;
    public float minimumLoadTime = 0.3f;
    
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(mainSceneName) && scenes.Count > 0;
    }
    
    public int GetSceneIndex(string sceneName)
    {
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].sceneName == sceneName)
                return i;
        }
        return -1;
    }
}