using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

namespace Nexus.Networking
{
    /// <summary>
    /// Simple relay system for room codes
    /// Maps room codes to IP addresses for easy joining
    /// 
    /// For production, you would want to use a proper backend service
    /// This is a simple example using a free service like JSONBin or similar
    /// </summary>
    public class RoomCodeRelay : MonoBehaviour
    {
        private static RoomCodeRelay _instance;
        public static RoomCodeRelay Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<RoomCodeRelay>();
                }
                return _instance;
            }
        }

        [Header("Relay Settings")]
        [SerializeField] private string relayServerUrl = ""; // Add your relay server URL here
        [SerializeField] private bool useRelay = false;

        private Dictionary<string, string> localRoomCache = new Dictionary<string, string>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Register a room code with an IP address
        /// </summary>
        public void RegisterRoom(string roomCode, string ipAddress, System.Action<bool> callback)
        {
            if (!useRelay || string.IsNullOrEmpty(relayServerUrl))
            {
                // Local mode - just cache it
                localRoomCache[roomCode] = ipAddress;
                callback?.Invoke(true);
                return;
            }

            // In production, send to relay server
            StartCoroutine(RegisterRoomCoroutine(roomCode, ipAddress, callback));
        }

        /// <summary>
        /// Look up an IP address from a room code
        /// </summary>
        public void LookupRoom(string roomCode, System.Action<string> callback)
        {
            if (!useRelay || string.IsNullOrEmpty(relayServerUrl))
            {
                // Local mode - check cache
                if (localRoomCache.TryGetValue(roomCode, out string ip))
                {
                    callback?.Invoke(ip);
                }
                else
                {
                    callback?.Invoke(null);
                }
                return;
            }

            // In production, query relay server
            StartCoroutine(LookupRoomCoroutine(roomCode, callback));
        }

        /// <summary>
        /// Unregister a room code
        /// </summary>
        public void UnregisterRoom(string roomCode, System.Action<bool> callback)
        {
            if (!useRelay || string.IsNullOrEmpty(relayServerUrl))
            {
                // Local mode - remove from cache
                localRoomCache.Remove(roomCode);
                callback?.Invoke(true);
                return;
            }

            // In production, remove from relay server
            StartCoroutine(UnregisterRoomCoroutine(roomCode, callback));
        }

        private IEnumerator RegisterRoomCoroutine(string roomCode, string ipAddress, System.Action<bool> callback)
        {
            // Example implementation - replace with your actual relay server API
            string url = $"{relayServerUrl}/register";
            
            WWWForm form = new WWWForm();
            form.AddField("roomCode", roomCode);
            form.AddField("ipAddress", ipAddress);

            using (UnityWebRequest www = UnityWebRequest.Post(url, form))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"Room {roomCode} registered successfully");
                    callback?.Invoke(true);
                }
                else
                {
                    Debug.LogError($"Failed to register room: {www.error}");
                    callback?.Invoke(false);
                }
            }
        }

        private IEnumerator LookupRoomCoroutine(string roomCode, System.Action<string> callback)
        {
            // Example implementation - replace with your actual relay server API
            string url = $"{relayServerUrl}/lookup?roomCode={roomCode}";

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string ipAddress = www.downloadHandler.text;
                    Debug.Log($"Room {roomCode} found at {ipAddress}");
                    callback?.Invoke(ipAddress);
                }
                else
                {
                    Debug.LogError($"Failed to lookup room: {www.error}");
                    callback?.Invoke(null);
                }
            }
        }

        private IEnumerator UnregisterRoomCoroutine(string roomCode, System.Action<bool> callback)
        {
            // Example implementation - replace with your actual relay server API
            string url = $"{relayServerUrl}/unregister";
            
            WWWForm form = new WWWForm();
            form.AddField("roomCode", roomCode);

            using (UnityWebRequest www = UnityWebRequest.Post(url, form))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"Room {roomCode} unregistered successfully");
                    callback?.Invoke(true);
                }
                else
                {
                    Debug.LogError($"Failed to unregister room: {www.error}");
                    callback?.Invoke(false);
                }
            }
        }

        /// <summary>
        /// Get local IP address
        /// </summary>
        public string GetLocalIPAddress()
        {
            string localIP = "127.0.0.1";
            
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        localIP = ip.ToString();
                        break;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not get local IP: {e.Message}");
            }

            return localIP;
        }
    }
}
