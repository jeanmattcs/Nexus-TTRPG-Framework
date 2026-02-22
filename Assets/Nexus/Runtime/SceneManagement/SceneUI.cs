using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI for scene selection. Uses template-based design.
/// Configure all elements in Unity Editor, then assign template reference.
/// </summary>
public class SceneUI : MonoBehaviour
{
    [Header("Main Panel")]
    public GameObject mainPanel;
    
    [Header("Current Scene Display")]
    public Image currentSceneThumbnail;
    public TextMeshProUGUI currentSceneNameText;
    public TextMeshProUGUI sceneIndexText;
    
    [Header("Scene List")]
    public RectTransform scrollContent;
    public SceneButtonTemplate buttonTemplate;
    
    [Tooltip("Optional: Container for organizing buttons in hierarchy")]
    public GameObject sceneListContainer;
    
    [Header("Navigation")]
    public Button nextButton;
    public Button previousButton;
    public Button reloadButton;
    public Button unloadButton;
    
    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.T;
    public bool startOpen = false;
    
    [Header("Colors")]
    public Color loadedColor = new Color(0.2f, 0.8f, 0.3f);
    public Color normalColor = Color.white;
    
    private SceneManager sceneManager;
    private SceneButtonTemplate[] sceneButtons;
    private bool isOpen;
    
    private Nexus.PlayerController playerController;
    private Nexus.TabletopManager tabletopManager;
    
    void Start()
    {
        sceneManager = SceneManager.Instance;
        
        if (sceneManager == null)
        {
            Debug.LogError("[SceneUI] SceneManager not found!");
            enabled = false;
            return;
        }
        
        if (buttonTemplate == null)
        {
            Debug.LogError("[SceneUI] Button template not assigned!");
            enabled = false;
            return;
        }
        
        // Find player controller & tabletop manager
        playerController = FindObjectOfType<Nexus.PlayerController>();
        tabletopManager = FindObjectOfType<Nexus.TabletopManager>();
        
        // Hide template
        buttonTemplate.gameObject.SetActive(false);
        
        // Setup UI
        SetupButtons();
        isOpen = startOpen;
        CreateSceneButtons();
        
        // Set initial state
        if (startOpen)
        {
            mainPanel.SetActive(true);
            // Only if the Scene UI starts open should it take control of cursor/input
            UpdateCursorState();
        }
        else
        {
            mainPanel.SetActive(false);
            // Do NOT touch cursor or input here; let other UIs (e.g. MultiplayerUI) manage them
        }
    }
    
    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }
        
        if (isOpen)
        {
            UpdateUI();
        }
    }
    
    void SetupButtons()
    {
        if (nextButton != null)
            nextButton.onClick.AddListener(() => sceneManager.LoadNext());
        
        if (previousButton != null)
            previousButton.onClick.AddListener(() => sceneManager.LoadPrevious());
        
        if (reloadButton != null)
            reloadButton.onClick.AddListener(() => sceneManager.Reload());
        
        if (unloadButton != null)
            unloadButton.onClick.AddListener(() => sceneManager.UnloadAll());
    }
    
    void CreateSceneButtons()
    {
        if (sceneManager.config == null) return;
        
        // Determine where to create buttons (use container if assigned, otherwise use scrollContent)
        RectTransform parent = sceneListContainer != null 
            ? sceneListContainer.GetComponent<RectTransform>() 
            : scrollContent;
        
        int count = sceneManager.config.scenes.Count;
        sceneButtons = new SceneButtonTemplate[count];
        
        for (int i = 0; i < count; i++)
        {
            var entry = sceneManager.config.scenes[i];
            
            // Instantiate from template
            SceneButtonTemplate btn = Instantiate(buttonTemplate, parent);
            
            // CRITICAL: Keep button inactive if UI is closed
            btn.gameObject.SetActive(isOpen);
            btn.name = $"SceneButton_{entry.sceneName}";
            
            // Setup button
            btn.Setup(entry.sceneName, entry.thumbnail, entry.description, i);
            
            int index = i; // Capture for closure
            btn.GetComponent<Button>().onClick.AddListener(() => sceneManager.LoadSceneByIndex(index));
            
            sceneButtons[i] = btn;
        }
        
        Debug.Log($"[SceneUI] Created {count} scene buttons (visible: {isOpen})");
    }
    
    void UpdateUI()
    {
        // Update current scene display
        if (currentSceneThumbnail != null)
        {
            Sprite thumb = sceneManager.CurrentThumbnail;
            currentSceneThumbnail.sprite = thumb;
            currentSceneThumbnail.enabled = thumb != null;
        }
        
        if (currentSceneNameText != null)
        {
            string name = sceneManager.CurrentSceneName;
            currentSceneNameText.text = string.IsNullOrEmpty(name) ? "No Scene Loaded" : name;
        }
        
        if (sceneIndexText != null)
        {
            int current = sceneManager.CurrentIndex;
            int total = sceneManager.TotalScenes;
            sceneIndexText.text = current >= 0 ? $"{current + 1} / {total}" : $"0 / {total}";
        }
        
        // Update scene buttons
        UpdateSceneButtons();
        
        // Update navigation buttons
        bool loading = sceneManager.IsLoading;
        if (nextButton != null) nextButton.interactable = !loading;
        if (previousButton != null) previousButton.interactable = !loading;
        if (reloadButton != null) reloadButton.interactable = !loading && sceneManager.CurrentIndex >= 0;
        if (unloadButton != null) unloadButton.interactable = !loading && sceneManager.CurrentIndex >= 0;
    }
    
    void UpdateSceneButtons()
    {
        if (sceneButtons == null) return;
        
        int currentIndex = sceneManager.CurrentIndex;
        
        for (int i = 0; i < sceneButtons.Length; i++)
        {
            if (sceneButtons[i] != null)
            {
                bool isLoaded = (i == currentIndex);
                sceneButtons[i].UpdateVisual(isLoaded, isLoaded ? loadedColor : normalColor);
            }
        }
    }
    
    public void TogglePanel()
    {
        isOpen = !isOpen;
        mainPanel.SetActive(isOpen);
        
        // Show/hide all scene buttons
        if (sceneButtons != null)
        {
            foreach (var btn in sceneButtons)
            {
                if (btn != null)
                {
                    btn.gameObject.SetActive(isOpen);
                }
            }
        }
        
        UpdateCursorState();
        
        if (isOpen)
        {
            UpdateUI();
        }
    }
    
    void UpdateCursorState()
    {
        if (isOpen)
        {
            // Show cursor and unlock
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Lock player input
            if (playerController != null)
            {
                playerController.InputLocked = true;
            }

            if (tabletopManager != null)
            {
                tabletopManager.InputLocked = true;
            }
        }
        else
        {
            // Show cursor and unlock
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Unlock player input
            if (playerController != null)
            {
                playerController.InputLocked = false;
            }

            if (tabletopManager != null)
            {
                tabletopManager.InputLocked = false;
            }
        }
    }
}
