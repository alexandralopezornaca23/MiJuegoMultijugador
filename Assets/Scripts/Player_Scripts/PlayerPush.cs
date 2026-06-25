using Unity.Netcode;
using UnityEngine;
using System.Linq;

// Script que permite a los jugadores empujarse entre si al hacer clic.
// La fuerza aplicada varia segun si el objetivo es del mismo equipo o del contrario.
// Si el empujado lleva un objeto y es del equipo contrario, lo suelta al recibir el empuje.

public class PlayerPush : NetworkBehaviour
{
    public float range = 2.5f;
    public float pushStrengthEnemy = 15f;
    public float pushStrengthTeam = 5f;
    public float overlapRadius = 1.2f; // Radio de deteccion, mas generoso que un raycast para compensar el desfase de red

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

        // OverlapSphere detecta todos los jugadores cercanos sin depender de la direccion exacta.
        // Esto es mas fiable que SphereCast cuando hay desfase de posicion entre cliente y servidor.
        Vector3 origin = transform.position + Vector3.up * 1f;
        Collider[] hits = Physics.OverlapSphere(origin, overlapRadius, layerMask);

        if (hits.Length == 0) return;

        Collider bestTarget = null;
        float bestDot = -1f;

        foreach (var col in hits)
        {
            if (col.transform.root == transform.root) continue;

            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist > range) continue;

            // Usamos el dot product para quedarnos solo con objetivos que esten delante
            // y elegir el que este mas centrado respecto al forward del atacante
            Vector3 toTarget = (col.transform.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, toTarget);

            if (dot > bestDot)
            {
                bestDot = dot;
                bestTarget = col;
            }
        }

        if (bestTarget == null) return;

        // GetComponentInParent para encontrar los scripts aunque el collider este en un objeto hijo del prefab
        var target = bestTarget.GetComponentInParent<ThirdPersonController>();
        var targetNetObj = bestTarget.GetComponentInParent<NetworkObject>();

        if (target == null || targetNetObj == null) return;

        ulong attackerId = rpcParams.Receive.SenderClientId;
        int attackerTeam = GetTeamForClient(attackerId);
        int targetTeam = GetTeamForClient(targetNetObj.OwnerClientId);
        bool isSameTeam = attackerTeam != 0 && attackerTeam == targetTeam;

        float strength = isSameTeam ? pushStrengthTeam : pushStrengthEnemy;

        // Calculamos la direccion del empuje entre posiciones reales en lugar de usar transform.forward
        // para que sea coherente aunque haya desfase de posicion en la red
        Vector3 pushDir = (bestTarget.transform.position - transform.position).normalized;
        pushDir.y = 0f;
        pushDir.Normalize();

        Vector3 force = pushDir * strength;
        force.y = 2f;

        if (!isSameTeam)
        {
            var targetPickup = bestTarget.GetComponentInParent<PickupObject>();
            if (targetPickup != null)
                targetPickup.ForceDropOnPush();
        }

        target.ReceivePushClientRpc(force);
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
        // Esfera amarilla: zona de deteccion. Rayo rojo: rango maximo del empuje.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, overlapRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * range);
    }
}