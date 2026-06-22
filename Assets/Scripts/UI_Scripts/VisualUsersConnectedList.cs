using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VisualUsersConnectedList : MonoBehaviour
{
    [Header("Configuración de Equipos")]
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

        // Creamos el tag dinámicamente usando el nombre del asset y del sprite
        string crownTag = "";
        if (crownSpriteAsset != null)
        {
            crownTag = $"<sprite=\"{crownSpriteAsset.name}\" name=\"{spriteName}\"> ";
        }

        foreach (var user in usersConnected)
        {
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