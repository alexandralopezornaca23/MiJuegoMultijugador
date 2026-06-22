using UnityEngine;
using UnityEngine.UI;

public class CopyJoinCode : MonoBehaviour
{
    [Header("Sprites del botón")]
    public Image buttonImage;
    public Sprite normalSprite;
    public Sprite copiedSprite;

    public void CopyCodeToClipboard()
    {
        string code = LobbyCodeManager.Singleton.lobbyCode.text;

        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        GUIUtility.systemCopyBuffer = code;

        if (buttonImage != null && copiedSprite != null)
        {
            buttonImage.sprite = copiedSprite;
            CancelInvoke();
            Invoke(nameof(ResetSprite), 1.5f);
        }
    }

    private void ResetSprite()
    {
        if (buttonImage != null && normalSprite != null)
        {
            buttonImage.sprite = normalSprite;
        }
    }
}