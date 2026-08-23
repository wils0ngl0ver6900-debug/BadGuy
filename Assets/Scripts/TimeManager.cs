using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    [Header("Paramètres du Temps ⏳")]
    public float timeScale = 60f;
    public float startHour = 8f;
    [Tooltip("Si vrai, le temps (et donc le cycle jour/nuit) ne progresse plus — utilisé pour bloquer l'heure pendant une course.")]
    public bool isPaused = false;

    [Header("Soleil (Jour) ☀️")]
    public Light sunLight;
    public float dayIntensity = 130000f;

    [Header("Lune (Nuit) 🌙")]
    public Light moonLight;
    public float moonMaxIntensity = 2000f;

    [HideInInspector] public float currentTimeOfDay;
    private int currentDay = 1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        currentTimeOfDay = startHour * 60f;
    }

    private void Update()
    {
        if (isPaused) return;

        currentTimeOfDay += Time.deltaTime * timeScale;

        if (currentTimeOfDay >= 1440f)
        {
            currentTimeOfDay -= 1440f;
            currentDay++;

            if (ShopManager.Instance != null) ShopManager.Instance.RecoverMarket();

            // ---> LA NOUVELLE LIGNE EST ICI <---
            // Met à jour la bourse tous les jours à minuit !
            if (StockMarketManager.Instance != null) StockMarketManager.Instance.UpdateMarketDaily();
            // ---> AJOUTE CETTE LIGNE POUR LA CRYPTO <---
            if (CryptoMarketManager.Instance != null)
                CryptoMarketManager.Instance.UpdateMarketDaily();
        }

        UpdateSunAndMoon();
    }

    private void UpdateSunAndMoon()
    {
        if (sunLight == null) return;

        float hour = currentTimeOfDay / 60f;

        // ==========================================
        // 1. CALCUL DES ANGLES (Orbites très étirées)
        // ==========================================

        // ORBITE DU SOLEIL (Visible de 5h à 22h - 17 heures de course)
        float sunAngle = 0f;
        if (hour >= 5f && hour <= 22f)
        {
            float sunProgress = (hour - 5f) / 17f;
            sunAngle = Mathf.Lerp(0f, 180f, sunProgress);
        }
        else
        {
            float sunNightProgress = (hour > 22f) ? (hour - 22f) / 7f : (hour + 2f) / 7f;
            sunAngle = Mathf.Lerp(180f, 360f, sunNightProgress);
        }

        // ORBITE DE LA LUNE (Visible de 18h à 9h - 15 heures de course)
        float moonAngle = 0f;
        bool isMoonTime = (hour >= 18f || hour <= 9f);
        if (isMoonTime)
        {
            float moonProgress = (hour >= 18f) ? (hour - 18f) / 15f : (hour + 6f) / 15f;
            moonAngle = Mathf.Lerp(10f, 170f, moonProgress);
        }
        else
        {
            float moonDayProgress = (hour - 9f) / 9f;
            moonAngle = Mathf.Lerp(180f, 360f, moonDayProgress);
        }

        sunLight.transform.localRotation = Quaternion.Euler(sunAngle, 0f, 0f);
        if (moonLight != null)
        {
            moonLight.transform.localRotation = Quaternion.Euler(moonAngle, 0f, 0f);
        }

        // ==========================================
        // 2. FONDUS LISSÉS "SMOOTHSTEP" (Sur 4 Heures)
        // ==========================================
        float currentSunInt = 0f;
        float currentMoonInt = 0f;

        // A. Pleine Nuit (22h00 à 05h00)
        if (hour >= 22f || hour <= 5f)
        {
            currentSunInt = 0f;
            currentMoonInt = moonMaxIntensity;
        }
        // B. Aube (05h00 à 09h00 - 4h de transition douce)
        else if (hour > 5f && hour < 9f)
        {
            float fade = (hour - 5f) / 4f; // Évolue de 0 à 1
            currentSunInt = Mathf.SmoothStep(0f, dayIntensity, fade);
            currentMoonInt = Mathf.SmoothStep(moonMaxIntensity, 0f, fade);
        }
        // C. Plein Jour (09h00 à 18h00)
        else if (hour >= 9f && hour <= 18f)
        {
            currentSunInt = dayIntensity;
            currentMoonInt = 0f;
        }
        // D. Crépuscule (18h00 à 22h00 - 4h de transition douce)
        else if (hour > 18f && hour < 22f)
        {
            float fade = (hour - 18f) / 4f; // Évolue de 0 à 1
            currentSunInt = Mathf.SmoothStep(dayIntensity, 0f, fade);
            currentMoonInt = Mathf.SmoothStep(0f, moonMaxIntensity, fade);
        }

        // Application des lumières
        sunLight.intensity = currentSunInt;
        if (moonLight != null) moonLight.intensity = currentMoonInt;
    }

    public string GetFormattedTime()
    {
        int hours = Mathf.FloorToInt(currentTimeOfDay / 60f);
        int minutes = Mathf.FloorToInt(currentTimeOfDay % 60f);
        return string.Format("{0:00}:{1:00}", hours, minutes);
    }
}