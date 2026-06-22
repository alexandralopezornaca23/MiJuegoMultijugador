using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class OptionsManager : MonoBehaviour
{
    [Header("Panel de Opciones")]
    public GameObject optionsPanel;
    public TMP_InputField nameChangeInput;
    public Button closeButton;

    [Header("Input del Menú Principal / Relay")]
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

        // Cargamos la preferencia guardada (por defecto false = windowed)
        bool savedFullscreen = PlayerPrefs.GetInt(FullscreenPrefKey, 0) == 1;
        Screen.fullScreen = savedFullscreen;

        if (fullscreenToggle != null)
        {
            // Sincronizamos el toggle con el estado actual sin disparar el listener
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

        // Sincronizamos el toggle con el estado actual de pantalla
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
        if (string.IsNullOrEmpty(newName))
            newName = "Guest" + UnityEngine.Random.Range(1000, 10000);

        OnlinePlayersManager.Singleton?.SetPlayerName(newName);

        if (mainMenuPlayerNameInput != null)
        {
            mainMenuPlayerNameInput.text = newName;
            mainMenuPlayerNameInput.interactable = false;
        }

        if (nameChangeInput != null)
            nameChangeInput.text = newName;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            ConnectedUserListManager.Singleton?.UpdateNameServerRpc(
                NetworkManager.Singleton.LocalClientId, newName);
        }

        WelcomeMessageManager welcomeManager = FindFirstObjectByType<WelcomeMessageManager>();
        welcomeManager?.UpdateWelcomeMessage();
    }

    public void ChangeNameButton()
    {
        ApplyNameChange();
    }
}