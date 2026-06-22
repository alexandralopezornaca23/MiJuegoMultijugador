using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class VictoryMessageManager : MonoBehaviour
{
    public static VictoryMessageManager Instance { get; private set; }

    [Header("Referencias UI (asigna desde inspector)")]
    public GameObject victoryContainer;
    public TMP_Text victoryText;

    [Header("Configuración")]
    [Tooltip("Nombre exacto de la escena donde SÍ queremos que el mensaje permanezca visible")]
    public string finalSceneName = "FinalLevels123";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (victoryContainer != null)
            {
                victoryContainer.SetActive(false);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        var sceneMgr = NetworkManager.Singleton?.SceneManager;
        if (sceneMgr != null)
        {
            sceneMgr.OnLoad += OnSceneLoadStarted;
        }

        SceneManager.sceneLoaded += OnUnitySceneLoaded;
    }

    private void OnDisable()
    {
        var sceneMgr = NetworkManager.Singleton?.SceneManager;
        if (sceneMgr != null)
        {
            sceneMgr.OnLoad -= OnSceneLoadStarted;
        }

        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
    }

    private void OnSceneLoadStarted(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperation asyncOperation)
    {
        if (sceneName != finalSceneName)
        {
            HideVictoryMessage();
        }
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != finalSceneName)
        {
            HideVictoryMessage();
        }
    }

    public void ShowMessage(int winningTeam, int losingTeam)
    {
        if (victoryContainer == null || victoryText == null)
        {
            return;
        }

        int myTeam = GetLocalTeam();
        string mensaje;
        Color color = Color.white;

        if (myTeam == winningTeam && myTeam != 0)
        {
            mensaje = "¡TU EQUIPO GANA!";
            color = Color.green;
        }
        else if (myTeam == losingTeam && myTeam != 0)
        {
            mensaje = "TU EQUIPO PIERDE";
            color = new Color(1f, 0.4f, 0.4f);
        }
        else
        {
            mensaje = winningTeam == 1 ? "¡EQUIPO ROJO GANA!" : "¡EQUIPO AZUL GANA!";
            color = Color.yellow;
        }

        victoryText.text = mensaje;
        victoryText.color = color;
        victoryContainer.SetActive(true);
    }

    public void HideVictoryMessage()
    {
        if (victoryContainer != null)
        {
            victoryContainer.SetActive(false);
        }
    }

    private int GetLocalTeam()
    {
        var manager = ConnectedUserListManager.Singleton;
        if (manager == null || manager.usersConnectedList == null)
        {
            return 0;
        }

        var localUser = manager.usersConnectedList.Find(u => u.userId == NetworkManager.Singleton.LocalClientId);
        return localUser != null ? localUser.team : 0;
    }
}