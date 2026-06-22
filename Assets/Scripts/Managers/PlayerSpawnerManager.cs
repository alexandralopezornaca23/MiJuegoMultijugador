using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnerManager : NetworkBehaviour
{
    [Header("Configuración de Spawn")]
    public GameObject playerToSpawn;

    [Tooltip("Arrastra aquí el GameObject que tiene el BoxCollider (Trigger)")]
    [SerializeField] private BoxCollider spawnAreaTrigger;

    public override void OnNetworkSpawn()
    {
        // Solo los clientes piden spawnear. 
        // El Host es servidor y cliente, así que IsClient es true para él.
        if (IsClient)
        {
            RequestSpawnServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnServerRpc(ulong clientId)
    {
        SpawnPlayer(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        // 1. Calculamos una posición aleatoria dentro del área del Trigger
        Vector3 randomSpawnPos = GetRandomPointInBounds(spawnAreaTrigger.bounds);
        Quaternion spawnRotation = spawnAreaTrigger.transform.rotation;

        // 2. Instanciamos en el servidor
        GameObject newPlayer = Instantiate(playerToSpawn, randomSpawnPos, spawnRotation);

        if (newPlayer.TryGetComponent<NetworkObject>(out var netObj))
        {
            // 3. Spawneamos con autoría
            netObj.SpawnWithOwnership(clientId);

            // 4. TRUCO VITAL: Usamos el Teleport que ya tienes en tu script.
            // Como el cliente tiene autoridad, el servidor debe decirle: "Ponte AQUÍ".
            if (newPlayer.TryGetComponent<ThirdPersonController>(out var playerScript))
            {
                // Llamamos al ClientRpc de teleportación para que el cliente mueva su CC
                playerScript.TeleportClientRpc(randomSpawnPos);

                if (ConnectedUserListManager.Singleton != null)
                {
                    ConnectedUserListManager.Singleton.RegisterPlayer(clientId, playerScript);
                }
            }
        }
    }

    private Vector3 GetRandomPointInBounds(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.min.y, // Mantenemos la altura del suelo del área
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }
}