using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

// Contenedor del segundo juego. Detecta cuando los objetos del equipo correcto entran en su zona,
// actualiza el contador de entregas y destruye la barrera cuando se completan todas.

public class TeamContainer : NetworkBehaviour
{
    [Header("Configuracion de Equipo")]
    public int myTeam = 1;
    public string teamName;

    [Header("Referencias Manuales (Escena)")]
    public List<GameObject> objectsToCollect = new List<GameObject>();

    [Header("Interfaz")]
    public TextMeshProUGUI statusText;

    [Header("Meta")]
    public GameObject objectToDestroy;

    // NetworkVariables para que el contador sea visible y actualizado en todos los clientes
    private NetworkVariable<int> totalToDeliver = new NetworkVariable<int>(0);
    private NetworkVariable<int> currentDelivered = new NetworkVariable<int>(0);

    // HashSet para rastrear que objetos faltan por entregar sin contar duplicados
    private HashSet<int> requiredInstanceIds = new HashSet<int>();

    public override void OnNetworkSpawn()
    {
        currentDelivered.OnValueChanged += OnCounterChanged;
        if (IsServer) ResetContainerServer();
        else UpdateUI(currentDelivered.Value, totalToDeliver.Value);
    }

    private void ResetContainerServer()
    {
        requiredInstanceIds.Clear();
        foreach (GameObject go in objectsToCollect)
        {
            if (go != null)
            {
                requiredInstanceIds.Add(go.GetInstanceID());
                if (go.TryGetComponent<PikeableObject>(out var pikeable))
                    pikeable.isDelivered.Value = false;
            }
        }
        totalToDeliver.Value = requiredInstanceIds.Count;
        currentDelivered.Value = 0;
        UpdateUI(0, totalToDeliver.Value);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("MasterObject"))
        {
            if (other.TryGetComponent<NetworkObject>(out var masterNet))
            {
                currentDelivered.Value = totalToDeliver.Value;
                ReleaseFromCarrier(masterNet);
                DespawnAndHide(masterNet);
                CheckIfAllDelivered();
                return;
            }
        }

        int instanceId = other.gameObject.GetInstanceID();
        if (other.CompareTag("DragObject") && requiredInstanceIds.Contains(instanceId))
        {
            if (other.TryGetComponent<NetworkObject>(out var netObj))
            {
                requiredInstanceIds.Remove(instanceId);
                currentDelivered.Value++;
                ReleaseFromCarrier(netObj);
                DespawnAndHide(netObj);
                CheckIfAllDelivered();
            }
        }
    }

    // Antes de hacer Despawn del objeto, buscamos si algun jugador lo lleva en la mano
    // y le forzamos a soltarlo para que no quede bloqueado sin poder recoger mas objetos.
    // No usamos el OwnerClientId del objeto porque cuando llega al trigger ya puede haber
    // vuelto al servidor. En su lugar preguntamos a cada PickupObject si lleva este objeto
    // a traves de IsCarrying(), que usa el registro interno del servidor. (por esto causaba errores)
    private void ReleaseFromCarrier(NetworkObject itemNetObj)
    {
        if (!IsServer) return;

        var pikeable = itemNetObj.GetComponent<PikeableObject>();
        if (pikeable == null || !pikeable.isHeld.Value) return;

        ulong itemId = itemNetObj.NetworkObjectId;

        foreach (var kvp in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
        {
            var pickup = kvp.Value.GetComponent<PickupObject>();
            if (pickup == null) continue;

            if (!pickup.IsCarrying(itemId)) continue;

            pickup.ForceDropOnPush();
            return;
        }

        // Si no se encuentra portador pero el objeto figura como llevado, lo limpiamos directamente
        pikeable.isHeld.Value = false;
    }

    private void OnCounterChanged(int previousValue, int newValue) => UpdateUI(newValue, totalToDeliver.Value);

    private void UpdateUI(int delivered, int total)
    {
        if (statusText != null)
        {
            int restantes = total - delivered;
            statusText.text = restantes > 0 ? $"{teamName}: {restantes}" : "Puerta del equipo " + teamName + " abierta!";
            statusText.color = restantes > 0 ? Color.white : Color.yellow;
        }
    }

    private void CheckIfAllDelivered()
    {
        if (currentDelivered.Value >= totalToDeliver.Value && totalToDeliver.Value >= 0)
        {
            if (objectToDestroy != null)
            {
                if (objectToDestroy.TryGetComponent<NetworkObject>(out var targetNet))
                {
                    if (targetNet.IsSpawned) DespawnAndHide(targetNet);
                }
                else Destroy(objectToDestroy);
            }
        }
    }

    // Avisamos a todos los clientes para que oculten el objeto antes de eliminarlo de la red
    private void DespawnAndHide(NetworkObject netObj)
    {
        if (netObj == null || !netObj.IsSpawned) return;

        HideObjectClientRpc(netObj.NetworkObjectId);
        netObj.gameObject.SetActive(false);
        netObj.Despawn(false);
    }

    [ClientRpc]
    private void HideObjectClientRpc(ulong targetNetworkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var netObj))
            netObj.gameObject.SetActive(false);
    }

    // Hacemos que el texto del contador mire siempre hacia la camara del jugador
    private void LateUpdate()
    {
        if (statusText != null && Camera.main != null)
        {
            statusText.canvas.transform.LookAt(
                statusText.canvas.transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up);
        }
    }

    public override void OnNetworkDespawn() => currentDelivered.OnValueChanged -= OnCounterChanged;
}