using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Collections.Generic;

namespace Nexus.Networking
{
    /// <summary>
    /// UI for hosting and joining multiplayer rooms
    /// </summary>
    public class MultiplayerUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject hostPanel;
        [SerializeField] private GameObject joinPanel;
        [SerializeField] private GameObject inGamePanel;

        [Header("Host Panel")]
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button cancelHostButton;

        [Header("Join Panel")]
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button cancelJoinButton;
        [SerializeField] private TextMeshProUGUI joinStatusText;

        [Header("Lobby List")]
        [SerializeField] private Button findRoomsButton;
        [SerializeField] private Transform roomListContainer; // ScrollView content
        [SerializeField] private GameObject roomListItemPrefab; // optional
        [SerializeField] private TextMeshProUGUI roomListStatusText; // optional
        [SerializeField] private string joinCodeAttributeKey = "join_code";

        [Header("Main Menu")]
        [SerializeField] private Button hostRoomButton;
        [SerializeField] private Button joinRoomButton;

        [Header("In-Game Panel")]
        [SerializeField] private TextMeshProUGUI currentRoomCodeText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Button leaveRoomButton;

        [Header("Settings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.M;

        private bool isUIVisible = false;
        private Nexus.PlayerController playerController;
        private Nexus.TabletopManager tabletopManager;
        private EOSLobby eosLobby;

        private void Start()
        {
            SetupButtons();
            ShowMainMenu();
            
            // Start with UI visible and cursor unlocked
            isUIVisible = true;
            UpdateCursorAndInput();
        }

        private void OnEnable()
        {
            SubscribeLobbyEvents(true);
        }

        private void OnDisable()
        {
            SubscribeLobbyEvents(false);
        }

        private void Update()
        {
            // Find player controllers if not found yet (they spawn at runtime)
            if (playerController == null)
            {
                playerController = FindObjectOfType<Nexus.PlayerController>();
            }
            if (tabletopManager == null)
            {
                tabletopManager = FindObjectOfType<Nexus.TabletopManager>();
            }

            if (Input.GetKeyDown(toggleKey))
            {
                ToggleUI();
            }

            // Update in-game UI
            if (inGamePanel != null && inGamePanel.activeSelf)
            {
                UpdateInGameUI();
            }
        }

        private void OnFindRooms()
        {
            EnsureLobby();
            if (eosLobby == null)
            {
                if (roomListStatusText != null) roomListStatusText.text = "Lobby service not ready";
                return;
            }
            if (roomListStatusText != null) roomListStatusText.text = "Searching...";
            ClearRoomList();
            eosLobby.FindLobbies(25);
        }

        private void OnFindLobbiesSuccess(List<Epic.OnlineServices.Lobby.LobbyDetails> lobbies)
        {
            ClearRoomList();
            if (roomListStatusText != null) roomListStatusText.text = lobbies == null ? "0 rooms" : $"{lobbies.Count} rooms";
            if (lobbies == null || lobbies.Count == 0) return;
            for (int i = 0; i < lobbies.Count; i++)
            {
                AddRoomItem(lobbies[i]);
            }
        }

        private void OnFindLobbiesFailed(string error)
        {
            if (roomListStatusText != null) roomListStatusText.text = string.IsNullOrEmpty(error) ? "Search failed" : error;
        }

        private void AddRoomItem(Epic.OnlineServices.Lobby.LobbyDetails details)
        {
            string code = "";
            try
            {
                Epic.OnlineServices.Lobby.Attribute attr;
                details.CopyAttributeByKey(new Epic.OnlineServices.Lobby.LobbyDetailsCopyAttributeByKeyOptions { AttrKey = joinCodeAttributeKey }, out attr);
                code = attr.Data.Value.AsUtf8;
            }
            catch { }
            if (string.IsNullOrEmpty(code)) code = "(no code)";

            GameObject item = null;
            if (roomListItemPrefab != null)
            {
                item = Instantiate(roomListItemPrefab, roomListContainer);
            }
            else
            {
                var go = new GameObject("RoomItem", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(roomListContainer, false);
                var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGO.transform.SetParent(go.transform, false);
                var tmp = textGO.GetComponent<TextMeshProUGUI>();
                tmp.text = $"Code: {code}";
                tmp.enableWordWrapping = false;
                item = go;
            }

            var button = item.GetComponent<Button>();
            var label = item.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = $"Code: {code}";
            if (button != null)
            {
                string captured = code;
                button.onClick.AddListener(() => OnJoinRoomByCode(captured));
            }
        }

        private void ClearRoomList()
        {
            if (roomListContainer == null) return;
            for (int i = roomListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(roomListContainer.GetChild(i).gameObject);
            }
        }

        private void OnJoinRoomByCode(string code)
        {
            if (roomCodeInput != null) roomCodeInput.text = code;
            OnJoinRoom();
        }

        private void EnsureLobby()
        {
            if (eosLobby == null)
            {
                eosLobby = FindObjectOfType<EOSLobby>();
                if (eosLobby == null)
                {
                    var go = new GameObject("EOSLobby (UI Auto)");
                    eosLobby = go.AddComponent<EOSLobby>();
                    DontDestroyOnLoad(go);
                }
            }
        }

        private void SubscribeLobbyEvents(bool subscribe)
        {
            EnsureLobby();
            if (eosLobby == null) return;
            if (subscribe)
            {
                eosLobby.FindLobbiesSucceeded += OnFindLobbiesSuccess;
                eosLobby.FindLobbiesFailed += OnFindLobbiesFailed;
            }
            else
            {
                eosLobby.FindLobbiesSucceeded -= OnFindLobbiesSuccess;
                eosLobby.FindLobbiesFailed -= OnFindLobbiesFailed;
            }
        }

        private void SetupButtons()
        {
            // Main menu buttons
            if (hostRoomButton != null)
                hostRoomButton.onClick.AddListener(ShowHostPanel);
            
            if (joinRoomButton != null)
                joinRoomButton.onClick.AddListener(ShowJoinPanel);

            // Host panel buttons
            if (startHostButton != null)
                startHostButton.onClick.AddListener(OnHostRoom);
            
            if (cancelHostButton != null)
                cancelHostButton.onClick.AddListener(ShowMainMenu);
            
            if (copyCodeButton != null)
                copyCodeButton.onClick.AddListener(CopyRoomCode);

            // Join panel buttons
            if (joinButton != null)
                joinButton.onClick.AddListener(OnJoinRoom);
            
            if (cancelJoinButton != null)
                cancelJoinButton.onClick.AddListener(ShowMainMenu);

            if (findRoomsButton != null)
                findRoomsButton.onClick.AddListener(OnFindRooms);

            // In-game buttons
            if (leaveRoomButton != null)
                leaveRoomButton.onClick.AddListener(OnLeaveRoom);
        }

        private void ToggleUI()
        {
            isUIVisible = !isUIVisible;

            if (NetworkServer.active || NetworkClient.isConnected)
            {
                // Show in-game panel when connected
                if (isUIVisible)
                    ShowInGamePanel();
                else
                    HideAllPanels();
            }
            else
            {
                // Show main menu when not connected
                if (isUIVisible)
                    ShowMainMenu();
                else
                    HideAllPanels();
            }

            UpdateCursorAndInput();
        }

        private void UpdateCursorAndInput()
        {
            // Handle cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Lock/unlock player input
            if (playerController != null)
            {
                playerController.InputLocked = isUIVisible;
            }

            if (tabletopManager != null)
            {
                tabletopManager.InputLocked = isUIVisible;
            }
        }

        private void ShowMainMenu()
        {
            HideAllPanels();
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(true);
        }

        private void ShowHostPanel()
        {
            HideAllPanels();
            if (hostPanel != null)
            {
                hostPanel.SetActive(true);
                
                // Generate and display room code
                string code = RoomCodeManager.Instance.GenerateRoomCode();
                if (roomCodeText != null)
                    roomCodeText.text = $"{code}";
                Debug.LogError($"[MultiplayerUI] Host panel opened. Room code: {code}");
            }
        }

        private void ShowJoinPanel()
        {
            HideAllPanels();
            if (joinPanel != null)
            {
                joinPanel.SetActive(true);
                
                if (joinStatusText != null)
                    joinStatusText.text = "";
                
                if (roomCodeInput != null)
                    roomCodeInput.text = "";
            }
        }

        private void ShowInGamePanel()
        {
            HideAllPanels();
            if (inGamePanel != null)
                inGamePanel.SetActive(true);
        }

        private void HideAllPanels()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (hostPanel != null) hostPanel.SetActive(false);
            if (joinPanel != null) joinPanel.SetActive(false);
            if (inGamePanel != null) inGamePanel.SetActive(false);
        }

        private void OnHostRoom()
        {
            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.HostRoom();
                // Debug: print room code after host is started
                string code = RoomCodeManager.Instance.GetCurrentRoomCode();
                Debug.LogError($"[MultiplayerUI] Host clicked. Room code: {code}");
                // Do not show in-game panel automatically to avoid full-screen overlays
                HideAllPanels();
                isUIVisible = false;
                UpdateCursorAndInput();
            }
        }

        private void OnJoinRoom()
        {
            if (roomCodeInput == null || string.IsNullOrEmpty(roomCodeInput.text))
            {
                if (joinStatusText != null)
                    joinStatusText.text = "Please enter a room code!";
                return;
            }

            string code = roomCodeInput.text.ToUpper().Trim();

            if (!RoomCodeManager.Instance.ValidateRoomCode(code))
            {
                if (joinStatusText != null)
                    joinStatusText.text = "Invalid room code format!";
                return;
            }

            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.JoinRoom(code);
                if (joinStatusText != null)
                    joinStatusText.text = "Connecting...";
                if (joinButton != null)
                    joinButton.interactable = false;
                // Keep UI visible until connection succeeds or fails
            }
        }

