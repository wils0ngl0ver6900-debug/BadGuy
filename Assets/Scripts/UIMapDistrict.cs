using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Image))]
public class UIMapDistrict : MonoBehaviour
{
    [Header("Liaison 3D -> 2D")]
    [Tooltip("Glisse ici le VRAI quartier physique de ta scène 3D")]
    public DistrictZone targetDistrict;

    [Header("Couleurs de Faction")]
    public Color playerColor = new Color(0f, 1f, 0f, 0.5f); // Vert semi-transparent
    public Color enemyColor = new Color(1f, 0f, 0f, 0.5f);  // Rouge semi-transparent
    public Color neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Gris semi-transparent

    [Header("Affichage (Optionnel)")]
    public TextMeshProUGUI infoText;

    private Image districtImage;

    void Start()
    {
        districtImage = GetComponent<Image>();
    }

    void Update()
    {
        // Sécurité : On vérifie que tout est bien lié
        if (targetDistrict == null || districtImage == null || TerritoryManager.Instance == null) return;

        // 1. On cherche les informations de CE quartier précis dans la base de données de ton TerritoryManager
        var districtData = TerritoryManager.Instance.cityDistricts.Find(x => x.districtName == targetDistrict.districtName);

        if (districtData == null) return;

        // 2. Mise à jour de la couleur selon l'état de la guerre de territoire
        if (districtData.playerControlPercentage >= 100)
        {
            // Le joueur a totalement pris le contrôle
            districtImage.color = playerColor;
        }
        else if (districtData.rivalGang != TerritoryManager.Faction.None && !districtData.rivalGangDefeated)
        {
            // Le gang ennemi est toujours vivant et contrôle la zone
            districtImage.color = enemyColor;
        }
        else
        {
            // Zone neutre ou en cours d'acquisition
            districtImage.color = neutralColor;
        }

        // 3. Mise à jour du texte
        if (infoText != null)
        {
            infoText.text = $"{districtData.districtName}\nContrôle : {districtData.playerControlPercentage}%";
        }
    }
}