using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Chat en partida inspirado en Valorant. Permite enviar mensajes al equipo o a todos.
// El panel aparece al recibir mensajes y desaparece solo despues de unos segundos.
// Al abrir el chat se bloquea el movimiento y la camara del jugador local.

public class GameChatManager : NetworkBehaviour
{
    public static GameChatManager Singleton { get; private set; }

    [Header("UI Referencias")]
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private TMP_Text chatLog;
    [SerializeField] private TMP_Text prefixText;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Configuracion")]
    [SerializeField] private float messageFadeDelay = 5f;
    [SerializeField] private float fadeDuration = 1f;

    [Header("Colores de equipo")]
    [SerializeField] private Color teamRosaColor = new Color(1f, 0.4f, 0.7f);
    [SerializeField] private Color teamAzulColor = new Color(0.3f, 0.7f, 1f);

    private bool isChatOpen = false;
    private bool isAllMode = false;  // true cuando el jugador ha escrito /all para enviar a todos
    private float fadeTimer = 0f;
    private bool isFading = false;
    private CanvasGroup chatCanvasGroup;

    private List<string> messageLines = new List<string>();

    private ThirdPersonController localController;
    private CameraController localCameraController;

    // Propiedad publica para que otros scripts (como PlayerPush) puedan saber si el chat esta abierto
    public bool IsChatOpen => isChatOpen;

    private void Awake()
    {
        if (Singleton == null)
            Singleton = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        chatCanvasGroup = chatPanel.GetComponent<CanvasGroup>();
        if (chatCanvasGroup == null)
            chatCanvasGroup = chatPanel.AddComponent<CanvasGroup>();

        chatPanel.SetActive(false);
        if (chatInput != null) chatInput.gameObject.SetActive(false);
        if (prefixText != null) prefixText.gameObject.SetActive(false);
        if (chatLog != null) chatLog.text = "";

        if (chatInput != null)
            chatInput.onValueChanged.AddListener(OnInputValueChanged);
    }

    private void Update()
    {
        // Si el menu de pausa esta abierto, el chat no procesa ninguna entrada
        if (PauseMenuManager.Singleton != null && IsPauseOpen()) return;

        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            if (!isChatOpen)
                OpenChat();
            else
                SubmitChatMessage();
        }

        if (isChatOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // Si estamos en modo todos, Escape vuelve al modo equipo en lugar de cerrar el chat
            if (isAllMode)
            {
                isAllMode = false;
                if (prefixText != null)
                {
                    prefixText.text = "(Equipo)";
                    prefixText.color = new Color(0.7f, 0.7f, 0.7f);
                }
            }
            else
            {
                CloseChat();
            }
        }

