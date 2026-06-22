using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Configuración de Sensibilidad")]
    public float sensitivity = 5f;
    public Vector2 cameraLimit = new Vector2(-45, 40);

    [Header("Configuración de Colisión")]
    [Tooltip("La cámara real (el objeto hijo)")]
    public Transform cameraTransform;
    [Tooltip("Capas que la cámara no debe atravesar (ej. Default, Static)")]
    public LayerMask collisionLayers;
    [Tooltip("Qué tan cerca puede estar la cámara del pivote")]
    public float minDistance = 0.5f;
    [Tooltip("Suavizado de la colisión")]
    public float collisionSmooth = 15f;
    [Tooltip("Radio del rayo (para evitar que las esquinas atraviesen)")]
    public float detectionRadius = 0.2f;

    float mouseX;
    float mouseY;
    float offsetDistanceY;
    float defaultDistance;
    Vector3 cameraDirection;
    Transform player;

    public Transform Player
    {
        get { return player; }
        set { player = value; }
    }

    void Start()
    {
        offsetDistanceY = transform.position.y;

        // Si no asignaste la cámara en el inspector, intenta buscar al hijo
        if (cameraTransform == null)
            cameraTransform = GetComponentInChildren<Camera>().transform;

        // Guardamos la distancia y dirección original de la cámara respecto al pivote
        cameraDirection = cameraTransform.localPosition.normalized;
        defaultDistance = cameraTransform.localPosition.magnitude;
    }

    void Update()
    {
        if (player == null) return;

        // 1. Seguir al jugador
        transform.position = player.position + new Vector3(0, offsetDistanceY, 0);

        // 2. Rotación con el ratón
        mouseX += Input.GetAxis("Mouse X") * sensitivity;
        mouseY += Input.GetAxis("Mouse Y") * sensitivity;
        mouseY = Mathf.Clamp(mouseY, cameraLimit.x, cameraLimit.y);
        transform.rotation = Quaternion.Euler(-mouseY, mouseX, 0);

        // 3. LÓGICA DE COLISIÓN
        HandleCameraCollision();
    }

    void HandleCameraCollision()
    {
        // Posición ideal donde debería estar la cámara si no hubiera muros
        Vector3 targetCameraPos = transform.TransformPoint(cameraDirection * defaultDistance);
        RaycastHit hit;

        float targetDistance = defaultDistance;

        // Lanzamos una esfera (SphereCast) desde el pivote hacia la cámara para detectar muros
        // Es mejor que un rayo simple porque tiene grosor
        if (Physics.SphereCast(transform.position, detectionRadius, targetCameraPos - transform.position, out hit, defaultDistance, collisionLayers))
        {
            // Si golpea algo, calculamos la nueva distancia (con un pequeño margen de 0.1f)
            targetDistance = Mathf.Clamp(hit.distance - 0.1f, minDistance, defaultDistance);
        }

        // Movemos la cámara hijo localmente de forma suave
        Vector3 newLocalPos = cameraDirection * targetDistance;
        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, newLocalPos, Time.deltaTime * collisionSmooth);
    }
}