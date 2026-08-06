using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Gère le garage de la planque : stockage de 1 à 5 véhicules, débloqué en même temps
// que garageModule dans SafehouseManager (safehouseLevel >= 2). Le nombre de places
// démarre à maxGarageSlots (1 par défaut) et peut être agrandi via PurchaseGarageSlot()
// jusqu'à 5, contre de l'argent propre. Gère aussi son propre panneau UI (liste des
// véhicules stockés, bouton Récupérer par emplacement) — ouvert via un Interactable
// (ActionType.Garage) posé quelque part dans la pièce du garage, même principe que
// pour ouvrir les labos.
public class GarageManager : MonoBehaviour
{
    public static GarageManager Instance;

    [System.Serializable]
    public class GarageCarEntry
    {
        [Tooltip("Doit correspondre EXACTEMENT à CarController.carModelName pour ce véhicule.")]
        public string modelName;
        [Tooltip("Le prefab à faire réapparaître quand on récupère ce véhicule.")]
        public GameObject prefab;
    }

    [System.Serializable]
    public class StoredVehicle
    {
        public string modelName;
        public StoredVehicle(string modelName) { this.modelName = modelName; }
    }

    [Header("Catalogue des véhicules stockables 🚗")]
    [Tooltip("Fais correspondre chaque carModelName existant à son prefab pour pouvoir le refaire apparaître depuis le garage.")]
    public GarageCarEntry[] knownCarPrefabs;

    [Header("Places de Parking 🅿️")]
    [Range(1, 5)] public int maxGarageSlots = 1;
    private const int ABSOLUTE_MAX_SLOTS = 5;
    public int costPerExtraSlot = 20000;

    [Header("État Actuel (ne pas éditer à la main)")]
    public List<StoredVehicle> storedVehicles = new List<StoredVehicle>();

    [Header("Sortie du Garage (récupération)")]
    public Transform vehicleSpawnPoint;
    public float spawnCheckRadius = 3f;
    public LayerMask vehicleLayerMask;

    [Header("UI du Garage")]
    public GameObject garageUIPanel;
    [Tooltip("5 cases, dans l'ordre : texte affiché pour l'emplacement 1 à 5.")]
    public TextMeshProUGUI[] slotLabels;
    [Tooltip("5 cases, dans l'ordre : bouton \"Récupérer\" de l'emplacement 1 à 5 (désactivé automatiquement si l'emplacement est vide ou pas encore débloqué).")]
    public Button[] slotButtons;

