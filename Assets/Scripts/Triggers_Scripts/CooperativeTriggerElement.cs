using Unity.Netcode;
using UnityEngine;

// Interruptor cooperativo que activa o desactiva un objeto de la escena
// cuando un jugador entra o sale de su zona. El cambio se sincroniza
// para que todos los clientes vean el mismo estado al mismo tiempo.

public class CooperativeTriggerElement : NetworkBehaviour
{
    public bool isPlayerNear;
    public GameObject associatedPlatform; // Objeto que se activa cuando hay un jugador en la zona

    private void Start()
    {
        associatedPlatform.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("NewNetworkPlayer"))
        {
            isPlayerNear = true;
            UpdateInteractiveObjectServerRPC(isPlayerNear);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("NewNetworkPlayer"))
        {
            isPlayerNear = false;
            UpdateInteractiveObjectServerRPC(isPlayerNear);
        }
    }

    // El cliente detecta la colision y avisa al servidor, que lo retransmite a todos
    [ServerRpc(RequireOwnership = false)]
    private void UpdateInteractiveObjectServerRPC(bool newIsPlayerNear)
    {
        UpdateInteractiveObjectClientRPC(newIsPlayerNear);
    }

    [ClientRpc]
    private void UpdateInteractiveObjectClientRPC(bool newIsPlayerReady)
    {
        isPlayerNear = newIsPlayerReady;
        associatedPlatform.SetActive(isPlayerNear);
    }
}