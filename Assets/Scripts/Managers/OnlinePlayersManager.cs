using System;
using UnityEngine;

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

    public string playerName;  // Nombre actual en uso

    private void Awake()
    {
        if (singleton == null)
        {
            singleton = this;
            DontDestroyOnLoad(gameObject);

            string savedName = PlayerPrefs.GetString("PlayerName", "");

            if (!string.IsNullOrEmpty(savedName))
            {
                playerName = savedName;
            }
            else
            {
                playerName = "";
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetPlayerName(string newName)
    {
        if (string.IsNullOrEmpty(newName)) return;

        playerName = newName;
        PlayerPrefs.SetString("PlayerName", newName);
        PlayerPrefs.Save();
    }
}