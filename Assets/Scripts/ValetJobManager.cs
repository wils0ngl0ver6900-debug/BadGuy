using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class ValetJobManager : MonoBehaviour
{
    public static ValetJobManager Instance;

    [Header("--- PROPOSITION DE JOB & TUTO 📋 ---")]
    public GameObject jobOfferPanel;
    public Button acceptJobBtn;
    public Button declineJobBtn;
    public GameObject tutorialPanel;
    public Button closeTutorialBtn;

    [Header("UI Globale 🖥️")]
    public GameObject mainJobPanel;
    public TextMeshProUGUI jobStatusText;
    public TextMeshProUGUI feedbackText;
    public GameObject parkingPromptUI;

    [Header("--- HUD & SYSTÈMES 📱 ---")]
    public GameObject[] extraHUDElementsToHide;
    private List<GameObject> hudMemory = new List<GameObject>();

    [Header("État du Job")]
    public bool isJobActive = false;
    public int maxVehiclesPerShift = 5;
    private int vehiclesProcessed = 0;
    private int cleanCashEarned = 0;
    private int failedStandardSearches = 0;
    private bool vehicleAlreadyGaraged = false;

    private bool isVehicleInPlay = false;
    private int destroyedVehiclesCount = 0;
    private bool npcSpawnedForThisCar = false;

    [Header("--- VÉHICULES 🚗 ---")]
    public GameObject[] vehiclePrefabs;
    public Transform vehicleSpawnPoint;
    public Transform parkingZoneTarget;
    public Transform valetStandReturnPosition;

    [Header("--- PNJ CLIENTS 🚶‍♂️ ---")]
    [Tooltip("Ajoutez ici les préfabs de personnages que vous souhaitez faire apparaître.")]
    public GameObject[] npcClientPrefabs;
    [Tooltip("L'endroit (ex: l'entrée du casino) vers lequel le client va marcher.")]
    public Transform npcCasinoDestination;

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
    public float lockShakeIntensity = 5f;

    public float lockTolerance = 10f;
    public float pinMoveSpeed = 3f;

    private float pinAngle = 0f;
    private float lockAngle = 0f;
    private float targetPinAngle = 0f;
    private float pinShakeTimer = 0f;
    private bool isLockRotated = false;
    private Vector2 lockOriginalPos;

    private bool isLockpicking = false;
    private bool isHacking = false;
    private bool isCurrentVehicleGangster = false;

    // ---> NOUVEAU : Sécurité pour l'animation de victoire
    private bool isWinningLockpick = false;

    private float currentTimer = 0f;
    private string targetHackCode = "";

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
        PlayerPrefs.DeleteKey("ValetTutorialDone");
        if (lockTransform != null) lockOriginalPos = lockTransform.anchoredPosition;

        HideAllPanels();

        if (jobOfferPanel != null) jobOfferPanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);

        if (acceptJobBtn != null) acceptJobBtn.onClick.AddListener(AcceptJob);
        if (declineJobBtn != null) declineJobBtn.onClick.AddListener(DeclineJob);
        if (closeTutorialBtn != null) closeTutorialBtn.onClick.AddListener(CloseTutorial);

        if (acceptSearchBtn != null) acceptSearchBtn.onClick.AddListener(StartSearchEvent);
        if (declineSearchBtn != null) declineSearchBtn.onClick.AddListener(FinishVehicleProcessing);

        if (parkingPromptUI != null) parkingPromptUI.SetActive(false);
    }

    private void HideAllPanels()
    {
        if (mainJobPanel != null) mainJobPanel.SetActive(false);
        if (searchPromptPanel != null) searchPromptPanel.SetActive(false);
        if (lockpickPanel != null) lockpickPanel.SetActive(false);
        if (hackPanel != null) hackPanel.SetActive(false);
        if (parkingPromptUI != null) parkingPromptUI.SetActive(false);
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

    public void ShowJobOffer()
    {
        if (jobOfferPanel != null)
        {
            jobOfferPanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (playerController != null) playerController.enabled = false;
            if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = false;
        }
    }

    public void AcceptJob()
    {
        if (jobOfferPanel != null) jobOfferPanel.SetActive(false);
        if (!isJobActive) StartCoroutine(StartJobRoutine());
    }

    public void DeclineJob()
    {
        if (jobOfferPanel != null) jobOfferPanel.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        if (playerController != null) playerController.enabled = true;
        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = true;

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Vous avez refusé le poste de voiturier.");
    }

    private IEnumerator StartJobRoutine()
    {
        isJobActive = true;
        isVehicleInPlay = false;
        vehiclesProcessed = 0;
        cleanCashEarned = 0;
        failedStandardSearches = 0;
        destroyedVehiclesCount = 0;

        if (parkingPromptUI != null) parkingPromptUI.SetActive(false);

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));
        }

        if (TimeManager.Instance != null)
        {
            float currentTime = TimeManager.Instance.currentTimeOfDay;
            if (currentTime < 1260f && currentTime > 240f)
            {
                TimeManager.Instance.currentTimeOfDay = 1320f;
            }
        }

        SafeToggleHUD(false);
        if (InventoryManager.Instance != null) InventoryManager.Instance.enabled = false;
        if (CallApp.Instance != null) CallApp.Instance.callsBlocked = true;
        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = false;

        if (PlayerPrefs.GetInt("ValetTutorialDone", 0) == 0 && tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (playerController != null) playerController.enabled = false;
        }
        else
        {
            yield return StartCoroutine(BeginGameplayRoutine());
        }
    }

    public void CloseTutorial()
    {
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        PlayerPrefs.SetInt("ValetTutorialDone", 1);
        StartCoroutine(BeginGameplayRoutine());
    }

    private IEnumerator BeginGameplayRoutine()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
        if (playerController != null) playerController.enabled = true;

        if (mainJobPanel != null) mainJobPanel.SetActive(true);
        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(false);

        NextVehicle();

        yield return new WaitForSeconds(0.5f);

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));
            UIManager.Instance.transitionPanel.SetActive(false);
        }
    }

    private void NextVehicle()
    {
        if (vehiclesProcessed >= maxVehiclesPerShift)
        {
            EndJob();
            return;
        }

        vehicleAlreadyGaraged = false;
        npcSpawnedForThisCar = false;
        isVehicleInPlay = false;

        if (parkingPromptUI != null) parkingPromptUI.SetActive(false);
        if (jobStatusText != null) jobStatusText.text = "Garez le véhicule du client dans le parking souterrain.";

        if (vehiclePrefabs != null && vehiclePrefabs.Length > 0 && vehicleSpawnPoint != null)
        {
            if (currentSpawnedVehicle != null) Destroy(currentSpawnedVehicle);

            GameObject prefabToSpawn = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
            currentSpawnedVehicle = Instantiate(prefabToSpawn, vehicleSpawnPoint.position, vehicleSpawnPoint.rotation);

            isVehicleInPlay = true;

            if (JobPathfinder.Instance != null && parkingZoneTarget != null)
            {
                JobPathfinder.Instance.SetTargets(parkingZoneTarget);
            }
        }
    }

    private void SpawnNPCClient()
    {
        if (npcClientPrefabs != null && npcClientPrefabs.Length > 0 && npcCasinoDestination != null && currentSpawnedVehicle != null)
        {
            GameObject npcPrefab = npcClientPrefabs[Random.Range(0, npcClientPrefabs.Length)];

            Vector3 spawnPos = currentSpawnedVehicle.transform.position + (currentSpawnedVehicle.transform.right * 1.5f);
            GameObject npc = Instantiate(npcPrefab, spawnPos, currentSpawnedVehicle.transform.rotation);

            NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.SetDestination(npcCasinoDestination.position);
            }

            Destroy(npc, 20f);
        }
    }

    public void SubmitParkingValidation(int damageTaken, int alignmentError)
    {
        if (!isJobActive || vehicleAlreadyGaraged) return;

        vehicleAlreadyGaraged = true;
        isVehicleInPlay = false;

        if (parkingPromptUI != null) parkingPromptUI.SetActive(false);
        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();

        if (currentSpawnedVehicle != null)
        {
            Rigidbody carRb = currentSpawnedVehicle.GetComponent<Rigidbody>();
            if (carRb != null)
            {
                carRb.linearVelocity = Vector3.zero;
                carRb.angularVelocity = Vector3.zero;
                carRb.isKinematic = true;
            }
        }

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

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        cleanCashEarned += currentVehicleReward;
        vehiclesProcessed++;

        if (feedbackText != null) feedbackText.text = "Retour au casino en cours...";
        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();

        StartCoroutine(ReturnToStandRoutine());
    }

    private void HandleDestroyedVehicle()
    {
        vehicleAlreadyGaraged = true;
        isVehicleInPlay = false;
        destroyedVehiclesCount++;

        HideAllPanels();
        mainJobPanel.SetActive(true);
        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();

        if (destroyedVehiclesCount >= 2)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("<color=red>Le Gérant : 'Tu as détruit 2 caisses ?! T'es viré !'</color>");
            EndJob(true);
        }
        else
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("<color=orange>Le Gérant : 'C'était quoi ce bruit ?! Ramène la prochaine en un seul morceau !'</color>");

            if (feedbackText != null) feedbackText.text = "Retour au casino en cours...";
            StartCoroutine(ReturnToStandRoutine());
        }
    }

    private IEnumerator ReturnToStandRoutine()
    {
        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));
        }

        if (currentSpawnedVehicle != null)
        {
            CarInteraction carInteract = currentSpawnedVehicle.GetComponent<CarInteraction>();
            if (carInteract != null)
            {
                try { carInteract.ExitCar(); } catch { Debug.LogWarning("CarInteraction a planté lors de l'éjection."); }
            }
            yield return new WaitForFixedUpdate();
            Destroy(currentSpawnedVehicle);
        }

        if (playerController != null && valetStandReturnPosition != null)
        {
            playerController.enabled = true;
            Renderer[] rends = playerController.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rends) if (r != null) r.enabled = true;
            Collider[] cols = playerController.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in cols) if (c != null && !c.isTrigger) c.enabled = true;

            Rigidbody rb = playerController.GetComponent<Rigidbody>();
            Vector3 safePosition = valetStandReturnPosition.position;
            safePosition.y += 2.0f;

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = safePosition;
            }

            playerController.transform.position = safePosition;
            playerController.transform.rotation = valetStandReturnPosition.rotation;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.WakeUp();
            }
        }

        yield return new WaitForSeconds(0.5f);

        NextVehicle();

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));
            UIManager.Instance.transitionPanel.SetActive(false);
        }
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
            hackInputField.characterLimit = targetHackCode.Length;
            hackInputField.ActivateInputField();

            hackInputField.onValueChanged.RemoveAllListeners();
            hackInputField.onValueChanged.AddListener(OnHackInputValueChanged);
        }
    }

    public void OnHackInputValueChanged(string input)
    {
        if (!isHacking) return;

        if (input.Trim() == targetHackCode)
        {
            isHacking = false;

            if (hackInputField != null) hackInputField.text = "";
            if (hackPanel != null) hackPanel.SetActive(false);

            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Alarme désactivée. Vite, la serrure !");

            StartLockpickMiniGame(gangsterLockpickTime);
        }
    }

    private void StartLockpickMiniGame(float timeToComplete)
    {
        isLockpicking = true;
        isWinningLockpick = false; // On réinitialise l'animation de victoire
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
        if (isJobActive && isVehicleInPlay && !vehicleAlreadyGaraged && currentSpawnedVehicle == null)
        {
            HandleDestroyedVehicle();
            return;
        }

        if (isJobActive && !vehicleAlreadyGaraged && currentSpawnedVehicle != null && !npcSpawnedForThisCar)
        {
            if (playerController != null)
            {
                float dist = Vector3.Distance(playerController.transform.position, currentSpawnedVehicle.transform.position);
                if (dist < 4f && (!playerController.gameObject.activeInHierarchy || !playerController.enabled))
                {
                    SpawnNPCClient();
                    npcSpawnedForThisCar = true;
                }
            }
        }

        if (vehicleAlreadyGaraged && parkingPromptUI != null && parkingPromptUI.activeSelf)
        {
            parkingPromptUI.SetActive(false);
        }

        if (isHacking)
        {
            currentTimer -= Time.deltaTime;
            UpdateTimerUI();
            if (currentTimer <= 0) FailBadGuyEvent("Le garde du corps vous a repéré pendant le piratage de l'alarme !");
        }
        else if (isLockpicking && !isWinningLockpick) // Bloque le timer si on est dans l'animation de victoire
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

            // ---> NOUVEAU : On lance la coroutine de victoire au lieu de valider instantanément
            if (lockAngle >= 88f && !isWinningLockpick)
            {
                StartCoroutine(WinLockpickRoutine());
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

    // ---> NOUVEAU : Coroutine pour l'effet visuel de la serrure qui s'ouvre !
    private IEnumerator WinLockpickRoutine()
    {
        isWinningLockpick = true;
        isLockpicking = false; // Arrête la soustraction du timer et les inputs

        float startAngle = lockAngle;
        float elapsed = 0f;
        float duration = 0.2f; // Le temps que met la serrure à se bloquer à fond (0.2s = rapide et sec)

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            lockAngle = Mathf.Lerp(startAngle, 90f, elapsed / duration);
            UpdateTransforms();
            yield return null;
        }

        lockAngle = 90f; // On s'assure qu'elle est bien calée à 90 degrés
        UpdateTransforms();

        // Petite pause d'une demi-seconde pour qu'on ait le temps de voir la serrure tournée à fond
        yield return new WaitForSeconds(0.4f);

        // Et hop, on déclenche le butin !
        WinBadGuyEvent();
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

            if (isLockRotated && lockAngle > 15f && !isWinningLockpick) // Bloque le tremblement si on a gagné
            {
                float currentShake = lockShakeIntensity * (lockAngle / 90f);
                lockTransform.anchoredPosition = lockOriginalPos + new Vector2(Random.Range(-currentShake, currentShake), Random.Range(-currentShake, currentShake));
            }
            else
            {
                lockTransform.anchoredPosition = lockOriginalPos;
            }
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
        isWinningLockpick = false;
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
            failedStandardSearches++;

            if (failedStandardSearches >= 2)
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Le patron vous a grillé en train de fouiller ! Vous êtes viré !</color>");
                EndJob(true);
            }
            else
            {
                FinishVehicleProcessing();
            }
        }
    }

    private void WinBadGuyEvent()
    {
        isLockpicking = false;
        isWinningLockpick = false;
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
        vehicleAlreadyGaraged = false;
        isVehicleInPlay = false;
        isWinningLockpick = false;
        HideAllPanels();

        if (parkingPromptUI != null) parkingPromptUI.SetActive(false);
        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();

        SafeToggleHUD(true);
        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = true;
        if (InventoryManager.Instance != null) InventoryManager.Instance.enabled = true;
        if (CallApp.Instance != null) CallApp.Instance.callsBlocked = false;

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