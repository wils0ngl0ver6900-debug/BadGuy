using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    [Header("Paramètres du Temps ⏳")]
    public float timeScale = 60f; // Vitesse d'écoulement (Ex: 60 = 1 sec en vrai fait 1 minute en jeu)
    public float startHour = 8f;  // Le jeu commence à 8h00 du matin

    [Header("Lumière & Ciel ☀️")]
    public Light sunLight; // Ta Directional Light
    public float nightIntensity = 0.1f; // Luminosité la nuit
    public float dayIntensity = 1f;     // Luminosité le jour

    [HideInInspector] public float currentTimeOfDay; // Le temps actuel en minutes

    private int currentDay = 1;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // On convertit l'heure de départ en minutes
        currentTimeOfDay = startHour * 60f;
    }

    private void Update()
    {
        // 1. Le temps avance selon la vitesse choisie
        currentTimeOfDay += Time.deltaTime * timeScale;

        // 2. Un jour complet = 1440 minutes (24h * 60m)
        if (currentTimeOfDay >= 1440f)
        {
            currentTimeOfDay -= 1440f; // On remet à zéro pour le lendemain
            currentDay++;

            // --- BONUS ÉCONOMIE ---
            // À minuit, l'offre et la demande du marché noir se réinitialisent !
            if (ShopManager.Instance != null) ShopManager.Instance.RecoverMarket();
        }

        UpdateSun();
    }

    private void UpdateSun()
    {
        if (sunLight == null) return;

        // 1. On calcule le cycle (0 à 1)
        float timePercent = currentTimeOfDay / 1440f;

        // 2. On transforme le cycle en angle entre 0 et 180 degrés.
        // Si timePercent est 0 (minuit), angle = 0. Si c'est 0.5 (midi), angle = 90. Si c'est 1 (minuit), angle = 180.
        float sunAngle = timePercent * 180f;

        // 3. Application de la rotation
        sunLight.transform.localRotation = Quaternion.Euler(sunAngle, 0f, 0f);

        // 4. Intensité : On active la lumière SEULEMENT quand le soleil est au-dessus de l'horizon (entre 10° et 170° pour éviter les éclipses rasantes)
        if (sunAngle > 10f && sunAngle < 170f)
        {
            sunLight.intensity = dayIntensity;
        }
        else
        {
            sunLight.intensity = nightIntensity;
        }
    }

    // --- LA FONCTION POUR LE TÉLÉPHONE ---
    public string GetFormattedTime()
    {
        int hours = Mathf.FloorToInt(currentTimeOfDay / 60f);
        int minutes = Mathf.FloorToInt(currentTimeOfDay % 60f);

        // Formate le texte avec deux chiffres obligatoires (ex: 08:05 au lieu de 8:5)
        return string.Format("{0:00}:{1:00}", hours, minutes);
    }
}