using Unity.Netcode;
using UnityEngine;
using System.Linq;

// Trigger que teletransporta jugadores al entrar en su zona.
// Puede teletransportar solo al jugador que entra (modo respawn/caida)
// o a todo su equipo a la vez (modo checkpoint).

public class TeamTeleportTrigger : NetworkBehaviour
{
    [Header("Destinos por equipo")]
    [SerializeField] private Transform destinationRosa;
    [SerializeField] private Transform destinationAzul;

    [Header("Opcional - Configuracion")]
    [SerializeField] private bool allowBothTeams = true;

    [Header("0 = ambos, 1 = solo rosa, 2 = solo azul")]
    [SerializeField] private int allowedTeam = 0;

    [Header("Modo de teleport")]
    [Tooltip("Si esta activo, solo se teletransporta el jugador que entra. Si esta desactivado, se teletransporta a todo el equipo.")]
    [SerializeField] private bool teleportOnlyThisPlayer = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!allowBothTeams && destinationRosa == null && destinationAzul == null) return;

        var player = other.GetComponentInParent<ThirdPersonController>();
        if (player == null) return;

        // Solo el propietario del personaje puede activar el trigger para evitar llamadas duplicadas
        if (!player.myNetworkObject.IsOwner) return;

        var userData = ConnectedUserListManager.Singleton.usersConnectedList
            .FirstOrDefault(u => u.userId == player.myNetworkObject.OwnerClientId);

        if (userData == null || userData.team == 0) return;

        // Si el trigger esta configurado para un equipo concreto, ignoramos al otro
        if (allowedTeam != 0 && userData.team != allowedTeam) return;

        Transform selectedDestination = userData.team == 1 ? destinationRosa : destinationAzul;

        if (selectedDestination == null) return;

        // Segun el modo elegido, teletransportamos solo a este jugador o a todo su equipo
        if (teleportOnlyThisPlayer)
            RequestIndividualTeleportServerRpc(selectedDestination.position);
        else
            RequestTeamTeleportServerRpc(userData.team, selectedDestination.position);
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