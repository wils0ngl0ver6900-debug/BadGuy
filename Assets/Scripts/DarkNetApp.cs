using UnityEngine;
using System.Collections;

[System.Serializable]
public class DarkNetArticle
{
    public string articleName; // Ex: "AK-47" (Sert uniquement pour toi dans l'UI)
    public int price; // Prix en argent sale

    [Tooltip("Glisse ici le ScriptableObject (ItemData) de l'arme ou de l'objet")]
    public ItemData itemData;
}

public class DarkNetApp : MonoBehaviour
{
    [Header("UI App DarkNet")]
    [Tooltip("Le Panel principal de l'application DarkNet")]
    public GameObject appPanel;

    [Header("Catalogue du DarkNet")]
    [Tooltip("Ajoute ici tous les objets achetables")]
    public DarkNetArticle[] catalog;

    // --- INTERFACE ---
    public void OpenApp()
    {
        if (appPanel != null) appPanel.SetActive(true);
    }

    public void CloseApp()
    {
        if (appPanel != null) appPanel.SetActive(false);
    }

    // --- SYSTÈME D'ACHAT ---
    public void BuyArticle(int index)
    {
        if (GameManager.Instance == null || SafehouseManager.Instance == null) return;
        if (index < 0 || index >= catalog.Length) return;

        DarkNetArticle article = catalog[index];

        if (GameManager.Instance.dirtyMoney >= article.price)
        {
            // 1. Paiement
            GameManager.Instance.dirtyMoney -= article.price;
            GameManager.Instance.SyncDirtyMoneyItem();

            // 2. LIVRAISON VIRTUELLE DANS LA STASHBOX
            if (article.itemData != null)
            {
                SafehouseManager.Instance.storedIllegalItems.Add(article.itemData);
            }

            // 3. Mise à jour de l'écran du joueur
            if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();

            // 4. Déclenchement du SMS de Tommy
            StartCoroutine(SendTommySMS());
        }
        else
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("<color=red>Fonds illicites insuffisants.</color>");
        }
    }

    // --- LE SMS ALÉATOIRE DE TOMMY (Le colocataire) ---
    private IEnumerator SendTommySMS()
    {
        // Délai de livraison du message (entre 10 et 30 secondes)
        yield return new WaitForSeconds(Random.Range(10.0f, 30.0f));

        // Les 6 messages d'un pote qui héberge tes colis louches
        string[] tommyMessages = new string[]
        {
            "Yo, un mec un peu louche a déposé un colis à ton nom. Je l'ai mis direct dans ton coffre.",
            "Gros, t'as reçu une livraison. Je l'ai rangée dans la planque pour pas que ça traîne dans le salon.",
            "Hé, ton paquet est arrivé. J'ai tout mis dans ton coffre perso, t'inquiète je fouille pas !",
            "Un livreur vient de passer. Le colis est au chaud dans ton coffre.",
            "Mec, t'as encore reçu un truc bizarre. C'est rangé dans le coffre avec ton autre matos.",
            "Yo ! J'ai réceptionné ton colis pendant que t'étais pas là. C'est dans le coffre."
        };

        // Tirage au sort d'un des messages
        string randomMessage = tommyMessages[Random.Range(0, tommyMessages.Length)];

        // Connexion avec ton système de messagerie
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