using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class MessageApp : MonoBehaviour
{
    public static MessageApp Instance;

    [Header("Les Vues (Écrans) 📱")]
    public GameObject messageAppPanel;
    public GameObject contactsView;
    public GameObject chatView;

    [Header("UI Chat (Bulles) 💬")]
    public Transform chatContentParent;
    public GameObject bubblePrefab;
    public TextMeshProUGUI contactNameText;

    [Header("UI Contacts (Liste) 👤")]
    public Transform contactsContentParent;
    public GameObject contactButtonPrefab;

    [System.Serializable]
    public class MessageData
    {
        public string text;
        public bool isPlayer;
    }

    private Dictionary<string, List<MessageData>> conversations = new Dictionary<string, List<MessageData>>();
    private string currentActiveContact = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
       

        CloseApp();
    }

    public void OpenApp()
    {
        messageAppPanel.SetActive(true);
        ShowContactsList();
    }

    public void CloseApp()
    {
        messageAppPanel.SetActive(false);
    }

    // --- LA FONCTION MAGIQUE DE RÉCEPTION ---
    public void ReceiveMessage(string contactName, string content, bool isFromPlayer = false)
    {
        if (!conversations.ContainsKey(contactName))
        {
            conversations[contactName] = new List<MessageData>();
        }

        conversations[contactName].Add(new MessageData { text = content, isPlayer = isFromPlayer });

        if (chatView.activeSelf && currentActiveContact == contactName)
        {
            AddBubbleToScreen(content, isFromPlayer);
        }
        else if (contactsView.activeSelf)
        {
            RefreshContactsView();
        }

        // --- C'EST ICI QUE LE SON EST AJOUTÉ ---
        if (!isFromPlayer)
        {
            // On affiche la notification visuelle
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification($"Nouveau SMS de : {contactName}");
            }

            // On joue le son SMS choisi dans les paramètres
            if (SettingsApp.Instance != null)
            {
                SettingsApp.Instance.PlayIncomingSMS();
            }
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
        contactNameText.text = contactName;

        contactsView.SetActive(false);
        chatView.SetActive(true);

        RefreshChatView();
    }

    // --- GÉNÉRATION VISUELLE ---
    private void RefreshContactsView()
    {
        foreach (Transform child in contactsContentParent) Destroy(child.gameObject);

        foreach (string contactName in conversations.Keys)
        {
            GameObject newBtn = Instantiate(contactButtonPrefab, contactsContentParent);
            newBtn.GetComponentInChildren<TextMeshProUGUI>().text = contactName;

            string contactToOpen = contactName;
            newBtn.GetComponent<Button>().onClick.AddListener(() => OpenConversation(contactToOpen));
        }
    }

    private void RefreshChatView()
    {
        foreach (Transform child in chatContentParent) Destroy(child.gameObject);

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