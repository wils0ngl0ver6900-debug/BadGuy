using UnityEngine;

// Suit exactement le même pattern que StockApp/BankApp (OpenApp/CloseApp) pour rester
// cohérent avec le reste du téléphone. Regroupe la caméra satellite + la pause du jeu.
public class MapApp : MonoBehaviour
{
    public static MapApp Instance;

    [Header("UI")]
    public GameObject mapAppPanel;

    [Header("Caméra Satellite")]
    public MapSatelliteCamera satelliteCamera;

    [HideInInspector] public bool isOpen = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (mapAppPanel != null) mapAppPanel.SetActive(false);
    }

    public void OpenApp()
    {
        if (mapAppPanel != null) mapAppPanel.SetActive(true);
        if (satelliteCamera != null) satelliteCamera.ActivateMap();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isOpen = true;
    }

    public void CloseApp()
    {
        if (mapAppPanel != null) mapAppPanel.SetActive(false);
        if (satelliteCamera != null) satelliteCamera.DeactivateMap();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;

        isOpen = false;
    }
}