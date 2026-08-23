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

    [Header("Contact Black Knight 🏁 (débloqué par quête)")]
    [Tooltip("Configuré dans l'Inspector (nom, photo) mais PAS ajouté à Contact List au départ — utilise UnlockBlackKnightContact() pour l'ajouter, à relier depuis la UnityEvent de fin de la quête qui le débloque.")]
    public ContactInfo blackKnightContact;
    [Tooltip("Doit correspondre exactement à Black Knight Contact > Contact Name.")]
    public string blackKnightContactName = "Black Knight";
    private bool isBlackKnightUnlocked = false;

    [Header("Dialogue d'appel de Black Knight")]
    [TextArea(2, 4)] public string blackKnightLine1 = "Alors, prêt à faire chauffer le bitume ce soir ?";
    [TextArea(2, 4)] public string blackKnightLine2 = "Cinq bagnoles, une piste, et de l'argent à la clé pour les deux premiers. Tu marches ?";
    [Tooltip("Message affiché si le joueur a déjà couru aujourd'hui (une course par jour, comme les jobs).")]
    [TextArea(2, 4)] public string blackKnightAlreadyRacedLine = "T'as déjà eu ta course pour aujourd'hui. Rappelle-moi demain.";

    // À relier depuis la UnityEvent onDialogueEnd (ou équivalent) de la quête qui débloque
    // Black Knight comme contact appelable.
    public void UnlockBlackKnightContact()
    {
        if (isBlackKnightUnlocked) return;
        isBlackKnightUnlocked = true;

        if (blackKnightContact != null && !contactList.Contains(blackKnightContact))
        {
            contactList.Add(blackKnightContact);
        }

        // GenerateContacts() ne tournait qu'une fois, dans Start() — bien avant que Black
        // Knight n'existe dans la liste. Sans la rappeler ici, il restait invisible dans
        // l'UI même si contactList le contenait bien (elle reconstruit tous les boutons
        // depuis zéro à chaque appel, donc pas de risque de doublon).
        GenerateContacts();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("<color=cyan>Nouveau contact ajouté à ton téléphone : Black Knight.</color>");
    }

    [HideInInspector] public bool callsBlocked = false;

    // Compteur de "demandes de blocage" actives (un par labo ouvert en même temps, par
    // exemple). Le téléphone ne redevient joignable que quand TOUT le monde a relâché
    // son blocage, jamais dès qu'UN SEUL le relâche.
    private static int blockRequests = 0;

    public static void RequestCallBlock()
    {
        blockRequests++;
        if (Instance == null) return;

        Instance.callsBlocked = true;

        // Si un appel est en train de sonner (pas encore décroché) au moment où le
        // blocage démarre, on le coupe proprement plutôt que de le laisser sonner
        // par-dessus le crafting.
        if (Instance.incomingCallPanel != null && Instance.incomingCallPanel.activeSelf && !Instance.isInCall)
        {
            Instance.DeclineCall();
        }
    }

    public static void ReleaseCallBlock()
    {
        blockRequests = Mathf.Max(0, blockRequests - 1);
        if (Instance != null) Instance.callsBlocked = blockRequests > 0;
    }

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
        // --- MISE À JOUR DU VERROU : On bloque l'appel si on est dans le coffre OU un labo ---
        if (isInCall || callsBlocked ||
           (SafehouseManager.Instance != null && SafehouseManager.Instance.isOpen) ||
           (WeedLabManager.Instance != null && WeedLabManager.Instance.isOpen) ||
           (HeroinLabManager.Instance != null && HeroinLabManager.Instance.isOpen) ||
           (CocaineLabManager.Instance != null && CocaineLabManager.Instance.isOpen))
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

        string voicemailLine = "Je suis occupé pour le moment. Laisse un message.";

        // Jimmy est un contact spécial : sa réplique dépend du garage (voir GarageManager).
        // Si un choix de véhicule est nécessaire, GarageManager ouvre son propre panneau et
        // renvoie null — dans ce cas on referme proprement l'appel via EndCall() (sa méthode
        // existante, déjà testée) plutôt que de le laisser actif derrière le panneau de choix
        // sans moyen de raccrocher, ce qui bloquait isInCall à true et cassait tous les
        // appels suivants, Jimmy ou pas.
        if (GarageManager.Instance != null && contactName == GarageManager.Instance.jimmyContactName)
        {
            string result = GarageManager.Instance.CheckDeliveryAvailability();
            if (result == null)
            {
                // EndCall() ferme le téléphone (et redonne un curseur invisible, comportement
                // normal de retour au jeu) — on ouvre le panneau de sélection SEULEMENT
                // après, pas avant, sinon EndCall() écrase le curseur visible juste posé.
                EndCall();
                GarageManager.Instance.OpenDeliverySelection();
                return;
            }
            voicemailLine = result;
        }

        // Black Knight est un autre contact spécial : au lieu d'un répondeur, il propose la
        // course avec un vrai choix Oui/Non (voir l'extension ajoutée à DialogueManager).
        // Oui => la course démarre (StreetRaceManager) ; Non => rien ne se passe. Dans les
        // deux cas l'appel se termine normalement (EndCall(), déjà géré par DialogueManager
        // pour tout dialogue marqué comme appel téléphonique).
        if (contactName == blackKnightContactName)
        {
            // Une course par jour, comme les jobs : s'il a déjà couru aujourd'hui, un
            // simple message (pas de choix Oui/Non, rien à proposer).
            if (StreetRaceManager.Instance != null && StreetRaceManager.Instance.HasRacedToday())
            {
                Dialogue alreadyRaced = new Dialogue();
                alreadyRaced.lines = new DialogueLine[1];
                alreadyRaced.lines[0] = new DialogueLine { speakerName = contactName, sentence = blackKnightAlreadyRacedLine };

                currentCallDialogue = alreadyRaced;
                timerCoroutine = StartCoroutine(CallTimerRoutine());
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.StartDialogue(currentCallDialogue, true);
                }
                return;
            }

            Dialogue raceInvite = new Dialogue();
            raceInvite.lines = new DialogueLine[2];
            raceInvite.lines[0] = new DialogueLine { speakerName = contactName, sentence = blackKnightLine1 };
            raceInvite.lines[1] = new DialogueLine { speakerName = contactName, sentence = blackKnightLine2 };
            raceInvite.hasYesNoChoice = true;
            raceInvite.yesLabel = "Oui";
            raceInvite.noLabel = "Non";

            raceInvite.onYesChoice = new UnityEngine.Events.UnityEvent();
            raceInvite.onYesChoice.AddListener(() =>
            {
                if (StreetRaceManager.Instance != null) StreetRaceManager.Instance.StartRace();
            });
            raceInvite.onNoChoice = new UnityEngine.Events.UnityEvent();
            raceInvite.onNoChoice.AddListener(() =>
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=grey>Peut-être une autre fois.</color>");
            });

            currentCallDialogue = raceInvite;

            timerCoroutine = StartCoroutine(CallTimerRoutine());
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(currentCallDialogue, true);
            }
            return;
        }

        Dialogue voicemail = new Dialogue();
        voicemail.lines = new DialogueLine[1];
        voicemail.lines[0] = new DialogueLine { speakerName = contactName, sentence = voicemailLine };

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