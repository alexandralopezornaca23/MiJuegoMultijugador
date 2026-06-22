using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TeamEndZone : NetworkBehaviour
{
    [Header("Configuración")]
    public string newSceneToLoad = "NextScene";

    [Header("Filtro de equipos")]
    [Tooltip("0 = ambos, 1 = solo rojo/rosa, 2 = solo azul")]
    public int allowedTeam = 0;

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
        if (IsServer)
        {
            CalculateTeamSizes();
        }
    }

    private void CalculateTeamSizes()
    {
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

        var set = (team == 1) ? team1PlayersInside : team2PlayersInside;
        bool wasAdded = set.Add(clientId);

        if (wasAdded)
        {
            CheckIfTeamIsComplete(team);
        }
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

        var set = (team == 1) ? team1PlayersInside : team2PlayersInside;
    }

    private int GetPlayerTeam(ulong clientId)
    {
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
        var currentSet = (team == 1) ? team1PlayersInside : team2PlayersInside;
        int required = GetTeamTotal(team);

        if (required <= 0) return;

        if (currentSet.Count >= required)
        {
            sceneChangeTriggered = true;

            int winner = team;
            int loser = (team == 1) ? 2 : 1;

            ShowVictoryMessageToAllClientRPC(winner, loser);
            LoadSceneServerRPC();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void LoadSceneServerRPC()
    {
        if (sceneChangeTriggered)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(newSceneToLoad, LoadSceneMode.Single);
        }
    }

    [ClientRpc]
    private void ShowVictoryMessageToAllClientRPC(int winningTeam, int losingTeam)
    {
        VictoryMessageManager.Instance?.ShowMessage(winningTeam, losingTeam);
    }
}