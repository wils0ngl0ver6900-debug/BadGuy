using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Panneau de tuning : change la couleur et améliore moteur/freins/adhérence/blindage du
// véhicule actuellement garé dans la zone (voir TuningShopZone). Les améliorations vivent
// sur le composant CarUpgrades de la voiture elle-même, et persistent à travers le garage
// grâce à GarageManager.StoredVehicle.upgrades.
public class TuningShopManager : MonoBehaviour
{
    public static TuningShopManager Instance;

    [Header("UI du Garage de Tuning")]
    public GameObject shopUIPanel;
    public TextMeshProUGUI vehicleNameText;

    [Header("Peinture")]
    [Tooltip("Couleurs proposées. Relie chaque bouton correspondant à SelectColor(int) avec le même index.")]
    public Color[] availableColors;

    [Header("Prix des Améliorations (par palier, argent propre)")]
    public int[] engineCosts = { 5000, 12000, 25000 };
    public int[] brakeCosts = { 3000, 8000, 15000 };
    public int[] gripCosts = { 3000, 8000, 15000 };
    public int[] armorCosts = { 4000, 10000, 20000 };

    [Header("Textes d'état (optionnels)")]
    public TextMeshProUGUI engineLevelText;
    public TextMeshProUGUI brakeLevelText;
    public TextMeshProUGUI gripLevelText;
    public TextMeshProUGUI armorLevelText;

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

    public void OpenShopFor(CarController car)
    {
        if (car == null) return;

        currentUpgrades = car.GetComponent<CarUpgrades>();
        if (currentUpgrades == null)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("<color=red>Ce véhicule n'est pas modifiable (CarUpgrades manquant sur le prefab).</color>");
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

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(true);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void Update()
    {
        if (shopUIPanel != null && shopUIPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseShop();
        }
    }

    private void RefreshUI()
    {
        if (currentUpgrades == null) return;
        CarUpgrades.UpgradeData data = currentUpgrades.GetData();

        if (engineLevelText != null) engineLevelText.text = $"Moteur : Niveau {data.engineLevel}/{engineCosts.Length}";
        if (brakeLevelText != null) brakeLevelText.text = $"Freins : Niveau {data.brakeLevel}/{brakeCosts.Length}";
        if (gripLevelText != null) gripLevelText.text = $"Adhérence : Niveau {data.gripLevel}/{gripCosts.Length}";
        if (armorLevelText != null) armorLevelText.text = $"Blindage : Niveau {data.armorLevel}/{armorCosts.Length}";
    }

    // À relier à un bouton par couleur (index dans availableColors). Contrairement aux 4
    // améliorations mécaniques, la peinture reste accessible même sur un véhicule volé —
    // et dans ce cas précis, ça fait perdre une étoile de recherche (nouvelle silhouette,
    // plus dur à identifier), via la même méthode déjà utilisée pour changer de tenue.
    public void SelectColor(int colorIndex)
    {
        if (currentUpgrades == null) return;
        if (colorIndex < 0 || colorIndex >= availableColors.Length) return;

        currentUpgrades.SetColor(availableColors[colorIndex]);

        if (currentCar != null && !currentCar.isPlayerOwned && GameManager.Instance != null && GameManager.Instance.wantedLevel > 0)
        {
            GameManager.Instance.DropOneStarFromDisguise();
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=green>Nouvelle peinture appliquée !</color>");
        }
    }

    // --- Les 4 boutons d'amélioration, un par catégorie. Réservés aux véhicules achetés
    // (contrairement à la peinture ci-dessus) — vérifié individuellement sur chacun plutôt
    // qu'à l'ouverture du shop, pour que la peinture reste possible sur une caisse volée
    // sans permettre les vraies améliorations mécaniques dessus. Volontairement écrit à
    // plat (pas de factorisation générique) pour rester dans le même style que le reste du
    // projet : simple à lire et à modifier catégorie par catégorie sans y toucher ensemble. ---

    public void BuyEngineUpgrade()
    {
        if (currentUpgrades == null || GameManager.Instance == null) return;

        if (currentCar != null && !currentCar.isPlayerOwned)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Cette voiture n'est pas à toi — seule la peinture est possible dessus.</color>");
            return;
        }

        int level = currentUpgrades.GetData().engineLevel;
        if (level >= engineCosts.Length)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Moteur déjà au niveau maximum.</color>");
            return;
        }

        int cost = engineCosts[level];
        if (GameManager.Instance.cleanMoney < cost)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>Pas assez d'argent propre ({cost}€ nécessaires).</color>");
            return;
        }

        GameManager.Instance.cleanMoney -= cost;
        if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-cost, "Amélioration Moteur");

        currentUpgrades.UpgradeEngine();
        RefreshUI();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("<color=green>Moteur amélioré !</color>");
            UIManager.Instance.UpdateHUD();
        }
    }

    public void BuyBrakeUpgrade()
    {
        if (currentUpgrades == null || GameManager.Instance == null) return;

        if (currentCar != null && !currentCar.isPlayerOwned)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Cette voiture n'est pas à toi — seule la peinture est possible dessus.</color>");
            return;
        }

        int level = currentUpgrades.GetData().brakeLevel;
        if (level >= brakeCosts.Length)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Freins déjà au niveau maximum.</color>");
            return;
        }

        int cost = brakeCosts[level];
        if (GameManager.Instance.cleanMoney < cost)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>Pas assez d'argent propre ({cost}€ nécessaires).</color>");
            return;
        }

        GameManager.Instance.cleanMoney -= cost;
        if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-cost, "Amélioration Freins");

        currentUpgrades.UpgradeBrakes();
        RefreshUI();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("<color=green>Freins améliorés !</color>");
            UIManager.Instance.UpdateHUD();
        }
    }

    public void BuyGripUpgrade()
    {
        if (currentUpgrades == null || GameManager.Instance == null) return;

        if (currentCar != null && !currentCar.isPlayerOwned)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Cette voiture n'est pas à toi — seule la peinture est possible dessus.</color>");
            return;
        }

        int level = currentUpgrades.GetData().gripLevel;
        if (level >= gripCosts.Length)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Adhérence déjà au niveau maximum.</color>");
            return;
        }

        int cost = gripCosts[level];
        if (GameManager.Instance.cleanMoney < cost)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>Pas assez d'argent propre ({cost}€ nécessaires).</color>");
            return;
        }

        GameManager.Instance.cleanMoney -= cost;
        if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-cost, "Amélioration Adhérence");

        currentUpgrades.UpgradeGrip();
        RefreshUI();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("<color=green>Adhérence améliorée !</color>");
            UIManager.Instance.UpdateHUD();
        }
    }

    public void BuyArmorUpgrade()
    {
        if (currentUpgrades == null || GameManager.Instance == null) return;

        if (currentCar != null && !currentCar.isPlayerOwned)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Cette voiture n'est pas à toi — seule la peinture est possible dessus.</color>");
            return;
        }

        int level = currentUpgrades.GetData().armorLevel;
        if (level >= armorCosts.Length)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Blindage déjà au niveau maximum.</color>");
            return;
        }

        int cost = armorCosts[level];
        if (GameManager.Instance.cleanMoney < cost)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>Pas assez d'argent propre ({cost}€ nécessaires).</color>");
            return;
        }

        GameManager.Instance.cleanMoney -= cost;
        if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-cost, "Amélioration Blindage");

        currentUpgrades.UpgradeArmor();
        RefreshUI();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("<color=green>Blindage amélioré !</color>");
            UIManager.Instance.UpdateHUD();
        }
    }
}