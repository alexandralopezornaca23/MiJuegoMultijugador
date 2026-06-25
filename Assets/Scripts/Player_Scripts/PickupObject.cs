using Unity.Netcode;
using UnityEngine;

// Script que gestiona la recogida y soltado de objetos del segundo juego.
// Cuando un jugador recoge un objeto, el servidor le transfiere la propiedad para que
// pueda moverlo sin retardo. Al soltarlo, la propiedad vuelve al servidor.

public class PickupObject : NetworkBehaviour
{
    [SerializeField] private Transform handPoint;
    public NetworkObject heldObject; // Objeto que lleva el jugador en la mano (solo valido en el cliente propietario)

    // Registro en el servidor del objeto que lleva este jugador.
    // A diferencia de heldObject, este valor es fiable en el servidor aunque el ownership haya cambiado.
    private ulong heldObjectIdServer;

    [Header("Configuracion")]
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private LayerMask pickupLayer;
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    [SerializeField] private KeyCode dropKey = KeyCode.R;

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // Movemos el objeto con el Rigidbody para mantener la sincronizacion con el motor de fisicas
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
            TryPickup();
    }

    private void TryPickup()
    {
        if (heldObject != null) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, pickupRange, pickupLayer);
        if (hits.Length == 0) return;

        // Buscamos el objeto recogible mas cercano dentro del rango
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

        // Comprobamos en cliente que el objeto no este ya entregado o llevado antes de enviar el RPC
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

        // Validacion final en el servidor para evitar que dos jugadores recojan el mismo objeto a la vez
        if (pickupItem.isDelivered.Value || pickupItem.isHeld.Value) return;

        ulong requesterId = rpcParams.Receive.SenderClientId;
        int requesterTeam = GetTeamForClient(requesterId);

        // Si el objeto pertenece a un equipo concreto y el jugador no es de ese equipo, lo rechazamos
        if (pickupItem.allowedTeam != 0 && pickupItem.allowedTeam != requesterTeam)
        {
            string teamName = pickupItem.allowedTeam == 1 ? "rosa" : "azul";
            var clientRpcParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { requesterId } } };
            ShowFeedbackClientRpc($"Objeto del equipo {teamName}\nNo puedes recogerlo", clientRpcParams);
            return;
        }

        pickupItem.isHeld.Value = true;
        heldObjectIdServer = itemNetId; // Registramos en el servidor que este jugador lleva este objeto

        if (targetNetObj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // Transferimos la propiedad al cliente para que pueda mover el objeto sin retardo
        targetNetObj.ChangeOwnership(requesterId);
        SetItemStateClientRpc(itemNetId, true);

        var assignParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { requesterId } } };
        AssignHeldObjectClientRpc(itemNetId, assignParams);
    }

    [ClientRpc]
    private void AssignHeldObjectClientRpc(ulong itemNetId, ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var target))
            heldObject = target;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDropServerRpc(ulong itemNetId, Vector3 dropPosition, Quaternion dropRotation, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (netObj.OwnerClientId != senderId && senderId != NetworkManager.ServerClientId) return;

        ServerDropItem(netObj, dropPosition, dropRotation);
    }

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

        // Devolvemos la propiedad al servidor y limpiamos el estado del objeto
        netObj.ChangeOwnership(NetworkManager.ServerClientId);
        if (netObj.TryGetComponent<PikeableObject>(out var p)) p.isHeld.Value = false;

        heldObjectIdServer = 0;
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
            rb.useGravity = !isPickedUp;
    }

    [ClientRpc]
    private void ShowFeedbackClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        ConnectionCallbackManager.Singleton?.ShowFeedback(message);
    }

    // Permite que TeamContainer pregunte si este jugador lleva un objeto concreto.
    // Usa heldObjectIdServer en lugar del ownership porque cuando el objeto llega al trigger
    // su propiedad ya puede haber vuelto al servidor.
    public bool IsCarrying(ulong networkObjectId)
    {
        return heldObjectIdServer == networkObjectId;
    }

    // Fuerza al jugador a soltar el objeto que lleva, usado por TeamContainer y PlayerPush
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