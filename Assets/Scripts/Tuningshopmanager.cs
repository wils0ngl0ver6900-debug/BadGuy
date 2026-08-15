using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Panneau de tuning. Deux régimes :
// - Voiture ACHETÉE : menu complet (peinture au choix, réparation, 4 améliorations méca).
// - Voiture VOLÉE   : pas de menu — TuningShopZone appelle AutoServiceStolenCar() qui
//   repeint et répare automatiquement, sans intervention du joueur.
// Tous les prix sont configurables dans l'Inspector, section "Prix (argent propre)".
public class TuningShopManager : MonoBehaviour
{
    public static TuningShopManager Instance;

    [Header("UI du Garage de Tuning")]
    public GameObject shopUIPanel;
    public TextMeshProUGUI vehicleNameText;

    [Header("Peinture 🎨")]
    [Tooltip("Couleurs proposées pour les véhicules achetés. Relie chaque bouton à SelectColor(int) avec le même index.")]
    public Color[] availableColors;

    [Header("Prix (argent propre 💰)")]
    [Space(4)]
    [Tooltip("Coût du service automatique (repeinture + réparation) sur une voiture VOLÉE.")]
    public int stolenCarServiceCost = 500;

    [Space(4)]
    [Tooltip("Coût d'une repeinture sur une voiture ACHETÉE (couleur au choix).")]
    public int paintCostOwned = 1000;

    [Tooltip("Coût d'une réparation complète sur une voiture ACHETÉE.")]
    public int repairCostOwned = 2000;

    [Space(4)]
    [Tooltip("Coût de l'amélioration Moteur, par palier (index 0 = palier 1, index 1 = palier 2...).")]
    public int[] engineCosts = { 5000, 12000, 25000 };
    [Tooltip("Coût de l'amélioration Freins, par palier.")]
    public int[] brakeCosts = { 3000, 8000, 15000 };
    [Tooltip("Coût de l'amélioration Adhérence, par palier.")]
    public int[] gripCosts = { 3000, 8000, 15000 };
    [Tooltip("Coût de l'amélioration Blindage, par palier.")]
    public int[] armorCosts = { 4000, 10000, 20000 };

    [Header("Textes d'état (optionnels, mis à jour automatiquement)")]
    public TextMeshProUGUI engineLevelText;
    public TextMeshProUGUI brakeLevelText;
    public TextMeshProUGUI gripLevelText;
    public TextMeshProUGUI armorLevelText;
    public TextMeshProUGUI carHealthText;

