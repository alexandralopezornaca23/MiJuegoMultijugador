using UnityEngine;

// Controlador de camara en tercera persona. Sigue al jugador, rota con el raton
// y detecta colisiones con el escenario para no atravesar paredes.

public class CameraController : MonoBehaviour
{
    [Header("Configuracion de Sensibilidad")]
    public float sensitivity = 5f;
    public Vector2 cameraLimit = new Vector2(-45, 40);

    [Header("Configuracion de Colision")]
    [Tooltip("La camara real (el objeto hijo)")]
    public Transform cameraTransform;
    [Tooltip("Capas que la camara no debe atravesar")]
    public LayerMask collisionLayers;
    [Tooltip("Que tan cerca puede estar la camara del pivote")]
    public float minDistance = 0.5f;
    [Tooltip("Suavizado de la colision")]
    public float collisionSmooth = 15f;
    [Tooltip("Radio del SphereCast para detectar colisiones con mas margen")]
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

        if (cameraTransform == null)
            cameraTransform = GetComponentInChildren<Camera>().transform;

        // Guardamos la posicion original de la camara respecto al pivote para usarla como referencia
        cameraDirection = cameraTransform.localPosition.normalized;
        defaultDistance = cameraTransform.localPosition.magnitude;
    }

    void Update()
    {
        if (player == null) return;

        // El pivote de la camara sigue la posicion del jugador con un offset en Y
        transform.position = player.position + new Vector3(0, offsetDistanceY, 0);

        // Acumulamos la rotacion del raton y limitamos el angulo vertical
        mouseX += Input.GetAxis("Mouse X") * sensitivity;
        mouseY += Input.GetAxis("Mouse Y") * sensitivity;
        mouseY = Mathf.Clamp(mouseY, cameraLimit.x, cameraLimit.y);

        transform.rotation = Quaternion.Euler(-mouseY, mouseX, 0);

        HandleCameraCollision();
    }

    void HandleCameraCollision()
    {
        Vector3 targetCameraPos = transform.TransformPoint(cameraDirection * defaultDistance);

        float targetDistance = defaultDistance;

        // SphereCast desde el pivote hacia la posicion ideal de la camara.
        // Usamos esfera en lugar de rayo para evitar que las esquinas atraviesen la geometria.
        if (Physics.SphereCast(transform.position, detectionRadius, targetCameraPos - transform.position,
            out RaycastHit hit, defaultDistance, collisionLayers))
        {
            // Si hay colision acercamos la camara al pivote con un pequeno margen de separacion
            targetDistance = Mathf.Clamp(hit.distance - 0.1f, minDistance, defaultDistance);
        }

        // Movemos la camara de forma suave para evitar saltos bruscos al detectar colisiones
        Vector3 newLocalPos = cameraDirection * targetDistance;
        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, newLocalPos, Time.deltaTime * collisionSmooth);
    }
}