using UnityEngine;
using TMPro;

public class MessageApp : MonoBehaviour
{
    public static MessageApp Instance;

    [Header("UI Application ✉️")]
    public GameObject messageAppPanel;
    public Transform messageContentParent; // Le 'Content' de ta Scroll View
    public GameObject bubblePrefab; // Ton prefab Message_Bubble
    public TextMeshProUGUI contactNameText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }
    private void Start()
    {
        // On s'envoie deux faux messages au démarrage du jeu pour tester le design !
        ReceiveMessage("Inconnu", "T'as le matos ? Je t'attends aux docks.", false); // Faux message reçu (Gris)
        ReceiveMessage("Moi", "J'arrive dans 5 minutes. Prépare l'argent.", true); // Fausse réponse (Bleue)

        // On cache l'appli au démarrage
        CloseApp();
    }
    public void OpenApp()
    {
        messageAppPanel.SetActive(true);
    }

    public void CloseApp()
    {
        messageAppPanel.SetActive(false);
    }

    // La fonction magique à appeler pour envoyer un SMS dans le jeu !
    public void ReceiveMessage(string senderName, string content, bool isFromPlayer = false)
    {
        // On met à jour le nom en haut de l'appli
        contactNameText.text = senderName;

        // On crée la bulle visuelle
        GameObject newBubble = Instantiate(bubblePrefab, messageContentParent);
        MessageBubble bubbleScript = newBubble.GetComponent<MessageBubble>();

        if (bubbleScript != null)
        {
            bubbleScript.SetupMessage(content, isFromPlayer);
        }

        // --- Optionnel : Faire vibrer ou sonner le téléphone ---
        if (!isFromPlayer)
        {
            Debug.Log($"🔔 Nouveau message de {senderName} !");
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Nouveau message reçu.");
            // On pourra ajouter un AudioSource ici plus tard !
        }
    }
}