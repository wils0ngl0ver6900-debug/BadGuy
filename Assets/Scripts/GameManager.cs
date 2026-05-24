using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Économie & Criminologie")]
    public int dirtyMoney = 100;
    public int cleanMoney = 0;

    [Header("Système de Recherche (GTA Style) 🚔")]
    public int wantedLevel = 0; // De 0 à 5 étoiles
    public int crimePoints = 0; // Les points cachés qui remplissent les étoiles

    [Header("Système d'Évasion 🏃‍♂️")]
    public int spottersCount = 0; // Combien de FLICS ou TÉMOINS te voient ?
    public float evadeTimeRequired = 20f; // 20 secondes caché pour perdre les flics
    private float currentEvadeTimer = 0f;
    [HideInInspector] public bool isEvading = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        // LA LOGIQUE D'ÉVASION
        if (wantedLevel > 0)
        {
            if (spottersCount == 0)
            {
                // Personne ne nous voit, on commence à se faire oublier
                isEvading = true;
                currentEvadeTimer += Time.deltaTime;

                if (currentEvadeTimer >= evadeTimeRequired)
                {
                    LoseCops();
                }
            }
            else
            {
                // On a été repéré ! On remet le chrono d'évasion à zéro
                isEvading = false;
                currentEvadeTimer = 0f;
            }
        }
    }

    public void AddDirtyMoney(int amount)
    {
        dirtyMoney += amount;
        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
    }

    // --- LE NOUVEAU MOTEUR DE CRIME ---
    public void ReportCrime(int points)
    {
        crimePoints += points;
        currentEvadeTimer = 0f; // Un nouveau crime annule l'évasion
        isEvading = false;

        UpdateWantedLevel();
    }

    private void UpdateWantedLevel()
    {
        int oldLevel = wantedLevel;

        // Paliers façon GTA (À équilibrer selon tes goûts)
        if (crimePoints >= 150) wantedLevel = 5;
        else if (crimePoints >= 100) wantedLevel = 4;
        else if (crimePoints >= 60) wantedLevel = 3;
        else if (crimePoints >= 30) wantedLevel = 2;
        else if (crimePoints >= 10) wantedLevel = 1;
        else wantedLevel = 0;

        if (wantedLevel > oldLevel)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification($"<color=red>RECHERCHÉ : {wantedLevel} ÉTOILE(S) !</color>");
        }

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

    // --- DÉGUISEMENT (RÉDUCTION D'ÉTOILES) ---
    public void DropOneStarFromDisguise()
    {
        if (wantedLevel > 0 && spottersCount == 0)
        {
            // Fait redescendre les points juste en dessous du palier actuel
            if (wantedLevel == 5) crimePoints = 149;
            else if (wantedLevel == 4) crimePoints = 99;
            else if (wantedLevel == 3) crimePoints = 59;
            else if (wantedLevel == 2) crimePoints = 29;
            else if (wantedLevel == 1) crimePoints = 0;

            UpdateWantedLevel();
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=green>Déguisement efficace : -1 Étoile</color>");
        }
    }

    // --- ARRESTATION ---
    public void Busted()
    {
        dirtyMoney = 0;
        if (HotbarManager.Instance != null) HotbarManager.Instance.RemoveIllegalItems();

        List<ItemData> itemsToConfiscate = new List<ItemData>();
        foreach (var item in InventoryManager.Instance.items)
            if (item != null && item.isIllegal) itemsToConfiscate.Add(item);

        foreach (var item in itemsToConfiscate) InventoryManager.Instance.RemoveItem(item);

        LoseCops(); // Remet tout à zéro proprement
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("ARRÊTÉ ! Argent sale et objets illégaux confisqués.");

        // On détruit les flics à pied de la zone pour repartir propre
        CopAI[] cops = FindObjectsOfType<CopAI>();
        foreach (CopAI cop in cops) Destroy(cop.gameObject);
    }
}