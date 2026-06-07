using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UIMapPlayerIcon : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Glisse le modèle 3D du joueur ici")]
    public Transform playerTransform;
    [Tooltip("Glisse l'image du fond de ta carte (FondCarte) ici")]
    public RectTransform mapRect;

    [Header("Calibration de la Carte 3D")]
    [Tooltip("Coordonnées (X, Z) du point tout en BAS À GAUCHE de ta ville 3D")]
    public Vector2 worldBottomLeft = new Vector2(-500f, -500f);

    [Tooltip("Coordonnées (X, Z) du point tout en HAUT À DROITE de ta ville 3D")]
    public Vector2 worldTopRight = new Vector2(500f, 500f);

    private RectTransform iconRect;

    void Start()
    {
        iconRect = GetComponent<RectTransform>();

        // Si tu as oublié de glisser le joueur, le script le cherche tout seul avec son tag
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
    }

    void Update()
    {
        if (playerTransform == null || mapRect == null) return;

        // 1. On calcule où se trouve le joueur en pourcentage (de 0.0 à 1.0) sur les axes X et Z
        float normalizedX = Mathf.InverseLerp(worldBottomLeft.x, worldTopRight.x, playerTransform.position.x);

        // L'axe Z en 3D (Profondeur) correspond à l'axe Y en 2D (Hauteur sur l'écran)
        float normalizedY = Mathf.InverseLerp(worldBottomLeft.y, worldTopRight.y, playerTransform.position.z);

        // 2. On traduit ce pourcentage sur la taille de ton image de carte
        float mapWidth = mapRect.rect.width;
        float mapHeight = mapRect.rect.height;

        // On ajuste le centre (car l'ancre d'un RectTransform est généralement au milieu à 0.5, 0.5)
        iconRect.anchoredPosition = new Vector2(
            (normalizedX - 0.5f) * mapWidth,
            (normalizedY - 0.5f) * mapHeight
        );

        // 3. Optionnel : On fait tourner la flèche dans la direction du joueur
        // L'axe Y (rotation 3D du joueur) devient l'axe Z (rotation 2D de l'icône)
        Vector3 playerEuler = playerTransform.eulerAngles;
        iconRect.localEulerAngles = new Vector3(0, 0, -playerEuler.y);
    }
}