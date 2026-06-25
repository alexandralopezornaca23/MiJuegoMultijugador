using Unity.Netcode;
using UnityEngine;

// Script que recibe ordenes de teletransporte desde el servidor.
// Como el movimiento lo controla el cliente (ClientNetworkTransform),
// el servidor no puede mover al jugador directamente y usa este ClientRpc para indicarle donde ir.

public class PlayerTeleport : NetworkBehaviour
{
    [ClientRpc]
    public void TeleportClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams clientRpcParams = default)
    {
        // Aqui se podrian activar efectos visuales o de sonido del teletransporte
        // PlayTeleportParticles();
        // PlayTeleportSound();
        Debug.Log($"[Client {NetworkManager.LocalClientId}] Teletransportado a {position}");
    }
}