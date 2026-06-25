using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Boton de la pantalla de resultados finales para volver al lobby.
// Es visible para todos los jugadores pero solo el host puede pulsarlo.
// Los clientes ven el boton desactivado con un texto explicativo.

public class WinButton : NetworkBehaviour
{
    public string newSceneToLoad;
    public Button button;

    [Header("Opcional")]
    public TMP_Text hostOnlyLabel; // Texto que se muestra a los clientes explicando que deben esperar al host

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (button == null) return;

        // El boton es visible para todos pero solo interactuable para el host
        button.gameObject.SetActive(true);

        if (IsHost)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);

            if (hostOnlyLabel != null)
                hostOnlyLabel.gameObject.SetActive(false);
        }
        else
        {
            button.interactable = false;

            if (hostOnlyLabel != null)
            {
                hostOnlyLabel.gameObject.SetActive(true);
                hostOnlyLabel.text = "Solo el Host puede volver al lobby, solo espera si quieres volver a jugar...";
            }
        }
    }

    private void OnButtonClicked()
    {
        if (VictoryMessageManager.Instance != null)
            VictoryMessageManager.Instance.HideVictoryMessage();

        string sceneName = newSceneToLoad.Trim();
        if (string.IsNullOrEmpty(sceneName)) return;

        // Un poco de retardo para que la animacion de ocultar el mensaje termine antes de cambiar de escena
        Invoke(nameof(PerformSceneChange), 0.4f);
    }

    private void PerformSceneChange()
    {
        string sceneName = newSceneToLoad.Trim();
        RequestSceneChangeServerRpc(sceneName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSceneChangeServerRpc(string sceneName)
    {
        if (!IsServer) return;
        // LoadScene de Netcode mueve a todos los clientes al lobby simultaneamente
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}