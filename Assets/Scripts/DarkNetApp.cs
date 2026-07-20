using UnityEngine;
using System.Collections;

[System.Serializable]
public class DarkNetArticle
{
    public string articleName;
    public int price;
    public ItemData itemData;
}

public class DarkNetApp : MonoBehaviour
{
    [Header("UI App DarkNet")]
    public GameObject appPanel;

    [Header("Catalogue du DarkNet")]
    public DarkNetArticle[] catalog;

    public void OpenApp() { if (appPanel != null) appPanel.SetActive(true); }
    public void CloseApp() { if (appPanel != null) appPanel.SetActive(false); }

    public void BuyArticle(int index)
    {
        if (GameManager.Instance == null || SafehouseManager.Instance == null) return;
        if (index < 0 || index >= catalog.Length) return;

        DarkNetArticle article = catalog[index];

        if (GameManager.Instance.dirtyMoney >= article.price)
        {
            // 1. On prélève l'argent sale
            GameManager.Instance.dirtyMoney -= article.price;
            GameManager.Instance.SyncDirtyMoneyItem();

            if (article.itemData != null)
            {
                // 2. CORRECTION : On utilise la nouvelle fonction du SafehouseManager
                bool success = SafehouseManager.Instance.AddToStash(article.itemData, 1);

                // 3. SÉCURITÉ : Si le coffre est plein, on annule tout !
                if (!success)
                {
                    // Remboursement
                    GameManager.Instance.dirtyMoney += article.price;
                    GameManager.Instance.SyncDirtyMoneyItem();

                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowNotification("<color=red>Achat annulé : Votre coffre est plein !</color>");

                    return; // On arrête la fonction ici, pas de SMS envoyé.
                }
            }

            if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();

            StartCoroutine(SendTommySMS());
        }
        else
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("<color=red>Fonds illicites insuffisants.</color>");
        }
    }

    private IEnumerator SendTommySMS()
    {
        yield return new WaitForSeconds(Random.Range(10.0f, 30.0f));

        string[] tommyMessages = new string[]
        {
            "Yo, un mec un peu louche a déposé un colis à ton nom. Je l'ai mis direct dans ton coffre.",
            "Gros, t'as reçu une livraison. Je l'ai rangée dans la planque pour pas que ça traîne dans le salon.",
            "Hé, ton paquet est arrivé. J'ai tout mis dans ton coffre perso, t'inquiète je fouille pas !",
            "Un livreur vient de passer. Le colis est au chaud dans ton coffre.",
            "Mec, t'as encore reçu un truc bizarre. C'est rangé dans le coffre avec ton autre matos.",
            "Yo ! J'ai réceptionné ton colis pendant que t'étais pas là. C'est dans le coffre."
        };

        string randomMessage = tommyMessages[Random.Range(0, tommyMessages.Length)];

        if (MessageApp.Instance != null)
        {
            MessageApp.Instance.ReceiveMessage("Tommy", randomMessage, false);
        }
        else if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("Nouveau SMS de Tommy reçu.");
        }
    }
}