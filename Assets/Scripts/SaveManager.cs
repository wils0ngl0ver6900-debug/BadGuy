using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;

    // Le Canvas noir qu'on va créer pour cacher le spawn par défaut (Route 1)
    private GameObject instantBlackScreen;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // =========================================================
    // --- ROUTE 1 : CHARGEMENT DEPUIS LE MENU PRINCIPAL ---
    // =========================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (PlayerPrefs.GetInt("LoadRequestFromMenu", 0) == 1)
        {
            PlayerPrefs.SetInt("LoadRequestFromMenu", 0);

            // On crée IMMÉDIATEMENT un écran noir total avant que la 1ère image soit rendue !
            CreateInstantBlackScreen();
            StartCoroutine(DelayedLoadFromMenu());
        }
    }

    private void CreateInstantBlackScreen()
    {
        instantBlackScreen = new GameObject("InstantBlackScreen_Canvas");
        Canvas canvas = instantBlackScreen.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // L'écran noir passe par-dessus tout le HUD (même le téléphone)

        instantBlackScreen.AddComponent<CanvasScaler>();
        instantBlackScreen.AddComponent<GraphicRaycaster>();

        GameObject bgObj = new GameObject("BlackBackground");
        bgObj.transform.SetParent(instantBlackScreen.transform, false);

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = Color.black;

        RectTransform rect = bgObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private IEnumerator DelayedLoadFromMenu()
    {
        // On laisse 0.1s aux scripts pour s'initialiser
        yield return new WaitForSecondsRealtime(0.1f);

        // On applique les données caché derrière l'écran noir
        ApplySaveData();
        RefreshUI();

        // On laisse le monde 3D se charger tranquillement
        yield return new WaitForSecondsRealtime(0.5f);

        // On fait un beau fondu pour révéler le jeu
        if (instantBlackScreen != null)
        {
            Image bgImage = instantBlackScreen.GetComponentInChildren<Image>();
            if (bgImage != null)
            {
                float duration = 1.5f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    // "unscaled" permet à l'animation de jouer même si le jeu est en pause
                    elapsed += Time.unscaledDeltaTime;
                    bgImage.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }
            }
            Destroy(instantBlackScreen);
        }

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Partie chargée avec succès !</color>", true);
    }

    // =========================================================
    // --- ROUTE 2 : CHARGEMENT IN-GAME (DEPUIS LE TÉLÉPHONE) ---
    // =========================================================

    public void LoadGameData()
    {
        StartCoroutine(LoadGameRoutine());
    }

    private IEnumerator LoadGameRoutine()
    {
        if (PlayerPrefs.GetInt("HasSavedGame", 0) == 0)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Aucune sauvegarde trouvée.</color>", true);
            yield break;
        }

        // On ferme l'application Paramètres discrètement en arrière-plan
        if (SettingsApp.Instance != null) SettingsApp.Instance.CloseApp();

        // 1. ON CRÉE UN NOUVEAU CANVAS NOIR (Priorité maximale 9999)
        CreateInstantBlackScreen();
        Image fadeImage = instantBlackScreen.GetComponentInChildren<Image>();
        fadeImage.color = new Color(0, 0, 0, 0); // Transparent au début

        // FONDU VERS LE NOIR
        float duration = 1.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        fadeImage.color = Color.black;

        // 2. ON APPLIQUE LES DONNÉES PENDANT QUE L'ÉCRAN EST NOIR
        ApplySaveData();
        RefreshUI();

        // On force le jeu à reprendre son cours normal (si le téléphone l'avait mis en pause)
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(0.5f);

        // 3. FONDU VERS LE CLAIR
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        Destroy(instantBlackScreen);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("<color=yellow>Partie chargée avec succès !</color>", true);
    }

    // =========================================================
    // --- LE MOTEUR DE DONNÉES (Pour ne pas écrire le code 2x) ---
    // =========================================================

    private void ApplySaveData()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.cleanMoney = PlayerPrefs.GetInt("Save_CleanMoney", 0);
            GameManager.Instance.dirtyMoney = PlayerPrefs.GetInt("Save_DirtyMoney", 0);
            GameManager.Instance.wantedLevel = PlayerPrefs.GetInt("Save_WantedLevel", 0);
            GameManager.Instance.crimePoints = PlayerPrefs.GetInt("Save_CrimePoints", 0);
            GameManager.Instance.SyncDirtyMoneyItem();
        }

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.currentTimeOfDay = PlayerPrefs.GetFloat("Save_TimeOfDay", 8f * 60f);
        }

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            float x = PlayerPrefs.GetFloat("Save_PlayerPosX", player.transform.position.x);
            float y = PlayerPrefs.GetFloat("Save_PlayerPosY", player.transform.position.y);
            float z = PlayerPrefs.GetFloat("Save_PlayerPosZ", player.transform.position.z);

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            player.transform.position = new Vector3(x, y, z);
            player.currentHealth = PlayerPrefs.GetInt("Save_PlayerHealth", (int)player.maxHealth);
            player.currentShield = PlayerPrefs.GetInt("Save_PlayerShield", (int)player.maxShield);
        }
    }

    private void RefreshUI()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHUD();
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) UIManager.Instance.UpdateHealthDisplay((int)player.currentHealth, (int)player.maxHealth);
        }
    }

    // =========================================================
    // --- SAUVEGARDES (ÉCRITURE SUR LE DISQUE) ---
    // =========================================================

    public void AutoSave(string eventName)
    {
        Debug.Log($"[SaveManager] Sauvegarde automatique : {eventName}");
        ExecuteSaveData();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=#AAAAAA>Sauvegarde auto... ({eventName})</color>", true);
    }

    public void ManualSave()
    {
        Debug.Log("[SaveManager] Sauvegarde MANUELLE.");
        ExecuteSaveData();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=green>💾 Partie sauvegardée avec succès.</color>", true);
    }

    private void ExecuteSaveData()
    {
        if (GameManager.Instance != null)
        {
            PlayerPrefs.SetInt("Save_CleanMoney", GameManager.Instance.cleanMoney);
            PlayerPrefs.SetInt("Save_DirtyMoney", GameManager.Instance.dirtyMoney);
            PlayerPrefs.SetInt("Save_WantedLevel", GameManager.Instance.wantedLevel);
            PlayerPrefs.SetInt("Save_CrimePoints", GameManager.Instance.crimePoints);
        }

        if (TimeManager.Instance != null)
        {
            PlayerPrefs.SetFloat("Save_TimeOfDay", TimeManager.Instance.currentTimeOfDay);
        }

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            PlayerPrefs.SetFloat("Save_PlayerPosX", player.transform.position.x);
            PlayerPrefs.SetFloat("Save_PlayerPosY", player.transform.position.y);
            PlayerPrefs.SetFloat("Save_PlayerPosZ", player.transform.position.z);
            PlayerPrefs.SetInt("Save_PlayerHealth", (int)player.currentHealth);
            PlayerPrefs.SetInt("Save_PlayerShield", (int)player.currentShield);
        }

        PlayerPrefs.SetInt("HasSavedGame", 1);
        PlayerPrefs.Save();
    }
}