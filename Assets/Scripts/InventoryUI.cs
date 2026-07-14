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

    void OnEnable() { RefreshUI(); }
    void OnDisable() { if (UIManager.Instance != null) UIManager.Instance.HideTooltip(); }

    public void RefreshUI()
    {
        if (gridParent == null || InventoryManager.Instance == null) return;
        foreach (Transform child in gridParent) Destroy(child.gameObject);

        foreach (InventorySlot slot in InventoryManager.Instance.slots)
        {
            if (slotPrefab == null || slot.item == null) continue;

            GameObject newSlot = Instantiate(slotPrefab, gridParent);

            Transform iconTransform = newSlot.transform.Find("Icone");
            if (iconTransform != null)
            {
                Image iconImage = iconTransform.GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.sprite = slot.item.icon;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                }
            }

            TextMeshProUGUI textMesh = newSlot.GetComponentInChildren<TextMeshProUGUI>();
            if (textMesh != null)
            {
                textMesh.text = slot.amount > 1 ? $"{slot.item.itemName} (x{slot.amount})" : slot.item.itemName;
            }

            Transform rarityTransform = newSlot.transform.Find("Bordure_Rarete");
            if (rarityTransform != null)
            {
                Image rarityImage = rarityTransform.GetComponent<Image>();
                if (rarityImage != null)
                {
                    rarityImage.enabled = true;
                    switch (slot.item.rarity)
                    {
                        case ItemData.Rarity.Basique: rarityImage.sprite = cadreBasique; rarityImage.color = Color.green; break;
                        case ItemData.Rarity.PeuCourant: rarityImage.sprite = cadrePeuCourant; rarityImage.color = new Color(0f, 0.6f, 1f); break;
                        case ItemData.Rarity.Rare: rarityImage.sprite = cadreRare; rarityImage.color = new Color(0.6f, 0f, 1f); break;
                        case ItemData.Rarity.Legendaire: rarityImage.sprite = cadreLegendaire; rarityImage.color = new Color(1f, 0.8f, 0f); break;
                    }
                }
            }

            InventoryDragDrop dragScript = newSlot.GetComponent<InventoryDragDrop>();
            if (dragScript == null) dragScript = newSlot.AddComponent<InventoryDragDrop>();

            dragScript.slotReference = slot;
            dragScript.SetVisualMode(false);
        }
    }
}