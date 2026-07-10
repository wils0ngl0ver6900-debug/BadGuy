using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// NOUVEAU : La structure qui permet de lier un nom à une photo
[System.Serializable]
public class ContactInfo
{
    public string contactName;
    public Sprite contactPhoto;
}

public class CallApp : MonoBehaviour
{
    public static CallApp Instance;

    [Header("Panneaux de l'Application")]
    public GameObject appPanel;
    public GameObject contactsView;
    public GameObject incomingCallPanel;
    public GameObject activeCallPanel;

    [Header("Textes UI")]
    public TextMeshProUGUI incomingCallerNameText;
    public TextMeshProUGUI activeCallerNameText;
    public TextMeshProUGUI callTimerText;

    [Header("Photos UI (NOUVEAU) 🖼️")]
    public Image incomingCallerPhoto; // L'image sur le pop-up d'appel
    public Image activeCallerPhoto;   // L'image sur l'écran d'appel en cours

    [Header("Génération des Contacts")]
    public Transform contactsContentParent;
    public GameObject contactButtonPrefab;

    // NOUVEAU : Ta nouvelle liste de contacts enrichie
    public List<ContactInfo> contactList = new List<ContactInfo>();

    // --- VARIABLES INTERNES ---
    private string currentCaller = "";
    private Sprite currentCallerPhoto = null;
    private Dialogue currentCallDialogue;
    private Coroutine ringCoroutine;
    private Coroutine timerCoroutine;
    private bool isInCall = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        CloseApp();
        if (incomingCallPanel != null) incomingCallPanel.SetActive(false);
        if (activeCallPanel != null) activeCallPanel.SetActive(false);

