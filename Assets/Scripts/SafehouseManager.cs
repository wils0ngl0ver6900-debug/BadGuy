using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SafehouseManager : MonoBehaviour
{
    public static SafehouseManager Instance;

    [Header("Stockage de la Planque 📦")]
    public int storedDirtyMoney = 0;
    public List<InventorySlot> storedIllegalItems = new List<InventorySlot>();

    [Header("Interface UI")]
    public GameObject safehousePanel;
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI itemsText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        CloseSafehouse();
    }

    public void OpenSafehouse()
    {
        safehousePanel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        UpdateUI();
    }

    public void CloseSafehouse()
    {
        safehousePanel.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void UpdateUI()
    {
        if (moneyText != null) moneyText.text = $"Argent Sale : {storedDirtyMoney}$";

        int totalItems = 0;
        foreach (var slot in storedIllegalItems) totalItems += slot.amount;
        if (itemsText != null) itemsText.text = $"Objets Illégaux : {totalItems}";
    }

    public void DepositDirtyMoney()
    {
        int amount = GameManager.Instance.dirtyMoney;
        if (amount > 0)
        {
            storedDirtyMoney += amount;
            GameManager.Instance.dirtyMoney = 0;

            GameManager.Instance.SyncDirtyMoneyItem();
            UpdateUI();
            UIManager.Instance.ShowNotification("Argent sale sécurisé dans le coffre !");
            UIManager.Instance.UpdateHUD();
        }
        else
        {
            UIManager.Instance.ShowNotification("Pas d'argent sale sur vous.");
        }
    }

    public void WithdrawDirtyMoney()
    {
        if (storedDirtyMoney > 0)
        {
            bool success = GameManager.Instance.AddDirtyMoney(storedDirtyMoney);
            if (success)
            {
                storedDirtyMoney = 0;
                UpdateUI();
                UIManager.Instance.ShowNotification("Argent sale récupéré !");
            }
        }
    }

    public void DepositIllegalItems()
    {
        int count = 0;
        for (int i = InventoryManager.Instance.slots.Count - 1; i >= 0; i--)
        {
            var slot = InventoryManager.Instance.slots[i];
            if (slot.item != null && slot.item.isIllegal && slot.item != GameManager.Instance.dirtyMoneyItemDef)
            {
                storedIllegalItems.Add(new InventorySlot(slot.item, slot.amount));
                InventoryManager.Instance.slots.RemoveAt(i);
                count += slot.amount;
            }
        }

        UpdateUI();
        InventoryUI ui = FindObjectOfType<InventoryUI>();
        if (ui != null) ui.RefreshUI();

        if (count > 0) UIManager.Instance.ShowNotification($"{count} objets illégaux sécurisés !");
        else UIManager.Instance.ShowNotification("Aucun objet illégal dans votre sac à dos.");
    }

    public void WithdrawIllegalItems()
    {
        if (storedIllegalItems.Count == 0) return;

        int count = 0;
        for (int i = storedIllegalItems.Count - 1; i >= 0; i--)
        {
            var slot = storedIllegalItems[i];
            int added = InventoryManager.Instance.AddItem(slot.item, slot.amount, true);
            count += added;
            slot.amount -= added;

            if (slot.amount <= 0) storedIllegalItems.RemoveAt(i);
        }

        UpdateUI();
        InventoryUI ui = FindObjectOfType<InventoryUI>();
        if (ui != null) ui.RefreshUI();

        if (count > 0) UIManager.Instance.ShowNotification($"{count} objets récupérés !");
        else UIManager.Instance.ShowNotification("Votre sac à dos est plein !");
    }
}