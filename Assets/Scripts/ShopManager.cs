using UnityEngine;
using TMPro;
using System.Collections.Generic; // INDISPENSABLE pour le Dictionnaire
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance;

    [Header("Interface Magasin")]
    public GameObject shopPanel;
    public TextMeshProUGUI shopTitleText;
    public ShopSlot[] shopSlots;
    [Header("Économie Dynamique 📉")]
    public float demandDropPerSale = 0.15f; // Chaque vente fait chuter le prix de 15%
    public float minDemand = 0.20f; // Le prix ne peut pas descendre en dessous de 20% de sa valeur

    // Ce dictionnaire mémorise la "demande" (de 1.0 à 0.2) pour chaque objet
    private Dictionary<ItemData, float> marketDemand = new Dictionary<ItemData, float>();
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        CloseShop();
    }

    // --- MODE ACHAT ---
    public void OpenShop(ItemData[] itemsForSale, bool isIllegal, string shopName)
    {
        shopTitleText.text = shopName;
        shopPanel.SetActive(true);

        // CONFIGURATION SÉCURISÉE : On active la souris TOUT DE SUITE 
        // comme ça, même si un slot bug, la souris reste disponible !
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (shopSlots == null || shopSlots.Length == 0)
        {
            Debug.LogError("[ShopManager] Attention ! Aucun ShopSlot n'est assigné dans l'Inspector !");
            return;
        }

        // On désactive proprement les anciens slots avec une sécurité anti-Null
        foreach (var slot in shopSlots)
        {
            if (slot != null) slot.gameObject.SetActive(false);
        }

        for (int i = 0; i < itemsForSale.Length; i++)
        {
            if (i >= shopSlots.Length) break;
            if (shopSlots[i] != null)
            {
                shopSlots[i].SetupForBuy(itemsForSale[i], isIllegal);
            }
        }
    }

    // --- MODE VENTE (RECELEUR) ---
    public void OpenSellShop(string shopName)
    {
        if (shopTitleText != null) shopTitleText.text = shopName;

        if (shopPanel != null) shopPanel.SetActive(true);

        // On active et libère la souris pour les menus
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // On force le rafraîchissement en mode revente d'inventaire
        RefreshSellShop();
    }

    private void RefreshSellShop()
    {
        foreach (var slot in shopSlots) slot.gameObject.SetActive(false);

        var inventoryItems = InventoryManager.Instance.items;
        for (int i = 0; i < inventoryItems.Count; i++)
        {
            if (i >= shopSlots.Length) break;
            shopSlots[i].SetupForSell(inventoryItems[i]);
        }
    }

    public void CloseShop()
    {
        shopPanel.SetActive(false);

        // CORRECTION : On cache la souris en jeu, mais on la confine pour que le joueur puisse tourner
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
        bool hasSpace = InventoryManager.Instance.AddItem(item);
        if (hasSpace)
        {
            if (useDirtyMoney) GameManager.Instance.dirtyMoney -= price;
            else GameManager.Instance.cleanMoney -= price;

            UIManager.Instance.UpdateHUD();
            UIManager.Instance.ShowNotification($"Acheté : {item.itemName}");
        }
    }
    // --- NOUVEAU : SYSTÈME D'OFFRE ET DEMANDE ---
    public float GetItemDemand(ItemData item)
    {
        // Si c'est la première fois qu'on vend cet objet, la demande est à 100% (1.0f)
        if (!marketDemand.ContainsKey(item))
        {
            marketDemand[item] = 1.0f;
        }
        return marketDemand[item];
    }

    public int GetDynamicItemPrice(ItemData item)
    {
        // Calcule le prix final : Prix de base * Demande actuelle
        return Mathf.RoundToInt(item.valueInBlackMarket * GetItemDemand(item));
    }
    public void TrySellItem(ItemData item)
    {
        // 1. On récupère le prix avec la cote actuelle du marché
        int dynamicPrice = GetDynamicItemPrice(item);

        GameManager.Instance.AddDirtyMoney(dynamicPrice);
        InventoryManager.Instance.RemoveItem(item);

        UIManager.Instance.UpdateHUD();
        UIManager.Instance.ShowNotification($"Vendu : {item.itemName} pour {dynamicPrice}$");

        // 2. LE MARCHÉ S'EFFONDRE ! On réduit la demande pour la prochaine vente
        if (marketDemand.ContainsKey(item))
        {
            marketDemand[item] = Mathf.Max(minDemand, marketDemand[item] - demandDropPerSale);
        }

        RefreshSellShop();

        InventoryUI inventoryUI = FindObjectOfType<InventoryUI>();
        if (inventoryUI != null) inventoryUI.RefreshUI();
    }

    // BONUS : Fonction pour faire remonter les prix (à appeler quand le joueur dort par exemple !)
    public void RecoverMarket()
    {
        marketDemand.Clear(); // Réinitialise toute l'économie à 100%
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Un nouveau jour se lève. Les prix du marché noir sont rétablis !");
    }
}