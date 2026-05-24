using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance; // Permet aux autres scripts de parler à la caméra facilement

    [Header("Suivi Basique")]
    public Transform target;
    public float smoothSpeed = 0.125f;
    public Vector3 offset;

    [Header("Effets Visuels (Drogue) 😵")]
    public bool isBadTrip = false;
    public float shakeIntensity = 0.2f; // Force du tremblement

    private Camera cam;
    private float originalFOV;

    void Awake()
    {
        if (Instance == null) Instance = this;

        cam = GetComponent<Camera>();
        if (cam != null) originalFOV = cam.fieldOfView;
    }

    void Start()
    {
        if (target != null)
        {
            offset = transform.position - target.position;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. Position de base
        Vector3 desiredPosition = target.position + offset;

        // 2. EFFET TREMBLEMENT (SHAKE)
        if (isBadTrip)
        {
            desiredPosition += new Vector3(
                Random.Range(-shakeIntensity, shakeIntensity),
                Random.Range(-shakeIntensity, shakeIntensity),
                Random.Range(-shakeIntensity, shakeIntensity)
            );
        }

        // 3. Déplacement fluide
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }

    // Fonction pour allumer/éteindre l'effet de tremblement depuis PlayerController
    public void SetBadTrip(bool state)
    {
        isBadTrip = state;
    }
}