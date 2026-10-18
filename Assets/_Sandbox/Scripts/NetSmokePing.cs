using Unity.Collections;
using Unity.Netcode;

namespace Musketeers.Sandbox
{
    // In-scene NetworkObject that relays a ping through the host to everyone (TECH_DESIGN §5 style:
    // client→host Rpc, then host→ClientsAndHost Rpc).
    public class NetSmokePing : NetworkBehaviour
    {
        public static NetSmokePing Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            NetSmokeLog.Add($"Ping object spawned (IsHost={IsHost}, clientId={NetworkManager.LocalClientId})");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SendPing(string playerName)
        {
            PingServerRpc(new FixedString64Bytes(playerName));
        }

        [Rpc(SendTo.Server)]
        void PingServerRpc(FixedString64Bytes playerName, RpcParams rpcParams = default)
        {
            PingAllRpc(playerName, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void PingAllRpc(FixedString64Bytes playerName, ulong senderClientId)
        {
            NetSmokeLog.Add($"{playerName}: ping");
        }
    }
}
