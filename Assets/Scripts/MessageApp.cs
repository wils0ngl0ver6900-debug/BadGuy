using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class MessageApp : MonoBehaviour
{
    public static MessageApp Instance;

    [Header("Les Vues (Écrans) 📱")]
    public GameObject messageAppPanel;
    public GameObject contactsView; // Ton nouvel écran Contacts_View
    public GameObject chatView;     // Ton écran Chat_View

    [Header("UI Chat (Bulles) 💬")]
    public Transform chatContentParent; // Le 'Content' de ton ancienne Scroll View
    public GameObject bubblePrefab;
    public TextMeshProUGUI contactNameText; // Le titre en haut du chat

    [Header("UI Contacts (Liste) 👤")]
    public Transform contactsContentParent; // Le 'Content' de ta nouvelle Scroll View
    public GameObject contactButtonPrefab; // Le Prefab Contact_Button

    // Structure de données pour mémoriser un message
    [System.Serializable]
    public class MessageData
    {
        public string text;
        public bool isPlayer;
    }

    // Le cerveau de l'appli : Un dictionnaire qui associe un Nom de contact à son historique de messages
    private Dictionary<string, List<MessageData>> conversations = new Dictionary<string, List<MessageData>>();
    private string currentActiveContact = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        // On simule de faux messages reçus pour tester
        ReceiveMessage("Inconnu", "T'as le matos ? Je t'attends aux docks.", false);
        ReceiveMessage("Inconnu", "J'arrive dans 5 minutes.", true); // Je réponds à l'Inconnu

        ReceiveMessage("Maman", "Tu viens manger ce soir ?", false); // Nouveau contact !

        CloseApp();
    }

    public void OpenApp()
    {
        messageAppPanel.SetActive(true);
        ShowContactsList(); // On ouvre l'appli sur la liste des contacts par défaut
    }

    public void CloseApp()
    {
        messageAppPanel.SetActive(false);
    }

    // --- LA FONCTION MAGIQUE DE RÉCEPTION ---
    public void ReceiveMessage(string contactName, string content, bool isFromPlayer = false)
    {
        // 1. Si on n'a jamais parlé à ce contact, on lui crée un dossier
        if (!conversations.ContainsKey(contactName))
        {
            conversations[contactName] = new List<MessageData>();
        }

        // 2. On sauvegarde le message dans son dossier
        conversations[contactName].Add(new MessageData { text = content, isPlayer = isFromPlayer });

        // 3. Mise à jour de l'affichage
        if (chatView.activeSelf && currentActiveContact == contactName)
        {
            // Si on est DÉJÀ en train de lui parler, on fait poper la bulle en direct
            AddBubbleToScreen(content, isFromPlayer);
        }
        else if (contactsView.activeSelf)
        {
            // Si on est sur le menu, on rafraîchit la liste
            RefreshContactsView();
        }

        if (!isFromPlayer && UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"Nouveau SMS de : {contactName}");
        }
    }

    // --- NAVIGATION ---
    public void ShowContactsList()
    {
        chatView.SetActive(false);
        contactsView.SetActive(true);
        RefreshContactsView();
    }

    public void OpenConversation(string contactName)
    {
        currentActiveContact = contactName;
        contactNameText.text = contactName; // Le titre affiche enfin la bonne personne !

        contactsView.SetActive(false);
        chatView.SetActive(true);

        RefreshChatView();
    }

    // --- GÉNÉRATION VISUELLE ---
    private void RefreshContactsView()
    {
        foreach (Transform child in contactsContentParent) Destroy(child.gameObject);

        // On génère un bouton pour chaque contact dans notre base de données
        foreach (string contactName in conversations.Keys)
        {
            GameObject newBtn = Instantiate(contactButtonPrefab, contactsContentParent);
            newBtn.GetComponentInChildren<TextMeshProUGUI>().text = contactName;

            // On connecte le bouton à la fonction d'ouverture du chat
            string contactToOpen = contactName;
            newBtn.GetComponent<Button>().onClick.AddListener(() => OpenConversation(contactToOpen));
        }
    }

    private void RefreshChatView()
    {
        foreach (Transform child in chatContentParent) Destroy(child.gameObject);

        // On lit tout l'historique du contact et on recrée les bulles
        if (conversations.ContainsKey(currentActiveContact))
        {
            foreach (MessageData msg in conversations[currentActiveContact])
            {
                AddBubbleToScreen(msg.text, msg.isPlayer);
            }
        }
    }

    private void AddBubbleToScreen(string text, bool isPlayer)
    {
        GameObject newBubble = Instantiate(bubblePrefab, chatContentParent);
        MessageBubble bubbleScript = newBubble.GetComponent<MessageBubble>();
        if (bubbleScript != null) bubbleScript.SetupMessage(text, isPlayer);
    }
}