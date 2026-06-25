using TMPro;
using UnityEngine;

// Script simple que guarda y controla la visibilidad del codigo de sala en la UI.
// El ConnectionCallbackManager es quien decide cuando mostrarlo u ocultarlo.

public class LobbyCodeManager : MonoBehaviour
{
    private static LobbyCodeManager singleton;
    public static LobbyCodeManager Singleton => singleton;

    // Referencia al texto de la UI donde se muestra el codigo de sala
    public TMP_Text lobbyCode;

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
        }
    }

    public void SetCodeVisibility(bool visible)
    {
        if (lobbyCode != null)
            lobbyCode.gameObject.SetActive(visible);
    }
}