using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Video;

public class CashierJobManager : MonoBehaviour
{
    public static CashierJobManager Instance;

    [Header("UI Globale 🖥️")]
    public GameObject mainJobPanel;

    [Header("État du Job")]
    public bool isJobActive = false;
    public int maxClientsPerShift = 6;
    private int clientsProcessed = 0;
    private int cleanCashEarned = 0;

    [Header("Vidéo de Fond 🎥")]
    public VideoPlayer bgVideoPlayer;

    [Header("Tutoriel 🎓")]
    public GameObject tutorialPanel;
    public Button closeTutorialBtn;

    [Header("Mini-Jeu : La Caisse 💵")]
    public GameObject registerPanel;
    public TextMeshProUGUI gasCostText;
    public TextMeshProUGUI amountPaidText;
    public TextMeshProUGUI currentChangeText;
    public TextMeshProUGUI feedbackText;

    private int currentGasCost;
    private int currentAmountPaid;
    private int currentChangeGiven;
    private int expectedChange;

    [Header("Événement : Le Braquage 🔫")]
    public GameObject robberyPanel;
    public int robberyChancePercent = 30;
    public Button choice1Btn;
    public Button choice2Btn;
    public Button choice3Btn;
    public Button choice4Btn;
    [Tooltip("Liste des objets que le braqueur peut lâcher lors du contre-braquage")]
    public ItemData[] possibleRobberLoot;

    [Header("Timer de Décision ⏱️")]
    public Slider robberyTimerSlider;
    public float robberyDecisionTime = 5f;
    private float currentRobberyTimer;
    private bool isRobberyDecisionActive = false;

    [Header("Transition VHS Tuto 📼")]
    public GameObject vhsTutoPanel;
    public VideoPlayer vhsVideoPlayer;
    public TextMeshProUGUI vhsTutoText;
    public GameObject spaceToContinueObj;

    private bool isVhsTutoActive = false;
    private string pendingMiniGame = "";
    private string fullTutoText = "";
    private Coroutine typewriterCoroutine;

    [Header("--- RÉCOMPENSES DU BRAQUAGE ---")] // NOUVEAU : Section pour l'équilibrage
    [Header("1. Discrétion (Argent Propre)")]
    public int stealthRewardMin = 150;
    public int stealthRewardMax = 400;

    [Header("3. Initié (Argent Sale)")]
    public int insideJobRewardMin = 2000;
    public int insideJobRewardMax = 15000;

    [Header("4. Duel (Sale + Prime Patron)")]
    public int duelDirtyRewardMin = 500;
    public int duelDirtyRewardMax = 1000;
    public int duelCleanBonus = 200;


    [Header("--- MINI-JEUX DU BRAQUAGE ---")]

    [Header("1. Discrétion (Victime) 🥷")]
    public GameObject stealthPanel;
    public Slider stealthSlider;
    public GameObject stealthWarningIcon;
    public TextMeshProUGUI stealthStolenAmountText;
    private bool isStealthActive = false;
    private bool isRobberLookingLethal = false;
    private int stealthMaxAmount = 0;

    [Header("2. Intimidation (Arme) 🎯")]
    public GameObject standoffPanel;
    public Slider standoffSlider;
    private bool isStandoffActive = false;
    private float standoffSpeed = 2.5f;

    [Header("3. Piratage Coffre (Initié) 💻")]
    public GameObject safePanel;
    public TextMeshProUGUI safeKeysText;
    public TextMeshProUGUI safeTimerText;
    private bool isSafeCrackActive = false;
    private List<KeyCode> safeSequence = new List<KeyCode>();
    private int safeCurrentIndex = 0;
    private int safeCurrentRound = 0;
    private int safeMaxRounds = 3;
    private float safeTimer = 15f;

    [Header("4. Le Duel (Déjà configuré) ⏱️")]
    public GameObject quickDrawPanel;
    public RectTransform targetZone;
    public RectTransform targetButtonRect;
    public float reactionTime = 0.8f;
    private Coroutine quickDrawCoroutine;

