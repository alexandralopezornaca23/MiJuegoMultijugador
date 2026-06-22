using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public class ConnectedUserListData : INetworkSerializable
{
    public string userConnectedName;
    public ulong userId;
    public bool isReady;
    public int team; //0 = No ready, 1 = Rojo, 2 = Azul

    public ConnectedUserListData()
    {
        isReady = false;
        team = 0;
    }


    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref userConnectedName);
        serializer.SerializeValue(ref userId);
        serializer.SerializeValue(ref isReady);
        serializer.SerializeValue(ref team);
    }
}

public class ConnectedUserListManager : NetworkBehaviour
{
    private static ConnectedUserListManager singleton;
    public static ConnectedUserListManager Singleton => singleton;

    private void Awake()
    {
        if (singleton == null)
        {
            singleton = this;
            usersConnectedList = new List<ConnectedUserListData>();
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public List<ConnectedUserListData> usersConnectedList;

    

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += MethodRemoveUserFromList;
            NetworkManager.Singleton.OnClientStopped += OnClientStopped;
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        }
    }

    private new void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= MethodRemoveUserFromList;
            NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
            NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        }
    }

    private void OnClientStopped(bool wasHost)
    {
        usersConnectedList.Clear();
        UpdateVisualUserList();
    }

    private void OnServerStopped(bool wasHost)
    {
        usersConnectedList.Clear();
        UpdateVisualUserList();
    }

    private void MethodRemoveUserFromList(ulong disconnectedUserId)
    {
        if (IsServer)
        {
            usersConnectedList.RemoveAll(u => u.userId == disconnectedUserId);
            UnregisterPlayer(disconnectedUserId);
            UpdateUsersConnectedListClientRPC(usersConnectedList.ToArray());
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            usersConnectedList.Clear();
        }
        ConnectedUserListData userData = new ConnectedUserListData
        {
            userId = NetworkManager.Singleton.LocalClientId,
            userConnectedName = OnlinePlayersManager.Singleton.playerName,
            isReady = false,
            team = 0
        };
        AddNewConnectedUserServerRpc(userData);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddNewConnectedUserServerRpc(ConnectedUserListData newUserConnected)
    {
        if (!usersConnectedList.Any(u => u.userId == newUserConnected.userId))
        {
            if (!usersConnectedList.Any(u => u.userId == newUserConnected.userId))
            {
                // Asignación automática alternada entre listas de equipos
                int countBeforeAdd = usersConnectedList.Count;

                if (countBeforeAdd == 0) // Es el host
                {
                    newUserConnected.team = 2; // Azul
                }
                else
                {
                    // Alternar equipo respecto al último jugador añadido
                    var lastTeam = usersConnectedList.Last().team;
                    newUserConnected.team = lastTeam == 2 ? 1 : 2; // 1 = Rosa, 2 = Azul
                }

                newUserConnected.isReady = true;
                usersConnectedList.Add(newUserConnected);
            }

            UpdateUsersConnectedListClientRPC(usersConnectedList.ToArray());
        }
    }

    [ClientRpc]
    private void UpdateUsersConnectedListClientRPC(ConnectedUserListData[] newUsersConnectedList)
    {
        usersConnectedList = newUsersConnectedList.ToList();
        UpdateVisualUserList();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerTeamServerRpc(int newTeam, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        var user = usersConnectedList.Find(u => u.userId == clientId);
        if (user != null)
        {
            user.team = newTeam;
            user.isReady = newTeam != 0;
            // Llamamos al ClientRpc para sincronizar a todos
            UpdateUsersConnectedListClientRPC(usersConnectedList.ToArray());
        }
    }

    public void RequestTeamChange(int newTeam)
    {
        // Solo los clientes pueden solicitar cambio de equipo
        if (NetworkManager.Singleton.IsConnectedClient)
        {
            SetPlayerTeamServerRpc(newTeam);
        }
    }

    public void UpdateVisualUserList()
    {
        VisualUsersConnectedList userList = FindAnyObjectByType<VisualUsersConnectedList>();
        if (userList != null)
        {
            userList.UpdateUsersConnectedList(usersConnectedList);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateNameServerRpc(ulong userId, string newName)
    {
        var user = usersConnectedList.Find(u => u.userId == userId);
        if (user != null)
        {
            user.userConnectedName = newName;
            UpdateUsersConnectedListClientRPC(usersConnectedList.ToArray());
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestTeamTeleportServerRpc(int teamId, Vector3 position, ServerRpcParams rpcParams = default)
    {
        // Solo servidor ejecuta esto
        TeleportTeam(teamId, position);
    }

    private Dictionary<ulong, ThirdPersonController> players = new Dictionary<ulong, ThirdPersonController>();

    public void RegisterPlayer(ulong clientId, ThirdPersonController playerScript)
    {
        if (playerScript != null && !players.ContainsKey(clientId))
        {
            players[clientId] = playerScript;
        }
    }

    public void UnregisterPlayer(ulong clientId)
    {
        players.Remove(clientId);
    }

    public void TeleportTeam(int teamId, Vector3 destinationPosition)
    {
        if (!IsServer) return;

        int count = 0;
        foreach (var user in usersConnectedList)
        {
            if (user.team == teamId && players.TryGetValue(user.userId, out var player))
            {
                player.TeleportClientRpc(destinationPosition);
                count++;
            }
        }
    }

    public void TeleportPlayer(ulong clientId, Vector3 destinationPosition)
    {
        if (!IsServer) return;

        if (players.TryGetValue(clientId, out var player))
        {
            player.TeleportClientRpc(destinationPosition);
        }
    }
}