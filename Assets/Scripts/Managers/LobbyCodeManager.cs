using TMPro;
using UnityEngine;

public class LobbyCodeManager : MonoBehaviour
{
    private static LobbyCodeManager singleton;
    public static LobbyCodeManager Singleton => singleton;

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

    // Opcional: método público para actualizar visibilidad (por si lo necesitas desde otro sitio)
    public void SetCodeVisibility(bool visible)
    {
        if (lobbyCode != null)
        {
            lobbyCode.gameObject.SetActive(visible);
        }
    }
}