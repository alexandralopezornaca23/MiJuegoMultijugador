using TMPro;
using UnityEngine;

public class WelcomeMessageManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text welcomeText;

    [Header("Mensajes")]
    public string firstTimeMessage;
    public string returningMessage;

    private void Start()
    {
        UpdateWelcomeMessage();
    }

    private void OnEnable()
    {
        if (OnlinePlayersManager.Singleton != null)
        {
            UpdateWelcomeMessage();
        }
    }

    public void UpdateWelcomeMessage()
    {
        if (welcomeText == null) return;

        string savedName = PlayerPrefs.GetString("PlayerName", "");

        if (string.IsNullOrEmpty(savedName))
        {
            welcomeText.text = "¿Cómo te llamas?";
        }
        else
        {
            welcomeText.text = "¡Hola de nuevo!";
        }
    }
}