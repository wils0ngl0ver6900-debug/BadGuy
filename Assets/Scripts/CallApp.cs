using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Photos UI 🖼️")]
    public Image incomingCallerPhoto;
    public Image activeCallerPhoto;

    [Header("Génération des Contacts")]
    public Transform contactsContentParent;
    public GameObject contactButtonPrefab;

    public List<ContactInfo> contactList = new List<ContactInfo>();

    [HideInInspector] public bool callsBlocked = false;

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
        StartCoroutine(TriggerTutorialCallAfterDelay(30f));
    }

    private IEnumerator TriggerTutorialCallAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        Dialogue testDialogue = new Dialogue();
        testDialogue.lines = new DialogueLine[2];

        testDialogue.lines[0] = new DialogueLine();
        testDialogue.lines[0].speakerName = "Tommy";
        testDialogue.lines[0].sentence = "Hey boss, c'est Tommy. Faut qu'on parle affaire.";

        testDialogue.lines[1] = new DialogueLine();
        testDialogue.lines[1].speakerName = "Tommy";
        testDialogue.lines[1].sentence = "Passe me voir au garage quand tu as cinq minutes. Raccroche pas au nez !";

        ReceiveCall("Tommy", testDialogue);
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

    private Sprite GetPhotoForContact(string name)
    {
        foreach (ContactInfo info in contactList)
        {
            if (info.contactName == name) return info.contactPhoto;
        }
        return null;
    }

    public void ReceiveCall(string callerName, Dialogue dialogueSequence)
    {
        // --- MISE À JOUR DU VERROU : On bloque l'appel si on est dans le coffre OU la plantation ---
        if (isInCall || callsBlocked ||
           (SafehouseManager.Instance != null && SafehouseManager.Instance.isOpen) ||
           (WeedLabManager.Instance != null && WeedLabManager.Instance.isOpen))
        {
            return;
        }

        currentCaller = callerName;
        currentCallDialogue = dialogueSequence;
        currentCallerPhoto = GetPhotoForContact(callerName);

        if (PhoneManager.Instance != null && !PhoneManager.Instance.isPhoneOpen)
        {
            PhoneManager.Instance.TogglePhone();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ToggleHUD(false, true);
        }

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
                incomingCallerPhoto.gameObject.SetActive(false);
            }
        }

        if (incomingCallPanel != null) incomingCallPanel.SetActive(true);

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

        if (SettingsApp.Instance != null && SettingsApp.Instance.phoneAudioSource != null)
        {
            SettingsApp.Instance.phoneAudioSource.Stop();
        }

        if (incomingCallPanel != null) incomingCallPanel.SetActive(false);

        isInCall = true;

        OpenApp();
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

        timerCoroutine = StartCoroutine(CallTimerRoutine());

        if (DialogueManager.Instance != null && currentCallDialogue != null)
        {
            DialogueManager.Instance.StartDialogue(currentCallDialogue, true);
        }
    }

    public void DeclineCall()
    {
        if (ringCoroutine != null) StopCoroutine(ringCoroutine);

        if (SettingsApp.Instance != null && SettingsApp.Instance.phoneAudioSource != null)
        {
            SettingsApp.Instance.phoneAudioSource.Stop();
        }

        if (incomingCallPanel != null) incomingCallPanel.SetActive(false);

        currentCaller = "";
        currentCallerPhoto = null;
        currentCallDialogue = null;
        isInCall = false;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ToggleHUD(true);
        }

        if (PhoneManager.Instance != null && PhoneManager.Instance.isPhoneOpen)
        {
            PhoneManager.Instance.TogglePhone();
        }
    }

    private void GenerateContacts()
    {
        foreach (Transform child in contactsContentParent) Destroy(child.gameObject);

        foreach (ContactInfo contact in contactList)
        {
            GameObject newBtn = Instantiate(contactButtonPrefab, contactsContentParent);

            TextMeshProUGUI btnText = newBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = contact.contactName;

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
        if (isInCall || callsBlocked) return;

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

        CloseApp();

        if (PhoneManager.Instance != null && PhoneManager.Instance.isPhoneOpen)
        {
            PhoneManager.Instance.TogglePhone();
        }
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