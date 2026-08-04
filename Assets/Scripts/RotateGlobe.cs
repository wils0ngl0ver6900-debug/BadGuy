using UnityEngine;

public class RotateGlobe : MonoBehaviour
{
    [Tooltip("Vitesse de rotation de la planète")]
    public float rotationSpeed = 10f;

    [Tooltip("Axe de rotation (par défaut sur l'axe Y, de gauche à droite)")]
    public Vector3 rotationAxis = Vector3.up;

    void Update()
    {
        // On fait tourner la sphère sur elle-même en continu
        // On utilise Space.World pour éviter qu'elle tourne bizarrement si elle est inclinée
        transform.Rotate(rotationAxis, rotationSpeed * Time.unscaledDeltaTime, Space.World);
    }
}