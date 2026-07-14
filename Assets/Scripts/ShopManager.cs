using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    [Header("Interface Magasin")]
    public GameObject shopPanel;
    public TextMeshProUGUI shopTitleText;
    public ShopSlot[] shopSlots;

    [Header("Économie Dynamique 📉")]
    public float demandDropPerSale = 0.15f;
    public float minDemand = 0.20f;

    private Dictionary<ItemData, float> marketDemand = new Dictionary<ItemData, float>();

    private void Awake() { if (Instance == null) Instance = this; }

    void Start() { CloseShop(); }

    public void OpenShop(ItemData[] itemsForSale, bool isIllegal, string shopName)
    {
        shopTitleText.text = shopName;
        shopPanel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (shopSlots == null || shopSlots.Length == 0) return;

        foreach (var slot in shopSlots) { if (slot != null) slot.gameObject.SetActive(false); }

        for (int i = 0; i < itemsForSale.Length; i++)
        {
            if (i >= shopSlots.Length) break;
            if (shopSlots[i] != null) shopSlots[i].SetupForBuy(itemsForSale[i], isIllegal);
        }
    }

    public void OpenSellShop(string shopName)
    {
        if (shopTitleText != null) shopTitleText.text = shopName;
        if (shopPanel != null) shopPanel.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RefreshSellShop();
    }

    private void RefreshSellShop()
    {
        foreach (var slot in shopSlots) slot.gameObject.SetActive(false);

        var inventorySlots = InventoryManager.Instance.slots;
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (i >= shopSlots.Length) break;
            shopSlots[i].SetupForSell(inventorySlots[i].item);
        }
    }

    public void CloseShop()
    {
        shopPanel.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    public void TryBuyItem(ItemData item, bool isIllegalShop)
    {
        int price = item.buyPrice;
        if (isIllegalShop)
        {
            if (GameManager.Instance.dirtyMoney >= price) ProcessPurchase(item, price, true);
            else UIManager.Instance.ShowNotification("Pas assez d'Argent Sale !");
        }
        else
        {
            if (GameManager.Instance.cleanMoney >= price) ProcessPurchase(item, price, false);
            else UIManager.Instance.ShowNotification("Pas assez d'Argent Propre !");
        }
    }

    private void ProcessPurchase(ItemData item, int price, bool useDirtyMoney)
    {
        int amountAdded = InventoryManager.Instance.AddItem(item);
        if (amountAdded > 0)
        {
            if (useDirtyMoney)
            {
                GameManager.Instance.dirtyMoney -= price;
                GameManager.Instance.SyncDirtyMoneyItem(); // NOUVEAU : On met à jour l'inventaire direct
            }
            else
            {
                GameManager.Instance.cleanMoney -= price;
                if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-price, $"Achat : {item.itemName}");
            }

            UIManager.Instance.UpdateHUD();
        }
    }

    public float GetItemDemand(ItemData item)
    {
        if (!marketDemand.ContainsKey(item)) marketDemand[item] = 1.0f;
        return marketDemand[item];
    }

    public int GetDynamicItemPrice(ItemData item)
    {
        return Mathf.RoundToInt(item.valueInBlackMarket * GetItemDemand(item));
    }

    public void TrySellItem(ItemData item)
    {
        int dynamicPrice = GetDynamicItemPrice(item);

        GameManager.Instance.AddDirtyMoney(dynamicPrice);
        InventoryManager.Instance.RemoveItem(item, 1);

        UIManager.Instance.UpdateHUD();
        UIManager.Instance.ShowNotification($"Vendu : {item.itemName} pour {dynamicPrice}$");

        if (marketDemand.ContainsKey(item))
        {
            marketDemand[item] = Mathf.Max(minDemand, marketDemand[item] - demandDropPerSale);
        }

        RefreshSellShop();

        InventoryUI inventoryUI = FindObjectOfType<InventoryUI>();
        if (inventoryUI != null) inventoryUI.RefreshUI();
    }

    public void RecoverMarket()
    {
        marketDemand.Clear();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Un nouveau jour se lève. Les prix du marché noir sont rétablis !");
    }
}