using Unity.Netcode;
using UnityEngine;

public class PickupObject : NetworkBehaviour
{
    [SerializeField] private Transform handPoint;
    public NetworkObject heldObject;

    // NUEVO: el servidor siempre sabe qué objeto sostiene este jugador,
    // sin depender de "heldObject" (que solo está sincronizado en el cliente propietario)
    private ulong heldObjectIdServer;

    [Header("Configuración")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private LayerMask pickupLayer;
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    [SerializeField] private KeyCode dropKey = KeyCode.R;

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        if (heldObject != null)
        {
            if (heldObject.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.MovePosition(handPoint.position);
                rb.MoveRotation(handPoint.rotation);
            }
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (heldObject != null && Input.GetKeyDown(dropKey))
        {
            Vector3 pos = heldObject.transform.position;
            Quaternion rot = heldObject.transform.rotation;
            RequestDropServerRpc(heldObject.NetworkObjectId, pos, rot);
        }

        if (Input.GetKeyDown(pickupKey))
        {
            TryPickup();
        }
    }

    private void TryPickup()
    {
        if (heldObject != null) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, pickupRange, pickupLayer);
        if (hits.Length == 0) return;

        Collider closest = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = hit;
            }
        }
        if (closest == null) return;

        var netObj = closest.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsSpawned) return;

        var pikeable = netObj.GetComponent<PikeableObject>();
        if (pikeable != null && (pikeable.isDelivered.Value || pikeable.isHeld.Value)) return;

        RequestPickupServerRpc(netObj.NetworkObjectId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong itemNetId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var targetNetObj)) return;
        var pickupItem = targetNetObj.GetComponent<PikeableObject>();
        if (pickupItem == null) return;

        if (pickupItem.isDelivered.Value || pickupItem.isHeld.Value) return;

        ulong requesterId = rpcParams.Receive.SenderClientId;
        int requesterTeam = GetTeamForClient(requesterId);

        if (pickupItem.allowedTeam != 0 && pickupItem.allowedTeam != requesterTeam)
        {
            string teamName = pickupItem.allowedTeam == 1 ? "rosa" : "azul";
            var clientRpcParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { requesterId } } };
            ShowFeedbackClientRpc($"Objeto del equipo {teamName}\nNo puedes recogerlo", clientRpcParams);
            return;
        }

        pickupItem.isHeld.Value = true;
        heldObjectIdServer = itemNetId; // NUEVO: el servidor recuerda qué objeto sostiene este jugador

        if (targetNetObj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        targetNetObj.ChangeOwnership(requesterId);
        SetItemStateClientRpc(itemNetId, true);

        var assignParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { requesterId } } };
        AssignHeldObjectClientRpc(itemNetId, assignParams);
    }

    [ClientRpc]
    private void AssignHeldObjectClientRpc(ulong itemNetId, ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var target))
        {
            heldObject = target;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDropServerRpc(ulong itemNetId, Vector3 dropPosition, Quaternion dropRotation, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (netObj.OwnerClientId != senderId && senderId != NetworkManager.ServerClientId) return;

        ServerDropItem(netObj, dropPosition, dropRotation);
    }

    // NUEVO: lógica común de "soltar", usada tanto por el drop manual (tecla R)
    // como por el drop forzado al recibir un empujón.
    private void ServerDropItem(NetworkObject netObj, Vector3 dropPosition, Quaternion dropRotation)
    {
        netObj.transform.SetPositionAndRotation(dropPosition, dropRotation);

        if (netObj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(transform.forward * 3f + Vector3.up * 2f, ForceMode.Impulse);
        }

        netObj.ChangeOwnership(NetworkManager.ServerClientId);
        if (netObj.TryGetComponent<PikeableObject>(out var p)) p.isHeld.Value = false;

        heldObjectIdServer = 0; // NUEVO: limpiar el registro server-side
        SetItemStateClientRpc(netObj.NetworkObjectId, false);
        UnassignHeldObjectClientRpc();
    }

    [ClientRpc]
    private void UnassignHeldObjectClientRpc()
    {
        heldObject = null;
    }

    [ClientRpc]
    private void SetItemStateClientRpc(ulong itemNetId, bool isPickedUp)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;

        if (netObj.TryGetComponent<Rigidbody>(out var rb))
        {
            if (isPickedUp) rb.useGravity = false;
            else rb.useGravity = true;
        }
    }

    [ClientRpc]
    private void ShowFeedbackClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        ConnectionCallbackManager.Singleton?.ShowFeedback(message);
    }

    // MODIFICADO: ya no depende de "heldObject" (solo válido en el cliente propietario),
    // sino del registro server-side "heldObjectIdServer".
    public void ForceDropOnPush()
    {
        if (!IsServer) return;

        if (heldObjectIdServer == 0) return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(heldObjectIdServer, out var netObj)) return;

        ServerDropItem(netObj, netObj.transform.position, netObj.transform.rotation);
    }

    private int GetTeamForClient(ulong clientId)
    {
        var manager = ConnectedUserListManager.Singleton;
        if (manager == null) return 0;
        var user = manager.usersConnectedList.Find(u => u.userId == clientId);
        return user != null ? user.team : 0;
    }
}