        // Scroll manual con la rueda del raton mientras el chat esta abierto
        if (isChatOpen && scrollRect != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                    scrollRect.verticalNormalizedPosition + scroll * 0.90f);
            }
        }

        // Logica del fade automatico del panel cuando el chat esta cerrado pero hay mensajes
        if (!isChatOpen && chatPanel.activeSelf && messageLines.Count > 0)
        {
            if (!isFading)
            {
                fadeTimer -= Time.deltaTime;
                if (fadeTimer <= 0f)
                    isFading = true;
            }
            else
            {
                float newAlpha = Mathf.MoveTowards(
                    chatCanvasGroup.alpha, 0f, Time.deltaTime / fadeDuration);

                chatCanvasGroup.alpha = newAlpha;

                if (newAlpha <= 0f)
                {
                    isFading = false;
                    chatPanel.SetActive(false);
                }
            }
        }
    }

    // Detecta en tiempo real si el jugador escribe /all para cambiar al modo de mensaje global
    // Cuando lo detecta elimina el comando del input y cambia el prefijo a amarillo
    private void OnInputValueChanged(string text)
    {
        if (prefixText == null) return;

        if (text.Equals("/all", System.StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/all ", System.StringComparison.OrdinalIgnoreCase))
        {
            isAllMode = true;

            // Quitamos el listener temporalmente para poder cambiar el texto sin que se dispare otra vez
            chatInput.onValueChanged.RemoveListener(OnInputValueChanged);
            string remaining = text.Length > 4 ? text.Substring(4).TrimStart() : "";
            chatInput.text = remaining;
            chatInput.caretPosition = remaining.Length;
            chatInput.onValueChanged.AddListener(OnInputValueChanged);

            prefixText.text = "(Todos)";
            prefixText.color = new Color(1f, 0.8f, 0.2f);
        }
        else if (!isAllMode)
        {
            prefixText.text = "(Equipo)";
            prefixText.color = new Color(0.7f, 0.7f, 0.7f);
        }
    }

    private void SubmitChatMessage()
    {
        string text = chatInput.text.Trim();

        // Si el campo esta vacio al pulsar Enter, simplemente cerramos el chat
        if (string.IsNullOrEmpty(text))
        {
            CloseChat();
            return;
        }

        string playerName = OnlinePlayersManager.Singleton.playerName;
        int senderTeam = GetLocalTeam();

        SendGameMessageServerRpc(text, playerName, senderTeam, isAllMode);

        // Quitamos el listener, limpiamos el input y lo volvemos a conectar
        chatInput.onValueChanged.RemoveListener(OnInputValueChanged);
        chatInput.text = "";
        chatInput.onValueChanged.AddListener(OnInputValueChanged);

        isAllMode = false;
        if (prefixText != null)
        {
            prefixText.text = "(Equipo)";
            prefixText.color = new Color(0.7f, 0.7f, 0.7f);
        }

        chatInput.ActivateInputField();
        chatInput.Select();
    }

    public void OpenChat()
    {
        FindLocalComponents();

        // Bloqueamos el movimiento y la camara del jugador local mientras el chat esta abierto
        if (localController != null) localController.inputBlocked = true;
        if (localCameraController != null) localCameraController.enabled = false;

        isChatOpen = true;
        isAllMode = false;
        isFading = false;
        fadeTimer = 0f;

        chatPanel.SetActive(true);
        chatCanvasGroup.alpha = 1f;
        chatCanvasGroup.interactable = true;
        chatCanvasGroup.blocksRaycasts = true;

        chatInput.gameObject.SetActive(true);

        chatInput.onValueChanged.RemoveListener(OnInputValueChanged);
        chatInput.text = "";
        chatInput.onValueChanged.AddListener(OnInputValueChanged);

        if (prefixText != null)
        {
            prefixText.gameObject.SetActive(true);
            prefixText.text = "(Equipo)";
            prefixText.color = new Color(0.7f, 0.7f, 0.7f);
        }

        StartCoroutine(FocusInputNextFrame());
    }

    // Esperamos un frame antes de activar el input para evitar que Unity ignore el foco
    private System.Collections.IEnumerator FocusInputNextFrame()
    {
        yield return null;
        if (chatInput != null)
        {
            chatInput.ActivateInputField();
            chatInput.Select();
        }
    }

    public void CloseChat()
    {
        isChatOpen = false;
        isAllMode = false;

        chatInput.onValueChanged.RemoveListener(OnInputValueChanged);
        chatInput.text = "";
        chatInput.onValueChanged.AddListener(OnInputValueChanged);

        chatInput.gameObject.SetActive(false);
        chatInput.DeactivateInputField();

        if (prefixText != null)
            prefixText.gameObject.SetActive(false);

        // Devolvemos el control del movimiento y la camara al jugador
        if (localController != null) localController.inputBlocked = false;
        if (localCameraController != null) localCameraController.enabled = true;

        // Si hay mensajes, dejamos el panel visible con fade antes de ocultarlo
        if (messageLines.Count > 0)
        {
            chatPanel.SetActive(true);
            chatCanvasGroup.alpha = 0.8f;
            chatCanvasGroup.interactable = false;
            chatCanvasGroup.blocksRaycasts = false;
            fadeTimer = messageFadeDelay;
            isFading = false;
        }
        else
        {
            chatPanel.SetActive(false);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendGameMessageServerRpc(string message, string playerName,
        int senderTeam, bool isAllChat, ServerRpcParams rpcParams = default)
    {
        if (isAllChat)
        {
            // Mensaje global: llega a todos los clientes sin filtro
            ReceiveGameMessageClientRpc(message, playerName, senderTeam, true);
        }
        else
        {
            // Mensaje de equipo: filtramos los IDs de los clientes del mismo equipo
            // y enviamos el ClientRpc solo a ellos usando TargetClientIds
            var teamClientIds = ConnectedUserListManager.Singleton.usersConnectedList
                .Where(u => u.team == senderTeam)
                .Select(u => u.userId)
                .ToArray();

            if (teamClientIds.Length == 0) return;

            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = teamClientIds }
            };

            ReceiveGameMessageClientRpc(message, playerName, senderTeam, false, clientRpcParams);
        }
    }

    [ClientRpc]
    private void ReceiveGameMessageClientRpc(string message, string playerName,
        int senderTeam, bool isAllChat, ClientRpcParams clientRpcParams = default)
    {
        ShowGameMessage(playerName, message, senderTeam, isAllChat);
    }

    private void ShowGameMessage(string playerName, string message, int senderTeam, bool isAllChat)
    {
        // El nombre del emisor se colorea segun su equipo
        string nameColorHex = ColorUtility.ToHtmlStringRGB(
            senderTeam == 1 ? teamRosaColor : teamAzulColor);

        string channelPrefix = isAllChat
            ? "<color=#FFcc33>(Todos)</color> "
            : $"<color=#{nameColorHex}>(Equipo)</color> ";

        string formatted = $"{channelPrefix}<color=#{nameColorHex}>{playerName}</color>: {message}";

        messageLines.Add(formatted);
        chatLog.text = string.Join("\n", messageLines);

        StartCoroutine(ScrollToBottom());

        // Si el chat esta cerrado cuando llega un mensaje, mostramos el panel brevemente
        if (!isChatOpen)
        {
            chatPanel.SetActive(true);
            chatCanvasGroup.alpha = 0.8f;
            chatCanvasGroup.interactable = false;
            chatCanvasGroup.blocksRaycasts = false;
            fadeTimer = messageFadeDelay;
            isFading = false;
        }
    }

    // Recalcula el tamanio del contenedor del scroll y baja al ultimo mensaje
    // Necesitamos esperar dos frames para que Unity actualice los tamanios del layout
    private System.Collections.IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();

        RectTransform contentRect = scrollRect.content;
        chatLog.ForceMeshUpdate();

        float preferredHeight = chatLog.preferredHeight;
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, preferredHeight);

        yield return new WaitForEndOfFrame();

        scrollRect.verticalNormalizedPosition = 0f;
    }

    private bool IsPauseOpen()
    {
        return PauseMenuManager.Singleton != null &&
               PauseMenuManager.Singleton.pauseCanvasGroup != null &&
               PauseMenuManager.Singleton.pauseCanvasGroup.alpha > 0f;
    }

    // Busca el ThirdPersonController y CameraController que pertenecen al jugador local
    // No cacheamos estas referencias en Start porque el jugador puede no existir aun en ese momento
    private void FindLocalComponents()
    {
        localController = null;
        localCameraController = null;

        ThirdPersonController[] allControllers =
            Object.FindObjectsByType<ThirdPersonController>(FindObjectsSortMode.None);

        foreach (var controller in allControllers)
        {
            NetworkObject netObj = controller.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                localController = controller;
                break;
            }
        }

        localCameraController = Object.FindFirstObjectByType<CameraController>();
    }

    private int GetLocalTeam()
    {
        var manager = ConnectedUserListManager.Singleton;
        if (manager == null) return 0;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        var user = manager.usersConnectedList.Find(u => u.userId == localId);
        return user != null ? user.team : 0;
    }

    private new void OnDestroy()
    {
        if (chatInput != null)
            chatInput.onValueChanged.RemoveListener(OnInputValueChanged);
    }
}