    [HideInInspector] public bool isOpen = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (garageUIPanel != null) garageUIPanel.SetActive(false);
    }

    public bool IsUnlocked()
    {
        return SafehouseManager.Instance != null && SafehouseManager.Instance.safehouseLevel >= 2;
    }

    // ==============================================================
    // OUVERTURE / FERMETURE DU PANNEAU (même pattern que les labos)
    // ==============================================================

    public void OpenGarageUI()
    {
        if (!IsUnlocked())
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Débloque d'abord le garage dans ta planque !</color>");
            return;
        }

        isOpen = true;
        if (garageUIPanel != null) garageUIPanel.SetActive(true);

        RefreshGarageUI();

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(false);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void CloseGarageUI()
    {
        isOpen = false;
        if (garageUIPanel != null) garageUIPanel.SetActive(false);

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseGarageUI();
        }
    }

    // Met à jour les 5 cases : nom du véhicule si occupé, "-- Vide --" sinon, et grise
    // le bouton Récupérer correspondant si rien à récupérer à cet emplacement.
    public void RefreshGarageUI()
    {
        if (slotLabels == null) return;

        for (int i = 0; i < slotLabels.Length; i++)
        {
            bool hasVehicle = i < storedVehicles.Count;

            if (slotLabels[i] != null)
                slotLabels[i].text = hasVehicle ? storedVehicles[i].modelName : "-- Vide --";

            if (slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
                slotButtons[i].interactable = hasVehicle;
        }
    }

    // ==============================================================
    // STOCKAGE / RÉCUPÉRATION
    // ==============================================================

    // À appeler depuis une zone de dépôt (ex: GarageStoreZone) quand le joueur est au
    // volant d'une voiture et valide le rangement. "safeStandPoint" est optionnel : si
    // fourni, le joueur atterrit là plutôt qu'au exitPoint habituel de la voiture (plus
    // fiable dans le contexte du garage — évite de tomber sous la carte).
    public bool TryStoreVehicle(CarController car, CarInteraction interaction, Transform safeStandPoint = null)
    {
        if (car == null) return false;

        if (!IsUnlocked())
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Débloque d'abord le garage dans ta planque !</color>");
            return false;
        }

        // Seuls les véhicules achetés (CarForSale.Interact() met isPlayerOwned à true)
        // peuvent être rangés. Une caisse volée reste dehors.
        if (!car.isPlayerOwned)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Cette voiture n'est pas à toi — impossible de la ranger ici.</color>");
            return false;
        }

        if (storedVehicles.Count >= maxGarageSlots)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>Garage plein ({storedVehicles.Count}/{maxGarageSlots}) !</color>");
            return false;
        }

        storedVehicles.Add(new StoredVehicle(car.carModelName));

        if (car.isDrivenByPlayer)
        {
            // Filet de sécurité : si la référence transmise est nulle (composant introuvable
            // au moment où GarageStoreZone l'a cherchée), on retente une recherche directe
            // avant d'abandonner. Sans ça, on risquait de détruire la voiture sans jamais
            // avoir fait sortir le joueur — collisions et rendu restaient désactivés
            // ("mode conduite"), d'où la chute sous la carte, invisible.
            CarInteraction safeInteraction = interaction != null ? interaction : car.GetComponentInChildren<CarInteraction>();

            if (safeInteraction == null)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification("<color=red>Erreur : impossible de sortir du véhicule proprement, rangement annulé.</color>");
                storedVehicles.RemoveAt(storedVehicles.Count - 1); // on annule l'ajout fait juste au-dessus
                return false;
            }

            // On coupe les collisions de la voiture AVANT de repositionner le joueur.
            // Destroy() est différé à la fin de la frame : sans ça, la voiture reste
            // physiquement présente pendant un instant au moment même où les collisions
            // du joueur se réactivent au point de sortie. Si les deux se chevauchent
            // (selon le gabarit du véhicule), la résolution physique peut les repousser
            // violemment — souvent vers le bas, à travers le sol.
            foreach (Collider col in car.GetComponentsInChildren<Collider>())
            {
                if (col != null) col.enabled = false;
            }

            Vector3 targetPos = safeStandPoint != null ? safeStandPoint.position
                              : (safeInteraction.exitPoint != null ? safeInteraction.exitPoint.position : car.transform.position);

            // Recalage au sol par raycast : plutôt que de faire confiance à la hauteur Y
            // du point configuré (qui peut être légèrement fausse selon le terrain à cet
            // endroit précis), on retrouve le vrai sol en dessous et on pose le joueur juste
            // au-dessus. Si rien n'est détecté (pas de sol dans les 20 unités en dessous),
            // on garde la position d'origine plutôt que d'annuler.
            if (Physics.Raycast(targetPos + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 20f))
            {
                targetPos = groundHit.point + Vector3.up * 0.1f;
            }

            // --- LOG TEMPORAIRE DE DIAGNOSTIC : à retirer une fois le bug confirmé réglé ---
            Debug.Log($"[GARAGE-DEBUG] Rangement de '{car.carModelName}' | safeStandPoint={(safeStandPoint != null ? safeStandPoint.name : "NULL")} | position finale utilisée = {targetPos} | sol détecté = {groundHit.collider?.name ?? "AUCUN"} | position de la voiture au moment du rangement = {car.transform.position}");

            safeInteraction.ExitCarAt(targetPos);
        }

        Destroy(car.gameObject);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"<color=cyan>{car.carModelName} rangée au garage ({storedVehicles.Count}/{maxGarageSlots}).</color>");

        RefreshGarageUI();
        return true;
    }

    // À relier au bouton "Récupérer" de chaque emplacement du garage (voir slotButtons).
    public void RetrieveVehicle(int slotIndex)
    {
        if (!IsUnlocked()) return;
        if (slotIndex < 0 || slotIndex >= storedVehicles.Count) return;

        if (vehicleSpawnPoint == null)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Erreur : point de sortie du garage non configuré !</color>");
            return;
        }

        // Sécurité : on vérifie que rien ne bloque déjà la sortie avant de faire apparaître le véhicule.
        Collider[] hits = Physics.OverlapSphere(vehicleSpawnPoint.position, spawnCheckRadius, vehicleLayerMask);
        if (hits.Length > 0)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>La sortie du garage est bloquée !</color>");
            return;
        }

        StoredVehicle stored = storedVehicles[slotIndex];
        GameObject prefab = FindPrefabForModel(stored.modelName);

        if (prefab == null)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Erreur : véhicule introuvable dans le catalogue du garage !</color>");
            return;
        }

        GameObject spawned = Instantiate(prefab, vehicleSpawnPoint.position, vehicleSpawnPoint.rotation);
        // Ce véhicule vient du garage : il a forcément déjà été acheté pour y entrer.
        // Sans ça, une caisse fraîchement récupérée serait considérée comme "volée" et
        // impossible à re-ranger.
        CarController spawnedCar = spawned.GetComponent<CarController>();
        if (spawnedCar != null) spawnedCar.isPlayerOwned = true;

        storedVehicles.RemoveAt(slotIndex);

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=green>{stored.modelName} sortie du garage !</color>");

        RefreshGarageUI();
    }

    private GameObject FindPrefabForModel(string modelName)
    {
        if (knownCarPrefabs == null) return null;

        foreach (GarageCarEntry entry in knownCarPrefabs)
        {
            if (entry != null && entry.modelName == modelName) return entry.prefab;
        }
        return null;
    }

    // À relier au bouton "Agrandir le garage" (max 5 places).
    public void PurchaseGarageSlot()
    {
        if (maxGarageSlots >= ABSOLUTE_MAX_SLOTS)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Garage déjà au maximum (5 places) !</color>");
            return;
        }

        if (GameManager.Instance == null) return;

        if (GameManager.Instance.cleanMoney >= costPerExtraSlot)
        {
            GameManager.Instance.cleanMoney -= costPerExtraSlot;
            if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-costPerExtraSlot, "Extension Garage");

            maxGarageSlots++;
            RefreshGarageUI();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification($"<color=green>Garage agrandi : {maxGarageSlots}/{ABSOLUTE_MAX_SLOTS} places !</color>");
                UIManager.Instance.UpdateHUD();
            }
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Fonds propres insuffisants.</color>");
        }
    }
}