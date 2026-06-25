using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Trigger del tercer juego. Para ganar, todos los jugadores de un equipo
// deben estar dentro de la zona al mismo tiempo.
// Usa HashSets para rastrear exactamente quienes estan dentro en cada momento.

public class TeamEndZone : NetworkBehaviour
{
    [Header("Configuracion")]
    public string newSceneToLoad = "NextScene";

    [Header("Filtro de equipos")]
    [Tooltip("0 = ambos, 1 = solo rosa, 2 = solo azul")]
    public int allowedTeam = 0;

    [Header("Seguridad")]
    [Tooltip("Minimo de jugadores que debe tener un equipo para poder ganar. Evita victorias accidentales en pruebas con pocos jugadores.")]
    public int minimumPlayersToWin = 1;

    // Un HashSet por equipo para saber exactamente quienes estan dentro del trigger
    private HashSet<ulong> team1PlayersInside = new HashSet<ulong>();
    private HashSet<ulong> team2PlayersInside = new HashSet<ulong>();

    private int team1TotalCount = 0;
    private int team2TotalCount = 0;

    private bool sceneChangeTriggered = false;
    private ConnectedUserListManager listManager;

    private void Awake()
    {
        listManager = ConnectedUserListManager.Singleton;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // No calculamos los tamaños aqui porque puede que no todos los jugadores
        // esten registrados todavia. Lo hacemos justo antes de comprobar la victoria.
    }

    // Recalculamos el total de jugadores por equipo en el momento exacto de la comprobacion
    // para asegurarnos de tener el numero real de jugadores conectados en ese instante
    private void CalculateTeamSizes()
    {
        if (listManager == null)
            listManager = ConnectedUserListManager.Singleton;
        if (listManager == null) return;

        team1TotalCount = listManager.usersConnectedList.Count(u => u.team == 1 && u.isReady);
        team2TotalCount = listManager.usersConnectedList.Count(u => u.team == 2 && u.isReady);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || sceneChangeTriggered) return;
        if (!other.CompareTag("NewNetworkPlayer")) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;

        ulong clientId = netObj.OwnerClientId;
        int team = GetPlayerTeam(clientId);

        if (team == 0) return;
        if (!IsTeamAllowed(team)) return;

        // Añadimos al jugador al HashSet de su equipo (los HashSet ignoran duplicados automaticamente)
        var set = (team == 1) ? team1PlayersInside : team2PlayersInside;
        set.Add(clientId);

        CheckIfTeamIsComplete(team);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("NewNetworkPlayer")) return;

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null) return;

        ulong clientId = netObj.OwnerClientId;
        int team = GetPlayerTeam(clientId);
        if (team == 0) return;

        // Eliminamos al jugador del HashSet cuando sale para que el conteo sea siempre exacto
        var set = (team == 1) ? team1PlayersInside : team2PlayersInside;
        set.Remove(clientId);
    }

    private int GetPlayerTeam(ulong clientId)
    {
        if (listManager == null)
            listManager = ConnectedUserListManager.Singleton;
        if (listManager == null) return 0;

        var user = listManager.usersConnectedList.Find(u => u.userId == clientId);
        return user != null ? user.team : 0;
    }

    private bool IsTeamAllowed(int team)
    {
        return allowedTeam == 0 || allowedTeam == team;
    }

    private int GetTeamTotal(int team)
    {
        return team == 1 ? team1TotalCount : team2TotalCount;
    }

    private void CheckIfTeamIsComplete(int team)
    {
        CalculateTeamSizes();

        var currentSet = (team == 1) ? team1PlayersInside : team2PlayersInside;
        int required = GetTeamTotal(team);

        // Proteccion 1: si el equipo tiene menos jugadores que el minimo configurado, no puede ganar
        // Esto evita victorias accidentales cuando el profesor entra solo durante pruebas
        if (required < minimumPlayersToWin)
        {
            return;
        }

        if (required <= 0) return;

        if (currentSet.Count >= required)
        {
            // Verificacion final: confirmamos que todos los IDs del HashSet siguen perteneciendo
            // al equipo correcto, descartando posibles estados inconsistentes
            int validPlayersInside = currentSet.Count(id => GetPlayerTeam(id) == team);

            if (validPlayersInside < required) return;

            sceneChangeTriggered = true;
            int winner = team;
            int loser = (team == 1) ? 2 : 1;

            ShowVictoryMessageToAllClientRPC(winner, loser);
            NetworkManager.Singleton.SceneManager.LoadScene(newSceneToLoad, LoadSceneMode.Single);
        }
    }

    [ClientRpc]
    private void ShowVictoryMessageToAllClientRPC(int winningTeam, int losingTeam)
    {
        VictoryMessageManager.Instance?.ShowMessage(winningTeam, losingTeam);
    }
}