using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Gestiona el menu de pausa. Al pausar desactiva el movimiento, la camara y el animator
// del jugador local sin afectar al resto de jugadores ni al servidor.
// En multijugador no se puede usar Time.timeScale = 0 porque detendria la sincronizacion de red.

public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Singleton;

    [Header("UI References")]
    public CanvasGroup pauseCanvasGroup;
    public Button resumeButton;
    public Button disconnectButton;
    public Button backToLobbyButton;

    [Header("Bloqueo del boton Volver al Lobby")]
    [Tooltip("Texto opcional que se muestra a los clientes explicando que solo el host puede volver al lobby")]
    public TMP_Text backToLobbyHostOnlyLabel;

    private bool isPaused = false;

    private ThirdPersonController localPlayerController;
    private CameraController localCameraController;
    private Animator localAnimator;

    private void Awake()
    {
        if (Singleton == null)
        {
            Singleton = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        resumeButton?.onClick.AddListener(ResumeGame);
        disconnectButton?.onClick.AddListener(DisconnectGame);
        backToLobbyButton?.onClick.AddListener(BackToLobby);
        HidePauseMenu();
    }

    private void Update()
    {
        // Si el chat esta abierto, Escape no abre el menu de pausa
        if (GameChatManager.Singleton != null && GameChatManager.Singleton.IsChatOpen)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            ShowPauseMenu();
            DisableLocalControls();
        }
        else
        {
            HidePauseMenu();
            EnableLocalControls();
        }
    }

    private void ShowPauseMenu()
    {
        pauseCanvasGroup.alpha = 1f;
        pauseCanvasGroup.interactable = true;
        pauseCanvasGroup.blocksRaycasts = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Cada vez que se abre el menu, ajustamos el boton de volver al lobby segun seamos host o cliente
        UpdateBackToLobbyButton();
    }

    private void HidePauseMenu()
    {
        pauseCanvasGroup.alpha = 0f;
        pauseCanvasGroup.interactable = false;
        pauseCanvasGroup.blocksRaycasts = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // El boton de volver al lobby es visible para todos pero solo interactuable para el host.
    // A los clientes les mostramos un texto explicando que solo el host puede usarlo.
    private void UpdateBackToLobbyButton()
    {
        if (backToLobbyButton == null) return;

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        backToLobbyButton.interactable = isHost;

        if (backToLobbyHostOnlyLabel != null)
        {
            if (isHost)
            {
                backToLobbyHostOnlyLabel.gameObject.SetActive(false);
            }
            else
            {
                backToLobbyHostOnlyLabel.gameObject.SetActive(true);
                backToLobbyHostOnlyLabel.text = "Solo el Host puede volver al lobby";
            }
        }
    }

    // Busca los componentes del jugador local para desactivarlos al pausar.
    private void FindLocalPlayerComponents()
    {
        localPlayerController = null;
        localCameraController = null;
        localAnimator = null;

        NetworkObject[] allNetworkObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        foreach (NetworkObject netObj in allNetworkObjects)
        {
            if (netObj.IsOwner)
            {
                localPlayerController = netObj.GetComponentInChildren<ThirdPersonController>();
                localAnimator = netObj.GetComponentInChildren<Animator>();
                break;
            }
        }

        if (localPlayerController == null)
        {
            ThirdPersonController[] allControllers = Object.FindObjectsByType<ThirdPersonController>(FindObjectsSortMode.None);
            foreach (ThirdPersonController controller in allControllers)
            {
                NetworkObject netObj = controller.GetComponentInParent<NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    localPlayerController = controller;
                    break;
                }
            }
        }

        if (localAnimator == null)
        {
            Animator[] allControllers = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (Animator controller in allControllers)
            {
                NetworkObject netObj = controller.GetComponentInParent<NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    localAnimator = controller;
                    break;
                }
            }
        }

        localCameraController = Object.FindFirstObjectByType<CameraController>();
    }

    private void DisableLocalControls()
    {
        FindLocalPlayerComponents();

        if (localPlayerController != null) localPlayerController.enabled = false;
        if (localCameraController != null) localCameraController.enabled = false;
        if (localAnimator != null) localAnimator.enabled = false;
    }

    private void EnableLocalControls()
    {
        FindLocalPlayerComponents();

        if (localPlayerController != null) localPlayerController.enabled = true;
        if (localCameraController != null) localCameraController.enabled = true;
        if (localAnimator != null) localAnimator.enabled = true;
    }

    public void ResumeGame()
    {
        if (isPaused)
            TogglePause();
    }

    public void DisconnectGame()
    {
        LeaveSession();
    }

    public void BackToLobby()
    {
        if (NetworkManager.Singleton == null)
        {
            SceneManager.LoadScene("MainMenu");
            return;
        }

        // Solo el host puede volver al lobby. Si por lo que sea un cliente llega aqui, no hacemos nada.
        if (NetworkManager.Singleton.IsServer)
        {
            if (isPaused)
            {
                EnableLocalControls();
                HidePauseMenu();
                isPaused = false;
            }
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
        }
    }

    private void LeaveSession()
    {
        if (isPaused)
            EnableLocalControls();

        HidePauseMenu();
        isPaused = false;

        // Guardamos el historial sin borrarlo para poder recuperarlo al volver a entrar a la sala
        ChatHistoryManager.Instance?.SaveAndLeaveRoom();

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();
        else
            SceneManager.LoadScene("MainMenu");
    }
}