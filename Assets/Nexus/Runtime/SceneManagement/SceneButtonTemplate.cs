using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Template component for scene selection buttons.
/// Attach to a GameObject with Button component.
/// Configure all visual elements in Unity Editor.
/// </summary>
[RequireComponent(typeof(Button))]
public class SceneButtonTemplate : MonoBehaviour
{
    [Header("Template Elements - Configure in Editor")]
    public Image thumbnail;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI indexText;
    public Image backgroundImage;
    public GameObject loadedIndicator;
    
    [Header("Runtime Data - Don't Edit")]
    public string sceneName;
    public int sceneIndex;
    
    /// <summary>
    /// Initialize button with scene data
    /// </summary>
    public void Setup(string name, Sprite thumb, string description, int index)
    {
        sceneName = name;
        sceneIndex = index;
        
        // Set thumbnail
        if (thumbnail != null)
        {
            thumbnail.sprite = thumb;
            thumbnail.enabled = thumb != null;
        }
        
        // Set name
        if (nameText != null)
        {
            nameText.text = name;
        }
        
        // Set description
        if (descriptionText != null)
        {
            descriptionText.text = description;
            descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));
        }
        
        // Set index
        if (indexText != null)
        {
            indexText.text = $"#{index + 1}";
        }
        
        // Hide loaded indicator initially
        if (loadedIndicator != null)
        {
            loadedIndicator.SetActive(false);
        }
    }
    
    /// <summary>
    /// Update visual state based on loaded status
    /// </summary>
    public void UpdateVisual(bool isLoaded, Color backgroundColor)
    {
        // Update background color
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }
        
        // Update text style
        if (nameText != null)
        {
            nameText.fontStyle = isLoaded ? FontStyles.Bold : FontStyles.Normal;
        }
        
        // Show/hide loaded indicator
        if (loadedIndicator != null)
        {
            loadedIndicator.SetActive(isLoaded);
        }
    }
}