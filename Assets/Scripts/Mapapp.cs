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
        Debug.Log("[MapApp] OpenApp() appelée");
        if (mapAppPanel != null) mapAppPanel.SetActive(true);
        if (satelliteCamera != null) satelliteCamera.ActivateMap();

        Time.timeScale = 0f; // La map met le jeu en pause

        // On s'assure que la souris est bien libre et visible
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isOpen = true;
    }

    public void CloseApp()
    {
        Debug.Log($"[MapApp] CloseApp() appelée — mapAppPanel assigné : {mapAppPanel != null}");
        if (mapAppPanel != null) mapAppPanel.SetActive(false);
        if (satelliteCamera != null) satelliteCamera.DeactivateMap();

        // Si ton téléphone gère la pause de son côté, tu peux retirer Time.timeScale = 1f.
        // Sinon, on remet le temps normal à la fermeture de l'application map.
        Time.timeScale = 1f;

        // --- LE CORRECTIF EST ICI ---
        // Le téléphone est toujours ouvert, donc on force la souris à RESTER visible !
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isOpen = false;
    }
}