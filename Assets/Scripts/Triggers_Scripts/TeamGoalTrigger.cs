using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Trigger de checkpoint entre zonas de juego dentro de la misma escena.
// Cuando un jugador entra, teletransporta a todo su equipo al punto de spawn
// de la siguiente zona. Funciona dentro de GameScene01 sin cambiar de escena.

public class TeamGoalTrigger : NetworkBehaviour
{
    [Header("Spawns por equipo")]
    [SerializeField] private Transform spawnPointTeam1; // Rosa
    [SerializeField] private Transform spawnPointTeam2; // Azul

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("NewNetworkPlayer")) return;

        NetworkObject playerNet = other.GetComponent<NetworkObject>();
        if (playerNet == null) return;

        ulong ownerId = playerNet.OwnerClientId;
        var playerData = ConnectedUserListManager.Singleton?.usersConnectedList
            .Find(u => u.userId == ownerId);

        if (playerData == null) return;
        if (playerData.team != 1 && playerData.team != 2) return;

        // Avisamos al servidor del equipo que debe teletransportarse
        TeleportTeamServerRpc(playerData.team);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TeleportTeamServerRpc(int teamId)
    {
        Transform targetSpawn = (teamId == 1) ? spawnPointTeam1 : spawnPointTeam2;
        if (targetSpawn == null) return;

        Vector3 targetPos = targetSpawn.position;
        Quaternion targetRot = targetSpawn.rotation;

        NetworkObject[] allNetObjs = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

        foreach (var netObj in allNetObjs)
        {
            if (!netObj.IsSpawned || netObj.OwnerClientId == ulong.MaxValue) continue;
            if (!netObj.CompareTag("NewNetworkPlayer")) continue;

            ulong ownerId = netObj.OwnerClientId;
            var data = ConnectedUserListManager.Singleton?.usersConnectedList
                .Find(u => u.userId == ownerId);

            if (data == null || data.team != teamId) continue;

            NetworkTransform netTrans = netObj.GetComponent<NetworkTransform>();
            if (netTrans == null) continue;

            // Offset en Y para que el personaje no aparezca dentro del suelo
            Vector3 finalPos = targetPos + new Vector3(0f, 1.5f, 0f);

            // SetState con teleport = true indica a Netcode que el cambio es instantaneo
            // y no debe interpolar desde la posicion anterior, evitando el efecto de deslizamiento
            netTrans.SetState(finalPos, targetRot, null, true);

            // Reseteamos la fisica para que el jugador no salga disparado con la inercia anterior
            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}