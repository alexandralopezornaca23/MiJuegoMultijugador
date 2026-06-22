using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class TeamContainer : NetworkBehaviour
{
    [Header("Configuración de Equipo")]
    public int myTeam = 1;
    public string teamName;

    [Header("Referencias Manuales (Escena)")]
    public List<GameObject> objectsToCollect = new List<GameObject>();

    [Header("Interfaz")]
    public TextMeshProUGUI statusText;

    [Header("Meta")]
    public GameObject objectToDestroy;

    private NetworkVariable<int> totalToDeliver = new NetworkVariable<int>(0);
    private NetworkVariable<int> currentDelivered = new NetworkVariable<int>(0);
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
                DespawnAndHide(netObj);
                CheckIfAllDelivered();
            }
        }
    }

    private void OnCounterChanged(int previousValue, int newValue) => UpdateUI(newValue, totalToDeliver.Value);

    private void UpdateUI(int delivered, int total)
    {
        if (statusText != null)
        {
            int restantes = total - delivered;
            statusText.text = restantes > 0 ? $"{teamName}: {restantes}" : "¡Puerta del equipo " + teamName + " abierta!";
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
        {
            netObj.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (statusText != null && Camera.main != null)
        {
            statusText.canvas.transform.LookAt(statusText.canvas.transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up);
        }
    }

    public override void OnNetworkDespawn() => currentDelivered.OnValueChanged -= OnCounterChanged;
}