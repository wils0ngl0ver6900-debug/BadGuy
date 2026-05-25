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
    [HideInInspector] public bool isBeingSeen { get; private set; }

    // LE CORRECTIF EST ICI : L'UIManager peut de nouveau lire isEvading sans erreur.
    // Il va chercher l'information intelligemment dans le PoliceManager !
    public bool isEvading
    {
        get
        {
            return PoliceManager.Instance != null &&
                   !PoliceManager.Instance.isPlayerSpotted &&
                   wantedLevel > 0;
        }
    }

    private NPCBrain[] allNPCsInScene;
    private float scanTimer = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
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
            if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
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
        dirtyMoney = 0;
        if (HotbarManager.Instance != null) HotbarManager.Instance.RemoveIllegalItems();
        List<ItemData> itemsToConfiscate = new List<ItemData>();
        foreach (var item in InventoryManager.Instance.items) if (item != null && item.isIllegal) itemsToConfiscate.Add(item);
        foreach (var item in itemsToConfiscate) InventoryManager.Instance.RemoveItem(item);

        LoseCops();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("ARRÊTÉ ! Argent sale et objets illégaux confisqués.");

        CopAI[] cops = FindObjectsOfType<CopAI>();
        foreach (CopAI cop in cops) Destroy(cop.gameObject);
    }
}