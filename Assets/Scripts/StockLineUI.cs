using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StockLineUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI ownedText;
    public Button buyButton;
    public Button sellButton;

    private string currentSymbol;

    public void Setup(Stock stock, PlayerPortfolioItem portfolioItem)
    {
        currentSymbol = stock.symbol;

        // 1. Calcul de la Variation (Couleurs)
        float changeAmount = stock.currentPrice - stock.previousPrice;
        float changePercent = (stock.previousPrice > 0) ? (changeAmount / stock.previousPrice) * 100f : 0f;
        string colorHex = changePercent >= 0 ? "#00FF00" : "#FF0000";
        string sign = changePercent >= 0 ? "+" : "";

        // 2. Affichage des infos de base (DEVISE MODIFIÉE ICI 💵)
        nameText.text = $"<b>{stock.name}</b> ({stock.symbol})";
        priceText.text = $"{stock.currentPrice} $  <size=80%><color={colorHex}>{sign}{changePercent:F2}%</color></size>";

        // 3. Affichage du portefeuille et activation du bouton Vendre
        if (portfolioItem != null && portfolioItem.sharesOwned > 0)
        {
            float profit = (stock.currentPrice * portfolioItem.sharesOwned) - (portfolioItem.averageBuyPrice * portfolioItem.sharesOwned);
            string profitColor = profit >= 0 ? "#00FF00" : "#FF0000";
            string profitSign = profit >= 0 ? "+" : "";

            // Le "N0" sert à arrondir les gros chiffres proprement (DEVISE MODIFIÉE ICI 💵)
            ownedText.text = $"En stock : {portfolioItem.sharesOwned} | Gain : <color={profitColor}>{profitSign}{Mathf.RoundToInt(profit)} $</color>";
            sellButton.interactable = true; // On peut vendre !
        }
        else
        {
            ownedText.text = "En stock : 0";
            sellButton.interactable = false; // Grisé, on n'a rien à vendre
        }

        // 4. Connexion automatique des boutons !
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => BuyAction());

        sellButton.onClick.RemoveAllListeners();
        sellButton.onClick.AddListener(() => SellAction());
    }

    private void BuyAction()
    {
        if (StockMarketManager.Instance.BuyStock(currentSymbol, 1))
        {
            StockApp.Instance.RefreshUI();
        }
    }

    private void SellAction()
    {
        if (StockMarketManager.Instance.SellStock(currentSymbol, 1))
        {
            StockApp.Instance.RefreshUI();
        }
    }
}