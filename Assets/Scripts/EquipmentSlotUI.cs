using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
    public int slotIndex;
    public Image itemIcon;
    public Sprite defaultIcon;

    public void RefreshSlot(ItemData item)
    {
        if (item != null)
        {
            itemIcon.enabled = true; // On active l'image
            itemIcon.sprite = item.icon;
            itemIcon.color = Color.white;
        }
        else
        {
            if (defaultIcon != null)
            {
                // Si on a mis une silhouette, on l'affiche
                itemIcon.enabled = true;
                itemIcon.sprite = defaultIcon;
                itemIcon.color = new Color(1f, 1f, 1f, 0.3f);
            }
            else
            {
                // S'il n'y a NI objet, NI silhouette, on cache l'icône complètement !
                itemIcon.enabled = false;
            }
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null)
        {
            InventoryDragDrop draggedItem = eventData.pointerDrag.GetComponent<InventoryDragDrop>();

            if (draggedItem != null && draggedItem.itemReference != null)
            {
                if (draggedItem.itemReference.isClothing)
                {
                    if ((int)draggedItem.itemReference.clothingSlot == slotIndex)
                    {
                        if (EquipmentManager.Instance != null)
                        {
                            EquipmentManager.Instance.Equip(draggedItem.itemReference);
                            if (UIManager.Instance != null) UIManager.Instance.HideTooltip();
                            Destroy(draggedItem.gameObject);
                        }
                    }
                    else
                    {
                        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Ce vêtement ne va pas à cet emplacement !");
                    }
                }
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (EquipmentManager.Instance != null && EquipmentManager.Instance.currentEquipment[slotIndex] != null)
            {
                EquipmentManager.Instance.Unequip(slotIndex);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EquipmentManager.Instance != null && EquipmentManager.Instance.currentEquipment[slotIndex] != null)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowTooltip(EquipmentManager.Instance.currentEquipment[slotIndex]);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (UIManager.Instance != null) UIManager.Instance.HideTooltip();
    }
}