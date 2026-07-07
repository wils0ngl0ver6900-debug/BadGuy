using UnityEngine;

public class StockApp : MonoBehaviour
{
    public static StockApp Instance;

    public GameObject stockAppPanel;
    public Transform contentParent; // Le Content de ta nouvelle ScrollView
    public GameObject stockLinePrefab; // Ton Prefab "Stock_Line"

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void OpenApp()
    {
        stockAppPanel.SetActive(true);
        RefreshUI();
    }

    public void CloseApp()
    {
        stockAppPanel.SetActive(false);
    }

    public void RefreshUI()
    {
        if (StockMarketManager.Instance == null) return;

        // 1. Nettoyer l'ancienne liste
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // 2. Générer les nouvelles lignes interactives
        foreach (Stock stock in StockMarketManager.Instance.marketStocks)
        {
            GameObject newLine = Instantiate(stockLinePrefab, contentParent);
            StockLineUI uiScript = newLine.GetComponent<StockLineUI>();

            if (uiScript != null)
            {
                // On cherche si le joueur possède cette action
                PlayerPortfolioItem pItem = StockMarketManager.Instance.GetPortfolioItem(stock.symbol);
                // On configure la ligne !
                uiScript.Setup(stock, pItem);
            }
        }
    }
}