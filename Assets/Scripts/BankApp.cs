using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class BankApp : MonoBehaviour
{
    public static BankApp Instance;

    [Header("UI Banque 🏦")]
    public GameObject bankAppPanel;
    public TextMeshProUGUI balanceText;

    [Header("Historique")]
    public Transform historyContent; // Le "Content" de ta Scroll View
    public GameObject transactionPrefab; // Le prefab "Transaction_Ligne"

    // Structure d'une transaction
    [System.Serializable]
    public class TransactionInfo
    {
        public string description;
        public int amount;
        public string time;
    }

    private List<TransactionInfo> transactionHistory = new List<TransactionInfo>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // Ouvre l'appli depuis le bouton de l'accueil du téléphone
    public void OpenApp()
    {
        bankAppPanel.SetActive(true);
        RefreshUI();
    }

    // Ferme l'appli
    public void CloseApp()
    {
        bankAppPanel.SetActive(false);
    }

    // Ajoute une transaction et met à jour l'affichage
    public void RecordTransaction(int amount, string desc)
    {
        TransactionInfo t = new TransactionInfo
        {
            amount = amount,
            description = desc,

            // ---> LA CORRECTION EST ICI <---
            // On demande l'heure fictive au TimeManager au lieu du PC
            time = TimeManager.Instance != null ? TimeManager.Instance.GetFormattedTime() : "00:00"
        };

        // Ajoute la nouvelle transaction tout en haut de la liste
        transactionHistory.Insert(0, t);

        // Limite l'historique à 30 pour ne pas saturer la mémoire du téléphone
        if (transactionHistory.Count > 30)
        {
            transactionHistory.RemoveAt(transactionHistory.Count - 1);
        }

        if (bankAppPanel.activeSelf) RefreshUI();
    }

    private void RefreshUI()
    {
        if (GameManager.Instance != null)
        {
            // Le .ToString("N0") permet d'avoir des espaces pour les milliers (ex: 15 000 au lieu de 15000)
            balanceText.text = $"{GameManager.Instance.cleanMoney.ToString("N0")} $";
        }

        // 1. Nettoyer l'ancienne liste
        foreach (Transform child in historyContent)
        {
            Destroy(child.gameObject);
        }

        // 2. Générer la nouvelle liste à jour
        foreach (TransactionInfo t in transactionHistory)
        {
            GameObject newLigne = Instantiate(transactionPrefab, historyContent);
            TransactionUI uiScript = newLigne.GetComponent<TransactionUI>();

            if (uiScript != null)
            {
                uiScript.Setup(t.description, t.amount, t.time);
            }
        }
    }
}