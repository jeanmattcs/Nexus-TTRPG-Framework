using System;
using UnityEngine;

namespace Nexus.Networking
{
    public class NullNetworkAddressResolver : NetworkAddressResolver
    {
        [SerializeField] private string fallbackAddress = "localhost";

        public override void Resolve(string roomCode, Action<string> onResolved, Action<string> onError)
        {
            onResolved?.Invoke(fallbackAddress);
        }
    }
}
