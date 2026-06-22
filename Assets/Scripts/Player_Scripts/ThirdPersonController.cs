using Unity.Netcode;
using UnityEngine;

/*
    This file has a commented version with details about how each line works.
    The commented version contains code that is easier and simpler to read. This file is minified.
*/

/// <summary>
/// Main script for third-person movement of the character in the game.
/// Make sure that the object that will receive this script (the player) 
/// has the Player tag and the Character Controller component.
/// </summary>

public class ThirdPersonController : NetworkBehaviour
{
    [Tooltip("Speed ​​at which the character moves. It is not affected by gravity or jumping.")]
    public float velocity = 5f;
    [Tooltip("This value is added to the speed value while the character is sprinting.")]
    public float sprintAdittion = 3.5f;
    [Tooltip("The higher the value, the higher the character will jump.")]
    public float jumpForce = 18f;
    [Tooltip("Stay in the air. The higher the value, the longer the character floats before falling.")]
    public float jumpTime = 0.85f;
    [Space]
    [Tooltip("Force that pulls the player down. Changing this value causes all movement, jumping and falling to be changed as well.")]
    public float gravity = 9.8f;
    float jumpElapsedTime = 0;

    // Player states
    bool isJumping = false;
    bool isSprinting = false;
    bool isCrouching = false;

    // Inputs
    float inputHorizontal;
    float inputVertical;
    bool inputJump;
    private float _verticalVelocity;
    bool inputCrouch;
    bool inputSprint;

    Animator animator;
    CharacterController cc;

    //Funciones de impulso por Empuje
    private Vector3 _impactForce = Vector3.zero;
    public float friction = 5f;
       

    //Funciones para Network
    public NetworkObject myNetworkObject;

    //Funciones para chat in Game
    public bool inputBlocked = false;

    void Awake()
    {
        // Forzamos la asignación aquí para que esté lista para los RPCs
        cc = GetComponent<CharacterController>();
        myNetworkObject = GetComponent<NetworkObject>();
        animator = GetComponent<Animator>();
    }

    [ClientRpc]
    public void ReceivePushClientRpc(Vector3 force)
    {
        if (!IsOwner) return;

        _impactForce += force;

        if (animator != null)
            animator.SetTrigger("Pushed");
    }

    void Start()
    {
        myNetworkObject = GetComponent<NetworkObject>();
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        // Message informing the user that they forgot to add an animator
        if (animator == null)
            Debug.LogWarning("Hey buddy, you don't have the Animator component in your player. Without it, the animations won't work.");

        //funciones para Network
        if (myNetworkObject.IsOwner)
        {
            FindAnyObjectByType<CameraController>().Player = this.transform;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Esto soluciona el problema de la cámara perdida
        if (IsOwner)
        {
            var cam = FindAnyObjectByType<CameraController>();
            if (cam != null)
            {
                cam.Player = this.transform;
            }
            else
            {
                Debug.LogError("No se encontró CameraController en la escena.");
            }
        }
    }

    [ClientRpc]
    public void TeleportClientRpc(Vector3 newPosition)
    {
        // Solo el dueño procesa su teleport
        if (!IsOwner) return;

        // IMPORTANTE: Si por timing 'cc' es nulo, lo buscamos de nuevo
        if (cc == null) cc = GetComponent<CharacterController>();

        if (cc != null)
        {
            cc.enabled = false;
            transform.position = newPosition;
            cc.enabled = true;
        }
    }

    // Update is only being used here to identify keys and trigger animations
    void Update()
    {
        if (!myNetworkObject.IsOwner) return;
        if (inputBlocked) return;

        // Input checkers
        inputHorizontal = Input.GetAxis("Horizontal");
        inputVertical = Input.GetAxis("Vertical");
        inputJump = Input.GetButtonDown("Jump");
        inputSprint = Input.GetAxis("Fire3") == 1f;

        // Unfortunately GetAxis does not work with GetKeyDown, so inputs must be taken individually
        inputCrouch = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.JoystickButton1);

        // Check if you pressed the crouch input key and change the player's state
        if (inputCrouch)
            isCrouching = !isCrouching;

        // Run and Crouch animation
        // If dont have animator component, this block wont run
        if (cc.isGrounded && animator != null)
        {
            // Crouch
            // Note: The crouch animation does not shrink the character's collider
            animator.SetBool("crouch", isCrouching);

            // Run
            float minimumSpeed = 0.9f;
            animator.SetBool("run", cc.velocity.magnitude > minimumSpeed);

            // Sprint
            isSprinting = cc.velocity.magnitude > minimumSpeed && inputSprint;
            animator.SetBool("sprint", isSprinting);
        }

        // Jump animation
        if (animator != null)
            animator.SetBool("air", cc.isGrounded == false);

        // Handle can jump or not
        if (inputJump && cc.isGrounded)
        {
            _verticalVelocity = jumpForce;
        }
    }

    // With the inputs and animations defined, FixedUpdate is responsible for applying movements and actions to the player
    private void FixedUpdate()
    {
        if (!myNetworkObject.IsOwner) return;
        if (inputBlocked)
        {
            if (!cc.isGrounded)
                _verticalVelocity -= gravity * Time.fixedDeltaTime;
            else if (_verticalVelocity < 0)
                _verticalVelocity = -2f;
            cc.Move(Vector3.up * _verticalVelocity * Time.fixedDeltaTime);
            return;
        }

        // 1. Reducción de fuerza de impacto
        if (_impactForce.magnitude > 0.1f)
            _impactForce = Vector3.Lerp(_impactForce, Vector3.zero, friction * Time.fixedDeltaTime);
        else
            _impactForce = Vector3.zero;

        // 2. Lógica de Gravedad y Salto (CORREGIDA)
        if (isJumping)
        {
            // Durante el salto, aplicamos la curva de fuerza hacia arriba
            _verticalVelocity = Mathf.SmoothStep(jumpForce, jumpForce * 0.30f, jumpElapsedTime / jumpTime);
            jumpElapsedTime += Time.fixedDeltaTime;

            if (jumpElapsedTime >= jumpTime)
            {
                isJumping = false;
            }
        }
        else
        {
            // Si NO estamos saltando, aplicamos gravedad
            if (cc.isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity -= gravity * Time.fixedDeltaTime;
            }
        }

        // 3. Dirección horizontal
        float velocityAdittion = isSprinting ? sprintAdittion : (isCrouching ? -(velocity * 0.5f) : 0);

        // Obtenemos la dirección de la cámara pero ignoramos su inclinación (Y)
        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();

        Vector3 moveDirection = (camForward * inputVertical + camRight * inputHorizontal).normalized;
        Vector3 horizontalMove = moveDirection * (velocity + velocityAdittion);

        // 4. Rotación
        if (moveDirection.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, angle, 0), 0.15f);
        }

        // 5. MOVIMIENTO FINAL (Ajustado)
        Vector3 finalVelocity = horizontalMove + (Vector3.up * _verticalVelocity) + _impactForce;

        // Movemos usando Time.fixedDeltaTime
        cc.Move(finalVelocity * Time.fixedDeltaTime);
    }

    //This function makes the character end his jump if he hits his head on something
    void HeadHittingDetect()
    {
        int layerMask = ~LayerMask.GetMask("Player");
        float headHitDistance = 0.6f;
        Vector3 origin = transform.position + Vector3.up * (cc.height - 0.1f);

        if (Physics.Raycast(origin, Vector3.up, headHitDistance, layerMask))
        {
            jumpElapsedTime = jumpTime;
            isJumping = false;
        }
    }
}