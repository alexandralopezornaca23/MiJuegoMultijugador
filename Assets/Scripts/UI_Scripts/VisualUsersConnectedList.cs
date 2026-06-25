using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Muestra la lista de jugadores conectados en el lobby, separados por equipo.
// Se actualiza cada vez que ConnectedUserListManager recibe cambios de la red.

public class VisualUsersConnectedList : MonoBehaviour
{
    [Header("Configuracion de Equipos")]
    public TMP_Text teamBlueText;
    public TMP_Text teamPinkText;

    [Header("Iconos")]
    public TMP_SpriteAsset crownSpriteAsset;
    public string spriteName = "Crown_Icon";

    [Header("Botones")]
    public Button buttonJoinBlue;
    public Button buttonJoinPink;

    private void Awake()
    {
        buttonJoinBlue?.onClick.AddListener(() => ChangeTeam(2));
        buttonJoinPink?.onClick.AddListener(() => ChangeTeam(1));
    }

    private void Start()
    {
        UpdateUsersConnectedList(ConnectedUserListManager.Singleton?.usersConnectedList);
    }

    public void UpdateUsersConnectedList(List<ConnectedUserListData> usersConnected)
    {
        ClearAll();
        if (usersConnected == null || usersConnected.Count == 0) return;

        System.Text.StringBuilder blueBuilder = new System.Text.StringBuilder();
        System.Text.StringBuilder pinkBuilder = new System.Text.StringBuilder();

        // Generamos el tag de la corona usando el nombre del SpriteAsset asignado en el inspector
        string crownTag = "";
        if (crownSpriteAsset != null)
            crownTag = $"<sprite=\"{crownSpriteAsset.name}\" name=\"{spriteName}\"> ";

        foreach (var user in usersConnected)
        {
            // El host siempre tiene userId == 0 en Netcode, le ponemos la corona delante del nombre
            bool isHost = (user.userId == 0);
            string line = (isHost ? crownTag : "") + user.userConnectedName + "\n";

            if (user.team == 2) blueBuilder.Append(line);
            else if (user.team == 1) pinkBuilder.Append(line);
        }

        if (teamBlueText != null) teamBlueText.text = blueBuilder.ToString().TrimEnd('\n');
        if (teamPinkText != null) teamPinkText.text = pinkBuilder.ToString().TrimEnd('\n');
    }

    private void ClearAll()
    {
        if (teamBlueText != null) teamBlueText.text = "";
        if (teamPinkText != null) teamPinkText.text = "";
    }

    private void ChangeTeam(int team)
    {
        ConnectedUserListManager.Singleton?.RequestTeamChange(team);
    }
}