using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ShopSlot : MonoBehaviour
{
    [Header("UI du Slot")]
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemPriceText;
    public Image itemIcon;
    public TextMeshProUGUI buttonActionText; // La fameuse case manquante !

    private ItemData currentItem;
    private bool isIllegalShop;
    private bool isSellMode;

    // Configuration pour ACHETER
    public void SetupForBuy(ItemData item, bool illegal)
    {
        isSellMode = false;
        currentItem = item;
        isIllegalShop = illegal;

        itemNameText.text = item.itemName;
        itemIcon.sprite = item.icon;

        string color = illegal ? "#FF0000" : "#00FF22";
        itemPriceText.text = $"<color={color}>{item.buyPrice}$</color>";

        if (buttonActionText != null) buttonActionText.text = "Acheter";

        gameObject.SetActive(true);
    }

    // Configuration pour VENDRE (Receleur)
    // Configuration pour VENDRE (Receleur)
    public void SetupForSell(ItemData item)
    {
        isSellMode = true;
        currentItem = item;

        itemNameText.text = item.itemName;
        itemIcon.sprite = item.icon;

        // --- NOUVEAU : CALCUL DU PRIX DYNAMIQUE ---
        int currentPrice = ShopManager.Instance.GetDynamicItemPrice(item);
        float currentDemand = ShopManager.Instance.GetItemDemand(item);

        // Code couleur : Vert si le prix est excellent, Orange si le marché est inondé !
        string priceColor = currentDemand < 1.0f ? "#FFA500" : "#00FF22";

        itemPriceText.text = $"<color={priceColor}>+{currentPrice}$</color>";

        if (buttonActionText != null) buttonActionText.text = "Vendre";

        gameObject.SetActive(true);
    }

    // À lier sur l'événement "OnClick()" du bouton UI
    public void OnActionButtonClicked()
    {
        if (isSellMode)
        {
            ShopManager.Instance.TrySellItem(currentItem);
        }
        else
        {
            ShopManager.Instance.TryBuyItem(currentItem, isIllegalShop);
        }
    }
}