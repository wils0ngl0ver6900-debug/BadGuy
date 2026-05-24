using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    public Transform gridParent;
    public GameObject slotPrefab;

    [Header("Images de Cadres Personnalisés")]
    public Sprite cadreBasique;
    public Sprite cadrePeuCourant;
    public Sprite cadreRare;
    public Sprite cadreLegendaire;

    void OnEnable()
    {
        RefreshUI();
    }

    void OnDisable()
    {
        if (UIManager.Instance != null) UIManager.Instance.HideTooltip();
    }

    public void RefreshUI()
    {
        if (gridParent == null) return;

        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }

        if (InventoryManager.Instance == null) return;

        foreach (ItemData item in InventoryManager.Instance.items)
        {
            if (slotPrefab == null) continue;

            GameObject newSlot = Instantiate(slotPrefab, gridParent);

            // 1. Mise à jour de l'icône de l'objet
            Transform iconTransform = newSlot.transform.Find("Icone");
            if (iconTransform != null)
            {
                Image iconImage = iconTransform.GetComponent<Image>();
                if (iconImage != null && item != null)
                {
                    iconImage.sprite = item.icon;
                    iconImage.color = Color.white; // FORCE LA VISIBILITÉ ICI AUSSI
                    iconImage.enabled = true;
                }
            }

            // 2. Mise à jour du texte
            TextMeshProUGUI textMesh = newSlot.GetComponentInChildren<TextMeshProUGUI>();
            if (textMesh != null && item != null) textMesh.text = item.itemName;

            // 3. Mise à jour du cadre de rareté
            Transform rarityTransform = newSlot.transform.Find("Bordure_Rarete");
            if (rarityTransform != null)
            {
                Image rarityImage = rarityTransform.GetComponent<Image>();
                if (rarityImage != null && item != null)
                {
                    rarityImage.enabled = true;
                    switch (item.rarity)
                    {
                        case ItemData.Rarity.Basique:
                            rarityImage.sprite = cadreBasique;
                            rarityImage.color = Color.green;
                            break;
                        case ItemData.Rarity.PeuCourant:
                            rarityImage.sprite = cadrePeuCourant;
                            rarityImage.color = new Color(0f, 0.6f, 1f);
                            break;
                        case ItemData.Rarity.Rare:
                            rarityImage.sprite = cadreRare;
                            rarityImage.color = new Color(0.6f, 0f, 1f);
                            break;
                        case ItemData.Rarity.Legendaire:
                            rarityImage.sprite = cadreLegendaire;
                            rarityImage.color = new Color(1f, 0.8f, 0f);
                            break;
                    }
                }
            }

            // 4. Drag and Drop + Configuration du visuel
            InventoryDragDrop dragScript = newSlot.GetComponent<InventoryDragDrop>();
            if (dragScript == null) dragScript = newSlot.AddComponent<InventoryDragDrop>();

            dragScript.itemReference = item;
            dragScript.SetVisualMode(false); // <-- ON LUI DIT QU'IL EST DANS L'INVENTAIRE
        }
    }
}