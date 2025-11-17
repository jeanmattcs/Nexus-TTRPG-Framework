using UnityEngine;

namespace Nexus.Networking
{
    public abstract class NetworkAddressResolver : MonoBehaviour
    {
        // Resolve a join code to a network address string understood by the active transport
        public abstract void Resolve(string roomCode, System.Action<string> onResolved, System.Action<string> onError);

        // Optional: called after hosting starts so resolvers can publish/join a lobby
        public virtual void OnHostRoomCreated(string roomCode) { }

        // Optional: called when leaving room so resolvers can clean up/unpublish
        public virtual void OnHostRoomClosed(string roomCode) { }
    }
}
