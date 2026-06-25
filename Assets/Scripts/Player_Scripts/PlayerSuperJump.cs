using UnityEngine;
using Unity.Netcode;
using System.Collections;

// Script de truco para pruebas: permite dar un super salto pulsando F.
// Solo funciona en el cliente propietario y no afecta a otros jugadores.

public class PlayerSuperJump : NetworkBehaviour
{
    [Header("Configuracion Super Salto")]
    [SerializeField] private KeyCode jumpKey = KeyCode.F;
    [SerializeField] private float superJumpForceMultiplier = 2.5f;
    [SerializeField] private float superJumpTimeMultiplier = 1.5f;
    [SerializeField] private float jumpCooldown = 3f;

    private ThirdPersonController movementScript;
    private float nextJumpTime = 0f;
    private float originalJumpForce;
    private float originalJumpTime;

    public override void OnNetworkSpawn()
    {
        // Solo el propietario del personaje necesita este script activo
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        movementScript = GetComponent<ThirdPersonController>();
        if (movementScript != null)
        {
            // Guardamos los valores originales para restaurarlos despues del salto
            originalJumpForce = movementScript.jumpForce;
            originalJumpTime = movementScript.jumpTime;
        }
    }

    void Update()
    {
        if (!IsOwner || movementScript == null) return;

        if (Input.GetKeyDown(jumpKey) && Time.time >= nextJumpTime)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null && cc.isGrounded)
                StartCoroutine(PerformSuperJump());
        }
    }

    #region Super Salto
    private IEnumerator PerformSuperJump()
    {
        nextJumpTime = Time.time + jumpCooldown;

        // Multiplicamos temporalmente la fuerza y duracion del salto
        movementScript.jumpForce = originalJumpForce * superJumpForceMultiplier;
        movementScript.jumpTime = originalJumpTime * superJumpTimeMultiplier;

        // Usamos Reflection para activar la variable privada isJumping del ThirdPersonController
        // ya que no tiene un metodo publico para iniciarlo directamente
        typeof(ThirdPersonController).GetField("isJumping",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(movementScript, true);

        yield return new WaitForSeconds(movementScript.jumpTime);

        // Restauramos los valores originales al terminar el salto
        movementScript.jumpForce = originalJumpForce;
        movementScript.jumpTime = originalJumpTime;
    }
    #endregion
}