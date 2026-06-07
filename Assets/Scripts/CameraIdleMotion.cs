using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraIdleMotion : MonoBehaviour
{
    [Header("Respiration (Balancement permanent)")]
    public float swayAmount = 0.15f;
    public float swaySpeed = 0.15f;

    [Header("Secousses (Nervosité des bras)")]
    public float maxShake = 0.05f;
    public float shakeSpeed = 1.5f;

    [Header("Cerveau Humain (L'imprévisibilité)")]
    [Tooltip("Vitesse à laquelle l'opérateur passe du calme au tremblement")]
    public float moodVariationSpeed = 0.2f;

    [Header("Lentille (Micro-Zoom)")]
    public float maxZoomAmount = 0.4f;
    [Tooltip("Vitesse à laquelle la main tourne la bague de zoom")]
    public float zoomWanderSpeed = 0.15f;

    private Vector3 startPos;
    private Quaternion startRot;
    private Camera cam;
    private float startFOV;

    private float offsetSwayX, offsetSwayY;
    private float offsetShakeX, offsetShakeY;
    private float offsetMood;
    private float offsetZoom;

    void Start()
    {
        startPos = transform.position;
        startRot = transform.rotation;

        cam = GetComponent<Camera>();
        if (cam != null) startFOV = cam.fieldOfView;

        offsetSwayX = Random.Range(0f, 1000f);
        offsetSwayY = Random.Range(0f, 1000f);
        offsetShakeX = Random.Range(0f, 1000f);
        offsetShakeY = Random.Range(0f, 1000f);
        offsetMood = Random.Range(0f, 1000f);
        offsetZoom = Random.Range(0f, 1000f);
    }

    void Update()
    {
        float time = Time.time;

        float humanMood = Mathf.PerlinNoise(time * moodVariationSpeed, offsetMood);
        humanMood = Mathf.SmoothStep(0f, 1f, humanMood);

        if (cam != null)
        {
            float zoomFactor = Mathf.PerlinNoise(time * zoomWanderSpeed, offsetZoom);
            cam.fieldOfView = startFOV - (zoomFactor * maxZoomAmount);
        }

        float swayX = (Mathf.PerlinNoise(time * swaySpeed, offsetSwayX) - 0.5f) * swayAmount;
        float swayY = (Mathf.PerlinNoise(time * swaySpeed, offsetSwayY) - 0.5f) * swayAmount;

        float shakeX = (Mathf.PerlinNoise(time * shakeSpeed, offsetShakeX) - 0.5f) * maxShake;
        float shakeY = (Mathf.PerlinNoise(time * shakeSpeed, offsetShakeY) - 0.5f) * maxShake;

        float finalX = swayX + (shakeX * humanMood);
        float finalY = swayY + (shakeY * humanMood);

        transform.rotation = startRot * Quaternion.Euler(finalY, finalX, finalX * 0.2f);
        transform.position = startPos + new Vector3(finalX, finalY, 0f) * 0.15f;
    }
}