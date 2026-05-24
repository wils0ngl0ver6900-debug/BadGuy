using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SafehouseManager : MonoBehaviour
{
    public static SafehouseManager Instance;

    [Header("Stockage de la Planque 📦")]
    public int storedDirtyMoney = 0;
    public List<ItemData> storedIllegalItems = new List<ItemData>();

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
        if (itemsText != null) itemsText.text = $"Objets Illégaux : {storedIllegalItems.Count}";
    }

    // --- ACTIONS SUR L'ARGENT ---
    public void DepositDirtyMoney()
    {
        int amount = GameManager.Instance.dirtyMoney;
        if (amount > 0)
        {
            storedDirtyMoney += amount;
            GameManager.Instance.dirtyMoney = 0;

            GameManager.Instance.AddDirtyMoney(0); // Force l'update du HUD
            UpdateUI();
            UIManager.Instance.ShowNotification("Argent sale sécurisé dans le coffre !");
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
            GameManager.Instance.AddDirtyMoney(storedDirtyMoney);
            storedDirtyMoney = 0;

            UpdateUI();
            UIManager.Instance.ShowNotification("Argent sale récupéré !");
        }
    }

    // --- ACTIONS SUR LES OBJETS ILLÉGAUX ---
    public void DepositIllegalItems()
    {
        int count = 0;
        List<ItemData> itemsToKeep = new List<ItemData>();

        // On fouille le sac à dos du joueur
        foreach (var item in InventoryManager.Instance.items)
        {
            if (item != null && item.isIllegal)
            {
                storedIllegalItems.Add(item);
                count++;
            }
            else
            {
                itemsToKeep.Add(item); // On garde les objets légaux
            }
        }

        if (count > 0)
        {
            InventoryManager.Instance.items = itemsToKeep;
            UpdateUI();

            InventoryUI ui = FindObjectOfType<InventoryUI>();
            if (ui != null) ui.RefreshUI(); // Rafraîchit l'inventaire

            UIManager.Instance.ShowNotification($"{count} objets illégaux sécurisés !");
        }
        else
        {
            UIManager.Instance.ShowNotification("Aucun objet illégal dans votre sac à dos.");
        }
    }

    public void WithdrawIllegalItems()
    {
        if (storedIllegalItems.Count == 0) return;

        int count = 0;
        List<ItemData> itemsRemaining = new List<ItemData>();

        foreach (var item in storedIllegalItems)
        {
            if (InventoryManager.Instance.items.Count < InventoryManager.Instance.maxSlots)
            {
                InventoryManager.Instance.items.Add(item);
                count++;
            }
            else
            {
                itemsRemaining.Add(item); // Le sac est plein, ça reste dans le coffre
            }
        }

        storedIllegalItems = itemsRemaining;
        UpdateUI();

        if (count > 0)
        {
            UIManager.Instance.ShowNotification($"{count} objets récupérés !");
            InventoryUI ui = FindObjectOfType<InventoryUI>();
            if (ui != null) ui.RefreshUI();
        }
        else
        {
            UIManager.Instance.ShowNotification("Votre sac à dos est plein !");
        }
    }
}