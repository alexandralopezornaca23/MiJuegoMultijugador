using TMPro;
using Unity.Netcode;
using UnityEngine;

// Script del menu principal. Gestiona el nombre del jugador, la conexion como host o cliente
// y los botones de la interfaz del menu.

public class MainMenuManager : MonoBehaviour
{
    [Header("Main Menu UI")]
    public TMP_InputField playerNameInput;

    [Header("Managers")]
    private OptionsManager optionsManager;

    private void Start()
    {
        Screen.fullScreen = false;

        // Si el jugador ya tiene un nombre guardado lo mostramos y bloqueamos el campo
        // para que no tenga que escribirlo cada vez que abre el juego
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

    // Si el campo esta vacio genera un nombre de invitado aleatorio para no entrar sin identificacion
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