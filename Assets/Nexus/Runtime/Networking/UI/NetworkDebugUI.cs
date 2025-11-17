using UnityEngine;
using Mirror;
using TMPro;

namespace Nexus.Networking
{
    /// <summary>
    /// Simple debug UI to show network status
    /// Useful for testing and debugging multiplayer
    /// </summary>
    public class NetworkDebugUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI pingText;
        [SerializeField] private TextMeshProUGUI playersText;
        [SerializeField] private TextMeshProUGUI objectsText;

        [Header("Settings")]
        [SerializeField] private bool showDebugUI = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                showDebugUI = !showDebugUI;
                UpdateVisibility();
            }

            if (showDebugUI)
            {
                UpdateDebugInfo();
            }
        }

        private void UpdateDebugInfo()
        {
            // Connection status
            if (statusText != null)
            {
                string status = "Disconnected";
                if (NetworkServer.active && NetworkClient.isConnected)
                    status = "Host";
                else if (NetworkServer.active)
                    status = "Server";
                else if (NetworkClient.isConnected)
                    status = "Client";

                statusText.text = $"Status: {status}";
            }

            // Ping (RTT)
            if (pingText != null && NetworkTime.rtt > 0)
            {
                pingText.text = $"Ping: {Mathf.RoundToInt((float)NetworkTime.rtt * 1000)}ms";
            }

            // Player count
            if (playersText != null && NetworkServer.active)
            {
                playersText.text = $"Players: {NetworkServer.connections.Count}";
            }

            // Networked objects count
            if (objectsText != null)
            {
                objectsText.text = $"Objects: {NetworkServer.spawned.Count}";
            }
        }

        private void UpdateVisibility()
        {
            if (statusText != null) statusText.gameObject.SetActive(showDebugUI);
            if (pingText != null) pingText.gameObject.SetActive(showDebugUI);
            if (playersText != null) playersText.gameObject.SetActive(showDebugUI);
            if (objectsText != null) objectsText.gameObject.SetActive(showDebugUI);
        }

        private void OnGUI()
        {
            if (!showDebugUI) return;

            // Fallback GUI if TextMeshPro components not assigned
            if (statusText == null)
            {
                GUILayout.BeginArea(new Rect(10, 10, 300, 200));
                GUILayout.BeginVertical("box");

                // Status
                string status = "Disconnected";
                if (NetworkServer.active && NetworkClient.isConnected)
                    status = "Host";
                else if (NetworkServer.active)
                    status = "Server";
                else if (NetworkClient.isConnected)
                    status = "Client";

                GUILayout.Label($"Status: {status}");

                // Ping
                if (NetworkTime.rtt > 0)
                {
                    GUILayout.Label($"Ping: {Mathf.RoundToInt((float)NetworkTime.rtt * 1000)}ms");
                }

                // Players
                if (NetworkServer.active)
                {
                    GUILayout.Label($"Players: {NetworkServer.connections.Count}");
                }

                // Objects
                GUILayout.Label($"Objects: {NetworkServer.spawned.Count}");

                // Room code
                string roomCode = RoomCodeManager.Instance?.GetCurrentRoomCode();
                if (!string.IsNullOrEmpty(roomCode))
                {
                    GUILayout.Label($"Room: {roomCode}");
                }

                GUILayout.Label($"Press {toggleKey} to toggle");

                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }
    }
}
