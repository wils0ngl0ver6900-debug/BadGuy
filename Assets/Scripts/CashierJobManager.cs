using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Video;

public class CashierJobManager : MonoBehaviour
{
    public static CashierJobManager Instance;

    [Header("UI Globale 🖥️")]
    public GameObject mainJobPanel; // <-- NOUVEAU : Le conteneur principal du job

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

    [Header("Butin du Braqueur 💎🌿")]
    public ItemData[] possibleRobberLoot;

    [Header("Mini-Jeu : Le Duel (Quick Draw) 🎯")]
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

        // --- EXTINCTION FORCÉE AU DÉMARRAGE DU JEU ---
        if (mainJobPanel != null) mainJobPanel.SetActive(false);

        if (registerPanel != null) registerPanel.SetActive(false);
        if (robberyPanel != null) robberyPanel.SetActive(false);
        if (quickDrawPanel != null) quickDrawPanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);

        if (closeTutorialBtn != null) closeTutorialBtn.onClick.AddListener(CloseTutorial);
    }

    public void StartJob()
    {
        if (isJobActive) return;
        isJobActive = true;
        clientsProcessed = 0;
        cleanCashEarned = 0;

        // --- ALLUMAGE DE TOUTE L'INTERFACE DU JOB ---
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
        {
            tutorialPanel.SetActive(true);
        }
        else
        {
            NextClient();
        }
    }

    public void CloseTutorial()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        PlayerPrefs.SetInt("CashierTutorialDone", 1);
        NextClient();
    }

    private void NextClient()
    {
        if (clientsProcessed >= maxClientsPerShift)
        {
            EndJob();
            return;
        }

        if (clientsProcessed > 0 && Random.Range(0, 100) < robberyChancePercent)
        {
            TriggerRobbery();
            return;
        }

        if (registerPanel != null) registerPanel.SetActive(true);

        currentChangeGiven = 0;

        if (feedbackText != null)
        {
            feedbackText.text = "Le client paie en espèces. Rendez l'appoint.";
            feedbackText.color = Color.white;
        }

        currentGasCost = Random.Range(12, 65);

        int[] bills = { 20, 50, 100 };
        currentAmountPaid = bills[0];
        foreach (int bill in bills) { if (bill > currentGasCost) { currentAmountPaid = bill; break; } }
        if (currentGasCost > 50) currentAmountPaid = 100;

        expectedChange = currentAmountPaid - currentGasCost;

        UpdateRegisterUI();
    }

    public void AddChange(int amount)
    {
        currentChangeGiven += amount;
        UpdateRegisterUI();
    }

    public void ResetChange()
    {
        currentChangeGiven = 0;
        UpdateRegisterUI();
    }

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
            clientsProcessed++;
            StartCoroutine(WaitAndNextClient());
        }
        else
        {
            cleanCashEarned -= 5;
            if (cleanCashEarned < 0) cleanCashEarned = 0;
            if (feedbackText != null) feedbackText.text = $"<color=red>Erreur ! Il fallait rendre {expectedChange}$ !</color>";
            clientsProcessed++;
            StartCoroutine(WaitAndNextClient());
        }
    }

    private IEnumerator WaitAndNextClient()
    {
        yield return new WaitForSeconds(1.5f);
        if (registerPanel != null) registerPanel.SetActive(false);
        NextClient();
    }

    private void TriggerRobbery()
    {
        if (registerPanel != null) registerPanel.SetActive(false);
        if (robberyPanel != null) robberyPanel.SetActive(true);
        robberyChancePercent = 0;

        bool hasWeapon = false;
        foreach (InventorySlot slot in InventoryManager.Instance.slots)
        {
            if (slot.item != null && slot.item.isWeapon) { hasWeapon = true; break; }
        }

        if (choice2Btn != null)
        {
            choice2Btn.interactable = hasWeapon;
            TextMeshProUGUI btnTxt = choice2Btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnTxt != null) btnTxt.text = hasWeapon ? "Sortir une arme (Le braquer)" : "<color=#555>Sortir une arme (Aucune Arme !)</color>";
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
            TextMeshProUGUI btnTxt = choice3Btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnTxt != null) btnTxt.text = hasGangControl ? "Délit d'initié (Vous tenez la zone)" : "<color=#555>Délit d'initié (Trop peu de respect ici)</color>";
        }
    }

    public void ChooseVictim()
    {
        if (robberyPanel != null) robberyPanel.SetActive(false);
        int registerTotal = Random.Range(300, 800);
        int myCut = registerTotal / 2;

        if (GameManager.Instance != null) GameManager.Instance.cleanMoney += myCut;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"Vous avez vidé la caisse. Vous glissez discrètement {myCut}$ (Propre) dans votre poche.");
            UIManager.Instance.UpdateHUD();
        }

        EndJob();
    }

    public void ChooseCounterRobbery()
    {
        if (robberyPanel != null) robberyPanel.SetActive(false);

        string lootName = "Rien";
        if (possibleRobberLoot.Length > 0 && InventoryManager.Instance != null)
        {
            ItemData stolenItem = possibleRobberLoot[Random.Range(0, possibleRobberLoot.Length)];
            InventoryManager.Instance.AddItem(stolenItem, 1, false);
            lootName = stolenItem.itemName;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"Le braqueur panique et lâche ses affaires ! Vous récupérez : {lootName}");
            UIManager.Instance.UpdateHUD();
        }

        EndJob();
    }

    public void ChooseInsideJob()
    {
        if (robberyPanel != null) robberyPanel.SetActive(false);

        int hugeCut = Random.Range(2000, 15000);
        if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(hugeCut);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"<color=red>Le braqueur vous reconnaît ! Vous videz le coffre-fort arrière ensemble. (+{hugeCut}$ Sale)</color>");
            UIManager.Instance.UpdateHUD();
        }

        EndJob();
    }

    public void ChooseQuickDraw()
    {
        if (robberyPanel != null) robberyPanel.SetActive(false);
        if (quickDrawPanel != null) quickDrawPanel.SetActive(true);

        if (targetZone != null && targetButtonRect != null)
        {
            float randomX = Random.Range(-targetZone.rect.width / 2f + 50f, targetZone.rect.width / 2f - 50f);
            float randomY = Random.Range(-targetZone.rect.height / 2f + 50f, targetZone.rect.height / 2f - 50f);
            targetButtonRect.anchoredPosition = new Vector2(randomX, randomY);
        }

        quickDrawCoroutine = StartCoroutine(QuickDrawTimer());
    }

    private IEnumerator QuickDrawTimer()
    {
        yield return new WaitForSecondsRealtime(reactionTime);

        if (quickDrawPanel != null) quickDrawPanel.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        if (GameManager.Instance != null) GameManager.Instance.Wasted();
        isJobActive = false;
    }

    public void OnTargetClicked()
    {
        if (quickDrawCoroutine != null) StopCoroutine(quickDrawCoroutine);
        if (quickDrawPanel != null) quickDrawPanel.SetActive(false);

        int lootMoney = Random.Range(500, 1000);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddDirtyMoney(lootMoney);
            GameManager.Instance.cleanMoney += 200;
        }

        string lootName = "Rien";
        if (possibleRobberLoot.Length > 0 && InventoryManager.Instance != null)
        {
            ItemData stolenItem = possibleRobberLoot[Random.Range(0, possibleRobberLoot.Length)];
            InventoryManager.Instance.AddItem(stolenItem, 1, false);
            lootName = stolenItem.itemName;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"<color=#00FF00>TIR PARFAIT !</color> (+{lootMoney}$ Sale, +200$ Prime, Butin: {lootName})");
            UIManager.Instance.UpdateHUD();
        }

        EndJob();
    }

    private void EndJob()
    {
        isJobActive = false;
        if (registerPanel != null) registerPanel.SetActive(false);
        if (robberyPanel != null) robberyPanel.SetActive(false);
        if (quickDrawPanel != null) quickDrawPanel.SetActive(false);

        StartCoroutine(EndJobRoutine());
    }

    private IEnumerator EndJobRoutine()
    {
        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));
        }

        // --- EXTINCTION DE TOUTE L'INTERFACE DU JOB ---
        if (mainJobPanel != null) mainJobPanel.SetActive(false);
        if (bgVideoPlayer != null) bgVideoPlayer.Stop();

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