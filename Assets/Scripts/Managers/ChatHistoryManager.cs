using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Este script se encarga de guardar y recuperar el historial de chat del lobby.
// Funciona como un gestor persistente: sobrevive entre escenas y recuerda los mensajes
// aunque el jugador salga y vuelva a entrar a la sala.
// Usa PlayerPrefs para guardar los mensajes en el disco del dispositivo.

public class ChatHistoryManager : MonoBehaviour
{
    // Instancia unica accesible desde cualquier script sin necesidad de buscarla
    public static ChatHistoryManager Instance { get; private set; }

    // Codigo de la sala actual y ID unico del jugador, usados para construir la clave de guardado
    private string currentRoomCode = "";
    private string currentPlayerId = "";

    // Lista de mensajes visibles en el chat de este jugador para esta sala
    private List<ChatMessage> myVisibleMessages = new List<ChatMessage>();

    // Maximo de mensajes que se guardan antes de empezar a borrar los mas antiguos
    private const int MAX_LINES = 150;

    // Clave unica de guardado en PlayerPrefs: combina el ID del jugador y el codigo de sala
    // para que cada jugador tenga su propio historial por sala y no se mezclen
    private string MyHistoryKey => $"MyChatHistory_{currentPlayerId}_{currentRoomCode}";


    // Clase interna que representa un mensaje de chat con todos sus datos
    [Serializable]
    private class ChatMessage
    {
        public DateTime timestamp;   // Hora en que se envio el mensaje
        public string playerName;    // Nombre del jugador que lo envio
        public string message;       // Texto del mensaje
        public bool isHost;          // Si el emisor es el host (para colorearlo diferente)

        // Convierte el mensaje a una cadena de texto para guardarlo en PlayerPrefs
        public string Serialize() =>
            $"{timestamp.Ticks}|{playerName}|{(isHost ? "1" : "0")}|{message}";

        // Reconstruye un ChatMessage a partir de una linea de texto guardada en disco
        public static ChatMessage Deserialize(string line)
        {
            // Dividimos la linea en 4 partes usando el separador |
            string[] parts = line.Split(new[] { '|' }, 4);
            if (parts.Length < 4) return null;
            if (!long.TryParse(parts[0], out long ticks)) return null;

            return new ChatMessage
            {
                timestamp = new DateTime(ticks),
                playerName = parts[1],
                isHost = parts[2] == "1",
                message = parts[3]
            };
        }

        // Devuelve el mensaje formateado con hora y color segun si el emisor es host o no
        public string ToDisplayLine()
        {
            string time = timestamp.ToString("HH:mm");
            string color = isHost ? "yellow" : "white";
            return $"[{time}] <color={color}>{playerName}</color>: {message}";
        }
    }


    private void Awake()
    {
        // Patron Singleton: solo puede existir una instancia de este script
        // Si ya existe una, destruimos este duplicado
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // No se destruye al cambiar de escena
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Nos suscribimos al evento de carga de escena para saber cuando volvemos al Lobby
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // Limpieza: quitamos el listener cuando el objeto se destruye
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Se ejecuta cada vez que se carga una escena
    // Si es el Lobby, volcamos el historial guardado al chat visible
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Lobby")
        {
            ApplyHistoryToCurrentChat();
        }
    }

    // Genera o recupera un ID unico y estable para este jugador guardado en PlayerPrefs
    // Esto evita que el historial se pierda si el jugador reinicia el juego
    private string GetStableClientId()
    {
        const string key = "StableInstanceId";
        if (!PlayerPrefs.HasKey(key))
        {
            // Si no tiene ID guardado, generamos uno nuevo y lo guardamos
            string newId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(key, newId);
            PlayerPrefs.Save();
        }
        return PlayerPrefs.GetString(key);
    }

