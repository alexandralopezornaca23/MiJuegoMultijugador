using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WinButton : NetworkBehaviour
{
    public string newSceneToLoad;
    public Button button;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (button == null) return;

        bool isHost = IsHost;

        button.gameObject.SetActive(isHost);
        button.interactable = isHost;

        if (isHost)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        if (VictoryMessageManager.Instance != null)
        {
            VictoryMessageManager.Instance.HideVictoryMessage();
        }

        string sceneName = newSceneToLoad.Trim();
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

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
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}