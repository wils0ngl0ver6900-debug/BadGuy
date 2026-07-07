using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class CryptoLineUI : MonoBehaviour
{
    [Header("UI Textes & Boutons")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI ownedText;
    public Button buyButton;
    public Button sellButton;

    [Header("Graphique 📈")]
    public RectTransform graphContainer;

    private string currentSymbol;
    private List<GameObject> graphLines = new List<GameObject>();

    public void Setup(CryptoCoin crypto, PlayerCryptoItem walletItem)
    {
        currentSymbol = crypto.symbol;

        float previousPrice = crypto.priceHistory.Count > 1 ? crypto.priceHistory[crypto.priceHistory.Count - 2] : crypto.currentPrice;
        float changeAmount = crypto.currentPrice - previousPrice;
        float changePercent = (previousPrice > 0) ? (changeAmount / previousPrice) * 100f : 0f;
        string colorHex = changePercent >= 0 ? "#00FF00" : "#FF0000";
        string sign = changePercent >= 0 ? "+" : "";

        nameText.text = $"<b>{crypto.name}</b> ({crypto.symbol})";
        priceText.text = $"{crypto.currentPrice} $  <size=80%><color={colorHex}>{sign}{changePercent:F2}%</color></size>";

        if (walletItem != null && walletItem.coinsOwned > 0)
        {
            float profit = (crypto.currentPrice * walletItem.coinsOwned) - (walletItem.averageBuyPrice * walletItem.coinsOwned);
            string profitColor = profit >= 0 ? "#00FF00" : "#FF0000";
            string profitSign = profit >= 0 ? "+" : "";
            ownedText.text = $"En stock : {walletItem.coinsOwned} | Gain : <color={profitColor}>{profitSign}{Mathf.RoundToInt(profit)} $</color>";
            sellButton.interactable = true;
        }
        else
        {
            ownedText.text = "En stock : 0";
            sellButton.interactable = false;
        }

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() =>
        {
            if (CryptoMarketManager.Instance.BuyCrypto(currentSymbol, 1))
                CryptoApp.Instance.RefreshUI();
        });

        sellButton.onClick.RemoveAllListeners();
        sellButton.onClick.AddListener(() =>
        {
            if (CryptoMarketManager.Instance.SellCrypto(currentSymbol, 1))
                CryptoApp.Instance.RefreshUI();
        });

        // Plus de Coroutine, on dessine en direct !
        DrawGraph(crypto.priceHistory, changePercent >= 0 ? Color.green : Color.red);
    }

    private void DrawGraph(List<float> history, Color color)
    {
        if (graphContainer == null) return;

        // 1. Nettoyage radical des anciennes lignes
        foreach (Transform child in graphContainer) Destroy(child.gameObject);
        graphLines.Clear();

        if (history.Count < 2) return;

        // 2. LA MAGIE : On force Unity à calculer la taille exacte de ta boîte à cet instant précis
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(graphContainer);

        float graphWidth = graphContainer.rect.width;
        float graphHeight = graphContainer.rect.height;

        // Sécurité ultime : Si Unity s'entête à dire que la boîte fait 0 pixel, on force une taille par défaut
        if (graphWidth <= 0) graphWidth = 220f;
        if (graphHeight <= 0) graphHeight = 80f;

        // 3. Calcul des échelles
        float yMax = Mathf.Max(history.ToArray());
        float yMin = Mathf.Min(history.ToArray());

        float yDifference = yMax - yMin;
        if (yDifference <= 0) yDifference = 5f;
        yMax += yDifference * 0.2f;
        yMin -= yDifference * 0.2f;

        Vector2 lastPoint = Vector2.zero;

        for (int i = 0; i < history.Count; i++)
        {
            float xPosition = (i / (float)(history.Count - 1)) * graphWidth;
            float yPosition = ((history[i] - yMin) / (yMax - yMin)) * graphHeight;
            Vector2 currentPoint = new Vector2(xPosition, yPosition);

            if (i > 0) CreateLineConnection(lastPoint, currentPoint, color);
            lastPoint = currentPoint;
        }
    }

    private void CreateLineConnection(Vector2 point1, Vector2 point2, Color color)
    {
        GameObject line = new GameObject("GraphLine", typeof(Image));
        line.transform.SetParent(graphContainer, false);

        Image img = line.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        RectTransform rect = line.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;

        Vector2 dir = (point2 - point1).normalized;
        float distance = Vector2.Distance(point1, point2);

        // ---> CHANGEMENT ICI : Épaisseur passée de 4f à 2f pour un look "Sparkline" plus pro
        rect.sizeDelta = new Vector2(distance, 2f);
        rect.anchoredPosition = point1;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.localEulerAngles = new Vector3(0, 0, angle);

        graphLines.Add(line);
    }
}