using UnityEngine;

public class CarInteraction : MonoBehaviour
{
    [Header("Références du Véhicule")]
    public CarController carController;
    public GameObject carCamera;
    public Transform exitPoint;

    [Header("Système de Carjacking 🏃")]
    public GameObject driverPrefab;

    private GameObject player;
    private Collider playerCollider;
    private MonoBehaviour playerMovementScript;
    private Renderer[] playerRenderers;

    private bool playerInCar = false;
    private bool canEnter = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        carCamera.SetActive(false);

        if (player != null)
        {
            playerCollider = player.GetComponent<Collider>();
            playerMovementScript = player.GetComponent("PlayerController") as MonoBehaviour;
            playerRenderers = player.GetComponentsInChildren<Renderer>();
        }
    }

    void Update()
    {
        if (canEnter && !playerInCar && Input.GetKeyDown(KeyCode.E))
        {
            EnterCar();
        }
        else if (playerInCar && Input.GetKeyDown(KeyCode.E))
        {
            ExitCar();
        }

        if (playerInCar && player != null)
        {
            player.transform.position = carController.transform.position;
        }
    }

    void EnterCar()
    {
        playerInCar = true;
        carController.isDrivenByPlayer = true;
        carCamera.SetActive(true);

        // Si c'est une voiture IA, on fait sortir le conducteur
        if (carController.isDrivenByAI)
        {
            carController.isDrivenByAI = false;
            // Spawn du PNJ conducteur si nécessaire
            if (driverPrefab != null) Instantiate(driverPrefab, exitPoint.position, Quaternion.identity);
            if (GameManager.Instance != null) GameManager.Instance.IncreaseNotoriety(20);
        }

        // On coupe le joueur à pied
        if (playerCollider != null) playerCollider.enabled = false;
        if (playerMovementScript != null) playerMovementScript.enabled = false;

        foreach (Renderer rend in playerRenderers)
        {
            if (rend.gameObject.name != "Icone_Joueur") rend.enabled = false;
        }

        if (MinimapFollow.Instance != null) MinimapFollow.Instance.target = carController.transform;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("<color=cyan>Appuyez sur [E] pour sortir.</color>");
            // ALLUME LE NOM DU VÉHICULE EN BAS À DROITE
            UIManager.Instance.ShowVehicleHUD(carController.carModelName.ToUpper());
        }
    }

    public void ExitCar()
    {
        playerInCar = false;
        carController.isDrivenByPlayer = false;
        carCamera.SetActive(false);

        player.transform.position = exitPoint.position;
        if (playerCollider != null) playerCollider.enabled = true;
        if (playerMovementScript != null) playerMovementScript.enabled = true;

        foreach (Renderer rend in playerRenderers) rend.enabled = true;

        if (MinimapFollow.Instance != null) MinimapFollow.Instance.target = player.transform;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideVehicleHUD();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canEnter = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canEnter = false;
        }
    }
}