using UnityEngine;
using System.Collections;

public class CarInteraction : MonoBehaviour
{
    [Header("Références du Véhicule")]
    public CarController carController;
    public GameObject carCamera;
    public Transform exitPoint;

    [Header("Système de Carjacking 🏃")]
    public GameObject driverPrefab;

    private GameObject player;
    private Collider[] playerColliders;
    private MonoBehaviour playerMovementScript;
    private Renderer[] playerRenderers;
    private Rigidbody playerRb;

    private bool playerInCar = false;
    private bool canEnter = false;

    // --- Effraction (voir CarBreakInConfig pour la configuration partagée) ---
    [HideInInspector] public bool isBreakInUnlocked = false;
    private GameObject spawnedPrompt;
    private TMPro.TextMeshProUGUI[] spawnedPromptLines;
    private bool isAttemptingBreakIn = false;
    private CarForSale forSaleComponent;

    // Cooldown partagé par outil (ItemData), pas par voiture : le même boîtier électronique
    // reste indisponible sur N'IMPORTE QUELLE voiture tant qu'il recharge.
    private static System.Collections.Generic.Dictionary<ItemData, float> toolCooldownEndTime = new System.Collections.Generic.Dictionary<ItemData, float>();

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        carCamera.SetActive(false);
        forSaleComponent = carController != null ? carController.GetComponent<CarForSale>() : null;

