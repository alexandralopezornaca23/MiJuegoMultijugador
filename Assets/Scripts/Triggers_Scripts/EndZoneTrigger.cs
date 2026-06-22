using Unity.Netcode;
using UnityEngine;

public class EndZoneTrigger : NetworkBehaviour
{
    public string newSceneToLoad;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("NewNetworkPlayer"))
        {
            LoadNewLevelServerRPC();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void LoadNewLevelServerRPC()
    {
        NetworkManager.Singleton.SceneManager.LoadScene(newSceneToLoad, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}