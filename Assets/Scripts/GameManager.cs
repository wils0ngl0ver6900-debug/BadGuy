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

    [Header("Système de Recherche (GTA Style) 🚔")]
    [Range(0, 5)] public int wantedLevel = 0;
    public int crimePoints = 0;
    [HideInInspector] public bool isBeingSeen { get; private set; }

    [Header("Points de Réapparition (Spawns) 🏥/🚓")]
    public Transform hospitalSpawnPoint;
    public Transform policeStationSpawnPoint;

    public bool isEvading
    {
        get
        {
            return wantedLevel > 0 && !isBeingSeen;
        }
    }

    private NPCBrain[] allNPCsInScene;
    private float scanTimer = 0f;
    private bool lastEvadingState = false;

    private float lastHitReportTime = 0f;

    // --- CORRECTIF : VERROU ANTI-DOUBLE DÉCLENCHEMENT ---
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
        else
        {
            lastEvadingState = false;
        }
    }

    private bool CheckIfAnyCopSeesPlayer()
    {
        allNPCsInScene = FindObjectsOfType<NPCBrain>();
        foreach (NPCBrain npc in allNPCsInScene)
        {
            if (npc.role == NPCBrain.NPCRole.Policier && npc.isSeeingPlayer) return true;
        }
        return false;
    }

    public void AddDirtyMoney(int amount)
    {
        dirtyMoney += amount;
        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
    }

    public void ReportCrime(int points)
    {
        crimePoints += points;
        UpdateWantedLevel();
    }

    public void ReportHitOrMurder()
    {
        if (Time.time - lastHitReportTime < 1.0f) return;
        lastHitReportTime = Time.time;

        if (wantedLevel < 2)
        {
            crimePoints = 30;
        }
        else if (wantedLevel == 2)
        {
            crimePoints = 60;
        }
        else if (wantedLevel == 3)
        {
            crimePoints = 100;
        }

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

        if (wantedLevel > oldLevel && UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"<color=red>RECHERCHÉ : {wantedLevel} ÉTOILE(S) !</color>");

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
        if (isDefeated) return; // On bloque si on est déjà en train de se faire arrêter !
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null && pc.currentHealth <= 0) return;

        StartCoroutine(DefeatSequence(true));
    }

    public void Wasted()
    {
        if (isDefeated) return; // On bloque si on est déjà mort !
        StartCoroutine(DefeatSequence(false));
    }

    private IEnumerator DefeatSequence(bool isBusted)
    {
        isDefeated = true; // On verrouille la séquence

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification(isBusted ? "<color=blue>ARRÊTÉ !</color>" : "<color=red>VOUS ÊTES MORT !</color>");
        }

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
        }

        ColorAdjustments colorAdjustments = null;
        GameObject volumeObj = GameObject.FindWithTag("GameController");
        if (volumeObj != null)
        {
            Volume globalVolume = volumeObj.GetComponent<Volume>();
            if (globalVolume != null && globalVolume.profile != null)
            {
                globalVolume.profile.TryGet(out colorAdjustments);
            }
        }

        Time.timeScale = 0.25f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        NPCBrain[] allBrains = FindObjectsOfType<NPCBrain>();
        foreach (NPCBrain brain in allBrains)
        {
            if (brain != null && brain.role == NPCBrain.NPCRole.Policier)
            {
                UnityEngine.AI.NavMeshAgent agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                brain.enabled = false;
            }
        }

        float elapsedColor = 0f;
        float fadeColorDuration = 3f;
        float initialSaturation = colorAdjustments != null ? colorAdjustments.saturation.value : 0f;

        while (elapsedColor < fadeColorDuration)
        {
            elapsedColor += Time.unscaledDeltaTime;
            if (colorAdjustments != null)
            {
                colorAdjustments.saturation.value = Mathf.Lerp(initialSaturation, -100f, elapsedColor / fadeColorDuration);
            }
            yield return null;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(0.3f));
        }

        // --- CORRECTIF NOIR ET BLANC ---
        // On ne remet pas 'initialSaturation' car il a peut-être été corrompu, on force le reset à 0 !
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = 0f;
        }

        // --- CORRECTIF RESPAWN VOITURE ---
        // Pendant que l'écran est noir, on éjecte secrètement le joueur de la voiture !
        CarInteraction[] allInteractions = FindObjectsOfType<CarInteraction>();
        foreach (CarInteraction interaction in allInteractions)
        {
            if (interaction.carController != null && interaction.carController.isDrivenByPlayer)
            {
                interaction.ExitCar();
            }
        }

        // Comme ExitCar a réactivé les contrôles, on les re-bloque en attendant la fin de la cinématique
        if (pc != null) pc.enabled = false;

        if (isBusted)
        {
            dirtyMoney = 0;
            if (HotbarManager.Instance != null) HotbarManager.Instance.RemoveIllegalItems();
            List<ItemData> itemsToConfiscate = new List<ItemData>();
            foreach (var item in InventoryManager.Instance.items) if (item != null && item.isIllegal) itemsToConfiscate.Add(item);
            foreach (var item in itemsToConfiscate) InventoryManager.Instance.RemoveItem(item);
        }
        else
        {
            cleanMoney -= 500;
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
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                }
            }
        }

        yield return new WaitForSeconds(2.5f);

        if (pc != null)
        {
            pc.transform.SetParent(null);
            pc.gameObject.SetActive(true);

            if (MinimapFollow.Instance != null)
            {
                MinimapFollow.Instance.target = pc.transform;
            }

            Transform targetPoint = isBusted ? policeStationSpawnPoint : hospitalSpawnPoint;

            if (targetPoint != null)
            {
                pc.transform.position = targetPoint.position;
                pc.transform.rotation = targetPoint.rotation;
            }

            Rigidbody playerRb = pc.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }

            foreach (Transform child in pc.transform)
            {
                child.gameObject.SetActive(true);
            }
            foreach (Renderer r in pc.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
            }

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

        isDefeated = false; // On déverrouille pour la prochaine fois !
    }
}