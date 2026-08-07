using UnityEngine;

// Pose ce script sur CHAQUE prefab de voiture (à côté de CarController). Garde en mémoire
// les améliorations appliquées (couleur, moteur, freins, adhérence, blindage) et les
// traduit en vrais changements sur CarController — toujours recalculés depuis les valeurs
// d'origine du prefab (mémorisées une fois au lancement), jamais empilés les uns sur les
// autres, pour que le résultat reste prévisible peu importe l'ordre des achats.
[RequireComponent(typeof(CarController))]
public class CarUpgrades : MonoBehaviour
{
    [System.Serializable]
    public class UpgradeData
    {
        public int engineLevel = 0;   // 0 = stock
        public int brakeLevel = 0;
        public int gripLevel = 0;
        public int armorLevel = 0;
        public bool hasCustomColor = false;
        public Color customColor = Color.white;
    }

    [Header("Peinture")]
    [Tooltip("Le(s) Renderer(s) à repeindre. Si vide, cherche automatiquement sur les enfants au lancement.")]
    public Renderer[] paintRenderers;

    [Header("Paliers d'amélioration (multiplicateurs, index 0 = niveau 1)")]
    public float[] engineMultipliers = { 1.15f, 1.30f, 1.50f };
    public float[] brakeMultipliers = { 1.2f, 1.4f, 1.7f };
    public float[] gripMultipliers = { 1.2f, 1.5f, 1.9f };
    public float[] armorMultipliers = { 1.25f, 1.5f, 2f };

    private CarController car;
    private UpgradeData current = new UpgradeData();

    // Valeurs "stock" mémorisées une fois au lancement, pour toujours recalculer depuis la
    // base plutôt que d'empiler les multiplicateurs à chaque changement.
    private float baseMaxSpeed, baseAcceleration, baseBraking, baseDriftGrip, baseMaxHealth;
    private bool baseValuesCaptured = false;

    private void Awake()
    {
        car = GetComponent<CarController>();
        if (paintRenderers == null || paintRenderers.Length == 0)
            paintRenderers = GetComponentsInChildren<Renderer>();
    }

    private void Start()
    {
        CaptureBaseValuesIfNeeded();
        ApplyAll();
    }

    private void CaptureBaseValuesIfNeeded()
    {
        if (baseValuesCaptured || car == null) return;

        baseMaxSpeed = car.maxSpeed;
        baseAcceleration = car.accelerationForce;
        baseBraking = car.brakingForce;
        // L'adhérence de base (gripLevel) est déjà tout près de son maximum (0.95 typique) :
        // améliorer cette valeur-là plafonnerait quasi instantanément. driftGrip (0.3 de
        // base) a bien plus de marge et représente mieux "meilleure tenue de route en
        // dérapage/freinage à main", donc c'est celui-ci que le palier "adhérence" augmente.
        baseDriftGrip = car.driftGrip;
        baseMaxHealth = car.maxHealth;
        baseValuesCaptured = true;
    }

    public UpgradeData GetData() => current;

    // Réapplique un jeu de données sauvegardé (venant du garage, voir GarageManager) sur
    // CETTE instance de voiture — utilisé après un Instantiate() pour restaurer un véhicule
    // amélioré plutôt que de repartir sur des stats neuves.
    public void SetData(UpgradeData data)
    {
        CaptureBaseValuesIfNeeded();
        current = data ?? new UpgradeData();
        ApplyAll();
    }

    public void UpgradeEngine()
    {
        if (current.engineLevel >= engineMultipliers.Length) return;
        current.engineLevel++;
        ApplyAll();
    }

    public void UpgradeBrakes()
    {
        if (current.brakeLevel >= brakeMultipliers.Length) return;
        current.brakeLevel++;
        ApplyAll();
    }

    public void UpgradeGrip()
    {
        if (current.gripLevel >= gripMultipliers.Length) return;
        current.gripLevel++;
        ApplyAll();
    }

    public void UpgradeArmor()
    {
        if (current.armorLevel >= armorMultipliers.Length) return;
        current.armorLevel++;
        ApplyAll();
    }

    public void SetColor(Color color)
    {
        current.hasCustomColor = true;
        current.customColor = color;
        ApplyAll();
    }

    private void ApplyAll()
    {
        if (car == null) return;
        CaptureBaseValuesIfNeeded();

        float engineMult = current.engineLevel > 0 ? engineMultipliers[current.engineLevel - 1] : 1f;
        float brakeMult = current.brakeLevel > 0 ? brakeMultipliers[current.brakeLevel - 1] : 1f;
        float gripMult = current.gripLevel > 0 ? gripMultipliers[current.gripLevel - 1] : 1f;
        float armorMult = current.armorLevel > 0 ? armorMultipliers[current.armorLevel - 1] : 1f;

        car.maxSpeed = baseMaxSpeed * engineMult;
        car.accelerationForce = baseAcceleration * engineMult;
        car.brakingForce = baseBraking * brakeMult;
        car.driftGrip = Mathf.Clamp01(baseDriftGrip * gripMult);

        float newMaxHealth = baseMaxHealth * armorMult;
        // On ne perd jamais de vie actuelle en améliorant le blindage (on ne fait
        // qu'agrandir le plafond), et on ne dépasse jamais ce nouveau plafond non plus.
        bool wasAtFullHealth = car.currentHealth >= car.maxHealth;
        car.maxHealth = newMaxHealth;
        car.currentHealth = wasAtFullHealth ? newMaxHealth : Mathf.Min(car.currentHealth, newMaxHealth);

        if (current.hasCustomColor && paintRenderers != null)
        {
            foreach (Renderer r in paintRenderers)
            {
                if (r != null) r.material.color = current.customColor;
            }
        }
    }
}