using UnityEngine;
using UnityEngine.EventSystems;

public class HotbarSlot : MonoBehaviour, IDropHandler
{
    public int slotNumber;
    public ItemData itemInSlot;

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;
        InventoryDragDrop draggedItem = eventData.pointerDrag.GetComponent<InventoryDragDrop>();
        if (draggedItem == null) return;

        if (draggedItem.itemReference != null && !draggedItem.itemReference.isEquippable)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"{draggedItem.itemReference.itemName} ne peut pas être équipé !");
            return;
        }

        if (itemInSlot != null && draggedItem.originalHotbarSlot != this)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Cette case est déjà occupée !");
            return;
        }

        itemInSlot = draggedItem.itemReference;
        draggedItem.transform.SetParent(transform);

        // --- LA MAGIE DU CENTRAGE PARFAIT EST ICI ---
        RectTransform rect = draggedItem.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localPosition = Vector3.zero; // Force l'objet exactement au milieu du parent
        }
        draggedItem.transform.localScale = Vector3.one;

        // --- ON APPLIQUE LE MODE VISUEL DE LA HOTBAR ---
        draggedItem.SetVisualMode(true);

        if (draggedItem.originalHotbarSlot != null && draggedItem.originalHotbarSlot != this)
        {
            draggedItem.originalHotbarSlot.itemInSlot = null;
        }
        draggedItem.originalHotbarSlot = this;

        if (InventoryManager.Instance.items.Contains(draggedItem.itemReference))
        {
            InventoryManager.Instance.RemoveItem(draggedItem.itemReference);
            FindObjectOfType<InventoryUI>().RefreshUI();
        }

        if (HotbarManager.Instance != null && HotbarManager.Instance.currentSelectedIndex == slotNumber)
            HotbarManager.Instance.SelectSlot(slotNumber);
    }
}