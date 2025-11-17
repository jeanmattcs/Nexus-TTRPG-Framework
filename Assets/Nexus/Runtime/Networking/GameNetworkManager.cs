using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;

namespace Nexus.Networking
{
    /// <summary>
    /// Custom Network Manager for room-based multiplayer
    /// Handles hosting and joining rooms with codes
    /// </summary>
    public class GameNetworkManager : NetworkManager
    {
        [Header("Room Settings")]
        // Use the inherited playerPrefab from NetworkManager instead of declaring our own
        
        [Header("Spawn Settings")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(0, 2, 0);
        [SerializeField] private float spawnRadius = 5f;
        [SerializeField] private Transform spawnAnchor; // Optional scene anchor for spawn
        [SerializeField] private NetworkAddressResolver addressResolver;

        private Dictionary<uint, GameObject> spawnedPlayers = new Dictionary<uint, GameObject>();

        public static GameNetworkManager Instance { get; private set; }

        public override void Awake()
        {
            base.Awake();
            
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Ensure all token prefabs in Resources/Tokens are registered as spawnable on server
            RegisterTokenPrefabsServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Ensure client can spawn the same token prefabs
            RegisterTokenPrefabsClient();
        }

        /// <summary>
        /// Host a new room
        /// </summary>
        public void HostRoom()
        {
            // Use existing code if UI already generated it; otherwise generate now
            string roomCode = RoomCodeManager.Instance.GetCurrentRoomCode();
            if (string.IsNullOrEmpty(roomCode))
            {
                roomCode = RoomCodeManager.Instance.GenerateRoomCode();
            }

#if EOT_PRESENT
            StartCoroutine(HostWithEosInit(roomCode));
#else
            StartHost();
            if (addressResolver != null)
            {
                try { addressResolver.OnHostRoomCreated(roomCode); } catch (System.Exception ex) { Debug.LogError($"AddressResolver OnHostRoomCreated error: {ex.Message}"); }
            }
            Debug.Log($"Hosting room with code: {roomCode}");
#endif
        }

#if EOT_PRESENT
        private IEnumerator HostWithEosInit(string roomCode)
        {
            if (!EpicTransport.EOSSDKComponent.Initialized && !EpicTransport.EOSSDKComponent.IsConnecting)
            {
                var sdk = Object.FindObjectOfType<EpicTransport.EOSSDKComponent>();
                if (sdk != null)
                {
                    // Force DeviceId flow (no web login, no domain) to prevent missing token errors
                    sdk.authInterfaceLogin = false;
                    sdk.connectInterfaceCredentialType = Epic.OnlineServices.ExternalCredentialType.DeviceidAccessToken;
                }
                EpicTransport.EOSSDKComponent.Initialize();
            }

            float t = 0f; float timeout = 20f;
            while (!EpicTransport.EOSSDKComponent.Initialized && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!EpicTransport.EOSSDKComponent.Initialized)
            {
                Debug.LogError("EOS failed to initialize; cannot host.");
                yield break;
            }

            StartHost();
            if (addressResolver != null)
            {
                try { addressResolver.OnHostRoomCreated(roomCode); } catch (System.Exception ex) { Debug.LogError($"AddressResolver OnHostRoomCreated error: {ex.Message}"); }
            }
            Debug.Log($"Hosting room with code: {roomCode}");
        }
#endif

        /// <summary>
        /// Join an existing room with a code
        /// </summary>
        public void JoinRoom(string roomCode)
        {
            if (!RoomCodeManager.Instance.ValidateRoomCode(roomCode))
            {
                Debug.LogError("Invalid room code format!");
                return;
            }

            RoomCodeManager.Instance.SetCurrentRoomCode(roomCode);

            if (addressResolver != null)
            {
                addressResolver.Resolve(roomCode,
                    (resolved) =>
                    {
                        networkAddress = resolved;
                        StartClient();
                        Debug.Log($"Attempting to join room: {roomCode} -> {resolved}");
                    },
                    (err) =>
                    {
                        var ui = Object.FindObjectOfType<Nexus.Networking.MultiplayerUI>();
                        if (ui != null) ui.OnDisconnected(string.IsNullOrEmpty(err) ? "Failed to resolve room" : err);
                        Debug.LogError($"Failed to resolve room code '{roomCode}': {err}");
                    }
                );
            }
            else
            {
                networkAddress = "localhost";
                StartClient();
                Debug.Log($"Attempting to join room (no resolver, defaulting to localhost): {roomCode}");
            }
        }

        /// <summary>
        /// Called on the server when a player is added
        /// </summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // Determine base spawn point
            Vector3 basePos = spawnAnchor != null ? spawnAnchor.position : spawnPosition;
            // Spawn player at a random position around base point on XZ
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * spawnRadius;
            randomOffset.y = 0f;
            Vector3 spawnPos = basePos + randomOffset;

            GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

            NetworkServer.AddPlayerForConnection(conn, player);
            
            spawnedPlayers[(uint)conn.connectionId] = player;
            
            Debug.Log($"Player spawned for connection {conn.connectionId} at {spawnPos}");
        }

