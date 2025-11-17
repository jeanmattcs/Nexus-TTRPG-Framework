using UnityEngine;
using Mirror;

namespace Nexus.Networking
{
    /// <summary>
    /// Synchronizes scene changes across all clients
    /// When the host changes scenes, all clients follow
    /// </summary>
    public class NetworkedSceneManager : MonoBehaviour
    {
        /// <summary>
        /// Call this from the host to change scenes for all clients
        /// </summary>
        public void ChangeSceneForAll(string sceneName)
        {
            if (NetworkServer.active)
            {
                // Use Mirror's built-in scene management
                NetworkManager.singleton.ServerChangeScene(sceneName);
            }
        }

        private void LoadScene(string sceneName)
        {
            // Use Unity's built-in scene manager
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }
}
