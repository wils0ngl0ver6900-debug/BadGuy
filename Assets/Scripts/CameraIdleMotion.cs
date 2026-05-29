using UnityEngine;

public class CameraIdleMotion : MonoBehaviour
{
    [Header("Amplitude du mouvement")]
    public float positionAmplitude = 0.5f; // Déplacement léger
    public float rotationAmplitude = 1.0f; // Rotation légère

    [Header("Vitesse du mouvement")]
    public float speed = 0.2f; // Très lent pour ne pas gêner

    private Vector3 startPos;
    private Quaternion startRot;

    void Start()
    {
        startPos = transform.position;
        startRot = transform.rotation;
    }

    void Update()
    {
        // Calcul mathématique pour un mouvement fluide (Sinus)
        float time = Time.time * 0.15f;

        // Mouvement de position
        float xOffset = Mathf.Sin(time) * positionAmplitude;
        float yOffset = Mathf.Cos(time * 0.8f) * positionAmplitude;

        transform.position = startPos + new Vector3(xOffset, yOffset, 0);

        // Légère rotation pour donner du "détail" à la perspective
        float rotX = Mathf.Cos(time * 0.3f) * (rotationAmplitude * 0.5f);
        float rotY = Mathf.Sin(time * 0.3f) * (rotationAmplitude * 0.5f); 

        transform.rotation = startRot * Quaternion.Euler(rotX, rotY, 0);
    }
}