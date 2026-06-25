using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Script del lobby que controla el boton de iniciar partida.
// Solo el host puede pulsarlo. Usa el SceneManager de Netcode para que
// todos los clientes cambien de escena al mismo tiempo.

public class LobbyManager : MonoBehaviour
{
    public string newGameScene = "GameScene01";
    public Button loadNewSceneButton;

    private void Start()
    {
        // Solo el servidor puede iniciar la partida, los clientes ven el boton desactivado
        loadNewSceneButton.interactable = NetworkManager.Singleton.IsServer;

        // Desbloqueamos el cursor porque puede venir bloqueado de escenas de juego anteriores
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    public void LoadNewGame()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // LoadScene de Netcode garantiza que todos los clientes cambien de escena a la vez
            NetworkManager.Singleton.SceneManager.LoadScene(newGameScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}