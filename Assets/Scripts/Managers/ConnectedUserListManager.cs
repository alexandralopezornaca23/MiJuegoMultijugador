using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// Script central del proyecto. Guarda y sincroniza la lista de todos los jugadores conectados,
// incluyendo su nombre, equipo y estado. Casi todos los demas scripts lo consultan
// para saber a que equipo pertenece cada jugador.

// Clase que representa los datos de un jugador conectado.
// Implementa INetworkSerializable para poder enviarse directamente por la red como un objeto completo.
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

    // Metodo obligatorio de INetworkSerializable: define que campos se envian por la red
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

    // Lista de todos los jugadores conectados, accesible publicamente para que otros scripts la consulten
    public List<ConnectedUserListData> usersConnectedList;

    // Diccionario que vincula cada clientId con el controlador fisico de su personaje en escena.
    // Necesario para poder teletransportar jugadores por equipo desde el servidor.
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
        UpdateVisualUserList();
    }

    private void OnServerStopped(bool wasHost)
    {
        usersConnectedList.Clear();
        UpdateVisualUserList();
    }

    // Cuando un jugador se desconecta, el servidor lo elimina de la lista y avisa a todos los clientes
    private void MethodRemoveUserFromList(ulong disconnectedUserId)
    {
        if (IsServer)
        {
            usersConnectedList.RemoveAll(u => u.userId == disconnectedUserId);
            UnregisterPlayer(disconnectedUserId);
            UpdateUsersConnectedListClientRPC(usersConnectedList.ToArray());
        }
    }

    // Cuando el objeto de red aparece en escena, cada jugador registra sus datos en el servidor
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

            // El primer jugador en unirse (el host) va al equipo Azul.
            // El resto se asignan de forma alternada para equilibrar los equipos automaticamente.
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

    // El servidor envia la lista actualizada a todos los clientes para que todos tengan la misma informacion
    [ClientRpc]
    private void UpdateUsersConnectedListClientRPC(ConnectedUserListData[] newUsersConnectedList)
    {
        usersConnectedList = newUsersConnectedList.ToList();
        UpdateVisualUserList();
    }

    // El servidor actualiza el equipo del jugador que ha solicitado el cambio
    // Usamos rpcParams para saber que cliente hizo la peticion sin que el cliente tenga que enviarlo
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

    // Busca el componente visual del lobby y le pasa la lista actualizada para que la dibuje
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

    // Registra el controlador de personaje de un jugador para poder teletransportarlo despues
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

    // Teletransporta a todos los jugadores de un equipo a una posicion concreta
    public void TeleportTeam(int teamId, Vector3 destinationPosition)
    {
        if (!IsServer) return;

        foreach (var user in usersConnectedList)
        {
            if (user.team == teamId && players.TryGetValue(user.userId, out var player))
            {
                player.TeleportClientRpc(destinationPosition);
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