using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ChatManager : NetworkBehaviour
{
    public TMP_InputField messageToSend_input;
    public TMP_Text chatlog;

    [Header("Feedback de límite")]
    public string characterLimitMessage = "¡Límite de caracteres alcanzado!";
    public string lineLimitMessage = "¡Máximo de 3 líneas de chat alcanzado!";

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
        SendMessageServerRpc(message, playerName);

        messageToSend_input.text = "";
        messageToSend_input.ActivateInputField();
    }

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

    [ServerRpc(RequireOwnership = false)]
    private void SendMessageServerRpc(string message, string playerName)
    {
        bool isHostSender = IsHost;
        SendMessageClientRpc(message, playerName, isHostSender);
    }

    [ClientRpc]
    private void SendMessageClientRpc(string message, string playerName, bool isHostSender)
    {
        // Mostramos el mensaje en pantalla en tiempo real
        ShowMessageLocally(playerName, message, isHostSender);
        // Guardamos en el historial local de este cliente
        ChatHistoryManager.Instance?.AddMessageToHistory(playerName, message, isHostSender);
    }

    private void ShowMessageLocally(string playerName, string message, bool isHostSender)
    {
        if (chatlog == null) return;

        string time = System.DateTime.Now.ToString("HH:mm");
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
        // Guardamos el historial pero NO lo borramos,
        // así al reconectar se puede recuperar
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