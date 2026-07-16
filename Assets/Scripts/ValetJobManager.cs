using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class ValetJobManager : MonoBehaviour
{
    public static ValetJobManager Instance;

    [Header("UI Globale 🖥️")]
    public GameObject mainJobPanel;
    public TextMeshProUGUI jobStatusText;
    public TextMeshProUGUI feedbackText;

    [Header("État du Job")]
    public bool isJobActive = false;
    public int maxVehiclesPerShift = 5;
    private int vehiclesProcessed = 0;
    private int cleanCashEarned = 0;

    [Header("--- VÉHICULES 🚗 ---")]
    public GameObject[] vehiclePrefabs;
    public Transform vehicleSpawnPoint;
    public Transform parkingZoneTarget;

    public Transform valetStandReturnPosition;

    private PlayerController playerController;
    private GameObject currentSpawnedVehicle;

    [Header("--- STATIONNEMENT & ÉCONOMIE ---")]
    public int maxRewardPerVehicle = 100;
    public int penaltyPerDamage = 2;
    public int penaltyPerAlignmentError = 5;
    private int currentVehicleReward = 0;

    [Header("--- ÉVÉNEMENTS BAD GUY 😈 ---")]
    public int searchEventChancePercent = 40;
    public int gangsterVehicleChancePercent = 20;

    public GameObject searchPromptPanel;
    public Button acceptSearchBtn;
    public Button declineSearchBtn;

    public ItemData[] standardLoot;
    public ItemData[] gangsterLoot;

    [Header("--- TIMERS DES MINI-JEUX ---")]
    public float standardSearchTime = 15f;
    public float gangsterHackTime = 8f;
    public float gangsterLockpickTime = 12f;

    [Header("UI : Piratage")]
    public GameObject hackPanel;
    public TextMeshProUGUI hackCodeText;
    public TMP_InputField hackInputField;

    [Header("--- UI : CROCHETAGE (Style Fallout) 🪛 ---")]
    public GameObject lockpickPanel;
    public RectTransform lockTransform;
    public RectTransform pinTransform;
    public TextMeshProUGUI miniGameTimerText;

    public float lockTolerance = 10f;
    public float pinMoveSpeed = 3f;

    private float pinAngle = 0f;
    private float lockAngle = 0f;
    private float targetPinAngle = 0f;
    private float pinShakeTimer = 0f;
    private bool isLockRotated = false;

    private bool isLockpicking = false;
    private bool isHacking = false;
    private bool isCurrentVehicleGangster = false;
    private float currentTimer = 0f;
    private string targetHackCode = "";

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();

        HideAllPanels();
        if (acceptSearchBtn != null) acceptSearchBtn.onClick.AddListener(StartSearchEvent);
        if (declineSearchBtn != null) declineSearchBtn.onClick.AddListener(FinishVehicleProcessing);
    }

    private void HideAllPanels()
    {
        if (mainJobPanel != null) mainJobPanel.SetActive(false);
        if (searchPromptPanel != null) searchPromptPanel.SetActive(false);
        if (lockpickPanel != null) lockpickPanel.SetActive(false);
        if (hackPanel != null) hackPanel.SetActive(false);
    }

    public void StartJob()
    {
        if (isJobActive) return;
        isJobActive = true;
        vehiclesProcessed = 0;
        cleanCashEarned = 0;

        if (mainJobPanel != null) mainJobPanel.SetActive(true);
        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(false);

        NextVehicle();
    }

    private void NextVehicle()
    {
        if (vehiclesProcessed >= maxVehiclesPerShift)
        {
            EndJob();
            return;
        }

        if (jobStatusText != null) jobStatusText.text = "Garez le véhicule du client dans le parking souterrain.";

        if (vehiclePrefabs != null && vehiclePrefabs.Length > 0 && vehicleSpawnPoint != null)
        {
            if (currentSpawnedVehicle != null) Destroy(currentSpawnedVehicle);

            GameObject prefabToSpawn = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
            currentSpawnedVehicle = Instantiate(prefabToSpawn, vehicleSpawnPoint.position, vehicleSpawnPoint.rotation);

            if (JobPathfinder.Instance != null && parkingZoneTarget != null)
            {
                JobPathfinder.Instance.SetTargets(parkingZoneTarget);
            }
        }
    }

    public void SubmitParkingValidation(int damageTaken, int alignmentError)
    {
        if (!isJobActive) return;

        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();

        currentVehicleReward = maxRewardPerVehicle;
        currentVehicleReward -= (damageTaken * penaltyPerDamage);
        currentVehicleReward -= (alignmentError * penaltyPerAlignmentError);

        if (currentVehicleReward < 0) currentVehicleReward = 0;

        string feedback = $"Véhicule garé.\nDégâts : {damageTaken} | Mauvais alignement : {alignmentError}\nGain estimé : <color=green>{currentVehicleReward}$</color>";
        if (feedbackText != null) feedbackText.text = feedback;

        if (Random.Range(0, 100) < searchEventChancePercent) TriggerSearchOpportunity();
        else FinishVehicleProcessing();
    }

    private void FinishVehicleProcessing()
    {
        HideAllPanels();
        mainJobPanel.SetActive(true);
        cleanCashEarned += currentVehicleReward;
        vehiclesProcessed++;

        if (feedbackText != null) feedbackText.text = "Retour au casino en cours...";

        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();

        StartCoroutine(ReturnToStandRoutine());
    }

    private IEnumerator ReturnToStandRoutine()
    {
        // 1. Fondu au noir 
        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));
        }

        // 2. ÉJECTION DU VÉHICULE
        if (currentSpawnedVehicle != null)
        {
            CarInteraction carInteract = currentSpawnedVehicle.GetComponent<CarInteraction>();
            if (carInteract != null)
            {
                // On encercle l'éjection d'un try/catch pour éviter qu'une erreur bloque la suite
                try { carInteract.ExitCar(); } catch { Debug.LogWarning("CarInteraction a planté lors de l'éjection."); }
            }

            yield return new WaitForFixedUpdate();
            Destroy(currentSpawnedVehicle);
        }

        // 3. RÉANIMATION GLOBALE & TÉLÉPORTATION
        if (playerController != null && valetStandReturnPosition != null)
        {
            // --- A. ON FORCE TOUT LE JOUEUR À S'ALLUMER ---
            playerController.enabled = true;

            // Rallume TOUS les rendus (pour ne plus être invisible)
            Renderer[] rends = playerController.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rends)
            {
                if (r != null) r.enabled = true;
            }

            // Rallume TOUS les colliders (pour ne plus traverser le sol)
            Collider[] cols = playerController.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in cols)
            {
                if (c != null && !c.isTrigger) c.enabled = true;
            }

            // --- B. TÉLÉPORTATION PHYSIQUE SÉCURISÉE ---
            Rigidbody rb = playerController.GetComponent<Rigidbody>();

            // On le place à +2 mètres de haut par sécurité
            Vector3 safePosition = valetStandReturnPosition.position;
            safePosition.y += 2.0f;

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                // La méthode parfaite pour bouger un Rigidbody :
                rb.position = safePosition;
            }

            playerController.transform.position = safePosition;
            playerController.transform.rotation = valetStandReturnPosition.rotation;

            // On attend 2 frames physiques (pour que Unity "comprenne" qu'il y a un sol en dessous)
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // --- C. RÉVEIL DU MOTEUR PHYSIQUE ---
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.WakeUp();
            }
        }

        yield return new WaitForSeconds(0.5f);

        // 4. Fin du fondu au noir
        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));
            UIManager.Instance.transitionPanel.SetActive(false);
        }

        NextVehicle();
    }

    private void TriggerSearchOpportunity()
    {
        isCurrentVehicleGangster = (Random.Range(0, 100) < gangsterVehicleChancePercent);

        if (searchPromptPanel != null)
        {
            searchPromptPanel.SetActive(true);
            TextMeshProUGUI promptText = searchPromptPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (promptText != null)
            {
                if (isCurrentVehicleGangster)
                    promptText.text = "L'intérieur de cette berline blindée sent la poudre. Une mallette est verrouillée par un digicode sur le siège passager.\n<color=red>Risque : ÉLEVÉ</color>\nTenter de la pirater ?";
                else
                    promptText.text = "Avant de couper le contact, tu remarques que la boîte à gants est fermée à clé. La serrure a l'air basique.\n<color=orange>Risque : MODÉRÉ</color>\nLa forcer ?";
            }
        }
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void StartSearchEvent()
    {
        if (searchPromptPanel != null) searchPromptPanel.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (isCurrentVehicleGangster) StartHackMiniGame();
        else StartLockpickMiniGame(standardSearchTime);
    }

    private void StartHackMiniGame()
    {
        isHacking = true;
        currentTimer = gangsterHackTime;
        if (hackPanel != null) hackPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;

        targetHackCode = Random.Range(100000, 999999).ToString();
        if (hackCodeText != null) hackCodeText.text = $"DÉSACTIVATION ALARME : TAPEZ {targetHackCode}";

        if (hackInputField != null)
        {
            hackInputField.text = "";
            hackInputField.ActivateInputField();
        }
    }

    public void OnHackInputValueChanged(string input)
    {
        if (!isHacking) return;

        if (input == targetHackCode)
        {
            isHacking = false;
            if (hackPanel != null) hackPanel.SetActive(false);
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Alarme désactivée. Vite, la serrure !");

            StartLockpickMiniGame(gangsterLockpickTime);
        }
    }

    private void StartLockpickMiniGame(float timeToComplete)
    {
        isLockpicking = true;
        currentTimer = timeToComplete;
        if (lockpickPanel != null) lockpickPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.Locked;

        pinAngle = 0f;
        lockAngle = 0f;
        UpdateTransforms();

        targetPinAngle = Random.Range(-80f, 80f);
        lockTolerance = isCurrentVehicleGangster ? 5f : 12f;
    }

    private void Update()
    {
        if (isHacking)
        {
            currentTimer -= Time.deltaTime;
            UpdateTimerUI();
            if (currentTimer <= 0) FailBadGuyEvent("Le garde du corps vous a repéré pendant le piratage de l'alarme !");
        }
        else if (isLockpicking)
        {
            currentTimer -= Time.deltaTime;
            UpdateTimerUI();

            if (currentTimer <= 0)
            {
                string msg = isCurrentVehicleGangster ? "Le garde du corps est arrivé !" : "Le patron s'impatiente, vous avez dû abandonner.";
                FailBadGuyEvent(msg);
                return;
            }

            HandleLockpickingInput();
        }
    }

    private void HandleLockpickingInput()
    {
        if (!isLockRotated)
        {
            float mouseMove = Input.GetAxis("Mouse X");
            pinAngle -= mouseMove * pinMoveSpeed;
            pinAngle = Mathf.Clamp(pinAngle, -90f, 90f);
        }

        float distanceFromTarget = Mathf.Abs(targetPinAngle - pinAngle);
        float maxAllowedLockAngle = 90f;

        if (distanceFromTarget > lockTolerance)
        {
            float difficultyScale = Mathf.Clamp01((distanceFromTarget - lockTolerance) / 90f);
            maxAllowedLockAngle = 90f * (1f - difficultyScale);
        }

        if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space))
        {
            isLockRotated = true;
            lockAngle = Mathf.Lerp(lockAngle, maxAllowedLockAngle, Time.deltaTime * 5f);

            if (Mathf.Abs(lockAngle - maxAllowedLockAngle) < 1f && maxAllowedLockAngle < 85f)
            {
                pinShakeTimer += Time.deltaTime * 30f;
                float shakeOffset = Mathf.Sin(pinShakeTimer) * 2f;
                if (pinTransform != null) pinTransform.localRotation = Quaternion.Euler(0, 0, pinAngle + shakeOffset);

                currentTimer -= Time.deltaTime * 1.5f;
            }
            else
            {
                pinShakeTimer = 0f;
            }

            if (lockAngle >= 88f)
            {
                WinBadGuyEvent();
            }
        }
        else
        {
            isLockRotated = false;
            pinShakeTimer = 0f;
            lockAngle = Mathf.Lerp(lockAngle, 0f, Time.deltaTime * 10f);
        }

        UpdateTransforms();
    }

    private void UpdateTransforms()
    {
        if (pinShakeTimer == 0f && pinTransform != null)
        {
            pinTransform.localRotation = Quaternion.Euler(0, 0, pinAngle);
        }

        if (lockTransform != null)
        {
            lockTransform.localRotation = Quaternion.Euler(0, 0, -lockAngle);
        }
    }

    private void UpdateTimerUI()
    {
        if (miniGameTimerText != null)
        {
            miniGameTimerText.text = $"Temps : {currentTimer:F1}s";
            miniGameTimerText.color = currentTimer < 4f ? Color.red : Color.white;
        }
    }

    private void FailBadGuyEvent(string reason)
    {
        isHacking = false;
        isLockpicking = false;
        HideAllPanels();
        Cursor.lockState = CursorLockMode.Confined;

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>ÉCHEC : {reason}</color>");

        if (isCurrentVehicleGangster)
        {
            if (GameManager.Instance != null) GameManager.Instance.Wasted();
            EndJob(true);
        }
        else
        {
            currentVehicleReward = 0;
            FinishVehicleProcessing();
        }
    }

    private void WinBadGuyEvent()
    {
        isLockpicking = false;
        HideAllPanels();
        Cursor.lockState = CursorLockMode.Confined;

        string lootName = "Rien";
        ItemData[] pool = isCurrentVehicleGangster ? gangsterLoot : standardLoot;

        if (pool != null && pool.Length > 0 && InventoryManager.Instance != null)
        {
            ItemData stolenItem = pool[Random.Range(0, pool.Length)];
            InventoryManager.Instance.AddItem(stolenItem, 1, false);
            lootName = stolenItem.itemName;
        }

        if (UIManager.Instance != null)
        {
            string color = isCurrentVehicleGangster ? "#FF5555" : "#00FF00";
            UIManager.Instance.ShowNotification($"<color={color}>Serrure forcée ! Butin : {lootName}</color>");
        }

        FinishVehicleProcessing();
    }

    private void EndJob(bool forceQuit = false)
    {
        isJobActive = false;
        HideAllPanels();

        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();

        if (!forceQuit)
        {
            if (cleanCashEarned > 0)
            {
                if (GameManager.Instance != null) GameManager.Instance.cleanMoney += cleanCashEarned;
                if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(cleanCashEarned, "Salaire : Voiturier Casino");
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=#00FF41>Service terminé ! Salaire : {cleanCashEarned}$</color>");
            }
        }

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(true);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }
}