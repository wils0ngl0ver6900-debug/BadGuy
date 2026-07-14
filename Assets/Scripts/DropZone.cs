using UnityEngine;
using UnityEngine.EventSystems;

public class DropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public enum ZoneType { Trash, HotbarSlot }
    public ZoneType type = ZoneType.Trash;

    [Header("UI au survol")]
    public GameObject textToDisplay;

    public void OnDrop(PointerEventData eventData)
    {
        InventoryDragDrop draggedItem = eventData.pointerDrag?.GetComponent<InventoryDragDrop>();
        if (draggedItem != null)
        {
            if (type == ZoneType.Trash)
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowNotification($"Objet jeté : {draggedItem.itemReference.itemName}");
                }

                if (draggedItem.originalHotbarSlot != null)
                {
                    draggedItem.originalHotbarSlot.itemInSlot = null;
                }

                // CORRECTION : On déduit la pile jetée du total global du joueur
                if (GameManager.Instance != null && draggedItem.itemReference == GameManager.Instance.dirtyMoneyItemDef)
                {
                    GameManager.Instance.dirtyMoney -= draggedItem.slotReference.amount;
                    if (GameManager.Instance.dirtyMoney < 0) GameManager.Instance.dirtyMoney = 0;
                    if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
                }

                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.RemoveItem(draggedItem.itemReference, draggedItem.slotReference.amount);
                }

                InventoryUI inventoryUI = GameObject.FindObjectOfType<InventoryUI>();
                if (inventoryUI != null)
                {
                    inventoryUI.RefreshUI();
                }

                HideText();
                Destroy(draggedItem.gameObject);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (textToDisplay != null) textToDisplay.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideText();
    }

    private void HideText()
    {
        if (textToDisplay != null) textToDisplay.SetActive(false);
    }

    void OnDisable()
    {
        HideText();
    }
}