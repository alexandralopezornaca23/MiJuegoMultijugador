using Unity.Netcode;
using UnityEngine;
using System.Linq;

public class PlayerPush : NetworkBehaviour
{
    public float range = 2f;
    public float pushStrengthEnemy = 15f;  // fuerza contra equipo contrario
    public float pushStrengthTeam = 5f;    // fuerza contra mismo equipo

    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (!IsOwner) return;
        if (Input.GetMouseButtonDown(0))
        {
            if (animator != null)
                animator.SetTrigger("Push");
            RequestPushServerRpc();
        }
    }

    [ServerRpc]
    void RequestPushServerRpc(ServerRpcParams rpcParams = default)
    {
        int layerMask = LayerMask.GetMask("Player");
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * 1.5f + transform.forward * 0.5f;
        Vector3 direction = transform.forward;
        float sphereRadius = 0.4f;

        if (Physics.SphereCast(origin, sphereRadius, direction, out hit, range, layerMask))
        {
            if (hit.collider.gameObject == gameObject) return;

            if (!hit.collider.TryGetComponent<ThirdPersonController>(out var target)) return;
            if (!hit.collider.TryGetComponent<NetworkObject>(out var targetNetObj)) return;

            // Obtenemos el equipo del atacante y del objetivo
            ulong attackerId = rpcParams.Receive.SenderClientId;
            int attackerTeam = GetTeamForClient(attackerId);
            int targetTeam = GetTeamForClient(targetNetObj.OwnerClientId);

            bool isSameTeam = attackerTeam != 0 && attackerTeam == targetTeam;

            // Calculamos la fuerza según si es mismo equipo o no
            float strength = isSameTeam ? pushStrengthTeam : pushStrengthEnemy;
            Vector3 force = direction * strength;
            force.y = 2f;

            // Solo tiramos el objeto si es del equipo contrario
            if (!isSameTeam)
            {
                if (hit.collider.TryGetComponent<PickupObject>(out var targetInventory))
                {
                    targetInventory.ForceDropOnPush();
                }
            }

            target.ReceivePushClientRpc(force);
        }
    }

    private int GetTeamForClient(ulong clientId)
    {
        var manager = ConnectedUserListManager.Singleton;
        if (manager == null) return 0;
        var user = manager.usersConnectedList.Find(u => u.userId == clientId);
        return user != null ? user.team : 0;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * range);
    }
}