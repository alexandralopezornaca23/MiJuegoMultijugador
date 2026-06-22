using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    public string newGameScene = "GameScene01";

    public Button loadNewSceneButton;

    private void Start()
    {
        loadNewSceneButton.interactable = NetworkManager.Singleton.IsServer;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    public void LoadNewGame()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(newGameScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}

