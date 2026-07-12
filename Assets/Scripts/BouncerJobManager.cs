using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class BouncerJobManager : MonoBehaviour
{
    public static BouncerJobManager Instance;

    [Header("Économie (Récompenses) 💰")]
    public int rewardPerValidClient = 25;
    public int penaltyPerError = 15;

    [Header("UI du Mini-Jeu 🚪")]
    public GameObject bouncerUIPanel;
    private CanvasGroup bouncerCanvasGroup;

    [Header("Tutoriel & Conteneur 🎓")]
    public GameObject tutorialPanel;
    public Button closeTutorialBtn;
    public GameObject gameplayContainer;

    [Header("Animation & UI Carte 🪪")]
    public RectTransform idCardTransform;
    private Vector2 idCardOriginalPos;

    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dobText;
    public TextMeshProUGUI expDateText;
    public TextMeshProUGUI issueDateText;
    public TextMeshProUGUI pobText;
    public Image photoImage;

    [Header("Système de Pot-de-vin 💵")]
    public TextMeshProUGUI bribeText;
    private bool isBribeOffered = false;
    private int bribeAmount = 0;

    [Header("Génération : Hommes 🧔")]
    public Sprite[] malePhotos;
    public string[] maleFirstNames = { "John", "Mike", "David", "Tommy", "Carl", "Claude", "Niko", "Franklin", "Trevor", "Vito" };

    [Header("Génération : Femmes 👩")]
    public Sprite[] femalePhotos;
    public string[] femaleFirstNames = { "Sarah", "Emma", "Chloe", "Lucia", "Catalina", "Maria", "Mercedes", "Amanda", "Tracey" };
    public string[] lastNames = { "Smith", "Doe", "Brown", "Wilson", "Miller", "Vercetti", "Bellic", "Vance", "Clinton", "Scaletta" };

    [Header("Détecteur de Métaux & Audio 🧲🎵")]
    public AudioSource backgroundMusic;
    private float defaultMusicVolume;

    public RectTransform scannerCursor;
    public RectTransform clientSilhouette;
    public Image scannerLight;
    public AudioSource beepAudio;
    public RectTransform weaponZone;

    private Vector2[] weaponSpots = new Vector2[] {
        new Vector2(-50f, 30f),
        new Vector2(50f, 30f),
        new Vector2(0f, -80f),
        new Vector2(-45f, -250f),
        new Vector2(45f, -250f)
    };

    [Header("Contrôles & Feedback")]
    public Button acceptBtn;
    public Button denyBtn;
    public TextMeshProUGUI feedbackText;

    private bool hasWeapon = false;
    private bool isIdValid = true;
    private bool isJobActive = false;
    private int clientsProcessed = 0;
    private int cashEarned = 0;
    public int maxClientsPerShift = 10;
    private int currentYear = 2026;

    private int currentErrorType = 0;
    private bool currentClientIsOld = false;
    private int currentClientGender = 0;

    // --- NOUVEAU : Mémoire du dernier visage utilisé ---
    private Sprite lastUsedPhoto = null;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        // LIGNE TEMPORAIRE POUR FORCER L'AFFICHAGE DU TUTORIEL À CHAQUE FOIS
        PlayerPrefs.DeleteKey("BouncerTutorialDone");

        if (bouncerUIPanel != null)
        {
            bouncerUIPanel.SetActive(false);
            bouncerCanvasGroup = bouncerUIPanel.GetComponent<CanvasGroup>();
            if (bouncerCanvasGroup == null) bouncerCanvasGroup = bouncerUIPanel.AddComponent<CanvasGroup>();
        }

        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (bribeText != null) bribeText.gameObject.SetActive(false);

        if (acceptBtn != null) acceptBtn.onClick.AddListener(() => Decide(true));
        if (denyBtn != null) denyBtn.onClick.AddListener(() => Decide(false));
        if (closeTutorialBtn != null) closeTutorialBtn.onClick.AddListener(CloseTutorial);

        if (idCardTransform != null) idCardOriginalPos = idCardTransform.anchoredPosition;

        if (backgroundMusic != null) defaultMusicVolume = backgroundMusic.volume;
    }

    public void StartJob()
    {
        if (isJobActive) return;
        isJobActive = true;
        clientsProcessed = 0;
        cashEarned = 0;
        lastUsedPhoto = null; // On réinitialise la mémoire au début du service

        bouncerUIPanel.SetActive(true);
        if (bouncerCanvasGroup != null) bouncerCanvasGroup.alpha = 1f;

        if (backgroundMusic != null)
        {
            backgroundMusic.volume = defaultMusicVolume;
            backgroundMusic.Play();
        }

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(false);

        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) pc.enabled = false;

        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = false;
        if (CallApp.Instance != null) CallApp.Instance.callsBlocked = true;

        if (PlayerPrefs.GetInt("BouncerTutorialDone", 0) == 0 && tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            if (gameplayContainer != null) gameplayContainer.SetActive(false);

            CanvasGroup tutoCG = tutorialPanel.GetComponent<CanvasGroup>();
            if (tutoCG != null) tutoCG.alpha = 1f;

            if (closeTutorialBtn != null) closeTutorialBtn.interactable = true;
            Cursor.visible = true;
        }
        else
        {
            if (gameplayContainer != null)
            {
                gameplayContainer.SetActive(true);
                CanvasGroup gameCG = gameplayContainer.GetComponent<CanvasGroup>();
                if (gameCG != null) gameCG.alpha = 1f;
            }
            GenerateNewClient();
        }
    }

    public void CloseTutorial()
    {
        if (closeTutorialBtn != null) closeTutorialBtn.interactable = false;
        StartCoroutine(FadeTutorialToGameplayRoutine());
    }

    private IEnumerator FadeTutorialToGameplayRoutine()
    {
        float duration = 0.5f;
        float elapsed = 0f;

        CanvasGroup tutoCG = tutorialPanel.GetComponent<CanvasGroup>();
        if (tutoCG == null) tutoCG = tutorialPanel.AddComponent<CanvasGroup>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            tutoCG.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }

        tutorialPanel.SetActive(false);
        tutoCG.alpha = 1f;

        if (gameplayContainer != null)
        {
            gameplayContainer.SetActive(true);

            CanvasGroup gameCG = gameplayContainer.GetComponent<CanvasGroup>();
            if (gameCG == null) gameCG = gameplayContainer.AddComponent<CanvasGroup>();

            gameCG.alpha = 0f;
            elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                gameCG.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }
            gameCG.alpha = 1f;
        }

        PlayerPrefs.SetInt("BouncerTutorialDone", 1);
        PlayerPrefs.Save();

        GenerateNewClient();
    }

    public void EndJob()
    {
        isJobActive = false;
        StartCoroutine(FadeOutJobRoutine());
    }

    private IEnumerator FadeOutJobRoutine()
    {
        float duration = 1.0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (bouncerCanvasGroup != null) bouncerCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            if (backgroundMusic != null) backgroundMusic.volume = Mathf.Lerp(defaultMusicVolume, 0f, t);

            yield return null;
        }

        bouncerUIPanel.SetActive(false);
        if (backgroundMusic != null) backgroundMusic.Stop();

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;

        yield return new WaitForEndOfFrame();

        Cursor.lockState = CursorLockMode.Confined;

        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) pc.enabled = true;

        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = true;
        if (CallApp.Instance != null) CallApp.Instance.callsBlocked = false;

        if (GameManager.Instance != null && cashEarned > 0)
        {
            GameManager.Instance.cleanMoney += cashEarned;

            if (BankApp.Instance != null)
            {
                BankApp.Instance.RecordTransaction(cashEarned, "Salaire : Service Sécurité");
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification($"<color=#00FF41>Service terminé ! Salaire : {cashEarned}$ (Argent Propre)</color>");
                UIManager.Instance.UpdateHUD();
            }
        }
    }

    private void GenerateNewClient()
    {
        feedbackText.text = "Examinez le client...";
        acceptBtn.interactable = true;
        denyBtn.interactable = true;
        scannerLight.color = Color.green;

        if (bribeText != null) bribeText.gameObject.SetActive(false);
        isBribeOffered = false;

        currentErrorType = Random.Range(0, 4);

        isIdValid = (currentErrorType != 1 && currentErrorType != 2);
        hasWeapon = (currentErrorType == 3);

        currentClientGender = Random.Range(0, 2);
        string chosenFirstName = "";
        Sprite chosenPhoto = null;
        int attempts = 0; // Sécurité anti-boucle infinie

        // --- NOUVEAU : Sélection intelligente de la photo ---
        if (currentClientGender == 0)
        {
            chosenFirstName = maleFirstNames[Random.Range(0, maleFirstNames.Length)];
            if (malePhotos.Length > 0)
            {
                do
                {
                    chosenPhoto = malePhotos[Random.Range(0, malePhotos.Length)];
                    attempts++;
                }
                // On boucle TANT QUE c'est la même photo, qu'il y a plus d'1 photo dispo, et qu'on a fait moins de 10 essais
                while (chosenPhoto == lastUsedPhoto && malePhotos.Length > 1 && attempts < 10);
            }
        }
        else
        {
            chosenFirstName = femaleFirstNames[Random.Range(0, femaleFirstNames.Length)];
            if (femalePhotos.Length > 0)
            {
                do
                {
                    chosenPhoto = femalePhotos[Random.Range(0, femalePhotos.Length)];
                    attempts++;
                }
                while (chosenPhoto == lastUsedPhoto && femalePhotos.Length > 1 && attempts < 10);
            }
        }

        // On sauvegarde cette nouvelle photo pour le prochain client
        lastUsedPhoto = chosenPhoto;

        string chosenLastName = lastNames[Random.Range(0, lastNames.Length)];
        nameText.text = chosenFirstName + " " + chosenLastName;
        if (chosenPhoto != null) photoImage.sprite = chosenPhoto;

        string[] cities = { "New York", "Los Angeles", "Chicago", "Miami", "Houston", "Detroit", "Boston" };
        if (pobText != null) pobText.text = "Lieu de naissance : " + cities[Random.Range(0, cities.Length)];

        currentClientIsOld = (chosenPhoto != null && chosenPhoto.name.ToLower().Contains("old"));
        int birthYear = 0;

        if (currentErrorType == 2) birthYear = Random.Range(currentYear - 20, currentYear - 17);
        else
        {
            if (currentClientIsOld) birthYear = Random.Range(1940, 1970);
            else birthYear = Random.Range(1985, currentYear - 22);
        }

        dobText.text = $"Date de naissance : {Random.Range(1, 28):D2}/{Random.Range(1, 13):D2}/{birthYear}";

        if (currentErrorType == 1) expDateText.text = $"Expiration : {Random.Range(1, 28):D2}/{Random.Range(1, 13):D2}/{currentYear - Random.Range(1, 4)}";
        else expDateText.text = $"Expiration : {Random.Range(1, 28):D2}/{Random.Range(1, 13):D2}/{currentYear + Random.Range(1, 5)}";

        if (issueDateText != null)
        {
            int issueYear = currentYear - Random.Range(1, 6);
            issueDateText.text = $"Délivrance : {Random.Range(1, 28):D2}/{Random.Range(1, 13):D2}/{issueYear}";
        }

        if (hasWeapon)
        {
            Vector2 chosenSpot = weaponSpots[Random.Range(0, weaponSpots.Length)];
            weaponZone.anchoredPosition = chosenSpot;
            weaponZone.gameObject.SetActive(true);
        }
        else weaponZone.gameObject.SetActive(false);

        if ((hasWeapon || !isIdValid) && Random.value < 0.3f)
        {
            isBribeOffered = true;
            bribeAmount = Random.Range(50, 150);
            if (bribeText != null)
            {
                bribeText.text = $"<color=red>Un mafieux vous glisse {bribeAmount}$ (Sale) pour fermer les yeux.</color>";
                bribeText.gameObject.SetActive(true);
            }
        }

        if (idCardTransform != null) StartCoroutine(AnimateCardInRoutine());
    }

    private IEnumerator AnimateCardInRoutine()
    {
        float duration = 0.4f;
        float elapsed = 0f;

        Vector2 startPos = new Vector2(-1500f, idCardOriginalPos.y);
        idCardTransform.anchoredPosition = startPos;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);

            idCardTransform.anchoredPosition = Vector2.Lerp(startPos, idCardOriginalPos, t);
            yield return null;
        }

        idCardTransform.anchoredPosition = idCardOriginalPos;
    }

    private IEnumerator AnimateCardOutRoutine()
    {
        float duration = 0.4f;
        float elapsed = 0f;

        Vector2 endPos = new Vector2(-1500f, idCardOriginalPos.y);
        Vector2 startPos = idCardOriginalPos;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);

            if (idCardTransform != null)
                idCardTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }
    }

    private void Update()
    {
        if (!isJobActive || (tutorialPanel != null && tutorialPanel.activeSelf)) return;

        if (clientSilhouette != null && RectTransformUtility.RectangleContainsScreenPoint(clientSilhouette, Input.mousePosition, null))
        {
            if (!scannerCursor.gameObject.activeSelf) scannerCursor.gameObject.SetActive(true);
            scannerCursor.position = Input.mousePosition;
            Cursor.visible = false;

            if (hasWeapon && weaponZone != null && RectTransformUtility.RectangleContainsScreenPoint(weaponZone, Input.mousePosition, null))
            {
                scannerLight.color = Color.red;
                if (beepAudio != null && !beepAudio.isPlaying) beepAudio.Play();
            }
            else
            {
                scannerLight.color = Color.green;
                if (beepAudio != null) beepAudio.Stop();
            }
        }
        else
        {
            if (scannerCursor.gameObject.activeSelf) scannerCursor.gameObject.SetActive(false);
            Cursor.visible = true;

            scannerLight.color = Color.green;
            if (beepAudio != null) beepAudio.Stop();
        }
    }

    public void Decide(bool allowEntry)
    {
        acceptBtn.interactable = false;
        denyBtn.interactable = false;
        if (bribeText != null) bribeText.gameObject.SetActive(false);

        string texteClient = (currentClientGender == 1) ? "La cliente" : "Le client";
        string texteArme = (currentClientGender == 1) ? "armée" : "armé";
        string texteMineur = (currentClientGender == 1) ? "Cette cliente était mineure" : "Ce client était mineur";
        string texteClean = (currentClientGender == 1) ? "Elle était clean" : "Il était clean";
        string texteSenior = (currentClientGender == 1) ? "cette senior" : "ce senior";
        string texteSuivant = (currentClientGender == 1) ? "Cliente suivante." : "Client suivant.";

        if (allowEntry && isBribeOffered)
        {
            if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(bribeAmount);
            feedbackText.text = $"<color=orange>Pot-de-vin accepté ! +{bribeAmount}$ (Sale), le boss n'y a vu que du feu.</color>";
            clientsProcessed++;
            StartCoroutine(NextClientRoutine());
            return;
        }

        bool isCorrect = (allowEntry) ? (isIdValid && !hasWeapon) : (!isIdValid || hasWeapon);

        if (isCorrect)
        {
            cashEarned += rewardPerValidClient;
            feedbackText.text = $"<color=green>Bon choix ! {texteSuivant}</color>";
        }
        else
        {
            cashEarned -= penaltyPerError;
            if (cashEarned < 0) cashEarned = 0;

            if (allowEntry)
            {
                if (hasWeapon)
                    feedbackText.text = $"<color=red>ERREUR : {texteClient} était {texteArme}, la sécu a dû intervenir !</color>";
                else if (currentErrorType == 2)
                {
                    if (currentClientIsOld)
                        feedbackText.text = $"<color=orange>ERREUR : Faux papiers évidents ! L'âge sur la carte ne correspond pas du tout à {texteSenior}.</color>";
                    else
                        feedbackText.text = $"<color=orange>ERREUR : {texteMineur} !</color>";
                }
                else if (currentErrorType == 1)
                    feedbackText.text = "<color=orange>ERREUR : Sa pièce d'identité était expirée.</color>";
            }
            else
            {
                feedbackText.text = $"<color=yellow>ERREUR : {texteClean}, le patron va râler pour le chiffre d'affaires.</color>";
            }
        }

        clientsProcessed++;
        StartCoroutine(NextClientRoutine());
    }

    private IEnumerator NextClientRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        if (idCardTransform != null) yield return StartCoroutine(AnimateCardOutRoutine());

        yield return new WaitForSeconds(0.5f);

        if (clientsProcessed < maxClientsPerShift) GenerateNewClient();
        else EndJob();
    }
}