        /// <summary>
        /// Called on the server when a player disconnects
        /// </summary>
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (spawnedPlayers.ContainsKey((uint)conn.connectionId))
            {
                spawnedPlayers.Remove((uint)conn.connectionId);
            }
            
            base.OnServerDisconnect(conn);
            Debug.Log($"Player disconnected: {conn.connectionId}");
        }

        /// <summary>
        /// Called on client when connected to server
        /// </summary>
        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log("Connected to server!");
            var ui = Object.FindObjectOfType<Nexus.Networking.MultiplayerUI>();
            if (ui != null)
            {
                ui.OnConnected();
            }
        }

        public override void OnStartHost()
        {
            base.OnStartHost();
            string code = RoomCodeManager.Instance != null ? RoomCodeManager.Instance.GetCurrentRoomCode() : "";
            Debug.LogError($"[Network] Host started. Room code: {code}");
        }

        /// <summary>
        /// Called on client when disconnected from server
        /// </summary>
        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            Debug.Log("Disconnected from server!");
            var ui = Object.FindObjectOfType<Nexus.Networking.MultiplayerUI>();
            if (ui != null)
            {
                ui.OnDisconnected();
            }
        }

        /// <summary>
        /// Stop hosting or client
        /// </summary>
        public void LeaveRoom()
        {
            string code = RoomCodeManager.Instance != null ? RoomCodeManager.Instance.GetCurrentRoomCode() : string.Empty;

            if (NetworkServer.active && NetworkClient.isConnected)
            {
                StopHost();
            }
            else if (NetworkClient.isConnected)
            {
                StopClient();
            }
            else if (NetworkServer.active)
            {
                StopServer();
            }
            
            spawnedPlayers.Clear();
            Debug.Log("Left room");

            // Let resolver clean up/unpublish
            if (addressResolver != null && !string.IsNullOrEmpty(code))
            {
                try { addressResolver.OnHostRoomClosed(code); } catch (System.Exception ex) { Debug.LogError($"AddressResolver OnHostRoomClosed error: {ex.Message}"); }
            }
        }

        private void RegisterTokenPrefabsServer()
        {
            GameObject[] tokenPrefabs = Resources.LoadAll<GameObject>("Tokens");
            if (tokenPrefabs == null || tokenPrefabs.Length == 0) return;
            foreach (var p in tokenPrefabs)
            {
                if (p == null) continue;
                // Add to spawnPrefabs if not already present
                bool exists = false;
                foreach (var sp in spawnPrefabs)
                {
                    if (sp == p || (sp != null && sp.name == p.name))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    spawnPrefabs.Add(p);
                }
            }
        }

        private void RegisterTokenPrefabsClient()
        {
            GameObject[] tokenPrefabs = Resources.LoadAll<GameObject>("Tokens");
            if (tokenPrefabs == null || tokenPrefabs.Length == 0) return;
            foreach (var p in tokenPrefabs)
            {
                if (p == null) continue;
                // Register prefab for client-side spawning
                if (p.GetComponent<NetworkIdentity>() != null)
                {
                    NetworkClient.RegisterPrefab(p);
                }
            }
        }
    }
}
