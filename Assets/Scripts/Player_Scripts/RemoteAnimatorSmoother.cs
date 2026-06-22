using UnityEngine;
using Unity.Netcode;

public class RemoteAnimatorSmoother : NetworkBehaviour
{
    [Tooltip("Velocidad de actualización del Animator en jugadores remotos. 0 = sin suavizado (más responsivo).")]
    [Range(0f, 20f)]
    public float remoteUpdateSpeed = 12f;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.applyRootMotion = false;

            if (animator != null)
            {
                animator.speed = 1f;
            }

            if (animator != null)
            {
                animator.SetFloat("Interpolation", remoteUpdateSpeed);
            }
        }
    }
}