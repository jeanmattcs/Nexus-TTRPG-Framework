using UnityEngine;

namespace Nexus
{
    public static class PhysicsDefaults
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureDefaultGravity()
        {
            // no-op
        }
    }
}
