using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConnectionCallbackManager : MonoBehaviour
{
    private static ConnectionCallbackManager singleton;
    public static ConnectionCallbackManager Singleton => singleton;

    [Header("UI Elements")]
    public TMP_Text informationText;

    [Header("UI Elements - Mensaje fijo Esperando Jugadores (Lobby)")]
    public TMP_Text waitingText;
    public CanvasGroup waitingCanvasGroup;

    [Header("Parpadeo Esperando Jugadores - Ajustes finos")]
    [Range(0.5f, 3f)] public float fadeInDuration = 1.5f;
    [Range(0.5f, 3f)] public float fadeOutDuration = 1.5f;
    [Range(0.5f, 2f)] public float holdVisibleTime = 1f;
    [Range(0.1f, 1f)] public float holdInvisibleTime = 0.3f;

    [Header("Feedback Settings")]
    public float displayTime = 2f;
    public float fadeTime = 0.75f;

    private CanvasGroup infoCanvasGroup;
    private Coroutine currentFeedbackCoroutine;
    private Coroutine waitingBlinkCoroutine;



    private string currentSceneName;

    private void Awake()
    {
        if (singleton == null)
        {
            singleton = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (informationText != null)
        {
            Transform parent = informationText.transform.parent;
            infoCanvasGroup = parent.GetComponent<CanvasGroup>();
            if (infoCanvasGroup == null)
                infoCanvasGroup = parent.gameObject.AddComponent<CanvasGroup>();

            infoCanvasGroup.alpha = 0f;
        }

        if (waitingCanvasGroup == null && waitingText != null)
        {
            waitingCanvasGroup = waitingText.GetComponentInParent<CanvasGroup>();
            if (waitingCanvasGroup == null)
                waitingCanvasGroup = waitingText.gameObject.AddComponent<CanvasGroup>();
        }

        if (waitingCanvasGroup != null)
            waitingCanvasGroup.alpha = 0f;
    }

    private void Start()
    {
        UpdateUIForCurrentScene();

        NetworkManager.Singleton.OnClientStarted += OnClientStartedMethod;
        NetworkManager.Singleton.OnClientStopped += OnClientStoppedMethod;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedMethod;
        NetworkManager.Singleton.OnClientConnectedCallback += LoadLobbyBecauseConnected;
        NetworkManager.Singleton.OnClientStopped += LoadMainMenuBecauseDesconnect;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (waitingBlinkCoroutine != null)
            StopCoroutine(waitingBlinkCoroutine);
    }

    private void Update()
    {
        // Solo en Lobby y solo si somos host
        if (currentSceneName != "Lobby" || !NetworkManager.Singleton?.IsServer == true)
            return;

        // Comprobación segura: si NetworkManager desaparece, salimos
        if (NetworkManager.Singleton == null)
            return;

        // Comprobamos si hay clientes conectados
        int clientCount = NetworkManager.Singleton.ConnectedClients.Count;
        bool isAlone = clientCount <= 1;

        if (isAlone)
        {
            ShowWaitingPlayers(true);
        }
        else
        {
            ShowWaitingPlayers(false);
        }
    }

    private void ShowWaitingPlayers(bool show)
    {
        if (waitingText == null || waitingCanvasGroup == null)
            return;

        if (show)
        {
            waitingText.text = "Esperando jugadores...";
            waitingCanvasGroup.alpha = 0f;

            if (waitingBlinkCoroutine == null)
            {
                waitingBlinkCoroutine = StartCoroutine(BlinkWaitingText());
            }
        }
        else
        {
            // Ocultar y parar todo
            if (waitingBlinkCoroutine != null)
            {
                StopCoroutine(waitingBlinkCoroutine);
                waitingBlinkCoroutine = null;
            }
            waitingCanvasGroup.alpha = 0f;
        }
    }

    private IEnumerator BlinkWaitingText()
    {
        while (true)
        {
            // Fade In: de 0 a 1 (suave)
            float timer = 0f;
            while (timer < fadeInDuration)
            {
                timer += Time.unscaledDeltaTime;
                waitingCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeInDuration);
                yield return null;
            }
            waitingCanvasGroup.alpha = 1f;

            // Mantener visible
            yield return new WaitForSecondsRealtime(holdVisibleTime);

            // Fade Out: de 1 a 0 (suave)
            timer = 0f;
            while (timer < fadeOutDuration)
            {
                timer += Time.unscaledDeltaTime;
                waitingCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOutDuration);
                yield return null;
            }
            waitingCanvasGroup.alpha = 0f;

            // Pequeña pausa completamente apagado antes de repetir
            yield return new WaitForSecondsRealtime(holdInvisibleTime);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentSceneName = scene.name;
        UpdateUIForCurrentScene();

        if (waitingBlinkCoroutine != null)
        {
            StopCoroutine(waitingBlinkCoroutine);
            waitingBlinkCoroutine = null;
        }
        if (waitingCanvasGroup != null)
            waitingCanvasGroup.alpha = 0f;
    }

    private void UpdateUIForCurrentScene()
    {
        bool isInMainMenu = currentSceneName == "MainMenu";

        if (LobbyCodeManager.Singleton != null)
        {
            LobbyCodeManager.Singleton.SetCodeVisibility(!isInMainMenu);
        }
    }

    private void ShowTemporaryFeedback(string message)
    {
        if (informationText == null || infoCanvasGroup == null) return;

        if (currentFeedbackCoroutine != null)
            StopCoroutine(currentFeedbackCoroutine);

        currentFeedbackCoroutine = StartCoroutine(TemporaryFeedbackCoroutine(message));
    }

    public void ShowFeedback(string message)
    {
        ShowTemporaryFeedback(message);
    }

    private IEnumerator TemporaryFeedbackCoroutine(string message)
    {
        informationText.text = message;

        // Fade In
        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.unscaledDeltaTime;
            infoCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeTime);
            yield return null;
        }
        infoCanvasGroup.alpha = 1f;

        // Tiempo visible
        yield return new WaitForSecondsRealtime(displayTime);

        // Fade Out
        timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.unscaledDeltaTime;
            infoCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);
            yield return null;
        }
        infoCanvasGroup.alpha = 0f;

        currentFeedbackCoroutine = null;
    }

    private void OnClientDisconnectedMethod(ulong obj)
    {
        // Lógica adicional si la necesitas
    }

    private void LoadMainMenuBecauseDesconnect(bool obj)
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void LoadLobbyBecauseConnected(ulong userConnectedID)
    {
        if (NetworkManager.Singleton.IsServer && userConnectedID == NetworkManager.Singleton.LocalClientId)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
        }
    }

    private void OnClientStoppedMethod(bool obj)
    {
        ShowTemporaryFeedback("Has salido de la sala...");
    }

    private void OnClientStartedMethod()
    {
        string message = "Conectado como ";
        message += NetworkManager.Singleton.IsHost ? "Host" : "Cliente";
        ShowTemporaryFeedback(message);
    }
}