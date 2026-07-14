using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class DockerJobManager : MonoBehaviour
{
    public static DockerJobManager Instance;

    [Header("État du Job")]
    public bool isJobActive = false;
    [HideInInspector] public int totalCratesToDeliver = 5;
    private int cratesProcessed = 0;
    private int cashEarned = 0;

    [Header("Génération des Caisses 📦")]
    public GameObject cratePrefab;
    public Transform[] crateSpawnPoints;
    [Range(0, 100)] public int illegalCrateChance = 10;
    private List<GameObject> spawnedCrates = new List<GameObject>();

    [Header("Économie (Légal)")]
    public int cleanReward = 30;

    [Header("Récompenses Illégales (Drogue) 💊")]
    public ItemData[] possibleDrugRewards;
    public int minDrugAmount = 5;
    public int maxDrugAmount = 20;
    public int dirtyReward = 150;

    [Header("Physique")]
    public float carrySpeed = 2f;
    private float savedPlayerSpeed;

    [Header("Tutoriel & Fin de Job 🎓🏁")]
    public GameObject tutorialPanel;
    public Button closeTutorialBtn;
    public Transform endJobPosition;

    [Header("UI du Mini-Jeu & HUD 📦")]
    public GameObject carriedCrateModel;
    public GameObject balanceUIPanel;
    public Slider balanceSlider;
    public float balanceDifficulty = 0.15f;
    public float mouseCorrectionSensitivity = 0.05f;

    public TextMeshProUGUI illegalCrateWarningText;
    public GameObject[] extraHUDElementsToHide;

    private List<GameObject> hudMemory = new List<GameObject>();

    [Header("Juice & UI Enhancements 🧃")]
    public TextMeshProUGUI clipboardProgressText;
    public ParticleSystem illegalLeakParticles;
    public RectTransform balancePanelRect;
    public float maxShakeIntensity = 10f;
    private Vector2 balancePanelOriginalPos;

    [Header("GPS & Cibles de Livraison 🗺️")]
    public Transform legalDropZone;
    public Transform illegalDropZone;

    [HideInInspector] public bool isCarryingCrate = false;
    [HideInInspector] public bool isCurrentCrateIllegal = false;
    private PlayerController playerController;

    private float currentBalance = 0.5f;
    private float currentDrift = 0f;
    private float driftChangeTimer = 0f;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();

        if (carriedCrateModel != null)
        {
            carriedCrateModel.SetActive(false);
            if (illegalLeakParticles == null) illegalLeakParticles = carriedCrateModel.GetComponentInChildren<ParticleSystem>(true);
        }

        if (balanceUIPanel != null) balanceUIPanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (illegalCrateWarningText != null) illegalCrateWarningText.gameObject.SetActive(false);
        if (illegalLeakParticles != null) illegalLeakParticles.Stop();

        if (balancePanelRect != null) balancePanelOriginalPos = balancePanelRect.anchoredPosition;

        if (closeTutorialBtn != null) closeTutorialBtn.onClick.AddListener(CloseTutorial);

        UpdateClipboardUI();
    }

    private void SafeToggleHUD(bool showHUD)
    {
        if (extraHUDElementsToHide == null) return;

        if (!showHUD)
        {
            hudMemory.Clear();
            foreach (GameObject hudElement in extraHUDElementsToHide)
            {
                if (hudElement != null && hudElement.activeSelf)
                {
                    hudMemory.Add(hudElement);
                    hudElement.SetActive(false);
                }
            }
        }
        else
        {
            foreach (GameObject hudElement in hudMemory)
            {
                if (hudElement != null) hudElement.SetActive(true);
            }
            hudMemory.Clear();
        }
    }

    private void SpawnCrates()
    {
        ClearCrates();

        if (cratePrefab == null || crateSpawnPoints == null || crateSpawnPoints.Length == 0) return;

        totalCratesToDeliver = crateSpawnPoints.Length;

        foreach (Transform spawnPoint in crateSpawnPoints)
        {
            if (spawnPoint == null) continue;

            GameObject newCrate = Instantiate(cratePrefab, spawnPoint.position, spawnPoint.rotation);

            DockerCrate crateScript = newCrate.GetComponent<DockerCrate>();
            if (crateScript != null)
            {
                crateScript.isGangCrate = (Random.Range(0, 100) < illegalCrateChance);
            }

            spawnedCrates.Add(newCrate);
        }
    }

    private void ClearCrates()
    {
        foreach (GameObject crate in spawnedCrates)
        {
            if (crate != null) Destroy(crate);
        }
        spawnedCrates.Clear();
    }

    public void StartJob()
    {
        if (isJobActive) return;

        isJobActive = true;
        cratesProcessed = 0;
        cashEarned = 0;

        // --- C'EST ICI QUE LA MAGIE OPÈRE : On spawn les caisses pendant l'écran noir ! ---
        SpawnCrates();
        UpdateClipboardUI();

        if (InventoryManager.Instance != null) InventoryManager.Instance.enabled = false;
        if (CallApp.Instance != null) CallApp.Instance.callsBlocked = true;

        SafeToggleHUD(false);

        if (PlayerPrefs.GetInt("DockerTutorialDone", 0) == 0 && tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (playerController != null) playerController.enabled = false;
        }
        else
        {
            BeginGameplay();
        }
    }

    public void CloseTutorial()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        PlayerPrefs.SetInt("DockerTutorialDone", 1);

        BeginGameplay();
    }

    private void BeginGameplay()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
        if (playerController != null) playerController.enabled = true;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("Service commencé ! Prenez une caisse et chargez le camion.");
    }

    public void EndJob()
    {
        isJobActive = false;
        ClearCrates();
        StartCoroutine(EndJobRoutine());
    }

    private IEnumerator EndJobRoutine()
    {
        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));
        }

        if (endJobPosition != null && playerController != null)
        {
            playerController.transform.position = endJobPosition.position;
            playerController.transform.rotation = endJobPosition.rotation;
        }

        SafeToggleHUD(true);

        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = true;
        if (InventoryManager.Instance != null) InventoryManager.Instance.enabled = true;
        if (CallApp.Instance != null) CallApp.Instance.callsBlocked = false;

        if (cashEarned > 0)
        {
            if (GameManager.Instance != null) GameManager.Instance.cleanMoney += cashEarned;
            if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(cashEarned, "Salaire : Manutention Portuaire");
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=#00FF41>Service terminé ! Salaire : {cashEarned}$</color>");
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Service terminé. Vous n'avez rien gagné.");
        }

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));
            UIManager.Instance.transitionPanel.SetActive(false);
        }
    }

    public void PickupCrate(bool illegalCrate)
    {
        if (isCarryingCrate || !isJobActive) return;

        isCarryingCrate = true;
        isCurrentCrateIllegal = illegalCrate;

        if (playerController != null)
        {
            savedPlayerSpeed = playerController.moveSpeed;
            playerController.moveSpeed = carrySpeed;
        }

        if (carriedCrateModel != null) carriedCrateModel.SetActive(true);

        if (illegalLeakParticles != null)
        {
            if (isCurrentCrateIllegal) illegalLeakParticles.Play();
            else illegalLeakParticles.Stop();
        }

        currentBalance = 0.5f;
        currentDrift = 0f;
        if (balanceUIPanel != null) balanceUIPanel.SetActive(true);
        if (balanceSlider != null) balanceSlider.value = currentBalance;

        if (isCurrentCrateIllegal)
        {
            if (illegalCrateWarningText != null)
            {
                illegalCrateWarningText.text = "<color=#B026FF>Cette caisse a une odeur particulière...\nLivrez la normalement, ou détournez la pour en garder le contenu.</color>";
                illegalCrateWarningText.gameObject.SetActive(true);
            }
            if (JobPathfinder.Instance != null) JobPathfinder.Instance.SetTargets(illegalDropZone, legalDropZone);
        }
        else
        {
            if (JobPathfinder.Instance != null) JobPathfinder.Instance.SetTargets(legalDropZone);
        }
    }

    private void Update()
    {
        if (isCarryingCrate)
        {
            ManageBalanceMiniGame();
        }
    }

    private void ManageBalanceMiniGame()
    {
        driftChangeTimer -= Time.deltaTime;
        if (driftChangeTimer <= 0f)
        {
            currentDrift = Random.Range(-balanceDifficulty, balanceDifficulty);
            driftChangeTimer = Random.Range(1.0f, 2.5f);
        }

        currentBalance += currentDrift * Time.deltaTime;

        float mouseMovement = Input.GetAxis("Mouse X");
        currentBalance += mouseMovement * mouseCorrectionSensitivity;

        if (balanceSlider != null) balanceSlider.value = currentBalance;

        if (balancePanelRect != null)
        {
            float dangerLevel = Mathf.Abs(currentBalance - 0.5f) * 2f;

            if (dangerLevel > 0.6f)
            {
                float currentShake = maxShakeIntensity * ((dangerLevel - 0.6f) / 0.4f);
                balancePanelRect.anchoredPosition = balancePanelOriginalPos + new Vector2(Random.Range(-currentShake, currentShake), Random.Range(-currentShake, currentShake));
            }
            else
            {
                balancePanelRect.anchoredPosition = balancePanelOriginalPos;
            }
        }

        if (currentBalance <= 0f || currentBalance >= 1f)
        {
            FailCrate();
        }
    }

    private void FailCrate()
    {
        ResetCarryState();
        cratesProcessed++;
        UpdateClipboardUI();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"<color=red>Vous avez fait tomber la caisse ! ({cratesProcessed}/{totalCratesToDeliver})</color>");

        if (cratesProcessed >= totalCratesToDeliver) EndJob();
    }

    public void DeliverCrate(bool deliveredToIllegalZone)
    {
        ResetCarryState();
        cratesProcessed++;
        UpdateClipboardUI();

        if (deliveredToIllegalZone)
        {
            if (isCurrentCrateIllegal)
            {
                if (possibleDrugRewards != null && possibleDrugRewards.Length > 0 && InventoryManager.Instance != null)
                {
                    ItemData drugToGive = possibleDrugRewards[Random.Range(0, possibleDrugRewards.Length)];
                    int amountToGive = Random.Range(minDrugAmount, maxDrugAmount + 1);

                    int spaceLeft = InventoryManager.Instance.maxSlots - InventoryManager.Instance.items.Count;
                    int amountGiven = Mathf.Min(amountToGive, spaceLeft);

                    for (int i = 0; i < amountGiven; i++) InventoryManager.Instance.items.Add(drugToGive);

                    if (amountGiven > 0)
                    {
                        if (UIManager.Instance != null)
                            UIManager.Instance.ShowNotification($"<color=red>Caisse détournée ! +{amountGiven} {drugToGive.itemName} ({cratesProcessed}/{totalCratesToDeliver})</color>");
                    }
                    else
                    {
                        if (UIManager.Instance != null)
                            UIManager.Instance.ShowNotification($"<color=orange>Caisse détournée, mais sac plein ! L'acheteur a tout pris sans payer. ({cratesProcessed}/{totalCratesToDeliver})</color>");
                    }
                }
                else
                {
                    if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(dirtyReward);
                    if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>Caisse détournée ! +{dirtyReward}$ (Sale) ({cratesProcessed}/{totalCratesToDeliver})</color>");
                }
            }
            else
            {
                if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(10);
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=orange>Caisse standard volée... ({cratesProcessed}/{totalCratesToDeliver})</color>");
            }
        }
        else
        {
            cashEarned += cleanReward;

            if (isCurrentCrateIllegal)
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=green>Caisse suspecte signalée au patron ! +{cleanReward}$ ({cratesProcessed}/{totalCratesToDeliver})</color>");
            }
            else
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=green>Caisse livrée ! +{cleanReward}$ ({cratesProcessed}/{totalCratesToDeliver})</color>");
            }
        }

        if (cratesProcessed >= totalCratesToDeliver)
        {
            EndJob();
        }
    }

    private void UpdateClipboardUI()
    {
        if (clipboardProgressText != null)
        {
            if (isJobActive)
            {
                clipboardProgressText.text = $"Manifeste\n{cratesProcessed} / {totalCratesToDeliver} caisses";
                clipboardProgressText.transform.parent.gameObject.SetActive(true);
            }
            else
            {
                clipboardProgressText.transform.parent.gameObject.SetActive(false);
            }
        }
    }

    private void ResetCarryState()
    {
        isCarryingCrate = false;

        if (playerController != null) playerController.moveSpeed = savedPlayerSpeed;
        if (carriedCrateModel != null) carriedCrateModel.SetActive(false);
        if (balanceUIPanel != null)
        {
            balanceUIPanel.SetActive(false);
            if (balancePanelRect != null) balancePanelRect.anchoredPosition = balancePanelOriginalPos;
        }
        if (illegalCrateWarningText != null) illegalCrateWarningText.gameObject.SetActive(false);
        if (illegalLeakParticles != null) illegalLeakParticles.Stop();
        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();
    }
}