        private void OnLeaveRoom()
        {
            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.LeaveRoom();
                ShowMainMenu();
            }
        }

        // Called by GameNetworkManager on successful client connect
        public void OnConnected()
        {
            // Hide all UI by default to ensure camera is unobstructed; user can press M to open UI
            HideAllPanels();
            isUIVisible = false;
            UpdateCursorAndInput();
            if (joinStatusText != null) joinStatusText.text = "";
            if (joinButton != null) joinButton.interactable = true;
        }

        // Called by GameNetworkManager on client disconnect
        public void OnDisconnected(string reason = "")
        {
            ShowMainMenu();
            isUIVisible = true;
            UpdateCursorAndInput();
            if (joinStatusText != null)
                joinStatusText.text = string.IsNullOrEmpty(reason) ? "Disconnected" : reason;
            if (joinButton != null) joinButton.interactable = true;
        }

        private void CopyRoomCode()
        {
            string code = RoomCodeManager.Instance.GetCurrentRoomCode();
            if (!string.IsNullOrEmpty(code))
            {
                GUIUtility.systemCopyBuffer = code;
                Debug.Log($"Room code {code} copied to clipboard!");
            }
        }

        private void UpdateInGameUI()
        {
            // Update room code display
            if (currentRoomCodeText != null)
            {
                string code = RoomCodeManager.Instance.GetCurrentRoomCode();
                currentRoomCodeText.text = $"Room: {code}";
            }

            // Update player count
            if (playerCountText != null)
            {
                int count = NetworkServer.connections.Count;
                playerCountText.text = $"Players: {count}";
            }
        }
    }
}