        if (player != null)
        {
            // GetComponentsInChildren (pas juste GetComponent) : le joueur a maintenant des
            // colliders sur chaque os du ragdoll, pas seulement la capsule principale. Sans
            // tous les désactiver en voiture, ils restent solides et se retrouvent incrustés
            // dans la voiture à chaque frame (le joueur est téléporté dessus) — la voiture
            // se fait alors repousser violemment par la résolution physique.
            playerColliders = player.GetComponentsInChildren<Collider>();
            playerMovementScript = player.GetComponent("PlayerController") as MonoBehaviour;
            playerRenderers = player.GetComponentsInChildren<Renderer>();
            playerRb = player.GetComponent<Rigidbody>();
        }
    }

    void Update()
    {
        if (canEnter && !playerInCar && Input.GetKeyDown(KeyCode.E))
        {
            // Une voiture "à vendre" (CarForSale) pas encore achetée ne doit pas pouvoir
            // être prise en main : sinon la touche [E] "monter en voiture" entre en conflit
            // avec le [E] "acheter" du système Interactable dès qu'on est près de la portière,
            // et on se retrouve à rouler avec sans avoir payé.
            if (forSaleComponent != null && !carController.isPlayerOwned)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification("<color=red>Tu dois d'abord l'acheter !</color>");
            }
            else if (NeedsBreakIn())
            {
                // Rien à faire ici : le prompt flottant (affiché tant qu'on est à portée,
                // voir OnTriggerEnter/Exit) indique déjà quelle touche presser pour chaque
                // méthode — [E] ne sert à rien tant qu'elle n'est pas débloquée.
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification("<color=yellow>Verrouillée — choisis une méthode d'effraction.</color>");
            }
            else
            {
                EnterCar();
            }
        }
        else if (playerInCar && Input.GetKeyDown(KeyCode.E))
        {
            // Impossible de descendre en pleine course (comportement normal partout ailleurs).
            if (StreetRaceManager.Instance != null && StreetRaceManager.Instance.IsRaceActive())
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification("<color=red>Impossible de sortir en pleine course !</color>");
            }
            else
            {
                ExitCar();
            }
        }

        if (canEnter && !playerInCar && NeedsBreakIn() && !isAttemptingBreakIn)
        {
            UpdateBreakInPrompt();
        }

        if (playerInCar && player != null)
        {
            player.transform.position = carController.transform.position;
        }
    }

    public void EnterCar()
    {
        playerInCar = true;
        carController.isDrivenByPlayer = true;
        carCamera.SetActive(true);

        // ---> LA NOUVELLE LIGNE MAGIQUE <---
        // On demande au carController (la racine de la voiture) de chercher le script !
        carController.GetComponent<MessageTrigger>()?.SendTheMessage();

        // Si c'est une voiture IA, on fait sortir le conducteur
        if (carController.isDrivenByAI)
        {
            carController.isDrivenByAI = false;
            // Spawn du PNJ conducteur si nécessaire
            if (driverPrefab != null) Instantiate(driverPrefab, exitPoint.position, Quaternion.identity);
            if (GameManager.Instance != null) GameManager.Instance.ReportCrime(20);
        }

        // On coupe le joueur à pied
        if (playerColliders != null)
        {
            foreach (Collider col in playerColliders)
            {
                if (col != null) col.enabled = false;
            }
        }
        if (playerMovementScript != null) playerMovementScript.enabled = false;

        if (playerRenderers != null)
        {
            foreach (Renderer rend in playerRenderers)
            {
                if (rend != null && rend.gameObject.name != "Icone_Joueur") rend.enabled = false;
            }
        }

        if (MinimapFollow.Instance != null) MinimapFollow.Instance.target = carController.transform;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("<color=cyan>Appuyez sur [E] pour sortir.</color>");
            // ALLUME LE NOM DU VÉHICULE EN BAS À DROITE
            UIManager.Instance.ShowVehicleHUD(carController.carModelName.ToUpper());
        }
    }

    public void ExitCar()
    {
        ExitCarAt(exitPoint != null ? exitPoint.position : transform.position);
    }

    // Variante qui laisse choisir où le joueur atterrit (ex: un point sûr dans le garage
    // plutôt que le exitPoint habituel de la voiture, pas forcément adapté à ce contexte).
    public void ExitCarAt(Vector3 worldPosition)
    {
        playerInCar = false;
        carController.isDrivenByPlayer = false;
        carCamera.SetActive(false);

        // Recalage au sol par raycast : exitPoint suppose une voiture à peu près à plat sur
        // une surface normale. Après un accident violent (voiture retournée, encastrée...),
        // sa position réelle peut être n'importe où — sans ce recalage, le joueur pouvait
        // atterrir sous la carte lors d'une éjection d'urgence (CarExplosionImproved).
        Vector3 targetPosition = worldPosition;
        if (Physics.Raycast(targetPosition + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 20f))
        {
            targetPosition = groundHit.point + Vector3.up * 0.1f;
        }

        // On téléporte via le Rigidbody plutôt que via transform.position directement :
        // sur un objet à Rigidbody, forcer transform.position désynchronise le moteur physique
        // d'une frame, ce qui peut faire passer le joueur à travers le sol selon l'endroit —
        // c'est ce qui causait le "sous la carte" en sortant dans le garage.
        if (playerRb != null)
        {
            playerRb.position = targetPosition;
            playerRb.linearVelocity = Vector3.zero;
        }
        else if (player != null)
        {
            player.transform.position = targetPosition;
        }

        // Remet UNIQUEMENT les os du ragdoll dans un état sûr (kinematic, vitesse nulle)
        // AVANT de réactiver leurs colliders — surtout PAS la racine ni l'Animator.
        // GameManager.DisablePlayerRagdoll() fait aussi rootRb.isKinematic = false, pensé
        // pour le contexte précis d'un retour de VRAI ragdoll de KO (où la racine avait été
        // mise kinematic exprès) — l'appeler ici cassait le mode kinematic normal de la
        // racine du joueur en dehors de tout KO, d'où le nouveau bug sur la Compactico.
        if (player != null)
        {
            Rigidbody[] boneRbs = player.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody boneRb in boneRbs)
            {
                if (boneRb.gameObject == player) continue; // La racine ne doit jamais être touchée ici
                boneRb.isKinematic = true;
                boneRb.linearVelocity = Vector3.zero;
                boneRb.angularVelocity = Vector3.zero;
            }
        }

        if (playerColliders != null)
        {
            foreach (Collider col in playerColliders)
            {
                if (col != null) col.enabled = true;
            }
        }
        if (playerMovementScript != null) playerMovementScript.enabled = true;

        if (playerRenderers != null)
        {
            foreach (Renderer rend in playerRenderers)
            {
                if (rend != null) rend.enabled = true;
            }
        }

        if (MinimapFollow.Instance != null && player != null) MinimapFollow.Instance.target = player.transform;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideVehicleHUD();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canEnter = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canEnter = false;
            HideBreakInPrompt();
        }
    }

    // ============================================================
    // EFFRACTION — s'applique par défaut à TOUTE voiture, sauf :
    // déjà possédée, à vendre (CarForSale), ou actuellement conduite
    // par un PNJ. Aucune configuration à faire voiture par voiture,
    // uniquement CarBreakInConfig (une fois, sur _Managers).
    // ============================================================

    private bool NeedsBreakIn()
    {
        if (CarBreakInConfig.Instance == null || CarBreakInConfig.Instance.methods == null || CarBreakInConfig.Instance.methods.Length == 0) return false;
        if (isBreakInUnlocked) return false;
        if (carController == null) return false;
        if (carController.isPlayerOwned) return false;
        if (carController.isDrivenByAI) return false; // circule avec un PNJ au volant -> carjacking normal, pas d'effraction
        if (forSaleComponent != null) return false;
        return true;
    }

    private void UpdateBreakInPrompt()
    {
        CarBreakInConfig config = CarBreakInConfig.Instance;

        if (spawnedPrompt == null)
        {
            if (config.promptPrefab == null) return;
            Vector3 spawnPos = (exitPoint != null ? exitPoint.position : transform.position) + config.promptOffset;
            spawnedPrompt = Instantiate(config.promptPrefab, spawnPos, Quaternion.identity, transform);
            spawnedPromptLines = spawnedPrompt.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        }

        if (!spawnedPrompt.activeSelf) spawnedPrompt.SetActive(true);

        // Rotation fixe et réglable (voir CarBreakInConfig.promptRotation), pas de LookAt
        // dynamique — avec une caméra vue du dessus, faire "regarder" le prompt vers la
        // caméra le mettrait à plat/à l'envers selon l'angle exact. Une rotation fixe
        // choisie une fois pour ta caméra est plus fiable qu'un calcul par frame.
        spawnedPrompt.transform.rotation = Quaternion.Euler(config.promptRotation);

        for (int i = 0; i < spawnedPromptLines.Length; i++)
        {
            if (i >= config.methods.Length) { spawnedPromptLines[i].gameObject.SetActive(false); continue; }

            CarBreakInMethod m = config.methods[i];
            bool hasTool = PlayerHasTool(m.requiredTool);
            bool onCooldown = m.useCooldownInstead && m.requiredTool != null && IsOnCooldown(m.requiredTool);
            int effectiveFailChance = GetEffectiveFailureChance(m);

            spawnedPromptLines[i].gameObject.SetActive(true);
            if (!hasTool)
                spawnedPromptLines[i].text = $"<color=#777777>{KeyBadge(m.triggerKey)} {m.methodName} (outil requis)</color>";
            else if (onCooldown)
                spawnedPromptLines[i].text = $"<color=#777777>{KeyBadge(m.triggerKey)} {m.methodName} (en recharge)</color>";
            else
                spawnedPromptLines[i].text = $"{KeyBadge(m.triggerKey)} {m.methodName} <color=#ff8888>({effectiveFailChance}% échec)</color>";

            if (hasTool && !onCooldown && Input.GetKeyDown(m.triggerKey))
            {
                TryStartBreakInMethod(m);
            }
        }
    }

    private void HideBreakInPrompt()
    {
        if (spawnedPrompt != null) spawnedPrompt.SetActive(false);
    }

    private string KeyLabel(KeyCode key)
    {
        string s = key.ToString();
        return s.StartsWith("Alpha") ? s.Substring(5) : s;
    }

    // Effet "touche clavier" avec la balise <mark> de TextMeshPro (encart derrière le
    // chiffre) — pas besoin de sprite dédié. Si tu as/trouves de vraies icônes de touches,
    // ce sera à remplacer par un composant Image séparé par ligne, plus de travail mais
    // rendu plus proche de l'image de référence (glyphes façon manette).
    private string KeyBadge(KeyCode key)
    {
        return $"<mark=#ffffff33 padding=\"10,10,4,4\"><b>{KeyLabel(key)}</b></mark>";
    }

    // Plus la voiture est rapide (et/ou chère, si Vehicle Value est renseigné sur son
    // CarController) plus le bonus de difficulté grimpe, jusqu'à Max Difficulty Bonus
    // Percent de CETTE méthode. Les deux facteurs ne s'additionnent pas brut : c'est le
    // plus élevé des deux qui domine, pour éviter qu'une voiture rapide ET chère devienne
    // absurdement difficile par simple cumul.
    private int GetEffectiveFailureChance(CarBreakInMethod method)
    {
        int bonus = 0;
        if (carController != null)
        {
            float speedFactor = Mathf.Clamp01((carController.maxSpeed - 30f) / 60f);
            float valueFactor = carController.vehicleValue > 0 ? Mathf.Clamp01(carController.vehicleValue / 100000f) : 0f;
            float combined = Mathf.Max(speedFactor, valueFactor);
            bonus = Mathf.RoundToInt(combined * method.maxDifficultyBonusPercent);
        }
        return Mathf.Clamp(method.failureChancePercent + bonus, 0, 95);
    }

    private void TryStartBreakInMethod(CarBreakInMethod method)
    {
        isAttemptingBreakIn = true;
        HideBreakInPrompt();
        StartCoroutine(RunBreakInMethod(method));
    }

    private bool PlayerHasTool(ItemData tool)
    {
        if (tool == null) return true;

        if (HotbarManager.Instance != null)
        {
            foreach (HotbarSlot slot in HotbarManager.Instance.hotbarSlots)
            {
                if (slot.itemInSlot == tool) return true;
            }
        }
        if (InventoryManager.Instance != null)
        {
            foreach (InventorySlot slot in InventoryManager.Instance.slots)
            {
                if (slot.item == tool) return true;
            }
        }
        return false;
    }

    private bool IsOnCooldown(ItemData tool)
    {
        return toolCooldownEndTime.TryGetValue(tool, out float endTime) && Time.time < endTime;
    }

    private IEnumerator RunBreakInMethod(CarBreakInMethod method)
    {
        CarBreakInConfig config = CarBreakInConfig.Instance;
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) pc.isDoingQTE = true; // même verrou de mouvement que les autres mini-actions du jeu

        bool minigameSuccess = false;
        bool caughtInTheAct = false;

        switch (method.minigameType)
        {
            case BreakInMinigameType.Lockpick:
                yield return RunLockpick(method, r => minigameSuccess = r);
                break;
            case BreakInMinigameType.Mash:
                yield return RunMash(method, r => minigameSuccess = r);
                break;
            case BreakInMinigameType.QuickTime:
                yield return RunQuickTime(method, r => { minigameSuccess = r.success; caughtInTheAct = r.caught; });
                break;
            case BreakInMinigameType.Progress:
                yield return RunProgressWithCodes(method, r => { minigameSuccess = r.success; caughtInTheAct = r.caught; });
                break;
        }

        if (pc != null) pc.isDoingQTE = false;
        isAttemptingBreakIn = false;

        if (method.requiredTool != null)
        {
            if (method.useCooldownInstead)
            {
                toolCooldownEndTime[method.requiredTool] = Time.time + method.cooldownSeconds;
            }
            else if (!minigameSuccess && method.consumeToolOnFailure && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.RemoveItem(method.requiredTool, 1);
            }
        }

        if (!minigameSuccess)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>Effraction ratée ({method.methodName}) !</color>");
            if (GameManager.Instance != null) GameManager.Instance.ReportCrime(caughtInTheAct ? 20 : 5);
            if (method.minigameType == BreakInMinigameType.QuickTime && method.alwaysTriggersAlarm)
                StartCoroutine(RunAlarm());
            yield break;
        }

        if (Random.Range(0, 100) < GetEffectiveFailureChance(method))
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=yellow>{method.methodName} n'a pas fonctionné sur ce modèle...</color>");
            yield break;
        }

        isBreakInUnlocked = true;

        bool alarmTriggered = method.alwaysTriggersAlarm || caughtInTheAct || Random.Range(0, 100) < method.alarmChancePercent;
        if (alarmTriggered)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>L'alarme se déclenche !</color>");
            if (GameManager.Instance != null) GameManager.Instance.ReportCrime(caughtInTheAct ? 25 : 15);
            StartCoroutine(RunAlarm());
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=green>Véhicule débloqué ({method.methodName}).</color>");
        }
    }

    // --- LOCKPICK : délègue à LockpickMinigame (façon Fallout, même UI que le casino) ---
    private IEnumerator RunLockpick(CarBreakInMethod method, System.Action<bool> onDone)
    {
        if (LockpickMinigame.Instance == null)
        {
            Debug.LogWarning("[CarInteraction] Aucun LockpickMinigame dans la scène.");
            onDone(false);
            yield break;
        }

        bool done = false, success = false;
        LockpickMinigame.Instance.StartMinigame(method.lockpickTime, method.lockpickTolerance,
            () => { success = true; done = true; },
            () => { success = false; done = true; });

        while (!done) yield return null;
        onDone(success);
    }

    // --- MASH : marteler une touche pour remplir la barre avant la fin du temps ---
    private IEnumerator RunMash(CarBreakInMethod method, System.Action<bool> onDone)
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowActionProgress($"Martèle [{KeyLabel(method.mashKey)}] ! ({method.methodName})");

        float fill = 0f, elapsed = 0f;
        bool success = false;

        while (elapsed < method.mashDuration)
        {
            if (Input.GetKeyDown(method.mashKey)) fill += method.mashFillPerPress;
            fill = Mathf.Clamp01(fill - method.mashDecayPerSecond * Time.deltaTime);

            if (UIManager.Instance != null) UIManager.Instance.UpdateActionProgress(fill);
            if (fill >= 1f) { success = true; break; }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (UIManager.Instance != null) UIManager.Instance.HideActionProgress();
        onDone(success);
    }

    // --- QUICKTIME : suite de touches rapide et courte (façon casser une vitre) ---
    private IEnumerator RunQuickTime(CarBreakInMethod method, System.Action<(bool success, bool caught)> onDone)
    {
        KeyCode[] pool = { KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.M };
        bool failed = false, caught = false;

        yield return new WaitForSeconds(0.15f);

        for (int step = 0; step < method.qteSteps; step++)
        {
            KeyCode currentKey = pool[Random.Range(0, pool.Length)];
            if (UIManager.Instance != null) UIManager.Instance.ShowQTE(currentKey.ToString(), method.methodName);

            float timer = method.qteTimeToReact;
            bool stepSuccess = false;

            while (timer > 0)
            {
                timer -= Time.deltaTime;
                if (UIManager.Instance != null && UIManager.Instance.qteSlider != null)
                    UIManager.Instance.qteSlider.value = timer / method.qteTimeToReact;
                if (GameManager.Instance != null && GameManager.Instance.isBeingSeen) caught = true;

                if (Input.anyKeyDown)
                {
                    if (Input.GetKeyDown(currentKey)) { stepSuccess = true; break; }
                    else if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2)) { stepSuccess = false; break; }
                }
                yield return null;
            }

            if (!stepSuccess) { failed = true; break; }
            yield return new WaitForSeconds(0.08f);
        }

        if (UIManager.Instance != null) UIManager.Instance.HideQTE();
        onDone((!failed, caught));
    }

    // --- PROGRESS + CODES : barre passive qui s'interrompt N fois pour taper un code
    // (façon boîtier électronique) — reste à côté du véhicule, mais doit rester vigilant.
    private IEnumerator RunProgressWithCodes(CarBreakInMethod method, System.Action<(bool success, bool caught)> onDone)
    {
        bool caught = false;
        bool failed = false;

        // Répartit les interruptions à peu près régulièrement sur la durée totale (ex: 2
        // interruptions -> environ à 33% et 66% de la progression).
        int interruptions = Mathf.Max(0, method.codeInterruptions);
        float segmentDuration = method.progressDuration / (interruptions + 1);
        float totalElapsed = 0f;

        if (UIManager.Instance != null) UIManager.Instance.ShowActionProgress($"{method.methodName}...");

        for (int segment = 0; segment <= interruptions; segment++)
        {
            float segElapsed = 0f;
            while (segElapsed < segmentDuration)
            {
                if (player == null || Vector3.Distance(player.transform.position, transform.position) > 8f)
                {
                    failed = true;
                    break;
                }
                if (GameManager.Instance != null && GameManager.Instance.isBeingSeen) caught = true;

                segElapsed += Time.deltaTime;
                totalElapsed += Time.deltaTime;
                if (UIManager.Instance != null) UIManager.Instance.UpdateActionProgress(totalElapsed / method.progressDuration);
                yield return null;
            }

            if (failed) break;

            // Une interruption code après chaque segment SAUF le dernier (pas besoin d'un
            // code juste après avoir atteint 100%).
            if (segment < interruptions)
            {
                if (UIManager.Instance != null) UIManager.Instance.HideActionProgress();

                if (CodeEntryMinigame.Instance == null)
                {
                    Debug.LogWarning("[CarInteraction] Aucun CodeEntryMinigame dans la scène.");
                    failed = true;
                    break;
                }

                bool codeDone = false, codeSuccess = false;
                CodeEntryMinigame.Instance.StartMinigame(method.codeTimeLimit,
                    () => { codeSuccess = true; codeDone = true; },
                    () => { codeSuccess = false; codeDone = true; });

                while (!codeDone) yield return null;

                if (!codeSuccess) { failed = true; break; }

                if (UIManager.Instance != null) UIManager.Instance.ShowActionProgress($"{method.methodName}...");
            }
        }

        if (UIManager.Instance != null) UIManager.Instance.HideActionProgress();
        onDone((!failed, caught));
    }

    // --- ALARME : sonne pendant Alarm Duration secondes ; si le joueur passe à portée d'un
    // PNJ (civil ou policier) pendant ce temps, il gagne une étoile de recherche (une seule
    // fois par déclenchement, pas à chaque vérification).
    private IEnumerator RunAlarm()
    {
        CarBreakInConfig config = CarBreakInConfig.Instance;
        if (config == null) yield break;

        float elapsed = 0f;
        bool starGranted = false;

        while (elapsed < config.alarmDuration)
        {
            if (!starGranted && player != null)
            {
                float playerDist = Vector3.Distance(player.transform.position, transform.position);
                if (playerDist <= config.alarmDetectionRadius)
                {
                    Collider[] nearby = Physics.OverlapSphere(transform.position, config.alarmDetectionRadius);
                    foreach (Collider col in nearby)
                    {
                        if (col.GetComponentInParent<NPCBrain>() != null)
                        {
                            starGranted = true;
                            if (GameManager.Instance != null) GameManager.Instance.ReportCrime(config.alarmWantedCrimePoints);
                            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>L'alarme attire l'attention !</color>");
                            break;
                        }
                    }
                }
            }

            yield return new WaitForSeconds(config.alarmCheckInterval);
            elapsed += config.alarmCheckInterval;
        }
    }
}