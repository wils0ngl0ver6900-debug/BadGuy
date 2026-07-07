using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Stock
{
    public string name;
    public string symbol;
    public float currentPrice;
    public float previousPrice;
    [Tooltip("Volatilité: 0.05 = l'action peut varier de -5% à +5% par jour")]
    public float volatility;
}

[System.Serializable]
public class PlayerPortfolioItem
{
    public string symbol;
    public int sharesOwned;
    public float averageBuyPrice;
}

public class StockMarketManager : MonoBehaviour
{
    public static StockMarketManager Instance;

    [Header("Le Marché 📉")]
    public List<Stock> marketStocks = new List<Stock>();

    [Header("Portefeuille du Joueur 💼")]
    public List<PlayerPortfolioItem> playerPortfolio = new List<PlayerPortfolioItem>();

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (marketStocks.Count == 0) InitializeDefaultMarket();
    }

    private void InitializeDefaultMarket()
    {
        // 8 entreprises fictives avec des comportements différents (Pas de crypto !)
        marketStocks.Add(new Stock { name = "Aegis National Bank", symbol = "ANB", currentPrice = 150f, previousPrice = 150f, volatility = 0.03f }); // Très stable
        marketStocks.Add(new Stock { name = "OmniTech Solutions", symbol = "OMNI", currentPrice = 45f, previousPrice = 45f, volatility = 0.08f }); // Tech, fluctuations moyennes
        marketStocks.Add(new Stock { name = "Titan Security", symbol = "TTS", currentPrice = 210f, previousPrice = 210f, volatility = 0.05f }); // Sécurité/Armement, cher mais solide
        marketStocks.Add(new Stock { name = "NovaCare Pharma", symbol = "NCP", currentPrice = 85f, previousPrice = 85f, volatility = 0.12f }); // Médical, sensible aux scandales
        marketStocks.Add(new Stock { name = "Apex Motors", symbol = "APX", currentPrice = 320f, previousPrice = 320f, volatility = 0.06f }); // Automobile de luxe, gros investissement
        marketStocks.Add(new Stock { name = "Krono Burger", symbol = "KRO", currentPrice = 15f, previousPrice = 15f, volatility = 0.04f }); // Fast-food, action pas chère pour débuter
        marketStocks.Add(new Stock { name = "Echo Media Group", symbol = "EMG", currentPrice = 25f, previousPrice = 25f, volatility = 0.15f }); // Réseaux sociaux/TV, très volatile
        marketStocks.Add(new Stock { name = "Vanguard Energy", symbol = "VGE", currentPrice = 110f, previousPrice = 110f, volatility = 0.07f }); // Pétrole/Énergie, valeur classique
    }

    public void UpdateMarketDaily()
    {
        foreach (Stock stock in marketStocks)
        {
            stock.previousPrice = stock.currentPrice;

            float randomChange = Random.Range(-stock.volatility, stock.volatility);
            stock.currentPrice += stock.currentPrice * randomChange;

            if (stock.currentPrice < 1f) stock.currentPrice = 1f;

            stock.currentPrice = Mathf.Round(stock.currentPrice * 100f) / 100f;
        }

        if (StockApp.Instance != null && StockApp.Instance.gameObject.activeInHierarchy)
        {
            StockApp.Instance.RefreshUI();
        }
    }

    // --- SYSTÈME D'ACHAT ET DE VENTE ---

    public bool BuyStock(string symbol, int quantity)
    {
        Stock stock = GetStock(symbol);
        if (stock == null) return false;

        int totalCost = Mathf.RoundToInt(stock.currentPrice * quantity);

        if (GameManager.Instance != null && GameManager.Instance.cleanMoney >= totalCost)
        {
            GameManager.Instance.cleanMoney -= totalCost;

            PlayerPortfolioItem item = GetPortfolioItem(symbol);
            if (item == null)
            {
                item = new PlayerPortfolioItem { symbol = symbol, sharesOwned = 0, averageBuyPrice = 0 };
                playerPortfolio.Add(item);
            }

            float totalValueBefore = item.sharesOwned * item.averageBuyPrice;
            item.sharesOwned += quantity;
            item.averageBuyPrice = (totalValueBefore + totalCost) / item.sharesOwned;

            if (BankApp.Instance != null)
            {
                BankApp.Instance.RecordTransaction(-totalCost, $"Achat Bourse: {quantity}x {symbol}");
            }

            if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();

            return true;
        }

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Fonds bancaires insuffisants !");
        return false;
    }

    public bool SellStock(string symbol, int quantity)
    {
        PlayerPortfolioItem item = GetPortfolioItem(symbol);
        if (item == null || item.sharesOwned < quantity) return false;

        Stock stock = GetStock(symbol);
        int totalGained = Mathf.RoundToInt(stock.currentPrice * quantity);

        item.sharesOwned -= quantity;
        if (item.sharesOwned <= 0) playerPortfolio.Remove(item);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.cleanMoney += totalGained;
        }

        if (BankApp.Instance != null)
        {
            BankApp.Instance.RecordTransaction(totalGained, $"Vente Bourse: {quantity}x {symbol}");
        }

        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();

        return true;
    }

    public Stock GetStock(string symbol)
    {
        return marketStocks.Find(s => s.symbol == symbol);
    }

    public PlayerPortfolioItem GetPortfolioItem(string symbol)
    {
        return playerPortfolio.Find(p => p.symbol == symbol);
    }
}