    private PlayerController playerController;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        PlayerPrefs.DeleteKey("CashierTutorialDone");
        playerController = FindObjectOfType<PlayerController>();

        if (mainJobPanel != null) mainJobPanel.SetActive(false);
        HideAllMiniGames();

        if (closeTutorialBtn != null) closeTutorialBtn.onClick.AddListener(CloseTutorial);
    }

    private void HideAllMiniGames()
    {
        if (registerPanel != null) registerPanel.SetActive(false);
        if (robberyPanel != null) robberyPanel.SetActive(false);
        if (quickDrawPanel != null) quickDrawPanel.SetActive(false);
        if (stealthPanel != null) stealthPanel.SetActive(false);
        if (standoffPanel != null) standoffPanel.SetActive(false);
        if (safePanel != null) safePanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (vhsTutoPanel != null) vhsTutoPanel.SetActive(false);
    }

    public void StartJob()
    {
        if (isJobActive) return;
        isJobActive = true;
        clientsProcessed = 0;
        cleanCashEarned = 0;

        if (mainJobPanel != null) mainJobPanel.SetActive(true);

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(false);
        if (playerController != null) playerController.enabled = false;
        if (HotbarManager.Instance != null && HotbarManager.Instance.cadreSelection != null)
            HotbarManager.Instance.cadreSelection.gameObject.SetActive(false);

        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = false;
        if (CallApp.Instance != null) CallApp.Instance.callsBlocked = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (bgVideoPlayer != null) bgVideoPlayer.Play();

        if (PlayerPrefs.GetInt("CashierTutorialDone", 0) == 0 && tutorialPanel != null)
            tutorialPanel.SetActive(true);
        else
            NextClient();
    }

    public void CloseTutorial()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        PlayerPrefs.SetInt("CashierTutorialDone", 1);
        NextClient();
    }

    private void NextClient()
    {
        if (clientsProcessed >= maxClientsPerShift) { EndJob(); return; }

        if (clientsProcessed > 0 && Random.Range(0, 100) < robberyChancePercent)
        {
            TriggerRobbery();
            return;
        }

        if (registerPanel != null) registerPanel.SetActive(true);
        currentChangeGiven = 0;
        if (feedbackText != null) { feedbackText.text = "Le client paie en espèces. Rendez l'appoint."; feedbackText.color = Color.white; }

        currentGasCost = Random.Range(12, 65);
        int[] bills = { 20, 50, 100 };
        currentAmountPaid = bills[0];
        foreach (int bill in bills) { if (bill > currentGasCost) { currentAmountPaid = bill; break; } }
        if (currentGasCost > 50) currentAmountPaid = 100;

        expectedChange = currentAmountPaid - currentGasCost;
        UpdateRegisterUI();
    }

    public void AddChange(int amount) { currentChangeGiven += amount; UpdateRegisterUI(); }
    public void ResetChange() { currentChangeGiven = 0; UpdateRegisterUI(); }

    private void UpdateRegisterUI()
    {
        if (gasCostText != null) gasCostText.text = $"Essence : <color=red>{currentGasCost}$</color>";
        if (amountPaidText != null) amountPaidText.text = $"Client donne : <color=green>{currentAmountPaid}$</color>";
        if (currentChangeText != null) currentChangeText.text = $"Monnaie rendue : <b>{currentChangeGiven}$</b>";
    }

    public void ValidateChange()
    {
        if (currentChangeGiven == expectedChange)
        {
            cleanCashEarned += 15;
            if (feedbackText != null) feedbackText.text = "<color=green>Appoint parfait ! +15$</color>";
        }
        else
        {
            cleanCashEarned = Mathf.Max(0, cleanCashEarned - 5);
            if (feedbackText != null) feedbackText.text = $"<color=red>Erreur ! Il fallait rendre {expectedChange}$ !</color>";
        }
        clientsProcessed++;
        StartCoroutine(WaitAndNextClient());
    }

    private IEnumerator WaitAndNextClient()
    {
        yield return new WaitForSeconds(1.5f);
        if (registerPanel != null) registerPanel.SetActive(false);
        NextClient();
    }

    // =========================================================
    // ÉVÉNEMENT BRAQUAGE
    // =========================================================

    private void TriggerRobbery()
    {
        if (registerPanel != null) registerPanel.SetActive(false);
        if (robberyPanel != null) robberyPanel.SetActive(true);
        robberyChancePercent = 0;

        isRobberyDecisionActive = true;
        currentRobberyTimer = robberyDecisionTime;
        if (robberyTimerSlider != null)
        {
            robberyTimerSlider.maxValue = robberyDecisionTime;
            robberyTimerSlider.value = robberyDecisionTime;
        }

        bool hasWeapon = false;
        foreach (InventorySlot slot in InventoryManager.Instance.slots)
            if (slot.item != null && slot.item.isWeapon) { hasWeapon = true; break; }

        if (choice2Btn != null)
        {
            choice2Btn.interactable = hasWeapon;
            choice2Btn.GetComponentInChildren<TextMeshProUGUI>().text = hasWeapon ? "Le braquer" : "<color=#555>Sortir une arme (Aucune arme !)</color>";
        }

        if (choice4Btn != null)
        {
            choice4Btn.interactable = hasWeapon;
            choice4Btn.GetComponentInChildren<TextMeshProUGUI>().text = hasWeapon ? "Légitime Défense" : "<color=#555>Légitime Défense (Aucune arme !)</color>";
        }

        bool hasGangControl = false;
        if (TerritoryManager.Instance != null && TerritoryManager.Instance.currentDistrictName != "Inconnu")
        {
            var d = TerritoryManager.Instance.cityDistricts.Find(x => x.districtName == TerritoryManager.Instance.currentDistrictName);
            if (d != null && d.playerControlPercentage >= 50) hasGangControl = true;
        }

        if (choice3Btn != null)
        {
            choice3Btn.interactable = hasGangControl;
            choice3Btn.GetComponentInChildren<TextMeshProUGUI>().text = hasGangControl ? "Associé" : "<color=#555>Associé (Influence Trop faible)</color>";
        }
    }

    // =========================================================
    // TRANSITION VHS (L'attente avant le mini-jeu)
    // =========================================================

    private void TriggerVhsTuto(string miniGameID, string tutoText)
    {
        isRobberyDecisionActive = false;
        if (robberyPanel != null) robberyPanel.SetActive(false);

        pendingMiniGame = miniGameID;
        fullTutoText = tutoText;

        if (vhsTutoPanel != null) vhsTutoPanel.SetActive(true);
        isVhsTutoActive = true;

        if (vhsVideoPlayer != null) vhsVideoPlayer.Play();
        if (spaceToContinueObj != null) spaceToContinueObj.SetActive(false);

        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = StartCoroutine(TypewriterEffect());
    }

    private IEnumerator TypewriterEffect()
    {
        if (vhsTutoText != null)
        {
            vhsTutoText.text = "";
            foreach (char c in fullTutoText)
            {
                vhsTutoText.text += c;
                yield return new WaitForSecondsRealtime(0.02f);
            }
        }

        if (spaceToContinueObj != null) spaceToContinueObj.SetActive(true);
    }

    private void StartPendingMiniGame()
    {
        isVhsTutoActive = false;
        if (vhsTutoPanel != null) vhsTutoPanel.SetActive(false);
        if (vhsVideoPlayer != null) vhsVideoPlayer.Stop();

        switch (pendingMiniGame)
        {
            case "Victim":
                if (stealthPanel != null) stealthPanel.SetActive(true);
                if (stealthSlider != null) stealthSlider.value = 0;
                isStealthActive = true;
                isRobberLookingLethal = false;

                // Calcul dynamique basé sur les variables de l'Inspector
                stealthMaxAmount = Random.Range(stealthRewardMin, stealthRewardMax + 1);
                if (stealthStolenAmountText != null) stealthStolenAmountText.text = "Détourné : <color=#00FF00>0$</color>";

                if (stealthWarningIcon != null) stealthWarningIcon.SetActive(false);
                StartCoroutine(StealthRobberBehavior());
                break;
            case "Standoff":
                if (standoffPanel != null) standoffPanel.SetActive(true);
                isStandoffActive = true;
                break;
            case "InsideJob":
                if (safePanel != null) safePanel.SetActive(true);
                isSafeCrackActive = true;
                safeTimer = 15f;
                safeCurrentRound = 0;
                GenerateSafeSequence();
                break;
            case "Duel":
                if (quickDrawPanel != null) quickDrawPanel.SetActive(true);
                if (targetZone != null && targetButtonRect != null)
                {
                    float randomX = Random.Range(-targetZone.rect.width / 2f + 50f, targetZone.rect.width / 2f - 50f);
                    float randomY = Random.Range(-targetZone.rect.height / 2f + 50f, targetZone.rect.height / 2f - 50f);
                    targetButtonRect.anchoredPosition = new Vector2(randomX, randomY);
                }
                quickDrawCoroutine = StartCoroutine(QuickDrawTimer());
                break;
        }
    }

    private IEnumerator StealthRobberBehavior()
    {
        while (isStealthActive)
        {
            yield return new WaitForSeconds(Random.Range(2.0f, 4.5f));
            if (!isStealthActive) break;

            if (stealthWarningIcon != null) stealthWarningIcon.SetActive(true);

            yield return new WaitForSeconds(0.5f);
            if (!isStealthActive) break;

            isRobberLookingLethal = true;

            yield return new WaitForSeconds(Random.Range(0.8f, 1.5f));

            isRobberLookingLethal = false;
            if (stealthWarningIcon != null) stealthWarningIcon.SetActive(false);
        }
    }

    private void GenerateSafeSequence()
    {
        safeCurrentIndex = 0;
        safeSequence.Clear();
        KeyCode[] possibleKeys = { KeyCode.A, KeyCode.Z, KeyCode.E, KeyCode.Q, KeyCode.S, KeyCode.D };
        for (int i = 0; i < 5; i++) safeSequence.Add(possibleKeys[Random.Range(0, possibleKeys.Length)]);
        UpdateSafeUI();
    }

    public void ChooseVictim()
    {
        TriggerVhsTuto("Victim", "LA MAIN DANS LE SAC\n\nVous jouez la victime consentante. Maintenez [E] pendant 30 secondes pour vider la caisse et pour glisser quelques billets dans vos poches.\n\n<color=red>ATTENTION :</color> Relâchez tout quand le braqueur vous regarde ou c'est la mort.\n\nRécompense : 50% de la caisse (Propre).");
    }

    public void ChooseCounterRobbery()
    {
        TriggerVhsTuto("Standoff", "CONTRE-BRAQUAGE\n\nLe curseur se déplace très vite.\nAppuyez sur [E] pile quand il est dans la <color=green>ZONE VERTE</color> pour dégainer et menacer le braqueur.\n\n<color=red>ATTENTION :</color> Si vous ratez, il tire.\n\nRécompense : Le butin du braqueur.");
    }

    public void ChooseInsideJob()
    {
        TriggerVhsTuto("InsideJob", "LE DÉLIT D'INITIÉ\n\nCe quartier vous appartient, le braqueur vous reconnaît et s'excuse en vous proposant de s'allier et de partager le contenu du grand coffre dans le bureau du patron. Vous êtes complice. L'alarme retentira dans 15 Secondes.\nTapez les 3 séquences de lettres successives pour ouvrir le coffre-fort arrière.\n\n<color=red>ATTENTION :</color> Chaque erreur réduit le timer.\n\nRécompense : Le gros lot (Argent Sale).");
    }

    public void ChooseQuickDraw()
    {
        TriggerVhsTuto("Duel", "LÉGITIME DÉFENSE\n\nC'est lui ou vous.\nUne cible rouge va apparaître aléatoirement sur l'écran.\nCliquez dessus avant qu'il ne presse la détente.\n\n<color=red>ATTENTION :</color> Vous n'avez qu'une fraction de seconde.\n\nRécompense :  Butin + Argent Sale.");
    }

    // =========================================================
    // UPDATE : GESTION DES TIMERS ET MINI-JEUX
    // =========================================================

    private void Update()
    {
        if (isVhsTutoActive)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (vhsTutoText != null && vhsTutoText.text.Length < fullTutoText.Length)
                {
                    if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
                    vhsTutoText.text = fullTutoText;
                    if (spaceToContinueObj != null) spaceToContinueObj.SetActive(true);
                }
                else
                {
                    StartPendingMiniGame();
                }
            }
            return;
        }

        if (isRobberyDecisionActive)
        {
            currentRobberyTimer -= Time.deltaTime;
            if (robberyTimerSlider != null) robberyTimerSlider.value = currentRobberyTimer;

            if (currentRobberyTimer <= 0)
            {
                isRobberyDecisionActive = false;
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Vous avez trop hésité ! Le braqueur a tiré !</color>");
                FailRobberyEvent();
                return;
            }
        }

        if (isStealthActive)
        {
            if (Input.GetKey(KeyCode.E))
            {
                if (isRobberLookingLethal) { FailRobberyEvent(); return; }

                if (stealthSlider != null)
                {
                    stealthSlider.value += Time.deltaTime / 30f;

                    if (stealthStolenAmountText != null)
                    {
                        int currentAmount = Mathf.FloorToInt(stealthSlider.value * stealthMaxAmount);
                        stealthStolenAmountText.text = $"Dans la poche : <color=#00FF00>{currentAmount}$</color>";
                    }

                    if (stealthSlider.value >= 1f) WinRobberyEvent("Victim");
                }
            }
        }

        if (isStandoffActive)
        {
            if (standoffSlider != null) standoffSlider.value = Mathf.PingPong(Time.time * standoffSpeed, 1f);
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (standoffSlider != null && standoffSlider.value >= 0.4f && standoffSlider.value <= 0.6f) WinRobberyEvent("Standoff");
                else FailRobberyEvent();
            }
        }

        if (isSafeCrackActive)
        {
            safeTimer -= Time.deltaTime;
            if (safeTimerText != null) safeTimerText.text = $"Temps : {safeTimer:F1}s";

            if (safeTimer <= 0) { FailSafeEvent(); return; }

            if (Input.anyKeyDown)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) return;

                if (Input.GetKeyDown(safeSequence[safeCurrentIndex]))
                {
                    safeCurrentIndex++;

                    if (safeCurrentIndex >= safeSequence.Count)
                    {
                        safeCurrentRound++;

                        if (safeCurrentRound >= safeMaxRounds)
                        {
                            WinRobberyEvent("InsideJob");
                        }
                        else
                        {
                            GenerateSafeSequence();
                        }
                    }
                    else
                    {
                        UpdateSafeUI();
                    }
                }
                else
                {
                    safeTimer -= 1.5f;
                    if (safeTimerText != null) safeTimerText.color = Color.red;
                    StartCoroutine(ResetTimerColor());
                }
            }
        }
    }

    private void UpdateSafeUI()
    {
        if (safeKeysText == null) return;

        string display = $"<size=70%><color=#AAAAAA>Séquence {safeCurrentRound + 1}/{safeMaxRounds}</color></size>\n\n";

        for (int i = 0; i < safeSequence.Count; i++)
        {
            if (i < safeCurrentIndex) display += $"<color=green>{safeSequence[i]}</color> ";
            else if (i == safeCurrentIndex) display += $"<color=yellow><u>{safeSequence[i]}</u></color> ";
            else display += $"{safeSequence[i]} ";
        }
        safeKeysText.text = display;
    }

    private IEnumerator ResetTimerColor()
    {
        yield return new WaitForSeconds(0.2f);
        if (safeTimerText != null) safeTimerText.color = Color.white;
    }

    private IEnumerator QuickDrawTimer()
    {
        yield return new WaitForSecondsRealtime(reactionTime);
        FailRobberyEvent();
    }

    public void OnTargetClicked()
    {
        if (quickDrawCoroutine != null) StopCoroutine(quickDrawCoroutine);
        WinRobberyEvent("Duel");
    }

    // =========================================================
    // RÉSULTATS DES BRAQUAGES (ÉCHECS & VICTOIRES)
    // =========================================================

    private void FailRobberyEvent()
    {
        isRobberyDecisionActive = false;
        isVhsTutoActive = false;
        isStealthActive = false;
        isStandoffActive = false;
        isSafeCrackActive = false;
        isJobActive = false;

        HideAllMiniGames();
        if (mainJobPanel != null) mainJobPanel.SetActive(false);
        if (bgVideoPlayer != null) bgVideoPlayer.Stop();
        if (vhsVideoPlayer != null) vhsVideoPlayer.Stop();

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(true);
        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = true;
        if (CallApp.Instance != null) CallApp.Instance.callsBlocked = false;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
        if (playerController != null) playerController.enabled = true;

        if (GameManager.Instance != null) GameManager.Instance.Wasted();
    }

    private void FailSafeEvent()
    {
        isRobberyDecisionActive = false;
        isVhsTutoActive = false;
        isStealthActive = false;
        isStandoffActive = false;
        isSafeCrackActive = false;

        HideAllMiniGames();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("<color=orange>Temps écoulé ! L'alarme silencieuse retentit, le braqueur s'enfuit en courant !</color>");

        if (GameManager.Instance != null) GameManager.Instance.ReportCrime(50);
        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();

        EndJob();
    }

    private void WinRobberyEvent(string type)
    {
        isRobberyDecisionActive = false;
        isVhsTutoActive = false;
        isStealthActive = false; isStandoffActive = false; isSafeCrackActive = false;
        HideAllMiniGames();

        if (type == "Victim")
        {
            int myCut = stealthMaxAmount;
            if (GameManager.Instance != null) GameManager.Instance.cleanMoney += myCut;
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Vous avez vidé la caisse et glissé discrètement {myCut}$ (Propre) dans votre poche.");
        }
        else if (type == "Standoff")
        {
            string lootName = "Rien";
            if (possibleRobberLoot.Length > 0 && InventoryManager.Instance != null)
            {
                ItemData stolenItem = possibleRobberLoot[Random.Range(0, possibleRobberLoot.Length)];
                InventoryManager.Instance.AddItem(stolenItem, 1, false);
                lootName = stolenItem.itemName;
            }
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Coup de pression réussi ! Le braqueur panique et lâche : {lootName}");
        }
        else if (type == "InsideJob")
        {
            // Utilisation des variables de l'Inspector
            int hugeCut = Random.Range(insideJobRewardMin, insideJobRewardMax + 1);
            if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(hugeCut);
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>Coffre piraté avec succès ! Vous partagez le butin : +{hugeCut}$ Sale</color>");
        }
        else if (type == "Duel")
        {
            // Utilisation des variables de l'Inspector
            int lootMoney = Random.Range(duelDirtyRewardMin, duelDirtyRewardMax + 1);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddDirtyMoney(lootMoney);
                GameManager.Instance.cleanMoney += duelCleanBonus;
            }
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=#00FF00>TIR PARFAIT !</color> (+{lootMoney}$ Sale, +{duelCleanBonus}$ Prime Patron)");
        }

        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
        EndJob();
    }

    private void EndJob()
    {
        isJobActive = false;
        HideAllMiniGames();
        StartCoroutine(EndJobRoutine());
    }

    private IEnumerator EndJobRoutine()
    {
        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));
        }

        if (mainJobPanel != null) mainJobPanel.SetActive(false);
        if (bgVideoPlayer != null) bgVideoPlayer.Stop();
        if (vhsVideoPlayer != null) vhsVideoPlayer.Stop();

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(true);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        if (playerController != null) playerController.enabled = true;
        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = true;
        if (CallApp.Instance != null) CallApp.Instance.callsBlocked = false;

        if (cleanCashEarned > 0)
        {
            if (GameManager.Instance != null) GameManager.Instance.cleanMoney += cleanCashEarned;
            if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(cleanCashEarned, "Salaire : Station-Service");
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=#00FF41>Service terminé ! Salaire : {cleanCashEarned}$</color>");
        }

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));
            UIManager.Instance.transitionPanel.SetActive(false);
        }
    }
}