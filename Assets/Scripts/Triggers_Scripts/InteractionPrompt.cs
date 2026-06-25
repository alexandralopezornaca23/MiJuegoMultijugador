using TMPro;
using Unity.Netcode;
using UnityEngine;

// Muestra un icono flotante sobre los objetos recogibles cuando el jugador local se acerca.
// El icono cambia de color segun el equipo al que pertenece el objeto.
// Se oculta automaticamente si el jugador ya lleva ese objeto en la mano.

public class InteractionPrompt : NetworkBehaviour
{
    [Header("Configuracion")]
    [SerializeField] private GameObject promptPrefab;
    [SerializeField] private Vector3 positionOffset = new Vector3(0, 1.8f, 0);
    [SerializeField] private float triggerRadius = 5f;

    [Header("Iconos por equipo")]
    [SerializeField] private GameObject pinkTeamPrefabVariant;
    [SerializeField] private GameObject blueTeamPrefabVariant;
    [SerializeField] private string customText = "Recoger Objeto (E)";

    private GameObject currentPromptInstance;
    private Transform playerCamera;
    private Transform localPlayerTransform;
    private bool isInRange = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Este script solo lo necesita el cliente, no el servidor
        if (!IsClient)
        {
            enabled = false;
            return;
        }

        if (Camera.main != null) playerCamera = Camera.main.transform;
        TryFindLocalPlayer();
    }

    private void Update()
    {
        if (!IsClient) return;

        // Si aun no hemos encontrado al jugador local, seguimos buscando
        if (localPlayerTransform == null)
        {
            TryFindLocalPlayer();
            return;
        }

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        float distance = Vector3.Distance(transform.position, localPlayerTransform.position);
        bool shouldBeVisible = distance <= triggerRadius;

        // Ocultamos el icono si el jugador ya lleva este objeto en la mano
        var pickupScript = localPlayerTransform.GetComponent<PickupObject>();
        if (pickupScript != null && pickupScript.heldObject != null && pickupScript.heldObject == NetworkObject)
            shouldBeVisible = false;

        if (shouldBeVisible != isInRange)
        {
            isInRange = shouldBeVisible;
            if (isInRange) ShowPrompt();
            else HidePrompt();
        }

        // Mantenemos el icono mirando hacia la camara del jugador mientras esta visible
        if (isInRange && currentPromptInstance != null && playerCamera != null)
        {
            currentPromptInstance.transform.position = transform.position + positionOffset;
            Vector3 lookAtPos = playerCamera.position;
            lookAtPos.y = currentPromptInstance.transform.position.y;
            currentPromptInstance.transform.LookAt(lookAtPos);
            currentPromptInstance.transform.Rotate(0, 180, 0);
        }
    }

    // Busca el personaje que pertenece al jugador local entre todos los jugadores de la escena
    private void TryFindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("NewNetworkPlayer");
        foreach (var player in players)
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                localPlayerTransform = player.transform;
                return;
            }
        }
    }

    private void ShowPrompt()
    {
        if (currentPromptInstance != null) return;

        GameObject prefabToUse = GetCorrectPrefabVariant();
        currentPromptInstance = Instantiate(prefabToUse, transform.position + positionOffset, Quaternion.identity);

        var tmp = currentPromptInstance.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
            tmp.text = customText;
    }

    // Devuelve el prefab del icono correcto segun el equipo al que pertenece el objeto
    private GameObject GetCorrectPrefabVariant()
    {
        var pikeable = GetComponent<PikeableObject>();
        if (pikeable != null)
        {
            if (pikeable.allowedTeam == 1 && pinkTeamPrefabVariant != null) return pinkTeamPrefabVariant;
            if (pikeable.allowedTeam == 2 && blueTeamPrefabVariant != null) return blueTeamPrefabVariant;
        }
        return promptPrefab;
    }

    private void HidePrompt()
    {
        if (currentPromptInstance != null)
        {
            Destroy(currentPromptInstance);
            currentPromptInstance = null;
        }
    }

    public override void OnDestroy() { HidePrompt(); }
}