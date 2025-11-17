using UnityEngine;

namespace Nexus
{
    [DefaultExecutionOrder(-10000)]
    public class GravityGuard : MonoBehaviour
    {
        private static GravityGuard _instance;
        private bool loggedChange = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            // no-op
        }

        private void FixedUpdate()
        {
            // no-op
        }

        private void ForceDownGravity() { }
    }
}
