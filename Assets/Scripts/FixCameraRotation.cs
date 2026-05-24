using UnityEngine;

public class FixCameraRotation : MonoBehaviour
{
    private Quaternion initialRotation;

    void Start()
    {
        // On enregistre sa rotation de départ (X=90, Y=0, Z=0)
        initialRotation = Quaternion.Euler(90f, 180f, 0f);
    }

    void LateUpdate()
    {
        // À chaque frame, on force la caméra à garder cette rotation, peu importe comment le joueur tourne !
        transform.rotation = initialRotation;
    }
}