using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

// Script que gestiona la conexion a traves de Unity Relay.
// Permite crear una sala como host o unirse a una existente mediante codigo.
// Las salas admiten hasta 9 conexiones adicionales al host (10 jugadores en total).

public class RelayManager : MonoBehaviour
{
    public TMP_InputField input_roomCodeToJoin;
    public TMP_InputField playerNameInput;

    private const int MAX_NAME_LENGTH = 14;

    private async void Start()
    {
        // Inicializamos Unity Services y autenticacion anonima antes de cualquier conexion
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        string savedName = PlayerPrefs.GetString("PlayerName", "");

        if (!string.IsNullOrEmpty(savedName))
        {
            playerNameInput.text = savedName;
            playerNameInput.interactable = false;
        }
        else
        {
            playerNameInput.text = "";
            playerNameInput.interactable = true;

            var placeholder = playerNameInput.placeholder.GetComponent<TMP_Text>();
            if (placeholder != null)
                placeholder.text = "Escribe tu nombre...";
        }

        WelcomeMessageManager welcome = FindFirstObjectByType<WelcomeMessageManager>();
        welcome?.UpdateWelcomeMessage();

        // Limitamos el input a MAX_NAME_LENGTH caracteres y recortamos si se supera
        playerNameInput.characterLimit = MAX_NAME_LENGTH;
        playerNameInput.onValueChanged.AddListener(text =>
        {
            if (text.Length > MAX_NAME_LENGTH)
                playerNameInput.text = text.Substring(0, MAX_NAME_LENGTH);
        });
    }

    public async void CreateRelayRoomButton()
    {
        string name = GetValidPlayerName();

        if (name.Length > MAX_NAME_LENGTH)
        {
            var feedback = FindFirstObjectByType<ConnectionCallbackManager>();
            feedback?.ShowFeedback($"El nombre no puede superar los {MAX_NAME_LENGTH} caracteres.");
            return;
        }

        OnlinePlayersManager.Singleton.SetPlayerName(name);
        PlayerPrefs.SetString("PlayerName", name);
        PlayerPrefs.Save();

        WelcomeMessageManager welcome = FindFirstObjectByType<WelcomeMessageManager>();
        welcome?.UpdateWelcomeMessage();

        string joinCode = await CreateRelayRoom();
        if (joinCode != null)
        {
            LobbyCodeManager.Singleton.lobbyCode.text = joinCode;
            ChatHistoryManager.Instance?.InitializeForRoom(joinCode);
        }
        else
        {
            LobbyCodeManager.Singleton.lobbyCode.text = "ERROR CON EL CODIGO DE SALA";
        }
    }

    public async void JoinRelayRoomButton()
    {
        string name = GetValidPlayerName();
        OnlinePlayersManager.Singleton.SetPlayerName(name);
        PlayerPrefs.SetString("PlayerName", name);
        PlayerPrefs.Save();

        WelcomeMessageManager welcome = FindFirstObjectByType<WelcomeMessageManager>();
        welcome?.UpdateWelcomeMessage();

        string roomCode = input_roomCodeToJoin.text.Trim().ToUpper();
        if (string.IsNullOrWhiteSpace(roomCode)) return;

        LobbyCodeManager.Singleton.lobbyCode.text = roomCode;

        bool joined = await JoinRelayRoom(roomCode);
        if (joined)
            ChatHistoryManager.Instance?.InitializeForRoom(roomCode);
    }

    private string GetValidPlayerName()
    {
        string name = playerNameInput.text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            name = "Jugador" + UnityEngine.Random.Range(0, 1000);
            playerNameInput.text = name;
        }

        if (name.Length > MAX_NAME_LENGTH)
            name = name.Substring(0, MAX_NAME_LENGTH);

        return name;
    }

    // Solicita una Allocation a Unity Relay, configura el transporte con DTLS
    // y arranca el NetworkManager como host. Devuelve el codigo de sala o null si falla.
    private async Task<string> CreateRelayRoom(int maxConnections = 9)
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            if (NetworkManager.Singleton.StartHost())
                return joinCode;
        }
        catch
        {
            LobbyCodeManager.Singleton.lobbyCode.text = "No se ha encontrado sala";
        }
        return null;
    }

    // Busca la sala con el codigo introducido en los servidores de Unity
    // y conecta al cliente a traves del Relay
    private async Task<bool> JoinRelayRoom(string roomCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(roomCode);
            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

            return NetworkManager.Singleton.StartClient();
        }
        catch
        {
            return false;
        }
    }
}