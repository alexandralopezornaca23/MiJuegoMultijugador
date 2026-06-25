using UnityEngine;
using Unity.Netcode;

// Ajusta la configuracion del Animator para los jugadores remotos (los que no somos nosotros).
// Evita que las animaciones recibidas por red se vean bruscas o causen conflictos de movimiento.

public class RemoteAnimatorSmoother : NetworkBehaviour
{
    [Tooltip("Velocidad de actualizacion del Animator en jugadores remotos. 0 = sin suavizado.")]
    [Range(0f, 20f)]
    public float remoteUpdateSpeed = 12f;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        // Solo aplicamos estos ajustes a los jugadores que no somos nosotros
        if (!IsOwner)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;

            // Desactivamos applyRootMotion porque el movimiento ya lo gestiona ClientNetworkTransform.
            // Si estuviera activo, el Animator intentaria mover al personaje y entraria en conflicto con la red.
            animator.applyRootMotion = false;

            if (animator != null)
                animator.speed = 1f;

            if (animator != null)
                animator.SetFloat("Interpolation", remoteUpdateSpeed);
        }
    }
}