using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(RectTransform))]
public class UIMapDistrict : MonoBehaviour
{
    [Header("Liaison 3D -> 2D")]
    [Tooltip("Glisse ici le VRAI quartier physique de ta scène 3D")]
    public DistrictZone targetDistrict;

    [Header("Couleurs de Faction")]
    public Color playerColor = new Color(0f, 1f, 0f, 0.5f); // Vert semi-transparent
    public Color enemyColor = new Color(1f, 0f, 0f, 0.5f);  // Rouge semi-transparent
    public Color neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Gris semi-transparent

    [Header("Affichage UI")]
    public TextMeshProUGUI infoText;

    [Tooltip("Glisse l'image du fond de ta carte (FondCarte) ici, comme pour le PlayerIcon")]
    public RectTransform mapRect;

    private Image districtImage;
    private RectTransform rectTransform;
    private Collider districtCollider;

    void Start()
    {
        districtImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();

        // On récupère le collider du quartier 3D pour connaître sa taille réelle
        if (targetDistrict != null)
        {
            districtCollider = targetDistrict.GetComponent<Collider>();
            if (districtCollider == null)
            {
                Debug.LogWarning($"[UIMapDistrict] Le quartier {targetDistrict.name} n'a pas de Collider ! Impossible de calculer sa taille sur la carte.");
            }
        }
    }

    void Update()
    {
        if (targetDistrict == null || districtImage == null || TerritoryManager.Instance == null) return;

        // ========================================================
        // 1. GESTION DES COULEURS (Territoire)
        // ========================================================
        var districtData = TerritoryManager.Instance.cityDistricts.Find(x => x.districtName == targetDistrict.districtName);

        if (districtData != null)
        {
            if (districtData.playerControlPercentage >= 100)
            {
                districtImage.color = playerColor;
            }
            else if (districtData.rivalGang != TerritoryManager.Faction.None && !districtData.rivalGangDefeated)
            {
                districtImage.color = enemyColor;
            }
            else
            {
                districtImage.color = neutralColor;
            }

            if (infoText != null)
            {
                infoText.text = $"{districtData.districtName}\nContrôle : {districtData.playerControlPercentage}%";
            }
        }

        // ========================================================
        // 2. GESTION DU POSITIONNEMENT ET DE LA TAILLE EN TEMPS RÉEL
        // ========================================================
        if (districtCollider != null && mapRect != null && MapApp.Instance != null && MapApp.Instance.satelliteCamera != null)
        {
            // On récupère automatiquement les infos de la caméra satellite
            Vector2 worldBottomLeft = MapApp.Instance.satelliteCamera.worldBottomLeft;
            Vector2 worldTopRight = MapApp.Instance.satelliteCamera.worldTopRight;
            float currentYaw = MapApp.Instance.satelliteCamera.cameraYaw;

            // On récupère la boîte englobante (Bounds) du quartier dans le monde 3D
            Bounds bounds = districtCollider.bounds;

            // --- CALCUL DE LA POSITION DU CENTRE ---
            float normalizedX = Mathf.InverseLerp(worldBottomLeft.x, worldTopRight.x, bounds.center.x);
            float normalizedY = Mathf.InverseLerp(worldBottomLeft.y, worldTopRight.y, bounds.center.z);

            float mapWidth = mapRect.rect.width;
            float mapHeight = mapRect.rect.height;

            Vector2 centerOffset = new Vector2(normalizedX - 0.5f, normalizedY - 0.5f);

            // Orbite autour du centre si la carte est tournée (ex: 90 degrés)
            Vector3 rotatedOffset = Quaternion.Euler(0, 0, -currentYaw) * new Vector3(centerOffset.x, centerOffset.y, 0f);

            rectTransform.anchoredPosition = new Vector2(
                rotatedOffset.x * mapWidth,
                rotatedOffset.y * mapHeight
            );

            // --- CALCUL DE LA TAILLE (Largeur / Hauteur) ---
            float widthRatio = bounds.size.x / (worldTopRight.x - worldBottomLeft.x);
            float heightRatio = bounds.size.z / (worldTopRight.y - worldBottomLeft.y);

            rectTransform.sizeDelta = new Vector2(widthRatio * mapWidth, heightRatio * mapHeight);

            // --- ROTATION DU RECTANGLE ---
            // On tourne le rectangle sur lui-même pour qu'il s'aligne avec la caméra
            rectTransform.localEulerAngles = new Vector3(0, 0, -currentYaw);
        }
    }
}