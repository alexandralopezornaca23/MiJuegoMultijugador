using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    [Header("Componentes")]
    public Transform nameTagContainer;

    public TextMeshPro nameText3D;
    public SpriteRenderer crownRenderer;
    public SpriteRenderer teamRenderer;

    [Header("Sprites")]
    public Sprite pinkTeamSprite;
    public Sprite blueTeamSprite;
    public Sprite crownSprite;

    [Header("Ajustes visuales")]
    [SerializeField] private Vector3 nameTagOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float billboardSmooth = 12f;

    private Transform mainCamera;

    private void Start()
    {
        mainCamera = Camera.main?.transform;

        // Configuración inicial del texto
        if (nameText3D != null)
        {
            nameText3D.alignment = TextAlignmentOptions.Center;
            nameText3D.textWrappingMode = (TextWrappingModes)0;
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null || nameTagContainer == null) return;

        nameTagContainer.localPosition = nameTagOffset;
        Vector3 fromCameraToTag = nameTagContainer.position - mainCamera.position;
        fromCameraToTag.y = 0;

        Quaternion baseRotation = Quaternion.LookRotation(fromCameraToTag);

        Vector3 cameraForwardProjected = mainCamera.forward;
        cameraForwardProjected.y = 0;
        cameraForwardProjected.Normalize();

        float dot = Vector3.Dot(transform.forward, cameraForwardProjected);

        if (dot < -1f)
        {
            baseRotation *= Quaternion.Euler(0, 180, 0);
        }

        nameTagContainer.rotation = Quaternion.Slerp(
            nameTagContainer.rotation,
            baseRotation,
            Time.deltaTime * billboardSmooth
        );

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (ConnectedUserListManager.Singleton == null) return;

        var playerData = ConnectedUserListManager.Singleton.usersConnectedList
            .FirstOrDefault(u => u.userId == OwnerClientId);

        if (playerData == null) return;

        // Nombre
        if (nameText3D != null)
            nameText3D.text = playerData.userConnectedName;

        // Corona solo para host
        if (crownRenderer != null)
        {
            crownRenderer.sprite = crownSprite;
            crownRenderer.enabled = playerData.userId == 0;
        }

        // Icono de equipo
        if (teamRenderer != null)
        {
            teamRenderer.enabled = playerData.team != 0;

            if (playerData.team == 1)
                teamRenderer.sprite = pinkTeamSprite;
            else if (playerData.team == 2)
                teamRenderer.sprite = blueTeamSprite;
        }
    }
}