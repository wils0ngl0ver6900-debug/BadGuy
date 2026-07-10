using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class SettingsApp : MonoBehaviour
{
    public static SettingsApp Instance;

    [Header("UI App Paramètres")]
    public GameObject appPanel;

    [Header("Cible Visuelle")]
    public Image phoneMainWallpaper;

    [Header("Catalogues des Options")]
    public Sprite[] availableWallpapers;
    public AudioClip[] availableRingtones;
    public AudioClip[] availableSMSTones;

    [Header("UI Éléments (Menus déroulants et Boutons)")]
    public TMP_Dropdown wallpaperDropdown;
    public TMP_Dropdown ringtoneDropdown;
    public TMP_Dropdown smsDropdown;
    public Toggle notificationsToggle;
    public Toggle silentModeToggle;

    [Header("Système de Cheat Codes 🤫")]
    public TMP_InputField cheatCodeInput;

    [Header("Lecteur Audio (Pour la preview)")]
    public AudioSource phoneAudioSource;

    [HideInInspector] public bool showNotifications = true;
    [HideInInspector] public bool isSilentMode = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        LoadSettings();
        SetupListeners();
    }

    public void OpenApp()
    {
        if (appPanel != null) appPanel.SetActive(true);
    }

    public void CloseApp()
    {
        if (appPanel != null) appPanel.SetActive(false);
    }

    private void SetupListeners()
    {
        if (wallpaperDropdown != null)
            wallpaperDropdown.onValueChanged.AddListener(ChangeWallpaper);

        if (notificationsToggle != null)
            notificationsToggle.onValueChanged.AddListener(ToggleNotifications);

        if (silentModeToggle != null)
            silentModeToggle.onValueChanged.AddListener(ToggleSilentMode);
    }

    public void ChangeWallpaper(int index)
    {
        if (index >= 0 && index < availableWallpapers.Length)
        {
            if (phoneMainWallpaper != null) phoneMainWallpaper.sprite = availableWallpapers[index];
            PlayerPrefs.SetInt("SavedWallpaper", index);
        }
    }

    public void ChangeRingtone(int index)
    {
        if (index >= 0 && index < availableRingtones.Length)
            PlayerPrefs.SetInt("SavedRingtone", index);
    }

    public void ChangeSMSTone(int index)
    {
        if (index >= 0 && index < availableSMSTones.Length)
            PlayerPrefs.SetInt("SavedSMSTone", index);
    }

    public void ToggleNotifications(bool state)
    {
        showNotifications = state;
        PlayerPrefs.SetInt("SavedNotifs", state ? 1 : 0);
    }

    public void ToggleSilentMode(bool state)
    {
        isSilentMode = state;
        PlayerPrefs.SetInt("SavedSilentMode", state ? 1 : 0);
    }

    private void TogglePreview(AudioClip clip)
    {
        if (phoneAudioSource == null || clip == null || isSilentMode) return;

        if (phoneAudioSource.isPlaying && phoneAudioSource.clip == clip)
        {
            phoneAudioSource.Stop();
        }
        else
        {
            phoneAudioSource.clip = clip;
            phoneAudioSource.Play();
        }
    }

    public void TestRingtone()
    {
        int index = ringtoneDropdown.value;
        if (index >= 0 && index < availableRingtones.Length) TogglePreview(availableRingtones[index]);
    }

    public void TestSMSTone()
    {
        int index = smsDropdown.value;
        if (index >= 0 && index < availableSMSTones.Length) TogglePreview(availableSMSTones[index]);
    }

    public void PlayIncomingSMS()
    {
        if (isSilentMode) return;
        int currentIndex = PlayerPrefs.GetInt("SavedSMSTone", 0);

        if (phoneAudioSource != null && availableSMSTones.Length > currentIndex)
        {
            phoneAudioSource.clip = availableSMSTones[currentIndex];
            phoneAudioSource.Play();
        }
    }

    private void LoadSettings()
    {
        int savedWallpaper = PlayerPrefs.GetInt("SavedWallpaper", 0);
        if (wallpaperDropdown != null) wallpaperDropdown.value = savedWallpaper;
        ChangeWallpaper(savedWallpaper);

        int savedRingtone = PlayerPrefs.GetInt("SavedRingtone", 0);
        if (ringtoneDropdown != null) ringtoneDropdown.value = savedRingtone;

        int savedSMS = PlayerPrefs.GetInt("SavedSMSTone", 0);
        if (smsDropdown != null) smsDropdown.value = savedSMS;

        bool notifs = PlayerPrefs.GetInt("SavedNotifs", 1) == 1;
        showNotifications = notifs;
        if (notificationsToggle != null) notificationsToggle.isOn = notifs;

        bool silent = PlayerPrefs.GetInt("SavedSilentMode", 0) == 1;
        isSilentMode = silent;
        if (silentModeToggle != null) silentModeToggle.isOn = silent;
    }

    // --- MODIFICATION ICI : Appel au SaveManager ---
    public void SaveGame()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.ManualSave();
        }
    }

    public void LoadGame()
    {
        if (SaveManager.Instance != null)
        {
            // On appelle la nouvelle fonction de chargement !
            SaveManager.Instance.LoadGameData();
        }
        else
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("<color=red>Erreur système de sauvegarde.</color>", true);
        }
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void ValidateCheatCode()
    {
        if (cheatCodeInput == null || string.IsNullOrEmpty(cheatCodeInput.text)) return;

        string code = cheatCodeInput.text.Trim();
        cheatCodeInput.text = "";

        bool codeFound = true;

        switch (code)
        {
            case "7777":
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddDirtyMoney(1000000);
                    GameManager.Instance.cleanMoney += 1000000;
                    if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
                    Notify("Triche activée : <color=#00FF41>Jackpot ! (+1 000 000$)</color>");
                }
                break;
            case "0000":
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.LoseCops();
                    Notify("Triche activée : <color=white>Fantôme (Recherche annulée).</color>");
                }
                break;
            case "9999":
                Notify("Triche activée : <color=yellow>The World is Yours (Réputation Max)</color>");
                break;
            case "4444":
                if (MessageApp.Instance != null)
                {
                    MessageApp.Instance.ReceiveMessage("Inconnu", "T'as besoin de puissance de feu ? J'ai laissé un arsenal dans ton coffre.", false);
                }
                Notify("Triche activée : <color=red>Arsenal débloqué.</color>");
                break;
            case "1111":
                Notify("Triche activée : <color=cyan>Santé & Armure restaurées.</color>");
                break;
            default:
                codeFound = false;
                break;
        }

        if (!codeFound) Notify("<color=red>Code invalide.</color>");
        else if (phoneAudioSource != null && availableSMSTones.Length > 0) phoneAudioSource.PlayOneShot(availableSMSTones[0]);
    }

    private void Notify(string msg)
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification(msg, true);
    }
}