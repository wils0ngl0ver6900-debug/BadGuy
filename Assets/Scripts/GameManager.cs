using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Économie")]
    public int dirtyMoney = 100;
    public int cleanMoney = 0;

    [Header("Système de Recherche (GTA Style) 🚔")]
    [Range(0, 5)] public int wantedLevel = 0;
    public int crimePoints = 0;

    [Header("Système d'Évasion 🏃‍♂️")]
    public float evadeTimeRequired = 20f; // Modifiable dans Unity !
    [SerializeField] private float currentEvadeTimer = 0f;
    [HideInInspector] public bool isEvading = false;
    [HideInInspector] public bool isBeingSeen { get; private set; } // Personne ne nous voit ?

    private NPCBrain[] allNPCsInScene; // Cache pour optimisation
    private float scanTimer = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Premier scan au démarrage
        allNPCsInScene = FindObjectsOfType<NPCBrain>();
    }

    private void Update()
    {
        // Optimisation : On ne scanne les PNJ que 4 fois par seconde, pas 60.
        scanTimer += Time.deltaTime;
        if (scanTimer >= 0.25f)
        {
            isBeingSeen = CheckIfAnyCopSeesPlayer();
            scanTimer = 0f;
        }

        // --- LOGIQUE D'ÉVASION GTA STYLE ---
        if (wantedLevel > 0)
        {
            if (!isBeingSeen)
            {
                // Personne ne nous voit ! Le chrono d'évasion se lance
                isEvading = true;
                currentEvadeTimer += Time.deltaTime;

                if (currentEvadeTimer >= evadeTimeRequired)
                {
                    LoseCops();
                }
            }
            else
            {
                // On est repéré ! Évasion annulée, timer reset
                isEvading = false;
                currentEvadeTimer = 0f;
            }

            // --- PETIT BONUS VISUEL : ÉTOILES CLIGNOTANTES ---
            // On demande à l'UI de s'actualiser pour le clignotement
            if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
        }
    }

    // La fonction "Pro" universelle de détection
    private bool CheckIfAnyCopSeesPlayer()
    {
        // On récupère tous les PNJ activement dans la scène (vivants)
        // Note : FindObjectsOfType est lourd, mais avec notre scanTimer, c'est OK pour un proto.
        allNPCsInScene = FindObjectsOfType<NPCBrain>();

        foreach (NPCBrain npc in allNPCsInScene)
        {
            // Si c'est un flic ET qu'il nous voit
            if (npc.role == NPCBrain.NPCRole.Policier && npc.isSeeingPlayer)
            {
                return true; // Trouvé ! On arrête le scan.
            }
        }
        return false; // Personne ne nous voit.
    }

    public void AddDirtyMoney(int amount)
    {
        dirtyMoney += amount;
        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
    }

    // --- MOTEUR DE CRIME ---
    public void ReportCrime(int points)
    {
        crimePoints += points;
        currentEvadeTimer = 0f;
        isEvading = false;
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
        currentEvadeTimer = 0f;
        isEvading = false;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("<color=yellow>Indice de recherche perdu.</color>");
            UIManager.Instance.UpdateHUD();
        }
    }

    public void DropOneStarFromDisguise()
    {
        // Déguisement possible uniquement si PERSONNE ne nous voit
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
        dirtyMoney = 0;
        if (HotbarManager.Instance != null) HotbarManager.Instance.RemoveIllegalItems();
        List<ItemData> itemsToConfiscate = new List<ItemData>();
        foreach (var item in InventoryManager.Instance.items) if (item != null && item.isIllegal) itemsToConfiscate.Add(item);
        foreach (var item in itemsToConfiscate) InventoryManager.Instance.RemoveItem(item);

        LoseCops();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("ARRÊTÉ ! Argent sale et objets illégaux confisqués.");

        // On détruit les flics à pied pour nettoyer
        CopAI[] cops = FindObjectsOfType<CopAI>(); // Ou NPCBrain piétons
        foreach (CopAI cop in cops) Destroy(cop.gameObject);
    }
}