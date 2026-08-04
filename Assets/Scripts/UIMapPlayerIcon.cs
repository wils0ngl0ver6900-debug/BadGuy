using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UIMapPlayerIcon : MonoBehaviour
{
    [Header("Références")]
    public Transform playerTransform;
    public RectTransform mapRect;

    [Header("Calibration de la Carte 3D")]
    public Vector2 worldBottomLeft = new Vector2(-500f, -500f);
    public Vector2 worldTopRight = new Vector2(500f, 500f);

    private RectTransform iconRect;

    void Start()
    {
        iconRect = GetComponent<RectTransform>();

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
    }

    void Update()
    {
        if (playerTransform == null || mapRect == null) return;

        float normalizedX = Mathf.InverseLerp(worldBottomLeft.x, worldTopRight.x, playerTransform.position.x);
        float normalizedY = Mathf.InverseLerp(worldBottomLeft.y, worldTopRight.y, playerTransform.position.z);

        float mapWidth = mapRect.rect.width;
        float mapHeight = mapRect.rect.height;

        // Position de base
        Vector2 centerOffset = new Vector2(normalizedX - 0.5f, normalizedY - 0.5f);

        // --- NOUVEAU : Prise en compte de la rotation de la carte (cameraYaw) ---
        float currentYaw = 0f;
        if (MapApp.Instance != null && MapApp.Instance.satelliteCamera != null)
        {
            currentYaw = MapApp.Instance.satelliteCamera.cameraYaw;
        }

        // On orbite les coordonnées 2D dans le sens inverse de la caméra pour s'aligner sur l'image
        Vector3 rotatedOffset = Quaternion.Euler(0, 0, -currentYaw) * new Vector3(centerOffset.x, centerOffset.y, 0f);

        iconRect.anchoredPosition = new Vector2(
            rotatedOffset.x * mapWidth,
            rotatedOffset.y * mapHeight
        );

        // On fait tourner la flèche dans la direction du joueur, en prenant en compte l'angle de l'écran
        Vector3 playerEuler = playerTransform.eulerAngles;
        iconRect.localEulerAngles = new Vector3(0, 0, -playerEuler.y - currentYaw);
    }
}