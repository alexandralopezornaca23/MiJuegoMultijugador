using Unity.Netcode;
using UnityEngine;

// Script que instancia el personaje de cada jugador al entrar en la escena de juego.
// El servidor crea los personajes pero cada jugador tiene autoridad sobre el suyo.

public class PlayerSpawnerManager : NetworkBehaviour
{
    [Header("Configuracion de Spawn")]
    public GameObject playerToSpawn;

    [Tooltip("Arrastra aqui el GameObject que tiene el BoxCollider (Trigger)")]
    [SerializeField] private BoxCollider spawnAreaTrigger;

    public override void OnNetworkSpawn()
    {
        // IsClient es true tanto para clientes como para el host,
        // asi que todos piden al servidor que les cree su personaje
        if (IsClient)
        {
            RequestSpawnServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnServerRpc(ulong clientId)
    {
        // Antes de crear el personaje nuevo, eliminamos cualquier personaje anterior
        // de este mismo cliente que pudiera haber quedado de una partida previa.
        // Esto lo hace el servidor con Despawn, que es la forma correcta en Netcode.
        DespawnOldPlayerForClient(clientId);

        SpawnPlayer(clientId);
    }

    // Busca y elimina (desde el servidor) cualquier personaje que ya pertenezca a este cliente
    private void DespawnOldPlayerForClient(ulong clientId)
    {
        if (!IsServer) return;

        NetworkObject[] allNetObjs = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        foreach (var netObj in allNetObjs)
        {
            if (!netObj.IsSpawned) continue;
            if (!netObj.CompareTag("NewNetworkPlayer")) continue;

            // Si este personaje pertenece al cliente que esta entrando, lo despawneamos
            if (netObj.OwnerClientId == clientId)
            {
                netObj.Despawn(true);
            }
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        Vector3 randomSpawnPos = GetRandomPointInBounds(spawnAreaTrigger.bounds);
        Quaternion spawnRotation = spawnAreaTrigger.transform.rotation;

        GameObject newPlayer = Instantiate(playerToSpawn, randomSpawnPos, spawnRotation);

        if (newPlayer.TryGetComponent<NetworkObject>(out var netObj))
        {
            // Spawneamos el objeto en la red y le asignamos la autoridad al cliente correspondiente
            netObj.SpawnWithOwnership(clientId);

            if (newPlayer.TryGetComponent<ThirdPersonController>(out var playerScript))
            {
                // Como el cliente tiene autoridad sobre su posicion, usamos un ClientRpc
                // para indicarle donde debe colocarse en lugar de moverlo directamente desde el servidor
                playerScript.TeleportClientRpc(randomSpawnPos);

                // Registramos el controlador para poder teletransportar al jugador por equipo mas adelante
                if (ConnectedUserListManager.Singleton != null)
                    ConnectedUserListManager.Singleton.RegisterPlayer(clientId, playerScript);
            }
        }
    }

    private Vector3 GetRandomPointInBounds(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.min.y, // Mantenemos la altura del suelo para no spawnear en el aire
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }
}