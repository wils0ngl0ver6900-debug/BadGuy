using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;

    public InventorySlot(ItemData item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }
}

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Configuration")]
    public int maxSlots = 10;
    public List<InventorySlot> slots = new List<InventorySlot>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public int AddItem(ItemData itemToAdd, int amountToAdd = 1, bool silent = false)
    {
        if (itemToAdd == null || amountToAdd <= 0) return 0;
        int originalAmount = amountToAdd;

        if (itemToAdd.isStackable)
        {
            foreach (var slot in slots)
            {
                if (slot.item == itemToAdd && slot.amount < itemToAdd.maxStackSize)
                {
                    int spaceInSlot = itemToAdd.maxStackSize - slot.amount;
                    int amountToPut = Mathf.Min(spaceInSlot, amountToAdd);

                    slot.amount += amountToPut;
                    amountToAdd -= amountToPut;

                    if (amountToAdd <= 0) break;
                }
            }
        }

        while (amountToAdd > 0 && slots.Count < maxSlots)
        {
            int amountForNewSlot = itemToAdd.isStackable ? Mathf.Min(amountToAdd, itemToAdd.maxStackSize) : 1;
            slots.Add(new InventorySlot(itemToAdd, amountForNewSlot));
            amountToAdd -= amountForNewSlot;
        }

        int amountActuallyAdded = originalAmount - amountToAdd;

        if (amountActuallyAdded > 0 && !silent && UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"Obtenu : {itemToAdd.itemName} {(amountActuallyAdded > 1 ? "(x" + amountActuallyAdded + ")" : "")}");
        }
        else if (amountToAdd > 0 && !silent && UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("Inventaire plein !");
        }

        return amountActuallyAdded;
    }

    public void RemoveItem(ItemData itemToRemove, int amountToRemove = 1)
    {
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].item == itemToRemove)
            {
                if (slots[i].amount > amountToRemove)
                {
                    slots[i].amount -= amountToRemove;
                    return;
                }
                else
                {
                    amountToRemove -= slots[i].amount;
                    slots.RemoveAt(i);
                    if (amountToRemove <= 0) return;
                }
            }
        }
    }

    public int GetTotalItemAmount(ItemData item)
    {
        int total = 0;
        foreach (var slot in slots) if (slot.item == item) total += slot.amount;
        return total;
    }
}