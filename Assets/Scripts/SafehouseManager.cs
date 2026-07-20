using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SafehouseManager : MonoBehaviour
{
    public static SafehouseManager Instance;

    [Header("Progression Immobilière 🏠")]
    [Range(1, 4)] public int safehouseLevel = 1;
    public int tier2Cost = 50000;
    public int tier3Cost = 150000;
    public int tier4Cost = 500000;

    [Header("Modules de la Planque (GameObjects)")]
    public GameObject garageModule;
    public GameObject weedLabModule;
    public GameObject chemLabModule;

    [Header("Le Coffre Fort Dynamique 📦")]
    public int maxStashSlots = 20;
    public List<InventorySlot> stashSlots = new List<InventorySlot>();

    [Header("Interface UI")]
    public GameObject safehousePanel;
    public TextMeshProUGUI itemsText;
    public TextMeshProUGUI levelText;

    [Header("HUD à masquer 🙈")]
    public GameObject hotbarPanel;
    public GameObject minimapPanel;

    [HideInInspector]
    public bool isOpen = false; // Variable de sécurité pour les autres scripts

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        ApplySafehouseLevel();
        CloseSafehouse();
    }

    public void ApplySafehouseLevel()
    {
        if (garageModule != null) garageModule.SetActive(safehouseLevel >= 2);
        if (weedLabModule != null) weedLabModule.SetActive(safehouseLevel >= 3);
        if (chemLabModule != null) chemLabModule.SetActive(safehouseLevel >= 4);

        if (levelText != null) levelText.text = $"Planque Niveau {safehouseLevel}";

        maxStashSlots = 10 + (safehouseLevel * 10);
        ExpandStashCapacity();
    }

    private void ExpandStashCapacity()
    {
        while (stashSlots.Count < maxStashSlots)
        {
            stashSlots.Add(new InventorySlot(null, 0));
        }
    }

    public void PurchaseUpgrade()
    {
        int cost = 0;
        if (safehouseLevel == 1) cost = tier2Cost;
        else if (safehouseLevel == 2) cost = tier3Cost;
        else if (safehouseLevel == 3) cost = tier4Cost;
        else return;

        if (GameManager.Instance.cleanMoney >= cost)
        {
            GameManager.Instance.cleanMoney -= cost;
            if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-cost, $"Amélioration Planque Niv.{safehouseLevel + 1}");

            safehouseLevel++;
            ApplySafehouseLevel();

            UIManager.Instance.ShowNotification($"<color=green>Planque améliorée ! Coffre agrandi à {maxStashSlots} places.</color>");
            UpdateUI();
        }
    }

    public void OpenSafehouse()
    {
        isOpen = true; // On verrouille l'état du jeu
        safehousePanel.SetActive(true);

        // On masque le HUD
        if (hotbarPanel != null) hotbarPanel.SetActive(false);
        if (minimapPanel != null) minimapPanel.SetActive(false);

        // Optionnel : on force la fermeture du panel inventaire s'il était actif
        InventoryUI invUI = FindObjectOfType<InventoryUI>();
        if (invUI != null && invUI.gameObject.activeSelf) invUI.gameObject.SetActive(false);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        UpdateUI();
    }

    public void CloseSafehouse()
    {
        isOpen = false; // On déverrouille
        safehousePanel.SetActive(false);

        // On réaffiche le HUD
        if (hotbarPanel != null) hotbarPanel.SetActive(true);
        if (minimapPanel != null) minimapPanel.SetActive(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void UpdateUI()
    {
        int occupiedSlots = 0;
        foreach (var slot in stashSlots)
        {
            if (slot.item != null) occupiedSlots++;
        }

        if (itemsText != null) itemsText.text = $"Espace Coffre : {occupiedSlots} / {maxStashSlots}";
        if (levelText != null) levelText.text = $"Planque Niveau {safehouseLevel}";
    }

    public void DepositDirtyMoney()
    {
        int amount = GameManager.Instance.dirtyMoney;
        if (amount > 0)
        {
            bool success = AddToStash(GameManager.Instance.dirtyMoneyItemDef, amount);

            if (success)
            {
                GameManager.Instance.dirtyMoney = 0;
                GameManager.Instance.SyncDirtyMoneyItem();

                UpdateUI();
                FindObjectOfType<StashUI>()?.RefreshUI();

                UIManager.Instance.ShowNotification($"{amount}$ sécurisés dans le coffre !");
                UIManager.Instance.UpdateHUD();
            }
            else
            {
                UIManager.Instance.ShowNotification("<color=red>Le coffre est plein !</color>");
            }
        }
    }

    public bool AddToStash(ItemData itemToAdd, int amountToAdd)
    {
        if (itemToAdd.isStackable)
        {
            foreach (var slot in stashSlots)
            {
                if (slot.item == itemToAdd)
                {
                    slot.amount += amountToAdd;
                    return true;
                }
            }
        }

        for (int i = 0; i < stashSlots.Count; i++)
        {
            if (stashSlots[i].item == null)
            {
                stashSlots[i].item = itemToAdd;
                stashSlots[i].amount = amountToAdd;
                return true;
            }
        }

        return false;
    }

    public void DepositAllIllegalItems()
    {
        int itemsMoved = 0;

        for (int i = InventoryManager.Instance.slots.Count - 1; i >= 0; i--)
        {
            var playerSlot = InventoryManager.Instance.slots[i];

            if (playerSlot.item != null && playerSlot.item.isIllegal && playerSlot.item != GameManager.Instance.dirtyMoneyItemDef)
            {
                bool success = AddToStash(playerSlot.item, playerSlot.amount);

                if (success)
                {
                    itemsMoved++; // <--- CORRECTION : On compte le nombre de cases (slots) !
                    InventoryManager.Instance.slots.RemoveAt(i);
                }
                else
                {
                    UIManager.Instance.ShowNotification("<color=red>Le coffre est plein !</color>");
                    break;
                }
            }
        }

        UpdateUI();
        FindObjectOfType<StashUI>()?.RefreshUI();

        InventoryUI ui = FindObjectOfType<InventoryUI>();
        if (ui != null) ui.RefreshUI();

        if (itemsMoved > 0) UIManager.Instance.ShowNotification($"{itemsMoved} objets sécurisés !");
    }
}