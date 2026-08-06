using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
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
        [Tooltip("Petite photo/icône du véhicule, utilisée dans l'UI du garage et dans le choix de livraison de Jimmy.")]
        public Sprite photo;
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
    [Tooltip("5 cases, dans l'ordre : photo du véhicule pour l'emplacement 1 à 5 (masquée automatiquement si l'emplacement est vide).")]
    public Image[] slotPhotos;

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

    // Met à jour les 5 cases : nom du véhicule si occupé, "-- Vide --" sinon, la photo
    // correspondante (masquée si vide), et grise le bouton Récupérer correspondant.
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

            if (slotPhotos != null && i < slotPhotos.Length && slotPhotos[i] != null)
            {
                Sprite photo = hasVehicle ? GetPhotoForModel(storedVehicles[i].modelName) : null;
                slotPhotos[i].sprite = photo;
                slotPhotos[i].enabled = photo != null;
            }
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

        // Ici on sait que le rangement va se faire : le vrai travail (sortie de la voiture,
        // repositionnement) part dans une coroutine cachée derrière un fondu au noir —
        // exactement le principe de ValetJobManager.ReturnToStandRoutine(), qui ne pose
        // jamais ce genre de souci contrairement à l'ancienne version "à l'écran ouvert"
        // de cette fonction. Le fondu masque la manœuvre ; le Rigidbody est mis en
        // kinematic pendant la téléportation donc totalement insensible aux collisions
        // pendant qu'on le pose, puis relâché une fois la position stabilisée.
        StartCoroutine(StoreVehicleRoutine(car, interaction, safeStandPoint));
        return true;
    }

    private IEnumerator StoreVehicleRoutine(CarController car, CarInteraction interaction, Transform safeStandPoint)
    {
        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(0.5f));
        }

        string modelName = car != null ? car.carModelName : "Véhicule";
        bool wasDrivenByPlayer = car != null && car.isDrivenByPlayer;
        GameObject player = null;

        if (wasDrivenByPlayer)
        {
            CarInteraction safeInteraction = interaction != null ? interaction : car.GetComponentInChildren<CarInteraction>();

            if (car != null)
            {
                foreach (Collider col in car.GetComponentsInChildren<Collider>())
                {
                    if (col != null) col.enabled = false;
                }
            }

            if (safeInteraction != null)
            {
                safeInteraction.ExitCar(); // Sort normalement (écran déjà noir, peu importe où)
            }

            player = GameObject.FindGameObjectWithTag("Player");
        }

        yield return new WaitForFixedUpdate();
        if (car != null) Destroy(car.gameObject);

        if (player != null)
        {
            Vector3 targetPos = safeStandPoint != null ? safeStandPoint.position : player.transform.position;

            if (Physics.Raycast(targetPos + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 20f))
            {
                targetPos = groundHit.point + Vector3.up * 0.1f;
            }
            targetPos += Vector3.up * 2f; // Marge de sécurité (même valeur que le Valet)

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Immunisé contre toute collision/dépénétration le temps de se poser
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = targetPos;
            }
            player.transform.position = targetPos;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.WakeUp();
            }
        }

        storedVehicles.Add(new StoredVehicle(modelName));
        RefreshGarageUI();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"<color=cyan>{modelName} rangée au garage ({storedVehicles.Count}/{maxGarageSlots}).</color>");

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(0.5f));
        }
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

    private Sprite GetPhotoForModel(string modelName)
    {
        if (knownCarPrefabs == null) return null;

        foreach (GarageCarEntry entry in knownCarPrefabs)
        {
            if (entry != null && entry.modelName == modelName) return entry.photo;
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

    // ==============================================================
    // SERVICE DE LIVRAISON (JIMMY) 🚚📞
    // ==============================================================

    [Header("Service de Livraison (Jimmy) 🚚📞")]
    [Tooltip("Doit correspondre exactement au nom du contact 'Jimmy' dans CallApp.contactList.")]
    public string jimmyContactName = "Jimmy";
    [Tooltip("Temps (en secondes) avant que le véhicule arrive après l'appel — le temps que Jimmy \"conduise\" jusqu'à toi.")]
    public float deliveryDelay = 45f;

    [Header("Sélection du véhicule à livrer 🖼️")]
    [Tooltip("Panneau affiché quand Jimmy demande lequel de tes véhicules livrer (seulement s'il y en a plus d'un).")]
    public GameObject deliverySelectionPanel;
    [Tooltip("5 cases, dans l'ordre : photo du véhicule pour le choix de livraison 1 à 5.")]
    public Image[] deliverySlotPhotos;
    [Tooltip("5 cases, dans l'ordre : texte (nom du véhicule) pour le choix de livraison 1 à 5.")]
    public TextMeshProUGUI[] deliverySlotLabels;
    [Tooltip("5 cases, dans l'ordre : bouton \"Choisir\" pour livrer précisément ce véhicule (relié à ChooseVehicleForDelivery).")]
    public Button[] deliverySlotButtons;

    private bool isDeliveryInProgress = false;

    // Appelée depuis CallApp.MakeCall() quand le joueur appelle Jimmy. Retourne la réplique
    // à afficher dans le dialogue de l'appel (garage pas débloqué, rien à livrer, déjà en
    // route...), OU null si un choix de véhicule est nécessaire — dans ce cas le panneau de
    // sélection s'ouvre directement et CallApp doit sauter l'affichage du répondeur.
    public string RequestVehicleDelivery()
    {
        if (!IsUnlocked())
            return "T'as même pas de garage dans ta planque, comment tu veux que je te livre quoi que ce soit ?";

        if (isDeliveryInProgress)
            return "Doucement, je suis déjà en route avec ta caisse !";

        if (storedVehicles.Count == 0)
            return "T'as rien dans ton garage à te faire livrer, mec.";

        if (storedVehicles.Count == 1)
        {
            // Un seul véhicule dispo : pas besoin de choisir, on part direct dessus.
            return StartDeliveryFor(0);
        }

        OpenDeliverySelection();
        return null;
    }

    public void OpenDeliverySelection()
    {
        if (deliverySelectionPanel != null) deliverySelectionPanel.SetActive(true);
        RefreshDeliverySelectionUI();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void CloseDeliverySelection()
    {
        if (deliverySelectionPanel != null) deliverySelectionPanel.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void RefreshDeliverySelectionUI()
    {
        if (deliverySlotLabels == null) return;

        for (int i = 0; i < deliverySlotLabels.Length; i++)
        {
            bool hasVehicle = i < storedVehicles.Count;

            if (deliverySlotLabels[i] != null)
                deliverySlotLabels[i].text = hasVehicle ? storedVehicles[i].modelName : "-- Vide --";

            if (deliverySlotButtons != null && i < deliverySlotButtons.Length && deliverySlotButtons[i] != null)
                deliverySlotButtons[i].interactable = hasVehicle;

            if (deliverySlotPhotos != null && i < deliverySlotPhotos.Length && deliverySlotPhotos[i] != null)
            {
                Sprite photo = hasVehicle ? GetPhotoForModel(storedVehicles[i].modelName) : null;
                deliverySlotPhotos[i].sprite = photo;
                deliverySlotPhotos[i].enabled = photo != null;
            }
        }
    }

    // À relier à chaque bouton "Choisir" du panneau de sélection (index 0 à 4).
    public void ChooseVehicleForDelivery(int slotIndex)
    {
        CloseDeliverySelection();
        string result = StartDeliveryFor(slotIndex);

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=cyan>Jimmy : {result}</color>");
    }

    // Fait le vrai travail commun aux deux chemins (auto si un seul véhicule, ou choisi
    // manuellement) : retrouve la route la plus proche et lance la coroutine de livraison
    // pour CE véhicule précis (retiré de la liste tout de suite, pas à l'arrivée, pour éviter
    // qu'il soit choisi une deuxième fois pendant le trajet de Jimmy).
    private string StartDeliveryFor(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= storedVehicles.Count) return "Erreur de sélection.";

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return "Je te trouve pas sur la carte, réessaie plus tard.";

        TrafficNode[] allNodes = FindObjectsOfType<TrafficNode>();
        if (allNodes == null || allNodes.Length == 0)
            return "Y'a pas de route où te livrer par ici...";

        TrafficNode closest = null;
        float minDist = Mathf.Infinity;
        foreach (TrafficNode node in allNodes)
        {
            if (node == null) continue;
            float dist = Vector3.Distance(player.transform.position, node.transform.position);
            if (dist < minDist) { minDist = dist; closest = node; }
        }

        if (closest == null) return "Je trouve pas de route pour te rejoindre.";

        StoredVehicle chosen = storedVehicles[slotIndex];
        storedVehicles.RemoveAt(slotIndex);
        RefreshGarageUI();

        isDeliveryInProgress = true;
        StartCoroutine(DeliverVehicleRoutine(closest.transform.position, chosen));

        return $"J'arrive avec ta {chosen.modelName}, donne-moi {Mathf.RoundToInt(deliveryDelay)} secondes !";
    }

    private IEnumerator DeliverVehicleRoutine(Vector3 roadPosition, StoredVehicle stored)
    {
        yield return new WaitForSeconds(deliveryDelay);

        GameObject prefab = FindPrefabForModel(stored.modelName);

        if (prefab == null)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Erreur : véhicule introuvable pour la livraison !</color>");
            isDeliveryInProgress = false;
            yield break;
        }

        // Recalage au sol par raycast (le joueur a pu bouger depuis l'appel, on livre au
        // point de route, pas à lui — donc pas besoin de re-suivre sa position ici).
        Vector3 spawnPos = roadPosition;
        if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
        {
            spawnPos = hit.point + Vector3.up * 0.5f;
        }

        // Si un véhicule occupe déjà pile ce point de route, on décale légèrement au lieu
        // de faire apparaître les deux l'un dans l'autre.
        Collider[] blockers = Physics.OverlapSphere(spawnPos, spawnCheckRadius, vehicleLayerMask);
        if (blockers.Length > 0)
        {
            spawnPos += Vector3.forward * 5f;
        }

        GameObject spawned = Instantiate(prefab, spawnPos, Quaternion.identity);
        CarController spawnedCar = spawned.GetComponent<CarController>();
        if (spawnedCar != null) spawnedCar.isPlayerOwned = true;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"<color=green>Jimmy a livré ta {stored.modelName} !</color>");

        // Flèches de guidage jusqu'au véhicule livré, comme pour un job Valet — la cible est
        // directement le véhicule apparu, pas besoin d'un point séparé. Le fil est coupé
        // automatiquement dès que le joueur monte dedans (ou si jamais il disparaît).
        if (JobPathfinder.Instance != null && spawnedCar != null)
        {
            JobPathfinder.Instance.SetTargets(spawned.transform);
            StartCoroutine(MonitorDeliveredVehicleRoutine(spawnedCar));
        }

        isDeliveryInProgress = false;
    }

    private IEnumerator MonitorDeliveredVehicleRoutine(CarController deliveredCar)
    {
        while (deliveredCar != null && !deliveredCar.isDrivenByPlayer)
        {
            yield return new WaitForSeconds(0.5f);
        }

        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();
    }
}