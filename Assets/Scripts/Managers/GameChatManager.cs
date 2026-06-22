using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameChatManager : NetworkBehaviour
{
    public static GameChatManager Singleton { get; private set; }

    [Header("UI Referencias")]
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private TMP_Text chatLog;
    [SerializeField] private TMP_Text prefixText;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Configuración")]
    [SerializeField] private float messageFadeDelay = 5f;
    [SerializeField] private float fadeDuration = 1f;

    [Header("Colores de equipo")]
    [SerializeField] private Color teamRosaColor = new Color(1f, 0.4f, 0.7f);
    [SerializeField] private Color teamAzulColor = new Color(0.3f, 0.7f, 1f);

    private bool isChatOpen = false;
    private bool isAllMode = false;
    private float fadeTimer = 0f;
    private bool isFading = false;
    private CanvasGroup chatCanvasGroup;

    private List<string> messageLines = new List<string>();

    private ThirdPersonController localController;
    private CameraController localCameraController;

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

        if (isChatOpen && scrollRect != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                    scrollRect.verticalNormalizedPosition + scroll * 0.90f);
            }
        }

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

    private void OnInputValueChanged(string text)
    {
        if (prefixText == null) return;

        if (text.Equals("/all", System.StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/all ", System.StringComparison.OrdinalIgnoreCase))
        {
            isAllMode = true;

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

        if (string.IsNullOrEmpty(text))
        {
            CloseChat();
            return;
        }

        string playerName = OnlinePlayersManager.Singleton.playerName;
        int senderTeam = GetLocalTeam();

        SendGameMessageServerRpc(text, playerName, senderTeam, isAllMode);

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

        if (localController != null) localController.inputBlocked = false;
        if (localCameraController != null) localCameraController.enabled = true;

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
            ReceiveGameMessageClientRpc(message, playerName, senderTeam, true);
        }
        else
        {
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

    private void ShowGameMessage(string playerName, string message,
    int senderTeam, bool isAllChat)
    {
        string nameColorHex = ColorUtility.ToHtmlStringRGB(
            senderTeam == 1 ? teamRosaColor : teamAzulColor);

        // el prefijo también usa el color del equipo
        string prefixColorHex = nameColorHex;

        string channelPrefix = isAllChat
            ? "<color=#FFcc33>(Todos)</color> "
            : $"<color=#{prefixColorHex}>(Equipo)</color> ";

        string formatted =
            $"{channelPrefix}<color=#{nameColorHex}>{playerName}</color>: {message}";

        messageLines.Add(formatted);
        chatLog.text = string.Join("\n", messageLines);

        StartCoroutine(ScrollToBottom());

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

    private System.Collections.IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();

        // Calculamos el tamaño del Content manualmente
        // basándonos en el tamaño preferido del ChatLog
        RectTransform contentRect = scrollRect.content;
        RectTransform chatLogRect = (RectTransform)chatLog.transform;

        // Forzamos que el ChatLog recalcule su tamaño
        chatLog.ForceMeshUpdate();

        // Ajustamos el Content al tamaño del ChatLog
        float preferredHeight = chatLog.preferredHeight;
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, preferredHeight);

        yield return new WaitForEndOfFrame();

        // Scroll al fondo
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private bool IsPauseOpen()
    {
        return PauseMenuManager.Singleton != null &&
               PauseMenuManager.Singleton.pauseCanvasGroup != null &&
               PauseMenuManager.Singleton.pauseCanvasGroup.alpha > 0f;
    }

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