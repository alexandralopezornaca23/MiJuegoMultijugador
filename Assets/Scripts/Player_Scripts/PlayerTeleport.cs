using Unity.Netcode;
using UnityEngine;

public class PlayerTeleport : NetworkBehaviour
{
    [ClientRpc]
    public void TeleportClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams clientRpcParams = default)
    {
        // PlayTeleportParticles();
        // PlayTeleportSound();

        Debug.Log($"[Client {NetworkManager.LocalClientId}] Teletransportado a {position}");
    }
}
