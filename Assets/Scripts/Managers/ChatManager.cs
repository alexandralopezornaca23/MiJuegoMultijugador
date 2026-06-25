using TMPro;
using Unity.Netcode;
using UnityEngine;

// Script que gestiona el chat en tiempo real del lobby.
// Cuando un jugador envia un mensaje, este viaja al servidor y el servidor
// lo retransmite a todos los clientes conectados.

public class ChatManager : NetworkBehaviour
{
    public TMP_InputField messageToSend_input;
    public TMP_Text chatlog;

    [Header("Feedback de limite")]
    public string characterLimitMessage = "Limite de caracteres alcanzado!";
    public string lineLimitMessage = "Maximo de 3 lineas de chat alcanzado!";

    private void Start()
    {
        if (messageToSend_input != null)
        {
            messageToSend_input.onSubmit.AddListener(OnSubmit);
            messageToSend_input.onValueChanged.AddListener(OnMessageValueChanged);
        }
    }

    private void OnSubmit(string text)
    {
        string message = text.Trim();
        if (!string.IsNullOrEmpty(message))
        {
            SendChatMessage();
        }
    }

    public void SendChatMessage()
    {
        string message = messageToSend_input.text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        string playerName = OnlinePlayersManager.Singleton.playerName;

        // Enviamos el mensaje al servidor para que lo retransmita a todos
        SendMessageServerRpc(message, playerName);

        messageToSend_input.text = "";
        messageToSend_input.ActivateInputField();
    }

    // Se ejecuta cada vez que el jugador escribe algo en el input
    // Si llega al limite de caracteres o de lineas, muestra un aviso en pantalla
    private void OnMessageValueChanged(string currentText)
    {
        if (ConnectionCallbackManager.Singleton == null) return;

        bool atLimit = false;
        string feedbackMessage = "";

        if (messageToSend_input.characterLimit > 0)
        {
            if (currentText.Length >= messageToSend_input.characterLimit)
            {
                atLimit = true;
                feedbackMessage = characterLimitMessage;
            }
        }
        else if (messageToSend_input.lineLimit > 0)
        {
            int lineCount = currentText.Split('\n').Length;
            if (currentText.EndsWith("\n")) lineCount++;
            if (lineCount >= messageToSend_input.lineLimit && currentText.Length > 0)
            {
                atLimit = true;
                feedbackMessage = lineLimitMessage;
            }
        }

        if (atLimit)
        {
            ConnectionCallbackManager.Singleton.ShowFeedback(feedbackMessage);
        }
    }

    // El cliente llama a este RPC para que el servidor procese y retransmita el mensaje
    // RequireOwnership = false permite que cualquier cliente lo llame, no solo el dueno del objeto
    [ServerRpc(RequireOwnership = false)]
    private void SendMessageServerRpc(string message, string playerName)
    {
        bool isHostSender = IsHost;
        SendMessageClientRpc(message, playerName, isHostSender);
    }

    // El servidor llama a este RPC en todos los clientes para mostrar el mensaje en sus pantallas
    [ClientRpc]
    private void SendMessageClientRpc(string message, string playerName, bool isHostSender)
    {
        ShowMessageLocally(playerName, message, isHostSender);
        ChatHistoryManager.Instance?.AddMessageToHistory(playerName, message, isHostSender);
    }

    private void ShowMessageLocally(string playerName, string message, bool isHostSender)
    {
        if (chatlog == null) return;

        string time = System.DateTime.Now.ToString("HH:mm");
        // El nombre del host se muestra en amarillo, el de los clientes en blanco
        string color = isHostSender ? "yellow" : "white";
        string formatted = $"[{time}] <color={color}>{playerName}</color>: {message}";

        if (string.IsNullOrEmpty(chatlog.text))
            chatlog.text = formatted;
        else
            chatlog.text += "\n" + formatted;

        Canvas.ForceUpdateCanvases();
    }

    public void DisconnectGame()
    {
        // Guardamos el historial pero NO lo borramos para poder recuperarlo al reconectar
        ChatHistoryManager.Instance?.SaveAndLeaveRoom();
        NetworkManager.Singleton.Shutdown();
    }

    private new void OnDestroy()
    {
        if (messageToSend_input != null)
        {
            messageToSend_input.onSubmit.RemoveListener(OnSubmit);
            messageToSend_input.onValueChanged.RemoveListener(OnMessageValueChanged);
        }
    }
}