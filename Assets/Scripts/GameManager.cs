using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Économie")]
    public int dirtyMoney = 100;
    public int cleanMoney = 0;
    [Tooltip("Glisse ici le ScriptableObject 'Item_ArgentSale'")]
    public ItemData dirtyMoneyItemDef;

    [Header("Système de Recherche (GTA Style) 🚔")]
    [Range(0, 5)] public int wantedLevel = 0;
    public int crimePoints = 0;
    [HideInInspector] public bool isBeingSeen { get; private set; }

    [Header("Bagarre à mains nues 👊")]
    [Tooltip("Nombre de morts à mains nues nécessaires pour qu'une bagarre puisse dépasser 2 étoiles.")]
    public int unarmedKillsNeededToEscalate = 2;
    public int unarmedKills = 0;

    [Header("Points de Réapparition (Spawns) 🏥/🚓")]
    public Transform hospitalSpawnPoint;
    public Transform policeStationSpawnPoint;

    public bool isEvading { get { return wantedLevel > 0 && !isBeingSeen; } }

    private NPCBrain[] allNPCsInScene;
    private float scanTimer = 0f;
    private bool lastEvadingState = false;
    private float lastHitReportTime = 0f;
    private bool isDefeated = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        wantedLevel = 0;
        crimePoints = 0;
        allNPCsInScene = FindObjectsOfType<NPCBrain>();
        SyncDirtyMoneyItem();
    }

    private void Update()
    {
        scanTimer += Time.deltaTime;
        if (scanTimer >= 0.25f)
        {
            isBeingSeen = CheckIfAnyCopSeesPlayer();
            scanTimer = 0f;
        }

        if (wantedLevel > 0)
        {
            bool currentlyEvading = isEvading;
            if (currentlyEvading != lastEvadingState)
            {
                lastEvadingState = currentlyEvading;
                if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
            }
        }
        else lastEvadingState = false;
    }

    private bool CheckIfAnyCopSeesPlayer()
    {
        allNPCsInScene = FindObjectsOfType<NPCBrain>();
        foreach (NPCBrain npc in allNPCsInScene)
            if (npc.role == NPCBrain.NPCRole.Policier && npc.isSeeingPlayer) return true;
        return false;
    }

    public void SyncDirtyMoneyItem()
    {
        if (dirtyMoneyItemDef == null || InventoryManager.Instance == null) return;

        // On regarde combien d'argent on a DANS LE SAC physiquement
        int countInBag = InventoryManager.Instance.GetTotalItemAmount(dirtyMoneyItemDef);

        // Si le sac a moins que notre compteur global, on ajoute la différence
        if (countInBag < dirtyMoney)
        {
            InventoryManager.Instance.AddItem(dirtyMoneyItemDef, dirtyMoney - countInBag, true);
        }
        // S'il a plus (par exemple si on jette des billets), on détruit la différence
        else if (countInBag > dirtyMoney)
        {
            InventoryManager.Instance.RemoveItem(dirtyMoneyItemDef, countInBag - dirtyMoney);
        }
    }

    public bool AddDirtyMoney(int amount)
    {
        int countInBag = InventoryManager.Instance.GetTotalItemAmount(dirtyMoneyItemDef);

        if (amount > 0 && countInBag == 0)
        {
            if (InventoryManager.Instance.slots.Count >= InventoryManager.Instance.maxSlots)
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Inventaire plein ! Impossible de prendre l'argent.");
                return false;
            }
        }

        dirtyMoney += amount;
        SyncDirtyMoneyItem();

        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();

        if (QuestManager.Instance != null && amount > 0)
            QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.ArgentSale, amount);

        return true;
    }

    public void ReportCrime(int points)
    {
        crimePoints += points;
        UpdateWantedLevel();
    }

    // Une bagarre à mains nues ne doit pas escalader comme une fusillade : tant que le
    // joueur n'a pas tué assez de monde à mains nues (2 par défaut), les coups de poing
    // ne font simplement plus monter la recherche une fois 2 étoiles atteintes — ni au-delà,
    // ni en dessous (si le joueur est déjà à un niveau supérieur à cause d'armes à feu, on n'y touche pas).
    public void ReportMeleeCrime(int points)
    {
        if (unarmedKills < unarmedKillsNeededToEscalate && wantedLevel >= 2)
        {
            return;
        }
        ReportCrime(points);
    }

    public void RegisterUnarmedKill()
    {
        unarmedKills++;
    }

    public void ReportHitOrMurder(bool isMelee = false)
    {
        if (Time.time - lastHitReportTime < 1.0f) return;

        // Même plafond que ReportMeleeCrime : un coup à mains nues qui ne tue pas ne doit pas
        // non plus pousser au-delà de 2 étoiles tant que les 2 morts à mains nues ne sont pas atteintes.
        if (isMelee && unarmedKills < unarmedKillsNeededToEscalate && wantedLevel >= 2)
        {
            return;
        }

        lastHitReportTime = Time.time;

        if (wantedLevel < 2) crimePoints = 30;
        else if (wantedLevel == 2) crimePoints = 60;
        else if (wantedLevel == 3) crimePoints = 100;

        UpdateWantedLevel();
    }

    private void UpdateWantedLevel()
    {
        int oldLevel = wantedLevel;
        if (crimePoints >= 150) wantedLevel = 5;
        else if (crimePoints >= 100) wantedLevel = 4;
        else if (crimePoints >= 60) wantedLevel = 3;
        else if (crimePoints >= 30) wantedLevel = 2;
        else if (crimePoints >= 10) wantedLevel = 1;
        else wantedLevel = 0;

        if (wantedLevel > oldLevel)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>RECHERCHÉ : {wantedLevel} ÉTOILE(S) !</color>");
            if (QuestManager.Instance != null) QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.AttirerFlics, wantedLevel);
        }
        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
    }

    public void LoseCops()
    {
        wantedLevel = 0;
        crimePoints = 0;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("<color=yellow>Indice de recherche perdu.</color>");
            UIManager.Instance.UpdateHUD();
        }
        if (QuestManager.Instance != null) QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.SemerFlics, 1);
    }

    public void DropOneStarFromDisguise()
    {
        if (wantedLevel > 0 && !isBeingSeen)
        {
            if (wantedLevel == 5) crimePoints = 149;
            else if (wantedLevel == 4) crimePoints = 99;
            else if (wantedLevel == 3) crimePoints = 59;
            else if (wantedLevel == 2) crimePoints = 29;
            else if (wantedLevel == 1) crimePoints = 0;
            UpdateWantedLevel();
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=green> Silhouette modifiée : -1 Étoile</color>");
        }
    }

    public void Busted()
    {
        if (isDefeated) return;
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null && pc.currentHealth <= 0) return;
        StartCoroutine(DefeatSequence(true));
    }

    public void Wasted()
    {
        if (isDefeated) return;
        StartCoroutine(DefeatSequence(false));
    }

    // Même principe que TargetHealth.EnableRagdoll() pour les PNJ, adapté au joueur : contrairement
    // aux PNJ (déplacés par NavMeshAgent, sans Rigidbody sur la racine), le joueur a son propre
    // Rigidbody de mouvement sur la racine — il faut le neutraliser (kinematic) pour qu'il n'entre
    // pas en conflit avec les Rigidbody des os du ragdoll, qui eux prennent le relais visuellement.
    private void EnablePlayerRagdoll(GameObject player)
    {
        Animator anim = player.GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = false;

        Rigidbody rootRb = player.GetComponent<Rigidbody>();
        if (rootRb != null)
        {
            rootRb.isKinematic = true; // La capsule de mouvement ne doit plus bouger elle-même
        }

        Collider mainCollider = player.GetComponent<Collider>();
        if (mainCollider != null) mainCollider.enabled = false;

        Rigidbody[] rbs = player.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs)
        {
            if (rb.gameObject == player) continue; // La racine est déjà gérée au-dessus
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddForce(-player.transform.forward * 3f + Vector3.up * 1.5f, ForceMode.Impulse);
        }
    }

    private IEnumerator DefeatSequence(bool isBusted)
    {
        isDefeated = true;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification(isBusted ? "<color=blue>ARRÊTÉ !</color>" : "<color=red>VOUS ÊTES MORT !</color>");

        PlayerController pc = FindObjectOfType<PlayerController>();
        MonoBehaviour playerAim = null;
        MonoBehaviour playerCombat = null;

        if (pc != null)
        {
            pc.enabled = false;
            playerAim = pc.GetComponent("PlayerAim") as MonoBehaviour;
            playerCombat = pc.GetComponent("PlayerCombat") as MonoBehaviour;
            if (playerAim != null) playerAim.enabled = false;
            if (playerCombat != null) playerCombat.enabled = false;

            if (!isBusted) EnablePlayerRagdoll(pc.gameObject);
        }

        ColorAdjustments colorAdjustments = null;
        GameObject volumeObj = GameObject.FindWithTag("GameController");
        if (volumeObj != null)
        {
            Volume globalVolume = volumeObj.GetComponent<Volume>();
            if (globalVolume != null && globalVolume.profile != null) globalVolume.profile.TryGet(out colorAdjustments);
        }

        // Ralenti moins extrême qu'avant (0.25 écrasait quasiment tout mouvement du ragdoll
        // pendant les 3 secondes de désaturation — le corps semblait figé au lieu de tomber).
        Time.timeScale = 0.6f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        NPCBrain[] allBrains = FindObjectsOfType<NPCBrain>();
        foreach (NPCBrain brain in allBrains)
        {
            if (brain != null && brain.role == NPCBrain.NPCRole.Policier)
            {
                UnityEngine.AI.NavMeshAgent agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
                brain.enabled = false;
            }
        }

        float elapsedColor = 0f;
        float fadeColorDuration = 3f;
        float initialSaturation = colorAdjustments != null ? colorAdjustments.saturation.value : 0f;

        while (elapsedColor < fadeColorDuration)
        {
            elapsedColor += Time.unscaledDeltaTime;
            if (colorAdjustments != null) colorAdjustments.saturation.value = Mathf.Lerp(initialSaturation, -100f, elapsedColor / fadeColorDuration);
            yield return null;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(0.3f));
        }

        if (colorAdjustments != null) colorAdjustments.saturation.value = 0f;

        CarInteraction[] allInteractions = FindObjectsOfType<CarInteraction>();
        foreach (CarInteraction interaction in allInteractions)
        {
            if (interaction.carController != null && interaction.carController.isDrivenByPlayer)
            {
                GameObject vehicleToDestroy = interaction.carController.gameObject;
                interaction.ExitCar();
                Destroy(vehicleToDestroy);
            }
        }

        if (pc != null) pc.enabled = false;

        if (isBusted)
        {
            dirtyMoney = 0;
            SyncDirtyMoneyItem();

            if (HotbarManager.Instance != null) HotbarManager.Instance.RemoveIllegalItems();

            InventoryManager.Instance.slots.RemoveAll(s => s.item != null && s.item.isIllegal);
        }
        else
        {
            cleanMoney -= 500;
            if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-500, "Frais Hospitaliers");
            if (cleanMoney < 0) cleanMoney = 0;
        }

        LoseCops();
        if (PoliceManager.Instance != null) PoliceManager.Instance.DespawnAllCops();

        foreach (NPCBrain brain in allBrains)
        {
            if (brain != null && brain.role == NPCBrain.NPCRole.Policier)
            {
                brain.enabled = true;
                UnityEngine.AI.NavMeshAgent agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
            }
        }

        yield return new WaitForSeconds(2.5f);

        if (pc != null)
        {
            pc.transform.SetParent(null);
            pc.gameObject.SetActive(true);

            if (MinimapFollow.Instance != null) MinimapFollow.Instance.target = pc.transform;

            Transform targetPoint = isBusted ? policeStationSpawnPoint : hospitalSpawnPoint;

            if (targetPoint != null)
            {
                pc.transform.position = targetPoint.position;
                pc.transform.rotation = targetPoint.rotation;
            }

            Rigidbody playerRb = pc.GetComponent<Rigidbody>();
            if (playerRb != null) { playerRb.linearVelocity = Vector3.zero; playerRb.angularVelocity = Vector3.zero; }

            foreach (Transform child in pc.transform) child.gameObject.SetActive(true);
            foreach (Renderer r in pc.GetComponentsInChildren<Renderer>(true)) r.enabled = true;

            pc.Heal(pc.maxHealth);
            pc.enabled = true;

            if (playerAim != null) playerAim.enabled = true;
            if (playerCombat != null) playerCombat.enabled = true;
        }

        yield return new WaitForSeconds(0.5f);

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(2f));
            UIManager.Instance.transitionPanel.SetActive(false);
        }

        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();

        isDefeated = false;
    }
}