        GenerateContacts();
    }

    // ==========================================
    // --- FONCTION DE TEST TEMPORAIRE 🛠️ ---
    // ==========================================
    private void Update()
    {
        // Appuie sur la touche 'T' en jeu pour simuler un appel de Tommy
        if (Input.GetKeyDown(KeyCode.T))
        {
            // 1. On crée un faux dialogue de toutes pièces pour le test
            Dialogue testDialogue = new Dialogue();
            testDialogue.lines = new DialogueLine[2];

            testDialogue.lines[0] = new DialogueLine();
            testDialogue.lines[0].speakerName = "Tommy";
            testDialogue.lines[0].sentence = "Hey boss, c'est Tommy. Faut qu'on parle affaire.";

            testDialogue.lines[1] = new DialogueLine();
            testDialogue.lines[1].speakerName = "Tommy";
            testDialogue.lines[1].sentence = "Passe me voir au garage quand tu as cinq minutes. Raccroche pas au nez !";

            // 2. On déclenche l'appel !
            ReceiveCall("Tommy", testDialogue);
        }
    }

    public void OpenApp()
    {
        if (appPanel != null) appPanel.SetActive(true);
        if (!isInCall)
        {
            contactsView.SetActive(true);
            activeCallPanel.SetActive(false);
        }
    }

    public void CloseApp()
    {
        if (appPanel != null) appPanel.SetActive(false);
    }

    // Outil interne pour retrouver la photo d'un contact via son nom
    private Sprite GetPhotoForContact(string name)
    {
        foreach (ContactInfo info in contactList)
        {
            if (info.contactName == name) return info.contactPhoto;
        }
        return null;
    }

    // ==========================================
    // --- RECEVOIR UN APPEL DE MISSION ---
    // ==========================================

    public void ReceiveCall(string callerName, Dialogue dialogueSequence)
    {
        if (isInCall) return;

        currentCaller = callerName;
        currentCallDialogue = dialogueSequence;
        currentCallerPhoto = GetPhotoForContact(callerName); // On cherche sa photo dans le répertoire

        // 1. Textes et Image
        if (incomingCallerNameText != null) incomingCallerNameText.text = callerName;

        if (incomingCallerPhoto != null)
        {
            if (currentCallerPhoto != null)
            {
                incomingCallerPhoto.sprite = currentCallerPhoto;
                incomingCallerPhoto.gameObject.SetActive(true);
            }
            else
            {
                incomingCallerPhoto.gameObject.SetActive(false); // Cache l'encart si pas de photo
            }
        }

        if (incomingCallPanel != null) incomingCallPanel.SetActive(true);

        // 2. Sonnerie
        if (SettingsApp.Instance != null && !SettingsApp.Instance.isSilentMode)
        {
            ringCoroutine = StartCoroutine(RingRoutine());
        }
    }

    private IEnumerator RingRoutine()
    {
        while (true)
        {
            int ringIndex = PlayerPrefs.GetInt("SavedRingtone", 0);
            if (SettingsApp.Instance != null && SettingsApp.Instance.availableRingtones.Length > ringIndex)
            {
                AudioClip clip = SettingsApp.Instance.availableRingtones[ringIndex];
                if (SettingsApp.Instance.phoneAudioSource != null && clip != null)
                {
                    SettingsApp.Instance.phoneAudioSource.PlayOneShot(clip);
                    yield return new WaitForSeconds(clip.length + 1f);
                }
                else yield return new WaitForSeconds(3f);
            }
            else yield return new WaitForSeconds(3f);
        }
    }

    public void AcceptCall()
    {
        if (ringCoroutine != null) StopCoroutine(ringCoroutine);

        // --- MODIFICATION ICI : On coupe le son brutalement ---
        if (SettingsApp.Instance != null && SettingsApp.Instance.phoneAudioSource != null)
        {
            SettingsApp.Instance.phoneAudioSource.Stop();
        }
        // ------------------------------------------------------

        if (incomingCallPanel != null) incomingCallPanel.SetActive(false);

        isInCall = true;

        OpenApp();
        contactsView.SetActive(false);
        activeCallPanel.SetActive(true);
        if (activeCallerNameText != null) activeCallerNameText.text = currentCaller;

        // Affiche la photo sur l'écran actif
        if (activeCallerPhoto != null)
        {
            if (currentCallerPhoto != null)
            {
                activeCallerPhoto.sprite = currentCallerPhoto;
                activeCallerPhoto.gameObject.SetActive(true);
            }
            else activeCallerPhoto.gameObject.SetActive(false);
        }

        timerCoroutine = StartCoroutine(CallTimerRoutine());

        if (DialogueManager.Instance != null && currentCallDialogue != null)
        {
            DialogueManager.Instance.StartDialogue(currentCallDialogue, true);
        }
    }

    public void DeclineCall()
    {
        if (ringCoroutine != null) StopCoroutine(ringCoroutine);

        // --- MODIFICATION ICI : On coupe le son brutalement ---
        if (SettingsApp.Instance != null && SettingsApp.Instance.phoneAudioSource != null)
        {
            SettingsApp.Instance.phoneAudioSource.Stop();
        }
        // ------------------------------------------------------

        if (incomingCallPanel != null) incomingCallPanel.SetActive(false);

        currentCaller = "";
        currentCallerPhoto = null;
        currentCallDialogue = null;
        isInCall = false;
    }

    // ==========================================
    // --- APPELER UN PNJ ---
    // ==========================================

    private void GenerateContacts()
    {
        foreach (Transform child in contactsContentParent) Destroy(child.gameObject);

        foreach (ContactInfo contact in contactList)
        {
            GameObject newBtn = Instantiate(contactButtonPrefab, contactsContentParent);

            // Le texte du bouton
            TextMeshProUGUI btnText = newBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = contact.contactName;

            // La photo sur le bouton du répertoire (doit s'appeler "Photo" dans ton Prefab)
            Transform photoTransform = newBtn.transform.Find("Photo");
            if (photoTransform != null)
            {
                Image btnImage = photoTransform.GetComponent<Image>();
                if (btnImage != null && contact.contactPhoto != null)
                {
                    btnImage.sprite = contact.contactPhoto;
                    btnImage.gameObject.SetActive(true);
                }
                else if (btnImage != null) btnImage.gameObject.SetActive(false);
            }

            string cName = contact.contactName;
            newBtn.GetComponent<Button>().onClick.AddListener(() => MakeCall(cName));
        }
    }

    public void MakeCall(string contactName)
    {
        if (isInCall) return;

        currentCaller = contactName;
        currentCallerPhoto = GetPhotoForContact(contactName);
        isInCall = true;

        contactsView.SetActive(false);
        activeCallPanel.SetActive(true);
        if (activeCallerNameText != null) activeCallerNameText.text = currentCaller;

        if (activeCallerPhoto != null)
        {
            if (currentCallerPhoto != null)
            {
                activeCallerPhoto.sprite = currentCallerPhoto;
                activeCallerPhoto.gameObject.SetActive(true);
            }
            else activeCallerPhoto.gameObject.SetActive(false);
        }

        Dialogue voicemail = new Dialogue();
        voicemail.lines = new DialogueLine[1];
        voicemail.lines[0] = new DialogueLine { speakerName = contactName, sentence = "Je suis occupé pour le moment. Laisse un message." };

        currentCallDialogue = voicemail;

        timerCoroutine = StartCoroutine(CallTimerRoutine());
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(currentCallDialogue, true);
        }
    }

    public void EndCall()
    {
        isInCall = false;
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);

        activeCallPanel.SetActive(false);
        contactsView.SetActive(true);

        currentCaller = "";
        currentCallerPhoto = null;
        currentCallDialogue = null;
    }

    private IEnumerator CallTimerRoutine()
    {
        int seconds = 0;
        int minutes = 0;

        while (true)
        {
            if (callTimerText != null) callTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            yield return new WaitForSeconds(1f);
            seconds++;
            if (seconds >= 60)
            {
                minutes++;
                seconds = 0;
            }
        }
    }
}