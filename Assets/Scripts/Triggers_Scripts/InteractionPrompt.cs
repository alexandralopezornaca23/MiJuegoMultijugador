using TMPro;
using Unity.Netcode;
using UnityEngine;

public class InteractionPrompt : NetworkBehaviour
{
    [Header("Configuración")]
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
    //private bool waitingForPlayer = true;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
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

        if (localPlayerTransform == null)
        {
            TryFindLocalPlayer();
            return;
        }

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        float distance = Vector3.Distance(transform.position, localPlayerTransform.position);

        bool shouldBeVisible = distance <= triggerRadius;

        var pickupScript = localPlayerTransform.GetComponent<PickupObject>();
        if (pickupScript != null && pickupScript.heldObject != null && pickupScript.heldObject == NetworkObject)
        {
            shouldBeVisible = false;
        }

        if (shouldBeVisible != isInRange)
        {
            isInRange = shouldBeVisible;
            if (isInRange) ShowPrompt();
            else HidePrompt();
        }

        if (isInRange && currentPromptInstance != null && playerCamera != null)
        {
            currentPromptInstance.transform.position = transform.position + positionOffset;
            Vector3 lookAtPos = playerCamera.position;
            lookAtPos.y = currentPromptInstance.transform.position.y;
            currentPromptInstance.transform.LookAt(lookAtPos);
            currentPromptInstance.transform.Rotate(0, 180, 0);
        }
    }

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
        {
            tmp.text = customText;
        }
    }

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