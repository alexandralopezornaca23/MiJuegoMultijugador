using UnityEngine;

// Variante del Billboard que mantiene la orientacion del objeto alineada con la rotacion
// completa de la camara, incluyendo la inclinacion vertical. Util para canvas en espacio 3D.

public class LookAtCamera : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main != null)
            transform.LookAt(
                transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up);
    }
}