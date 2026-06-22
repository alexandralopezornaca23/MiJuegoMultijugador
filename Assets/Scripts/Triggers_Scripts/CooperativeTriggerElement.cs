using Unity.Netcode;
using UnityEngine;

public class CooperativeTriggerElement : NetworkBehaviour
{
    public bool isPlayerNear;
    public GameObject associatedPlatform;

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
