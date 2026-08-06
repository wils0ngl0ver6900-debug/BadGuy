using UnityEngine;
using System.Collections.Generic;

// Gère le garage de la planque : stockage de 1 à 5 véhicules, débloqué en même temps
// que garageModule dans SafehouseManager (safehouseLevel >= 2). Le nombre de places
// démarre à maxGarageSlots (1 par défaut) et peut être agrandi via PurchaseGarageSlot()
// jusqu'à 5, contre de l'argent propre — même logique que PurchaseUpgrade() côté planque.
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

    [Header("Sortie du Garage")]
    public Transform vehicleSpawnPoint;
    public float spawnCheckRadius = 3f;
    public LayerMask vehicleLayerMask;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool IsUnlocked()
    {
        return SafehouseManager.Instance != null && SafehouseManager.Instance.safehouseLevel >= 2;
    }

    // À appeler depuis une zone de dépôt (ex: GarageStoreZone) quand le joueur est au
    // volant d'une voiture et valide le rangement.
    public bool TryStoreVehicle(CarController car, CarInteraction interaction)
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

        if (interaction != null && car.isDrivenByPlayer)
        {
            interaction.ExitCar();
        }

        Destroy(car.gameObject);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"<color=cyan>{car.carModelName} rangée au garage ({storedVehicles.Count}/{maxGarageSlots}).</color>");

        return true;
    }

    // À relier au bouton "Récupérer" de chaque emplacement du garage dans la planque.
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