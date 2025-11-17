#if EOT_PRESENT
using System;
using System.Linq;
using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using EpicTransport;

namespace Nexus.Networking
{
    public class EOSLobbyAddressResolver : NetworkAddressResolver
    {
        [Header("Lobby Settings")]
        [SerializeField] private uint maxMembers = 8;
        [SerializeField] private LobbyPermissionLevel permissionLevel = LobbyPermissionLevel.Publicadvertised;
        [SerializeField] private bool presenceEnabled = false;
        [SerializeField] private string joinCodeAttributeKey = "join_code";

        private EOSLobby lobby;
        private string currentRoomCode = string.Empty;

        private void EnsureLobby()
        {
            if (lobby == null)
            {
                lobby = FindObjectOfType<EOSLobby>();
                if (lobby == null)
                {
                    var go = new GameObject("EOSLobby (Auto)");
                    lobby = go.AddComponent<EOSLobby>();
                    DontDestroyOnLoad(go);
                }
            }
        }

        private bool EnsureEOSReady(out string error)
        {
            error = string.Empty;
            var sdk = FindObjectOfType<EOSSDKComponent>();
            if (sdk == null)
            {
                error = "EOSSDKComponent missing in scene";
                return false;
            }
            if (!EOSSDKComponent.Initialized)
            {
                error = "EOS not initialized";
                return false;
            }
            return true;
        }

        public override void Resolve(string roomCode, Action<string> onResolved, Action<string> onError)
        {
            if (!EnsureEOSReady(out var err)) { onError?.Invoke(err); return; }
            EnsureLobby();

            // Build search option for join_code == roomCode
            var searchParam = new LobbySearchSetParameterOptions
            {
                ComparisonOp = ComparisonOp.Equal,
                Parameter = new AttributeData { Key = joinCodeAttributeKey, Value = roomCode }
            };

            void OnFindSuccess(System.Collections.Generic.List<LobbyDetails> lobbies)
            {
                lobby.FindLobbiesSucceeded -= OnFindSuccess;
                lobby.FindLobbiesFailed -= OnFindFailed;

                if (lobbies == null || lobbies.Count == 0)
                {
                    onError?.Invoke("No lobby found for code");
                    return;
                }

                var details = lobbies[0];

                EOSLobby.JoinLobbySuccess onJoinSuccess = null;
                EOSLobby.JoinLobbyFailure onJoinFailed = null;

                onJoinSuccess = (System.Collections.Generic.List<Epic.OnlineServices.Lobby.Attribute> attributes) =>
                {
                    lobby.JoinLobbySucceeded -= onJoinSuccess;
                    lobby.JoinLobbyFailed -= onJoinFailed;

                    var hostAttr = attributes.FirstOrDefault(a => a.Data.Key == EOSLobby.hostAddressKey);
                    string hostAddress = hostAttr.Data.Value.AsUtf8;
                    if (string.IsNullOrEmpty(hostAddress))
                    {
                        onError?.Invoke("Host address missing in lobby");
                        return;
                    }
                    onResolved?.Invoke(hostAddress);
                };

                onJoinFailed = (string e) =>
                {
                    lobby.JoinLobbySucceeded -= onJoinSuccess;
                    lobby.JoinLobbyFailed -= onJoinFailed;
                    onError?.Invoke(string.IsNullOrEmpty(e) ? "Failed to join lobby" : e);
                };

                lobby.JoinLobbySucceeded += onJoinSuccess;
                lobby.JoinLobbyFailed += onJoinFailed;
                lobby.JoinLobby(details, null, presenceEnabled);
            }
            void OnFindFailed(string e)
            {
                lobby.FindLobbiesSucceeded -= OnFindSuccess;
                lobby.FindLobbiesFailed -= OnFindFailed;
                onError?.Invoke(string.IsNullOrEmpty(e) ? "Lobby search failed" : e);
            }

            lobby.FindLobbiesSucceeded += OnFindSuccess;
            lobby.FindLobbiesFailed += OnFindFailed;
            lobby.FindLobbies(25, new[] { searchParam });
        }

        public override void OnHostRoomCreated(string roomCode)
        {
            if (!EnsureEOSReady(out var err)) { Debug.LogError($"[EOSResolver] {err}"); return; }
            EnsureLobby();
            currentRoomCode = roomCode;

            // Create lobby and publish host address + code attribute
            var codeAttr = new AttributeData { Key = joinCodeAttributeKey, Value = roomCode };
            var data = new[] { codeAttr };

            void OnCreated(System.Collections.Generic.List<Epic.OnlineServices.Lobby.Attribute> _)
            {
                lobby.CreateLobbySucceeded -= OnCreated;
                lobby.CreateLobbyFailed -= OnCreateFailed;
                Debug.Log($"[EOSResolver] Lobby created for code {roomCode}");
            }
            void OnCreateFailed(string e)
            {
                lobby.CreateLobbySucceeded -= OnCreated;
                lobby.CreateLobbyFailed -= OnCreateFailed;
                Debug.LogError($"[EOSResolver] Create lobby failed: {e}");
            }

            lobby.CreateLobbySucceeded += OnCreated;
            lobby.CreateLobbyFailed += OnCreateFailed;
            lobby.CreateLobby(maxMembers, permissionLevel, presenceEnabled, data);
        }

        public override void OnHostRoomClosed(string roomCode)
        {
            EnsureLobby();
            if (lobby != null && lobby.ConnectedToLobby)
            {
                void OnLeave() { lobby.LeaveLobbySucceeded -= OnLeave; }
                void OnLeaveFail(string e)
                {
                    lobby.LeaveLobbyFailed -= OnLeaveFail;
                    Debug.LogError($"[EOSResolver] Leave lobby failed: {e}");
                }
                lobby.LeaveLobbySucceeded += OnLeave;
                lobby.LeaveLobbyFailed += OnLeaveFail;
                lobby.LeaveLobby();
            }
            currentRoomCode = string.Empty;
        }
    }
}
#endif
