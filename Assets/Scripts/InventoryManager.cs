using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Configuration")]
    public int maxSlots = 10; // Limite d'inventaire moderne
    public List<ItemData> items = new List<ItemData>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public bool AddItem(ItemData item)
    {
        if (items.Count >= maxSlots)
        {
            UIManager.Instance.ShowNotification("Inventaire plein ! Impossible de porter plus d'objets.");
            return false;
        }

        items.Add(item);
        UIManager.Instance.ShowNotification($"Objet obtenu : {item.itemName}");
        // Ici tu pourras appeler une mise à jour visuelle de ton inventaire UI
        return true;
    }

    public void RemoveItem(ItemData item)
    {
        if (items.Contains(item))
        {
            items.Remove(item);
        }
    }
}