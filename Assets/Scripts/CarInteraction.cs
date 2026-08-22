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
    private Collider[] playerColliders;
    private MonoBehaviour playerMovementScript;
    private Renderer[] playerRenderers;
    private Rigidbody playerRb;

    private bool playerInCar = false;
    private bool canEnter = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        carCamera.SetActive(false);

        if (player != null)
        {
            // GetComponentsInChildren (pas juste GetComponent) : le joueur a maintenant des
            // colliders sur chaque os du ragdoll, pas seulement la capsule principale. Sans
            // tous les désactiver en voiture, ils restent solides et se retrouvent incrustés
            // dans la voiture à chaque frame (le joueur est téléporté dessus) — la voiture
            // se fait alors repousser violemment par la résolution physique.
            playerColliders = player.GetComponentsInChildren<Collider>();
            playerMovementScript = player.GetComponent("PlayerController") as MonoBehaviour;
            playerRenderers = player.GetComponentsInChildren<Renderer>();
            playerRb = player.GetComponent<Rigidbody>();
        }
    }

    void Update()
    {
        if (canEnter && !playerInCar && Input.GetKeyDown(KeyCode.E))
        {
            // Une voiture "à vendre" (CarForSale) pas encore achetée ne doit pas pouvoir
            // être prise en main : sinon la touche [E] "monter en voiture" entre en conflit
            // avec le [E] "acheter" du système Interactable dès qu'on est près de la portière,
            // et on se retrouve à rouler avec sans avoir payé.
            CarForSale forSale = carController != null ? carController.GetComponent<CarForSale>() : null;
            if (forSale != null && !carController.isPlayerOwned)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification("<color=red>Tu dois d'abord l'acheter !</color>");
            }
            else
            {
                EnterCar();
            }
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

    public void EnterCar()
    {
        playerInCar = true;
        carController.isDrivenByPlayer = true;
        carCamera.SetActive(true);

        // ---> LA NOUVELLE LIGNE MAGIQUE <---
        // On demande au carController (la racine de la voiture) de chercher le script !
        carController.GetComponent<MessageTrigger>()?.SendTheMessage();

        // Si c'est une voiture IA, on fait sortir le conducteur
        if (carController.isDrivenByAI)
        {
            carController.isDrivenByAI = false;
            // Spawn du PNJ conducteur si nécessaire
            if (driverPrefab != null) Instantiate(driverPrefab, exitPoint.position, Quaternion.identity);
            if (GameManager.Instance != null) GameManager.Instance.ReportCrime(20);
        }

        // On coupe le joueur à pied
        if (playerColliders != null)
        {
            foreach (Collider col in playerColliders)
            {
                if (col != null) col.enabled = false;
            }
        }
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
        ExitCarAt(exitPoint != null ? exitPoint.position : transform.position);
    }

    // Variante qui laisse choisir où le joueur atterrit (ex: un point sûr dans le garage
    // plutôt que le exitPoint habituel de la voiture, pas forcément adapté à ce contexte).
    public void ExitCarAt(Vector3 worldPosition)
    {
        playerInCar = false;
        carController.isDrivenByPlayer = false;
        carCamera.SetActive(false);

        // Recalage au sol par raycast : exitPoint suppose une voiture à peu près à plat sur
        // une surface normale. Après un accident violent (voiture retournée, encastrée...),
        // sa position réelle peut être n'importe où — sans ce recalage, le joueur pouvait
        // atterrir sous la carte lors d'une éjection d'urgence (CarExplosionImproved).
        Vector3 targetPosition = worldPosition;
        if (Physics.Raycast(targetPosition + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 20f))
        {
            targetPosition = groundHit.point + Vector3.up * 0.1f;
        }

        // On téléporte via le Rigidbody plutôt que via transform.position directement :
        // sur un objet à Rigidbody, forcer transform.position désynchronise le moteur physique
        // d'une frame, ce qui peut faire passer le joueur à travers le sol selon l'endroit —
        // c'est ce qui causait le "sous la carte" en sortant dans le garage.
        if (playerRb != null)
        {
            playerRb.position = targetPosition;
            playerRb.linearVelocity = Vector3.zero;
        }
        else if (player != null)
        {
            player.transform.position = targetPosition;
        }

        // Remet UNIQUEMENT les os du ragdoll dans un état sûr (kinematic, vitesse nulle)
        // AVANT de réactiver leurs colliders — surtout PAS la racine ni l'Animator.
        // GameManager.DisablePlayerRagdoll() fait aussi rootRb.isKinematic = false, pensé
        // pour le contexte précis d'un retour de VRAI ragdoll de KO (où la racine avait été
        // mise kinematic exprès) — l'appeler ici cassait le mode kinematic normal de la
        // racine du joueur en dehors de tout KO, d'où le nouveau bug sur la Compactico.
        if (player != null)
        {
            Rigidbody[] boneRbs = player.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody boneRb in boneRbs)
            {
                if (boneRb.gameObject == player) continue; // La racine ne doit jamais être touchée ici
                boneRb.isKinematic = true;
                boneRb.linearVelocity = Vector3.zero;
                boneRb.angularVelocity = Vector3.zero;
            }
        }

        if (playerColliders != null)
        {
            foreach (Collider col in playerColliders)
            {
                if (col != null) col.enabled = true;
            }
        }
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