using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CryptoCoin
{
    public string name;
    public string symbol;
    public float currentPrice;
    public float volatility;
    public List<float> priceHistory = new List<float>();
}

[System.Serializable]
public class PlayerCryptoItem
{
    public string symbol;
    public int coinsOwned;
    public float averageBuyPrice;
}

public class CryptoMarketManager : MonoBehaviour
{
    public static CryptoMarketManager Instance;

    [Header("Le Marché Crypto 🪙")]
    public List<CryptoCoin> marketCryptos = new List<CryptoCoin>();
    public List<PlayerCryptoItem> playerCryptoWallet = new List<PlayerCryptoItem>();

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Sécurité : on initialise seulement si la liste est vide dans l'Inspecteur
        if (marketCryptos.Count == 0) InitializeDefaultCryptos();
    }

    private void InitializeDefaultCryptos()
    {
        // 1. Les "Classiques" du marché
        marketCryptos.Add(CreateCrypto("Obsidian", "OBS", 1250f, 0.15f)); // Le "Bitcoin" du jeu : très cher, plutôt stable
        marketCryptos.Add(CreateCrypto("ByteCash", "BYTC", 150f, 0.18f)); // Solide, bon investissement

        // 2. Les cryptos du "Darknet" (Anonymes et volatiles)
        marketCryptos.Add(CreateCrypto("ShadowToken", "SHDW", 45f, 0.40f)); // Peut crasher ou exploser d'un coup
        marketCryptos.Add(CreateCrypto("GhostChain", "GHST", 80f, 0.25f)); // Réseau fantôme, apprécié des receleurs
        marketCryptos.Add(CreateCrypto("CipherLink", "CPL", 22f, 0.35f)); // Monnaie de hackers

        // 3. Les "Penny Cryptos" (Très peu chères, extrême volatilité pour les paris risqués)
        marketCryptos.Add(CreateCrypto("NeroCoin", "NERO", 12f, 0.30f)); // Ancienne crypto de base
        marketCryptos.Add(CreateCrypto("NexusCoin", "NXS", 3.5f, 0.50f)); // Faible valeur, très instable
        marketCryptos.Add(CreateCrypto("Vexel", "VXL", 0.8f, 0.70f)); // Vaut moins d'un dollar ! Peut faire x2 ou /2 en une nuit
    }

    private CryptoCoin CreateCrypto(string n, string s, float startPrice, float vol)
    {
        CryptoCoin c = new CryptoCoin { name = n, symbol = s, currentPrice = startPrice, volatility = vol };
        // On simule 10 jours d'historique plat au début pour le graphique
        for (int i = 0; i < 10; i++) c.priceHistory.Add(startPrice);
        return c;
    }

    public void UpdateMarketDaily()
    {
        foreach (CryptoCoin crypto in marketCryptos)
        {
            float randomChange = Random.Range(-crypto.volatility, crypto.volatility);
            crypto.currentPrice += crypto.currentPrice * randomChange;

            // La crypto peut descendre très bas (10 centimes minimum) mais pas disparaître
            if (crypto.currentPrice < 0.1f) crypto.currentPrice = 0.1f;

            crypto.currentPrice = Mathf.Round(crypto.currentPrice * 100f) / 100f;

            crypto.priceHistory.Add(crypto.currentPrice);
            if (crypto.priceHistory.Count > 10) crypto.priceHistory.RemoveAt(0);
        }

        if (CryptoApp.Instance != null && CryptoApp.Instance.gameObject.activeInHierarchy)
            CryptoApp.Instance.RefreshUI();
    }

    // --- SYSTÈME D'ACHAT / VENTE ---
    public bool BuyCrypto(string symbol, int quantity)
    {
        CryptoCoin crypto = GetCrypto(symbol);
        if (crypto == null) return false;

        int totalCost = Mathf.RoundToInt(crypto.currentPrice * quantity);

        if (GameManager.Instance != null && GameManager.Instance.cleanMoney >= totalCost)
        {
            GameManager.Instance.cleanMoney -= totalCost;

            PlayerCryptoItem item = GetWalletItem(symbol);
            if (item == null)
            {
                item = new PlayerCryptoItem { symbol = symbol, coinsOwned = 0, averageBuyPrice = 0 };
                playerCryptoWallet.Add(item);
            }

            float totalValueBefore = item.coinsOwned * item.averageBuyPrice;
            item.coinsOwned += quantity;
            item.averageBuyPrice = (totalValueBefore + totalCost) / item.coinsOwned;

            if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-totalCost, $"Achat Crypto: {quantity}x {symbol}");
            if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
            return true;
        }
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Fonds bancaires insuffisants !");
        return false;
    }

    public bool SellCrypto(string symbol, int quantity)
    {
        PlayerCryptoItem item = GetWalletItem(symbol);
        if (item == null || item.coinsOwned < quantity) return false;

        CryptoCoin crypto = GetCrypto(symbol);
        int totalGained = Mathf.RoundToInt(crypto.currentPrice * quantity);

        item.coinsOwned -= quantity;
        if (item.coinsOwned <= 0) playerCryptoWallet.Remove(item);

        if (GameManager.Instance != null) GameManager.Instance.cleanMoney += totalGained;
        if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(totalGained, $"Vente Crypto: {quantity}x {symbol}");
        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
        return true;
    }

    public CryptoCoin GetCrypto(string symbol) => marketCryptos.Find(c => c.symbol == symbol);
    public PlayerCryptoItem GetWalletItem(string symbol) => playerCryptoWallet.Find(p => p.symbol == symbol);
}