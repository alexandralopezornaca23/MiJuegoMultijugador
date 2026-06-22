using UnityEngine;

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
            transform.Rotate(0, 180, 0); // Voltear para que mire hacia la cámara
        }
    }
}
