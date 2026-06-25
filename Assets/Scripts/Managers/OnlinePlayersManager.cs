using System;
using UnityEngine;

// Script que guarda el nombre del jugador local y lo mantiene disponible
// para todos los demas scripts durante toda la ejecucion del juego.

// Estructura de datos preparada para vincular nombre e ID de red si se necesita en el futuro
[Serializable]
public class OnlineUserData
{
    public string onlinePlayerName;
    public ulong onlineUlongPlayerID;
}

public class OnlinePlayersManager : MonoBehaviour
{
    private static OnlinePlayersManager singleton;
    public static OnlinePlayersManager Singleton => singleton;

    public string playerName;

    private void Awake()
    {
        if (singleton == null)
        {
            singleton = this;
            DontDestroyOnLoad(gameObject);

            // Recuperamos el nombre guardado en sesiones anteriores para no pedirlo cada vez
            string savedName = PlayerPrefs.GetString("PlayerName", "");
            playerName = !string.IsNullOrEmpty(savedName) ? savedName : "";
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Punto unico para cambiar el nombre: actualiza la variable y lo guarda en disco
    public void SetPlayerName(string newName)
    {
        if (string.IsNullOrEmpty(newName)) return;
        playerName = newName;
        PlayerPrefs.SetString("PlayerName", newName);
        PlayerPrefs.Save();
    }
}