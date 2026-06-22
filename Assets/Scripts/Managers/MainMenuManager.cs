using TMPro;
using Unity.Netcode;
using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [Header("Main Menu UI")]
    public TMP_InputField playerNameInput;

    [Header("Managers")]
    private OptionsManager optionsManager;

    private void Start()
    {
        // Por defecto: pantalla reducida (windowed)
        // El jugador decide si pone pantalla completa desde opciones
        Screen.fullScreen = false;

        string savedName = PlayerPrefs.GetString("PlayerName", "");
        if (!string.IsNullOrEmpty(savedName))
        {
            playerNameInput.text = savedName;
            playerNameInput.interactable = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (optionsManager == null)
            optionsManager = FindFirstObjectByType<OptionsManager>();
    }

    private string GetValidPlayerName()
    {
        string name = playerNameInput.text.Trim();
        if (string.IsNullOrEmpty(name))
            name = "Guest" + UnityEngine.Random.Range(0, 1000);
        return name;
    }

    private void SavePlayerName(string name)
    {
        PlayerPrefs.SetString("PlayerName", name);
        PlayerPrefs.Save();
    }

    public void ConnectAsClient()
    {
        string name = GetValidPlayerName();
        OnlinePlayersManager.Singleton.playerName = name;
        SavePlayerName(name);
        NetworkManager.Singleton.StartClient();
    }

    public void ConnectAsHost()
    {
        string name = GetValidPlayerName();
        OnlinePlayersManager.Singleton.playerName = name;
        SavePlayerName(name);
        NetworkManager.Singleton.StartHost();
    }

    public void OnOptionsButtonClick()
    {
        optionsManager?.ShowOptions();
    }

    public void ShutDownConnection()
    {
        NetworkManager.Singleton.Shutdown();
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}