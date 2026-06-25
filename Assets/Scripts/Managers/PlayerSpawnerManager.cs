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
        SpawnPlayer(clientId);
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