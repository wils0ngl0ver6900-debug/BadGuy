using UnityEngine;

public class CryptoApp : MonoBehaviour
{
    public static CryptoApp Instance;

    public GameObject cryptoAppPanel;
    public Transform contentParent; // Le Content de la ScrollView Crypto
    public GameObject cryptoLinePrefab; // Ton nouveau Prefab "Crypto_Line"

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void OpenApp()
    {
        cryptoAppPanel.SetActive(true);
        RefreshUI();
    }

    public void CloseApp()
    {
        cryptoAppPanel.SetActive(false);
    }

    public void RefreshUI()
    {
        if (CryptoMarketManager.Instance == null) return;

        foreach (Transform child in contentParent) Destroy(child.gameObject);

        foreach (CryptoCoin crypto in CryptoMarketManager.Instance.marketCryptos)
        {
            GameObject newLine = Instantiate(cryptoLinePrefab, contentParent);
            CryptoLineUI uiScript = newLine.GetComponent<CryptoLineUI>();

            if (uiScript != null)
            {
                PlayerCryptoItem pItem = CryptoMarketManager.Instance.GetWalletItem(crypto.symbol);
                uiScript.Setup(crypto, pItem);
            }
        }
    }
}