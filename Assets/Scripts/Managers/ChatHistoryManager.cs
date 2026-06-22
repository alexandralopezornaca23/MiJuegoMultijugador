using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ChatHistoryManager : MonoBehaviour
{
    public static ChatHistoryManager Instance { get; private set; }

    private string currentRoomCode = "";
    private string currentPlayerId = "";
    private List<ChatMessage> myVisibleMessages = new List<ChatMessage>();
    private const int MAX_LINES = 150;

    private string MyHistoryKey => $"MyChatHistory_{currentPlayerId}_{currentRoomCode}";

    [Serializable]
    private class ChatMessage
    {
        public DateTime timestamp;
        public string playerName;
        public string message;
        public bool isHost;

        public string Serialize() =>
            $"{timestamp.Ticks}|{playerName}|{(isHost ? "1" : "0")}|{message}";

        public static ChatMessage Deserialize(string line)
        {
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

        public string ToDisplayLine()
        {
            string time = timestamp.ToString("HH:mm");
            string color = isHost ? "yellow" : "white";
            return $"[{time}] <color={color}>{playerName}</color>: {message}";
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Lobby")
        {
            ApplyHistoryToCurrentChat();
        }
    }

    private string GetStableClientId()
    {
        const string key = "StableInstanceId";
        if (!PlayerPrefs.HasKey(key))
        {
            string newId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(key, newId);
            PlayerPrefs.Save();
        }
        return PlayerPrefs.GetString(key);
    }

    public void InitializeForRoom(string roomCode)
    {
        roomCode = roomCode.Trim().ToUpper();

        // Guardamos el historial de la sala anterior antes de cambiar
        SaveMyHistory();

        currentRoomCode = roomCode;
        currentPlayerId = GetStableClientId();

        LoadMyHistory();

        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            ApplyHistoryToCurrentChat();
        }
    }

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

        if (myVisibleMessages.Count > MAX_LINES)
            myVisibleMessages.RemoveAt(0);

        SaveMyHistory();
    }

    private void LoadMyHistory()
    {
        myVisibleMessages.Clear();
        if (!PlayerPrefs.HasKey(MyHistoryKey)) return;

        string savedData = PlayerPrefs.GetString(MyHistoryKey);
        string[] lines = savedData.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            ChatMessage msg = ChatMessage.Deserialize(trimmed);
            if (msg != null)
                myVisibleMessages.Add(msg);
        }
    }

    private void ApplyHistoryToCurrentChat()
    {
        var chatManager = FindFirstObjectByType<ChatManager>();
        if (chatManager == null || chatManager.chatlog == null) return;

        chatManager.chatlog.text = "";

        foreach (var msg in myVisibleMessages)
        {
            string line = msg.ToDisplayLine();
            if (string.IsNullOrEmpty(chatManager.chatlog.text))
                chatManager.chatlog.text = line;
            else
                chatManager.chatlog.text += "\n" + line;
        }

        Canvas.ForceUpdateCanvases();
    }

    private void SaveMyHistory()
    {
        if (string.IsNullOrEmpty(currentRoomCode) || string.IsNullOrEmpty(currentPlayerId))
            return;

        var lines = new List<string>();
        foreach (var msg in myVisibleMessages)
            lines.Add(msg.Serialize());

        PlayerPrefs.SetString(MyHistoryKey, string.Join("\n", lines));
        PlayerPrefs.Save();
    }

    public void SaveAndLeaveRoom()
    {
        SaveMyHistory();
        myVisibleMessages.Clear();
        currentRoomCode = "";
        currentPlayerId = "";
    }


    public void ClearCurrentHistory()
    {
        if (!string.IsNullOrEmpty(MyHistoryKey))
            PlayerPrefs.DeleteKey(MyHistoryKey);

        myVisibleMessages.Clear();
        currentRoomCode = "";
        currentPlayerId = "";
    }
}