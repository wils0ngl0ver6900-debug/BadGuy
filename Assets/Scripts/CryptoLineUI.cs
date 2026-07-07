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
    public RectTransform graphContainer; // La zone où on dessine
    public Color graphColor = Color.green;

    private string currentSymbol;
    private List<GameObject> graphLines = new List<GameObject>(); // Mémoriser les lignes tracées

    public void Setup(CryptoCoin crypto, PlayerCryptoItem walletItem)
    {
        currentSymbol = crypto.symbol;

        float previousPrice = crypto.priceHistory.Count > 1 ? crypto.priceHistory[crypto.priceHistory.Count - 2] : crypto.currentPrice;
        float changeAmount = crypto.currentPrice - previousPrice;
        float changePercent = (previousPrice > 0) ? (changeAmount / previousPrice) * 100f : 0f;
        string colorHex = changePercent >= 0 ? "#00FF00" : "#FF0000";
        string sign = changePercent >= 0 ? "+" : "";

        // Textes
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

        // Actions boutons
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => { CryptoMarketManager.Instance.BuyCrypto(currentSymbol, 1); });
        sellButton.onClick.RemoveAllListeners();
        sellButton.onClick.AddListener(() => { CryptoMarketManager.Instance.SellCrypto(currentSymbol, 1); });

        // DESSINER LE GRAPHIQUE
        DrawGraph(crypto.priceHistory, changePercent >= 0 ? Color.green : Color.red);
    }

    private void DrawGraph(List<float> history, Color color)
    {
        // 1. Nettoyer l'ancien graphique
        foreach (GameObject line in graphLines) Destroy(line);
        graphLines.Clear();

        if (history.Count < 2) return; // Pas assez de données pour tracer une ligne

        // 2. Trouver les limites pour adapter l'échelle
        float yMax = history[0];
        float yMin = history[0];
        foreach (float val in history)
        {
            if (val > yMax) yMax = val;
            if (val < yMin) yMin = val;
        }

        // Marge pour que le graphique ne touche pas les bords extrêmes
        float yDifference = yMax - yMin;
        if (yDifference <= 0) yDifference = 5f;
        yMax += yDifference * 0.2f;
        yMin -= yDifference * 0.2f;

        float graphWidth = graphContainer.rect.width;
        float graphHeight = graphContainer.rect.height;

        Vector2 lastPoint = Vector2.zero;

        // 3. Placer les points
        for (int i = 0; i < history.Count; i++)
        {
            float xPosition = (i / (float)(history.Count - 1)) * graphWidth;
            float yPosition = ((history[i] - yMin) / (yMax - yMin)) * graphHeight;
            Vector2 currentPoint = new Vector2(xPosition, yPosition);

            if (i > 0)
            {
                CreateLineConnection(lastPoint, currentPoint, color);
            }
            lastPoint = currentPoint;
        }
    }

    private void CreateLineConnection(Vector2 point1, Vector2 point2, Color color)
    {
        GameObject line = new GameObject("GraphLine", typeof(Image));
        line.transform.SetParent(graphContainer, false);
        line.GetComponent<Image>().color = color;

        RectTransform rect = line.GetComponent<RectTransform>();
        Vector2 dir = (point2 - point1).normalized;
        float distance = Vector2.Distance(point1, point2);

        // Positionnement et étirement
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.sizeDelta = new Vector2(distance, 3f); // Épaisseur de la ligne (3f)
        rect.anchoredPosition = point1 + dir * distance * 0.5f;

        // Calcul de l'angle pour la rotation
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.localEulerAngles = new Vector3(0, 0, angle);

        graphLines.Add(line);
    }
}