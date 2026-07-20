using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StashUI : MonoBehaviour
{
    [Header("Références UI")]
    public Transform stashGridParent;
    public GameObject stashSlotPrefab;

    private List<GameObject> spawnedSlots = new List<GameObject>();

    void OnEnable()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (SafehouseManager.Instance == null) return;

        int currentCapacity = SafehouseManager.Instance.maxStashSlots;
        List<InventorySlot> stashData = SafehouseManager.Instance.stashSlots;

        while (spawnedSlots.Count < currentCapacity)
        {
            GameObject newSlot = Instantiate(stashSlotPrefab, stashGridParent);
            spawnedSlots.Add(newSlot);

            int slotIndex = spawnedSlots.Count - 1;
            Button btn = newSlot.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnSlotClicked(slotIndex));
            }
        }

        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            Transform slotTransform = spawnedSlots[i].transform;
            Image icon = slotTransform.Find("Icon").GetComponent<Image>();
            TextMeshProUGUI amountText = slotTransform.Find("AmountText").GetComponent<TextMeshProUGUI>();

            if (i < stashData.Count && stashData[i].item != null)
            {
                icon.sprite = stashData[i].item.icon;
                icon.enabled = true;

                if (stashData[i].amount > 1)
                {
                    amountText.text = stashData[i].amount.ToString();
                    amountText.enabled = true;
                }
                else
                {
                    amountText.enabled = false;
                }
            }
            else
            {
                icon.sprite = null;
                icon.enabled = false;
                amountText.enabled = false;
            }
        }
    }

    private void OnSlotClicked(int index)
    {
        if (SafehouseManager.Instance == null) return;

        List<InventorySlot> stashData = SafehouseManager.Instance.stashSlots;

        if (index < stashData.Count && stashData[index].item != null)
        {
            ItemData itemToWithdraw = stashData[index].item;
            int amountToWithdraw = stashData[index].amount;
            if (amountToWithdraw <= 0) amountToWithdraw = 1;

            // --- CAS SPÉCIAL : SI L'OBJET CLIQUÉ EST L'ARGENT SALE ---
            if (GameManager.Instance != null && itemToWithdraw == GameManager.Instance.dirtyMoneyItemDef)
            {
                bool success = GameManager.Instance.AddDirtyMoney(amountToWithdraw);
                if (success)
                {
                    stashData[index].item = null;
                    stashData[index].amount = 0;

                    UIManager.Instance.ShowNotification($"{amountToWithdraw}$ récupérés !");
                    RefreshUI();
                }
                return; // On arrête la fonction ici pour ne pas l'ajouter au sac à dos
            }

            // --- CAS NORMAL : LES DROGUES ET LES ARMES ---
            int amountAdded = InventoryManager.Instance.AddItem(itemToWithdraw, amountToWithdraw, true);

            if (amountAdded > 0)
            {
                stashData[index].amount -= amountAdded;

                if (stashData[index].amount <= 0)
                {
                    stashData[index].item = null;
                    stashData[index].amount = 0;
                }

                UIManager.Instance.ShowNotification($"{amountAdded}x {itemToWithdraw.itemName} récupéré !");
                RefreshUI();
                InventoryUI playerUI = FindObjectOfType<InventoryUI>();
                if (playerUI != null) playerUI.RefreshUI();
            }
            else
            {
                UIManager.Instance.ShowNotification("<color=red>Votre sac à dos est plein !</color>");
            }
        }
    }
}