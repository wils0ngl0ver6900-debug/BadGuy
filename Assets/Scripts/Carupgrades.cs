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

    // Couleur d'origine de chaque renderer, capturée avant toute repeinture. La plupart des
    // shaders (Standard/HDRP-URP Lit) affichent texture × material.color — si la carrosserie
    // n'est pas blanche à la base (rouge, bleue...), poser directement "jaune" donnait un
    // résultat mélangé (orange, vert...) plutôt que le jaune pur voulu. On compense en
    // divisant la couleur demandée par cette teinte d'origine.
    private Color[] baseRendererColors;

    private void Awake()
    {
        car = GetComponent<CarController>();

        if (paintRenderers == null || paintRenderers.Length == 0)
        {
            // On exclut volontairement :
            // - TrailRenderers (traces de pneus/skidmarks)
            // - ParticleSystemRenderers (fumée de dérapage)
            // - Renderers sur objets dont le nom contient "glass", "vitre", "window" ou
            //   "windshield" (vitres) : l'écraser avec une couleur opaque rendrait la
            //   carrosserie aveugle, et les vitres ont généralement un shader transparent
            //   qui réagit très mal à un changement de couleur arbitraire.
            System.Collections.Generic.List<Renderer> valid = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is TrailRenderer) continue;
                if (r is ParticleSystemRenderer) continue;
                string n = r.gameObject.name.ToLower();
                if (n.Contains("glass") || n.Contains("vitre") || n.Contains("window") || n.Contains("windshield")) continue;
                valid.Add(r);
            }
            paintRenderers = valid.ToArray();
        }
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

        if (paintRenderers != null)
        {
            baseRendererColors = new Color[paintRenderers.Length];
            for (int i = 0; i < paintRenderers.Length; i++)
            {
                baseRendererColors[i] = paintRenderers[i] != null ? paintRenderers[i].material.color : Color.white;
            }
        }

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
            for (int i = 0; i < paintRenderers.Length; i++)
            {
                if (paintRenderers[i] == null) continue;

                Color baseColor = (baseRendererColors != null && i < baseRendererColors.Length)
                    ? baseRendererColors[i]
                    : Color.white;

                // Compense la teinte d'origine (division par canal) pour que la couleur
                // choisie ressorte fidèlement sur la carrosserie, peu importe la teinte de
                // base du matériau.
                float rFactor = baseColor.r > 0.02f ? current.customColor.r / baseColor.r : current.customColor.r;
                float gFactor = baseColor.g > 0.02f ? current.customColor.g / baseColor.g : current.customColor.g;
                float bFactor = baseColor.b > 0.02f ? current.customColor.b / baseColor.b : current.customColor.b;

                // Le plafond limite l'AMPLIFICATION (sans lui, diviser par une teinte de
                // base proche de zéro peut multiplier des pixels sombres de la texture —
                // vitres, pneus — par un facteur énorme et les éclaircir à tort). Important :
                // si un canal dépasse le plafond, on réduit les 3 ENSEMBLE, dans les mêmes
                // proportions, plutôt que de plafonner chaque canal indépendamment — sinon,
                // si par exemple G et B se retrouvent écrêtés à la même valeur alors que R
                // reste plus bas, ça détruit le ratio entre canaux et donc la teinte demandée
                // (un bleu ciel pouvait ressortir vert/cyan à cause de ça).
                const float maxCorrection = 2.5f;
                float maxFactor = Mathf.Max(rFactor, Mathf.Max(gFactor, bFactor));
                if (maxFactor > maxCorrection)
                {
                    float scale = maxCorrection / maxFactor;
                    rFactor *= scale;
                    gFactor *= scale;
                    bFactor *= scale;
                }

                paintRenderers[i].material.color = new Color(rFactor, gFactor, bFactor, 1f);
            }
        }
    }
}