using UnityEngine;
using UnityEngine.UI;

// Copia el codigo de sala al portapapeles del sistema al pulsar el boton
// y cambia el icono del boton brevemente para confirmar la accion al jugador.

public class CopyJoinCode : MonoBehaviour
{
    [Header("Sprites del boton")]
    public Image buttonImage;
    public Sprite normalSprite;
    public Sprite copiedSprite;

    public void CopyCodeToClipboard()
    {
        string code = LobbyCodeManager.Singleton.lobbyCode.text;
        if (string.IsNullOrEmpty(code)) return;

        GUIUtility.systemCopyBuffer = code;

        // Cambiamos el sprite a "copiado" y lo restauramos despues de 1.5 segundos
        if (buttonImage != null && copiedSprite != null)
        {
            buttonImage.sprite = copiedSprite;
            CancelInvoke(); // Cancelamos cualquier reset pendiente antes de iniciar uno nuevo
            Invoke(nameof(ResetSprite), 1.5f);
        }
    }

    private void ResetSprite()
    {
        if (buttonImage != null && normalSprite != null)
            buttonImage.sprite = normalSprite;
    }
}