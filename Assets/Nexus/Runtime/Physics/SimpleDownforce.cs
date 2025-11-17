using UnityEngine;

namespace Nexus
{
    [DisallowMultipleComponent]
    public class SimpleDownforce : MonoBehaviour
    {
        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate() { }
    }
}
