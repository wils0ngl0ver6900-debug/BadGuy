using UnityEngine;
using UnityEngine.EventSystems;

// On ajoute les interfaces nécessaires pour le survol et le dépôt
public class DropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public enum ZoneType { Trash, HotbarSlot }
    public ZoneType type = ZoneType.Trash;

    [Header("UI au survol")]
    public GameObject textToDisplay; // Glisse ton objet texte ici dans l'Inspector

    public void OnDrop(PointerEventData eventData)
    {
        // On vérifie si l'objet qu'on lâche possède bien le script de Drag & Drop
        InventoryDragDrop draggedItem = eventData.pointerDrag?.GetComponent<InventoryDragDrop>();

        if (draggedItem != null)
        {
            if (type == ZoneType.Trash)
            {
                // Notification via le UIManager
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowNotification($"Objet jeté : {draggedItem.itemReference.itemName}");
                }

                // Si l'objet venait de la hotbar, on vide son ancien slot
                if (draggedItem.originalHotbarSlot != null)
                {
                    draggedItem.originalHotbarSlot.itemInSlot = null;
                }

                // On le retire du gestionnaire d'inventaire
                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.RemoveItem(draggedItem.itemReference);
                }

                // CORRECTION ICI : Remplacement par l'ancienne méthode compatible avec ta version d'Unity
                InventoryUI inventoryUI = GameObject.FindObjectOfType<InventoryUI>();
                if (inventoryUI != null)
                {
                    inventoryUI.RefreshUI();
                }

                // On cache le texte avant de détruire l'objet pour éviter qu'il reste bloqué à l'écran
                HideText();

                // On détruit l'icône de l'UI
                Destroy(draggedItem.gameObject);
            }
        }
    }

    // S'active automatiquement quand la souris entre dans la zone du bouton
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (textToDisplay != null)
        {
            textToDisplay.SetActive(true); // Affiche le texte
        }
    }

    // S'active automatiquement quand la souris sort de la zone du bouton
    public void OnPointerExit(PointerEventData eventData)
    {
        HideText();
    }

    private void HideText()
    {
        if (textToDisplay != null)
        {
            textToDisplay.SetActive(false); // Cache le texte
        }
    }

    // Sécurité au cas où l'inventaire se ferme brusquement pendant le survol
    void OnDisable()
    {
        HideText();
    }
}