using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    private RectTransform rectTransform;
    private Image crosshairImage;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        crosshairImage = GetComponent<Image>();
    }

    void Update()
    {
        // 1. Le viseur suit la position exacte de ta souris (même si elle est invisible)
        rectTransform.position = Input.mousePosition;

        // 2. Vérifications pour savoir si on doit l'afficher
        bool isPlaying = !Cursor.visible; // Vrai si on n'est pas dans un menu
        bool hasWeapon = false;

        // On regarde ce que le joueur tient en main
        if (HotbarManager.Instance != null)
        {
            ItemData equipped = HotbarManager.Instance.GetEquippedItem();
            if (equipped != null && equipped.isWeapon)
            {
                hasWeapon = true; // C'est une arme !
            }
        }

        // On affiche l'image du viseur SEULEMENT en jeu ET avec une arme
        crosshairImage.enabled = isPlaying && hasWeapon;
    }
}