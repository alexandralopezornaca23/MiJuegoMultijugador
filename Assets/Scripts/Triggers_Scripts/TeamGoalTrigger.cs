using Unity.Netcode;
using Unity.Netcode.Components; // Necesario para NetworkTransform
using UnityEngine;

public class TeamGoalTrigger : NetworkBehaviour
{
    [Header("Spawns por equipo - Arrastra los objetos vacíos aquí")]
    [SerializeField] private Transform spawnPointTeam1;  // Rosa (equipo 1)
    [SerializeField] private Transform spawnPointTeam2;  // Azul (equipo 2)

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("NewNetworkPlayer")) return;

        NetworkObject playerNet = other.GetComponent<NetworkObject>();
        if (playerNet == null)
        {
            Debug.LogError("[Trigger] El objeto que entró no tiene NetworkObject");
            return;
        }

        ulong ownerId = playerNet.OwnerClientId;
        var playerData = ConnectedUserListManager.Singleton?.usersConnectedList
            .Find(u => u.userId == ownerId);

        if (playerData == null)
        {
            Debug.LogError($"[Trigger] No se encontraron datos para el cliente {ownerId}");
            return;
        }

        if (playerData.team != 1 && playerData.team != 2)
        {
            Debug.LogWarning($"[Trigger] Jugador {ownerId} sin equipo válido (team={playerData.team})");
            return;
        }

        Debug.Log($"[Trigger] Detectado jugador {ownerId} del equipo {playerData.team} → enviando RPC");

        // Enviamos al servidor el equipo que debe teletransportarse
        TeleportTeamServerRpc(playerData.team);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TeleportTeamServerRpc(int teamId)
    {
        Debug.Log($"[Server-Teleport] RPC recibido para equipo {teamId}");

        // Seleccionamos el punto de spawn correspondiente
        Transform targetSpawn = (teamId == 1) ? spawnPointTeam1 : spawnPointTeam2;
        if (targetSpawn == null)
        {
            Debug.LogError($"[Server-Teleport] No se asignó spawn para equipo {teamId} en el Inspector");
            return;
        }

        Vector3 targetPos = targetSpawn.position;
        Quaternion targetRot = targetSpawn.rotation;

        Debug.Log($"[Server-Teleport] Posición objetivo: {targetPos} | Rotación: {targetRot.eulerAngles}");

        int teleportedCount = 0;

        // Buscamos todos los NetworkObjects en la escena (versión sin warning de obsoleto)
        NetworkObject[] allNetObjs = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

        foreach (var netObj in allNetObjs)
        {
            if (!netObj.IsSpawned || netObj.OwnerClientId == ulong.MaxValue) continue;
            if (!netObj.CompareTag("NewNetworkPlayer")) continue;

            ulong ownerId = netObj.OwnerClientId;
            var data = ConnectedUserListManager.Singleton?.usersConnectedList
                .Find(u => u.userId == ownerId);

            if (data == null || data.team != teamId) continue;

            // Obtenemos el NetworkTransform
            NetworkTransform netTrans = netObj.GetComponent<NetworkTransform>();
            if (netTrans == null)
            {
                Debug.LogError($"[Server-Teleport] El jugador {ownerId} NO tiene NetworkTransform adjunto");
                continue;
            }

            // Offset anti-suelo para evitar caídas inmediatas
            Vector3 finalPos = targetPos + new Vector3(0f, 1.5f, 0f);

            // Aplicamos el estado con teleport = true (cambio instantáneo)
            netTrans.SetState(finalPos, targetRot, null, true);

            // Reset de física para evitar que el Rigidbody corrija o caiga
            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                Debug.Log($"[Server-Teleport] Física reseteada para jugador {ownerId}");
            }

            Debug.Log($"[Server-Teleport] Jugador {ownerId} (equipo {teamId}) teletransportado a {finalPos}");
            teleportedCount++;
        }

        Debug.Log($"[Server-Teleport] Proceso completado - Jugadores teletransportados: {teleportedCount}");
    }
}