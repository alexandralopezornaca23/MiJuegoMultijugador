using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Script del panel de opciones. Permite cambiar el nombre del jugador y el modo de pantalla.
// Si el jugador esta conectado, el cambio de nombre se sincroniza con todos los clientes en tiempo real.

public class OptionsManager : MonoBehaviour
{
    [Header("Panel de Opciones")]
    public GameObject optionsPanel;
    public TMP_InputField nameChangeInput;
    public Button closeButton;

    [Header("Input del Menu Principal / Relay")]
    public TMP_InputField mainMenuPlayerNameInput;

    [Header("Pantalla Completa")]
    public Toggle fullscreenToggle;
    private const string FullscreenPrefKey = "IsFullscreen";

    private void Awake()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(HideOptions);
        }

        // Recuperamos la preferencia de pantalla completa guardada y la aplicamos
        bool savedFullscreen = PlayerPrefs.GetInt(FullscreenPrefKey, 0) == 1;
        Screen.fullScreen = savedFullscreen;

        if (fullscreenToggle != null)
        {
            // SetIsOnWithoutNotify sincroniza el toggle sin disparar el evento onValueChanged
            fullscreenToggle.SetIsOnWithoutNotify(savedFullscreen);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggleChanged);
        }
    }

    private void OnDestroy()
    {
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenToggleChanged);
    }

    private void OnFullscreenToggleChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(FullscreenPrefKey, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ShowOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(true);

        string currentName = OnlinePlayersManager.Singleton?.playerName ?? "";

        if (nameChangeInput != null)
        {
            nameChangeInput.text = currentName;
            nameChangeInput.interactable = true;
            nameChangeInput.Select();
            nameChangeInput.ActivateInputField();
        }

        if (mainMenuPlayerNameInput != null)
        {
            mainMenuPlayerNameInput.text = currentName;
            if (string.IsNullOrEmpty(currentName))
                mainMenuPlayerNameInput.interactable = true;
        }

        // Sincronizamos el toggle con el estado actual de pantalla al abrir el panel
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
    }

    public void HideOptions()
    {
        ApplyNameChange();
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    private void ApplyNameChange()
    {
        if (nameChangeInput == null) return;

        string newName = nameChangeInput.text.Trim();

        // Si el campo queda vacio asignamos un nombre de invitado aleatorio
        if (string.IsNullOrEmpty(newName))
            newName = "Guest" + UnityEngine.Random.Range(1000, 10000);

        // Guardamos el nombre anterior para saber si ha habido cambio real
        string previousName = OnlinePlayersManager.Singleton?.playerName ?? "";

        OnlinePlayersManager.Singleton?.SetPlayerName(newName);

        if (mainMenuPlayerNameInput != null)
        {
            mainMenuPlayerNameInput.text = newName;
            mainMenuPlayerNameInput.interactable = false;
        }

        if (nameChangeInput != null)
            nameChangeInput.text = newName;

        // Si estamos conectados, notificamos al servidor para que el cambio se vea
        // en la lista de jugadores de todos los clientes en tiempo real
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            ConnectedUserListManager.Singleton?.UpdateNameServerRpc(
                NetworkManager.Singleton.LocalClientId, newName);
        }

        WelcomeMessageManager welcomeManager = FindFirstObjectByType<WelcomeMessageManager>();
        welcomeManager?.UpdateWelcomeMessage();

        // Mostramos un mensaje de confirmacion segun si el nombre cambio o no
        if (ConnectionCallbackManager.Singleton != null)
        {
            if (previousName != newName)
                ConnectionCallbackManager.Singleton.ShowFeedback($"Nombre cambiado a: {newName}");
            else
                ConnectionCallbackManager.Singleton.ShowFeedback($"Nombre sin cambios: {newName}");
        }
    }

    public void ChangeNameButton()
    {
        ApplyNameChange();
    }
}