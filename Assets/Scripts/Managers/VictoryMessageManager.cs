using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

// Script que muestra el mensaje de victoria o derrota al final de la partida.
// Persiste entre escenas y se oculta automaticamente al cargar cualquier escena
// que no sea la escena de resultados finales.

public class VictoryMessageManager : MonoBehaviour
{
    public static VictoryMessageManager Instance { get; private set; }

    [Header("Referencias UI")]
    public GameObject victoryContainer;
    public TMP_Text victoryText;

    [Header("Configuracion")]
    [Tooltip("Nombre exacto de la escena donde el mensaje debe permanecer visible")]
    public string finalSceneName = "FinalLevels123";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (victoryContainer != null)
                victoryContainer.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        // Nos suscribimos tanto al SceneManager de Netcode como al de Unity
        // para cubrir cualquier tipo de cambio de escena que pueda ocurrir
        var sceneMgr = NetworkManager.Singleton?.SceneManager;
        if (sceneMgr != null)
            sceneMgr.OnLoad += OnSceneLoadStarted;

        SceneManager.sceneLoaded += OnUnitySceneLoaded;
    }

    private void OnDisable()
    {
        var sceneMgr = NetworkManager.Singleton?.SceneManager;
        if (sceneMgr != null)
            sceneMgr.OnLoad -= OnSceneLoadStarted;

        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
    }

    private void OnSceneLoadStarted(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperation asyncOperation)
    {
        if (sceneName != finalSceneName)
            HideVictoryMessage();
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != finalSceneName)
            HideVictoryMessage();
    }

    // Muestra el mensaje correspondiente segun si el jugador local ha ganado, perdido o es espectador
    public void ShowMessage(int winningTeam, int losingTeam)
    {
        if (victoryContainer == null || victoryText == null) return;

        int myTeam = GetLocalTeam();
        string mensaje;
        Color color = Color.white;

        if (myTeam == winningTeam && myTeam != 0)
        {
            mensaje = "TU EQUIPO GANA!";
            color = Color.green;
        }
        else if (myTeam == losingTeam && myTeam != 0)
        {
            mensaje = "TU EQUIPO PIERDE";
            color = new Color(1f, 0.4f, 0.4f);
        }
        else
        {
            // Si el jugador no tiene equipo asignado mostramos un mensaje generico
            mensaje = winningTeam == 1 ? "EQUIPO ROSA GANA!" : "EQUIPO AZUL GANA!";
            color = Color.yellow;
        }

        victoryText.text = mensaje;
        victoryText.color = color;
        victoryContainer.SetActive(true);
    }

    public void HideVictoryMessage()
    {
        if (victoryContainer != null)
            victoryContainer.SetActive(false);
    }

    private int GetLocalTeam()
    {
        var manager = ConnectedUserListManager.Singleton;
        if (manager == null || manager.usersConnectedList == null) return 0;

        var localUser = manager.usersConnectedList.Find(u => u.userId == NetworkManager.Singleton.LocalClientId);
        return localUser != null ? localUser.team : 0;
    }
}