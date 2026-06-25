using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Script que gestiona los mensajes de feedback visual al jugador y las transiciones de escena
// provocadas por eventos de red (conectarse, desconectarse, cambiar de escena).
// Tambien controla el mensaje de "Esperando jugadores..." en el lobby cuando el host esta solo.

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

    // CanvasGroup del texto de informacion, usado para hacer el fade in/out del feedback
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

        // Buscamos o creamos el CanvasGroup del texto de feedback para poder controlar su opacidad
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

        // Nos suscribimos a los eventos de red para reaccionar automaticamente
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
        // Solo el host en el lobby necesita comprobar si esta solo para mostrar el mensaje de espera
        if (currentSceneName != "Lobby" || !NetworkManager.Singleton?.IsServer == true)
            return;

        if (NetworkManager.Singleton == null)
            return;

        int clientCount = NetworkManager.Singleton.ConnectedClients.Count;
        bool isAlone = clientCount <= 1;

        ShowWaitingPlayers(isAlone);
    }

    private void ShowWaitingPlayers(bool show)
    {
        if (waitingText == null || waitingCanvasGroup == null)
            return;

        if (show)
        {
            waitingText.text = "Esperando jugadores...";
            waitingCanvasGroup.alpha = 0f;

            // Solo arrancamos la corrutina si no esta ya en marcha
            if (waitingBlinkCoroutine == null)
                waitingBlinkCoroutine = StartCoroutine(BlinkWaitingText());
        }
        else
        {
            if (waitingBlinkCoroutine != null)
            {
                StopCoroutine(waitingBlinkCoroutine);
                waitingBlinkCoroutine = null;
            }
            waitingCanvasGroup.alpha = 0f;
        }
    }

    // Corrutina que hace parpadear el texto de espera de forma suave e indefinida
    // hasta que se para desde ShowWaitingPlayers
    private IEnumerator BlinkWaitingText()
    {
        while (true)
        {
            float timer = 0f;
            while (timer < fadeInDuration)
            {
                timer += Time.unscaledDeltaTime;
                waitingCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeInDuration);
                yield return null;
            }
            waitingCanvasGroup.alpha = 1f;

            yield return new WaitForSecondsRealtime(holdVisibleTime);

            timer = 0f;
            while (timer < fadeOutDuration)
            {
                timer += Time.unscaledDeltaTime;
                waitingCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOutDuration);
                yield return null;
            }
            waitingCanvasGroup.alpha = 0f;

            yield return new WaitForSecondsRealtime(holdInvisibleTime);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentSceneName = scene.name;
        UpdateUIForCurrentScene();

        // Al cambiar de escena paramos el parpadeo y ocultamos el mensaje de espera
        if (waitingBlinkCoroutine != null)
        {
            StopCoroutine(waitingBlinkCoroutine);
            waitingBlinkCoroutine = null;
        }
        if (waitingCanvasGroup != null)
            waitingCanvasGroup.alpha = 0f;
    }

    // Oculta el codigo de sala si estamos en el menu principal
    private void UpdateUIForCurrentScene()
    {
        bool isInMainMenu = currentSceneName == "MainMenu";

        if (LobbyCodeManager.Singleton != null)
            LobbyCodeManager.Singleton.SetCodeVisibility(!isInMainMenu);
    }

    private void ShowTemporaryFeedback(string message)
    {
        if (informationText == null || infoCanvasGroup == null) return;

        // Si habia un mensaje mostrando, lo cancelamos y mostramos el nuevo
        if (currentFeedbackCoroutine != null)
            StopCoroutine(currentFeedbackCoroutine);

        currentFeedbackCoroutine = StartCoroutine(TemporaryFeedbackCoroutine(message));
    }

    // Metodo publico para que otros scripts muestren mensajes de feedback sin necesitar referencias directas
    public void ShowFeedback(string message)
    {
        ShowTemporaryFeedback(message);
    }

    // Muestra el mensaje con fade in, lo mantiene visible unos segundos y lo hace desaparecer con fade out
    private IEnumerator TemporaryFeedbackCoroutine(string message)
    {
        informationText.text = message;

        float timer = 0f;
        while (timer < fadeTime)
        {
            timer += Time.unscaledDeltaTime;
            infoCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeTime);
            yield return null;
        }
        infoCanvasGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(displayTime);

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

    private void OnClientDisconnectedMethod(ulong obj) { }

    private void LoadMainMenuBecauseDesconnect(bool obj)
    {
        SceneManager.LoadScene("MainMenu");
    }

    // Solo el host carga el Lobby cuando el cliente conectado es el mismo host
    // Esto evita que se cargue la escena varias veces al conectarse otros clientes
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