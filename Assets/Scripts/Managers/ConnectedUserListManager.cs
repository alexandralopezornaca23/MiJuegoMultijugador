using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// Script central del proyecto. Guarda y sincroniza la lista de todos los jugadores conectados,
// incluyendo su nombre, equipo y estado. Casi todos los demas scripts lo consultan
// para saber a que equipo pertenece cada jugador.

[Serializable]
public class ConnectedUserListData : INetworkSerializable
{
    public string userConnectedName;
    public ulong userId;
    public bool isReady;
    public int team; // 0 = sin equipo, 1 = Rosa, 2 = Azul

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

    // Diccionario que vincula cada clientId con el controlador fisico de su personaje en escena.
    private Dictionary<ulong, ThirdPersonController> players = new Dictionary<ulong, ThirdPersonController>();

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
        players.Clear(); // Limpiamos tambien las referencias de personajes al parar el cliente
        UpdateVisualUserList();
    }

    private void OnServerStopped(bool wasHost)
    {
        usersConnectedList.Clear();
        players.Clear();
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
            int countBeforeAdd = usersConnectedList.Count;

            if (countBeforeAdd == 0)
            {
                newUserConnected.team = 2; // 2 = Azul, 1 = Rosa
            }
            else
            {
                var lastTeam = usersConnectedList.Last().team;
                newUserConnected.team = lastTeam == 2 ? 1 : 2;
            }

            newUserConnected.isReady = true;
            usersConnectedList.Add(newUserConnected);

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
            UpdateUsersConnectedListClientRPC(usersConnectedList.ToArray());
        }
    }

    public void RequestTeamChange(int newTeam)
    {
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
        TeleportTeam(teamId, position);
    }

    // Registra el controlador de personaje de un jugador para poder teletransportarlo despues.
    // Si ya existe una entrada para ese clientId (de una partida anterior), la sobreescribimos
    // con el personaje nuevo en lugar de ignorarla.
    public void RegisterPlayer(ulong clientId, ThirdPersonController playerScript)
    {
        if (playerScript != null)
        {
            players[clientId] = playerScript;
        }
    }

    public void UnregisterPlayer(ulong clientId)
    {
        players.Remove(clientId);
    }

    // Teletransporta a todos los jugadores de un equipo a una posicion concreta.
    // Comprobamos que cada personaje siga existiendo y spawneado en la red antes de teletransportarlo,
    // para evitar errores con personajes "fantasma" de partidas anteriores.
    public void TeleportTeam(int teamId, Vector3 destinationPosition)
    {
        if (!IsServer) return;

        foreach (var user in usersConnectedList)
        {
            if (user.team != teamId) continue;
            if (!players.TryGetValue(user.userId, out var player)) continue;

            // Si la referencia esta muerta (personaje destruido) o no esta spawneada, la saltamos
            if (player == null) continue;
            if (player.myNetworkObject == null || !player.myNetworkObject.IsSpawned) continue;

            player.TeleportClientRpc(destinationPosition);
        }
    }

    public void TeleportPlayer(ulong clientId, Vector3 destinationPosition)
    {
        if (!IsServer) return;

        if (players.TryGetValue(clientId, out var player))
        {
            if (player == null) return;
            if (player.myNetworkObject == null || !player.myNetworkObject.IsSpawned) return;

            player.TeleportClientRpc(destinationPosition);
        }
    }
}