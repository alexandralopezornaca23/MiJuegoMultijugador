using TMPro;
using UnityEngine;

// Script que muestra un mensaje de bienvenida en el menu principal.
// Si el jugador ya tiene nombre guardado muestra "Hola de nuevo", si no, le pide que se identifique.

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
        // Actualizamos tambien en OnEnable para que el mensaje sea correcto
        // si el jugador cambia el nombre desde opciones y vuelve al menu
        if (OnlinePlayersManager.Singleton != null)
            UpdateWelcomeMessage();
    }

    public void UpdateWelcomeMessage()
    {
        if (welcomeText == null) return;

        string savedName = PlayerPrefs.GetString("PlayerName", "");

        welcomeText.text = string.IsNullOrEmpty(savedName) ? "Como te llamas?" : "Hola de nuevo!";
    }
}