using Unity.Netcode;
using UnityEngine;
using System.Linq;

public class TeamTeleportTrigger : NetworkBehaviour
{
    [Header("Destinos por equipo")]
    [SerializeField] private Transform destinationRosa;
    [SerializeField] private Transform destinationAzul;

    [Header("Opcional - Configuración")]
    [SerializeField] private bool allowBothTeams = true;  // Si false, solo permite un equipo específico
    [Header("0 = ambos, 1 = solo rosa, 2 = solo azul")]
    [SerializeField] private int allowedTeam = 0; // 0 = ambos, 1 = solo rosa, 2 = solo azul

    [Header("Modo de teleport")]
    [Tooltip("Si está activo, SOLO se teletransporta el jugador que entra en el trigger (uso para zonas de caída/respawn). Si está desactivado, se teletransporta a todo el equipo (uso para checkpoints).")]
    [SerializeField] private bool teleportOnlyThisPlayer = false;

    private void OnTriggerEnter(Collider other)
    {
        // Seguridad básica
        if (!allowBothTeams && destinationRosa == null && destinationAzul == null) return;

        var player = other.GetComponentInParent<ThirdPersonController>();
        if (player == null) return;

        // Solo el owner del jugador puede activar el trigger
        if (!player.myNetworkObject.IsOwner) return;

        // Obtenemos los datos del jugador
        var userData = ConnectedUserListManager.Singleton.usersConnectedList
            .FirstOrDefault(u => u.userId == player.myNetworkObject.OwnerClientId);

        if (userData == null || userData.team == 0) return;

        if (allowedTeam != 0 && userData.team != allowedTeam)
        {
            return; // Ignorar si no es el equipo permitido
        }

        // Seleccionamos el destino según el equipo
        Transform selectedDestination = null;
        if (userData.team == 1) // Rosa
        {
            selectedDestination = destinationRosa;
        }
        else if (userData.team == 2) // Azul
        {
            selectedDestination = destinationAzul;
        }

        // Si no hay destino válido para ese equipo no hacemos nada
        if (selectedDestination == null)
        {
            return;
        }

        if (teleportOnlyThisPlayer)
        {
            RequestIndividualTeleportServerRpc(selectedDestination.position);
        }
        else
        {
            RequestTeamTeleportServerRpc(userData.team, selectedDestination.position);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTeamTeleportServerRpc(int teamId, Vector3 position, ServerRpcParams rpcParams = default)
    {
        ConnectedUserListManager.Singleton.TeleportTeam(teamId, position);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestIndividualTeleportServerRpc(Vector3 position, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        ConnectedUserListManager.Singleton.TeleportPlayer(senderId, position);
    }
}