using UnityEngine;

// Hace que el objeto al que se anade siempre mire hacia la camara principal.
// Se usa en iconos y elementos visuales 3D para que sean siempre legibles.

public class Billboard : MonoBehaviour
{
    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCam != null)
        {
            transform.LookAt(mainCam.transform);
            transform.Rotate(0, 180, 0); // LookAt apunta el eje Z hacia la camara, el giro de 180 invierte la cara para que el contenido sea visible
        }
    }
}