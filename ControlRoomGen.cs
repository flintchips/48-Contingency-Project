using Unity.Netcode;
using UnityEngine;

namespace OpaliteMoonMod
{
    public class ControlRoomGen : NetworkBehaviour
    {
        public GameObject[] storageShelfSpawns;

        private bool[] serverStates;
        private bool applied;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Debug.Log($"[ControlRoomGen] OnNetworkSpawn IsServer={IsServer} IsClient={IsClient} IsSpawned={IsSpawned} id={NetworkObjectId}");

            if (IsServer)
            {
                int len = storageShelfSpawns != null ? storageShelfSpawns.Length : 0;
                serverStates = new bool[len];
                for (int i = 0; i < len; i++)
                    serverStates[i] = UnityEngine.Random.value >= 0.5f; // true = ON

                ApplyStates(serverStates);
            }
            else
            {
                // Client: object is spawned here → now safe to ask
                RequestLockerStatesServerRpc();
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestLockerStatesServerRpc()
        {
            if (serverStates == null) return;
            SyncLockersClientRpc(serverStates);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SyncLockersClientRpc(bool[] states)
        {
            if (IsServer) return; // host already applied
            ApplyStates(states);
        }

        private void ApplyStates(bool[] states)
        {
            if (applied || storageShelfSpawns == null || states == null) return;
            applied = true;

            int count = Mathf.Min(storageShelfSpawns.Length, states.Length);
            Debug.Log($"[ControlRoomGen] Applying {count} states on {(IsServer ? "SERVER" : "CLIENT")}");

            for (int i = 0; i < count; i++)
            {
                if (storageShelfSpawns[i] == null) continue;
                storageShelfSpawns[i].SetActive(states[i]);
                Debug.Log($"  Locker {i}: {(states[i] ? "ON" : "OFF")} ({storageShelfSpawns[i].name})");
            }
        }
    }
}