    // Se llama cuando el jugador entra a una sala nueva (como host o cliente)
    // Guarda el historial de la sala anterior, carga el de la nueva y lo muestra si ya estamos en el Lobby
    public void InitializeForRoom(string roomCode)
    {
        roomCode = roomCode.Trim().ToUpper();

        // Guardamos el historial de la sala anterior antes de cambiar de sala
        SaveMyHistory();

        currentRoomCode = roomCode;
        currentPlayerId = GetStableClientId();

        // Cargamos el historial de la nueva sala desde disco
        LoadMyHistory();

        // Si ya estamos en el Lobby, lo mostramos directamente
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            ApplyHistoryToCurrentChat();
        }
    }

    // Anade un nuevo mensaje al historial en memoria y lo guarda en disco
    // Se llama desde ChatManager cada vez que llega un mensaje por red
    public void AddMessageToHistory(string playerName, string message, bool isHost)
    {
        var msg = new ChatMessage
        {
            timestamp = DateTime.Now,
            playerName = playerName,
            message = message,
            isHost = isHost
        };

        myVisibleMessages.Add(msg);

        // Si superamos el limite de 150 mensajes, borramos el mas antiguo
        if (myVisibleMessages.Count > MAX_LINES)
            myVisibleMessages.RemoveAt(0);

        // Guardamos en disco inmediatamente para no perder datos
        SaveMyHistory();
    }

    // Carga el historial guardado en PlayerPrefs y lo mete en la lista en memoria
    private void LoadMyHistory()
    {
        myVisibleMessages.Clear();

        // Si no hay historial guardado para esta clave, no hacemos nada
        if (!PlayerPrefs.HasKey(MyHistoryKey)) return;

        string savedData = PlayerPrefs.GetString(MyHistoryKey);
        string[] lines = savedData.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Reconstruimos cada mensaje a partir de las lineas guardadas
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            ChatMessage msg = ChatMessage.Deserialize(trimmed);
            if (msg != null)
                myVisibleMessages.Add(msg);
        }
    }

    // Vuelca el historial en memoria al componente de texto del ChatManager
    // Se llama cuando volvemos al Lobby para que el jugador vea sus mensajes anteriores
    private void ApplyHistoryToCurrentChat()
    {
        var chatManager = FindFirstObjectByType<ChatManager>();
        if (chatManager == null || chatManager.chatlog == null) return;

        // Limpiamos el chat antes de volcar el historial
        chatManager.chatlog.text = "";

        foreach (var msg in myVisibleMessages)
        {
            string line = msg.ToDisplayLine();
            if (string.IsNullOrEmpty(chatManager.chatlog.text))
                chatManager.chatlog.text = line;
            else
                chatManager.chatlog.text += "\n" + line;
        }

        // Forzamos la actualizacion del canvas para que el texto se renderice correctamente
        Canvas.ForceUpdateCanvases();
    }

    // Serializa todos los mensajes en memoria y los guarda en PlayerPrefs
    private void SaveMyHistory()
    {
        // Si no tenemos sala ni jugador identificados, no guardamos nada
        if (string.IsNullOrEmpty(currentRoomCode) || string.IsNullOrEmpty(currentPlayerId))
            return;

        var lines = new List<string>();
        foreach (var msg in myVisibleMessages)
            lines.Add(msg.Serialize());

        PlayerPrefs.SetString(MyHistoryKey, string.Join("\n", lines));
        PlayerPrefs.Save();
    }

    // Guarda el historial y limpia el estado interno cuando el jugador abandona la sala
    public void SaveAndLeaveRoom()
    {
        SaveMyHistory();
        myVisibleMessages.Clear();
        currentRoomCode = "";
        currentPlayerId = "";
    }

    // Borra completamente el historial del jugador en esta sala, tanto en disco como en memoria
    public void ClearCurrentHistory()
    {
        if (!string.IsNullOrEmpty(MyHistoryKey))
            PlayerPrefs.DeleteKey(MyHistoryKey);

        myVisibleMessages.Clear();
        currentRoomCode = "";
        currentPlayerId = "";
    }
}