    private CarController currentCar;
    private CarUpgrades currentUpgrades;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (shopUIPanel != null) shopUIPanel.SetActive(false);
    }

    // ============================================================
    // VOITURE ACHETÉE : ouvre le menu
    // ============================================================

    public void OpenShopFor(CarController car)
    {
        if (car == null) return;

        currentUpgrades = car.GetComponent<CarUpgrades>();
        if (currentUpgrades == null)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("<color=red>CarUpgrades manquant sur ce prefab. Voir le guide d'intégration.</color>");
            return;
        }

        currentCar = car;

        if (shopUIPanel != null) shopUIPanel.SetActive(true);
        if (vehicleNameText != null) vehicleNameText.text = car.carModelName;

        RefreshUI();

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(false);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void CloseShop()
    {
        if (shopUIPanel != null) shopUIPanel.SetActive(false);
        currentCar = null;
        currentUpgrades = null;

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(true);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void Update()
    {
        if (shopUIPanel != null && shopUIPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseShop();
    }

    private void RefreshUI()
    {
        if (currentUpgrades == null) return;
        CarUpgrades.UpgradeData data = currentUpgrades.GetData();

        if (engineLevelText != null) engineLevelText.text = $"Moteur Niv.{data.engineLevel}/{engineCosts.Length} — {NextCostString(engineCosts, data.engineLevel)}";
        if (brakeLevelText != null) brakeLevelText.text = $"Freins Niv.{data.brakeLevel}/{brakeCosts.Length} — {NextCostString(brakeCosts, data.brakeLevel)}";
        if (gripLevelText != null) gripLevelText.text = $"Adhérence Niv.{data.gripLevel}/{gripCosts.Length} — {NextCostString(gripCosts, data.gripLevel)}";
        if (armorLevelText != null) armorLevelText.text = $"Blindage Niv.{data.armorLevel}/{armorCosts.Length} — {NextCostString(armorCosts, data.armorLevel)}";

        if (carHealthText != null && currentCar != null)
            carHealthText.text = $"État : {Mathf.RoundToInt(currentCar.currentHealth)}/{Mathf.RoundToInt(currentCar.maxHealth)} PV";
    }

    private string NextCostString(int[] costs, int level)
    {
        if (level >= costs.Length) return "MAX";
        return $"Prochain : {costs[level]}€";
    }

    // ============================================================
    // PEINTURE (voiture achetée)
    // ============================================================

    public void SelectColor(int colorIndex)
    {
        if (currentUpgrades == null || currentCar == null) return;
        if (colorIndex < 0 || colorIndex >= availableColors.Length) return;

        if (!TryDebit(paintCostOwned, "Peinture")) return;

        currentUpgrades.SetColor(availableColors[colorIndex]);
        RefreshUI();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=green>Nouvelle peinture appliquée !</color>");
    }

    // ============================================================
    // RÉPARATION (voiture achetée)
    // ============================================================

    public void RepairCar()
    {
        if (currentCar == null) return;

        if (currentCar.isEngineDead)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Moteur détruit — cette voiture ne peut plus être réparée ici.</color>");
            return;
        }

        if (Mathf.Approximately(currentCar.currentHealth, currentCar.maxHealth))
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>La voiture est déjà en parfait état !</color>");
            return;
        }

        if (!TryDebit(repairCostOwned, "Réparation véhicule")) return;

        currentCar.currentHealth = currentCar.maxHealth;
        currentCar.GetComponent<CarDeformation>()?.ResetDeformation();
        RefreshUI();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=green>Voiture réparée !</color>");
    }

    // ============================================================
    // AMÉLIORATIONS MÉCANIQUES (voiture achetée uniquement)
    // ============================================================

    public void BuyEngineUpgrade()
    {
        if (!CheckOwned()) return;
        int level = currentUpgrades.GetData().engineLevel;
        if (level >= engineCosts.Length) { Notify("Moteur déjà au maximum.", "yellow"); return; }
        if (!TryDebit(engineCosts[level], "Amélioration Moteur")) return;
        currentUpgrades.UpgradeEngine();
        RefreshUI();
        Notify("Moteur amélioré !", "green");
    }

    public void BuyBrakeUpgrade()
    {
        if (!CheckOwned()) return;
        int level = currentUpgrades.GetData().brakeLevel;
        if (level >= brakeCosts.Length) { Notify("Freins déjà au maximum.", "yellow"); return; }
        if (!TryDebit(brakeCosts[level], "Amélioration Freins")) return;
        currentUpgrades.UpgradeBrakes();
        RefreshUI();
        Notify("Freins améliorés !", "green");
    }

    public void BuyGripUpgrade()
    {
        if (!CheckOwned()) return;
        int level = currentUpgrades.GetData().gripLevel;
        if (level >= gripCosts.Length) { Notify("Adhérence déjà au maximum.", "yellow"); return; }
        if (!TryDebit(gripCosts[level], "Amélioration Adhérence")) return;
        currentUpgrades.UpgradeGrip();
        RefreshUI();
        Notify("Adhérence améliorée !", "green");
    }

    public void BuyArmorUpgrade()
    {
        if (!CheckOwned()) return;
        int level = currentUpgrades.GetData().armorLevel;
        if (level >= armorCosts.Length) { Notify("Blindage déjà au maximum.", "yellow"); return; }
        if (!TryDebit(armorCosts[level], "Amélioration Blindage")) return;
        currentUpgrades.UpgradeArmor();
        RefreshUI();
        Notify("Blindage amélioré !", "green");
    }

    // ============================================================
    // VOITURE VOLÉE : service automatique (repeinture + réparation)
    // ============================================================

    // Appelée par TuningShopZone quand une voiture VOLÉE entre dans la zone.
    // Pas de menu, pas de choix — tout se fait immédiatement, une seule fois par visite.
    public void AutoServiceStolenCar(CarController car)
    {
        if (car == null) return;

        if (GameManager.Instance != null && GameManager.Instance.cleanMoney < stolenCarServiceCost)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification($"<color=red>Pas assez d'argent propre pour le service ({stolenCarServiceCost}€).</color>");
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.cleanMoney -= stolenCarServiceCost;
            if (BankApp.Instance != null)
                BankApp.Instance.RecordTransaction(-stolenCarServiceCost, "Service voiture volée");
            if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
        }

        // Repeinture aléatoire parmi les couleurs disponibles
        CarUpgrades upgrades = car.GetComponent<CarUpgrades>();
        if (upgrades != null && availableColors != null && availableColors.Length > 0)
        {
            Color randomColor = availableColors[Random.Range(0, availableColors.Length)];
            upgrades.SetColor(randomColor);
        }

        // Réparation complète (moteur détruit = on remet juste la vie, pas l'état mort)
        if (!car.isEngineDead)
        {
            car.currentHealth = car.maxHealth;
            car.GetComponent<CarDeformation>()?.ResetDeformation();
        }

        if (GameManager.Instance != null && GameManager.Instance.wantedLevel > 0)
        {
            GameManager.Instance.DropOneStarFromDisguise();
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=green>Voiture repeinte et réparée !</color>");
        }
    }

    // ============================================================
    // HELPERS PRIVÉS
    // ============================================================

    private bool CheckOwned()
    {
        if (currentUpgrades == null || currentCar == null) return false;
        if (!currentCar.isPlayerOwned)
        {
            Notify("Cette voiture n'est pas à toi.", "red");
            return false;
        }
        return true;
    }

    private bool TryDebit(int cost, string label)
    {
        if (GameManager.Instance == null) return false;
        if (GameManager.Instance.cleanMoney < cost)
        {
            Notify($"Pas assez d'argent propre ({cost}€ nécessaires).", "red");
            return false;
        }
        GameManager.Instance.cleanMoney -= cost;
        if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-cost, label);
        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
        return true;
    }

    private void Notify(string msg, string color = "white")
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color={color}>{msg}</color>");
    }
}