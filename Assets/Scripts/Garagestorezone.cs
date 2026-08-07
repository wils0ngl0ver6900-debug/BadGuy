using UnityEngine;

// Zone à poser à l'entrée du garage : le joueur roule dedans au volant d'une voiture
// ACHETÉE, un prompt apparaît, [G] range le véhicule dans GarageManager. Même pattern
// que ValetParkingZone (OnTriggerEnter/Exit + GetComponentInParent) mais sans minigame
// d'alignement, ici on veut juste garer sa propre bagnole.
public class GarageStoreZone : MonoBehaviour
{
    [Header("UI de Prompt")]
    public GameObject storePromptUI;

    [Header("Touche pour garer")]
    public KeyCode storeKey = KeyCode.G;

    [Header("Sécurité de sortie du véhicule")]
    [Tooltip("Point où le joueur atterrit une fois la voiture rangée. Place-le sur le sol du garage, à un endroit clairement dégagé. Si vide, le exitPoint habituel de la voiture est utilisé à la place (pas toujours fiable ici).")]
    public Transform playerSafeStandPoint;

    private CarController currentCar;
    private CarInteraction currentInteraction;
    private bool canStore = false;

    // La voiture a plusieurs colliders (carrosserie, celui de CarForSale, celui du
    // DoorTrigger pour monter dedans...) : chacun déclenche SON PROPRE OnTriggerEnter en
    // entrant dans la zone. Sans ce drapeau, CallApp.RequestCallBlock() était appelé une
    // fois par collider mais relâché une seule fois — le compteur ne redescendait jamais à
    // zéro, callsBlocked restait bloqué à "true" pour de bon dès la première voiture garée.
    private bool hasRequestedCallBlock = false;

    private void Start()
    {
        if (storePromptUI != null) storePromptUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null && car.isDrivenByPlayer)
        {
            currentCar = car;
            // GetComponentInChildren plutôt que GetComponent : sur certains prefabs,
            // CarInteraction n'est pas forcément sur EXACTEMENT le même objet que
            // CarController. Un GetComponent strict qui échoue ici laissait
            // currentInteraction à null en silence, et donc ExitCarAt() n'était jamais
            // appelé au moment de garer — la voiture était détruite mais le joueur restait
            // "en mode conduite" (collisions désactivées, invisible), d'où la chute sous la carte.
            currentInteraction = car.GetComponentInChildren<CarInteraction>();
            canStore = true;
            if (storePromptUI != null) storePromptUI.SetActive(true);

            // Un appel qui sonne pile pendant la manœuvre de rangement fait sursauter le
            // panneau HUD/téléphone au même moment — même précaution que pour les labos.
            // Une seule demande de blocage par entrée dans la zone, peu importe combien de
            // colliders de la voiture déclenchent cet événement.
            if (!hasRequestedCallBlock)
            {
                CallApp.RequestCallBlock();
                hasRequestedCallBlock = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null && car == currentCar)
        {
            currentCar = null;
            currentInteraction = null;
            canStore = false;
            if (storePromptUI != null) storePromptUI.SetActive(false);

            ReleaseCallBlockIfNeeded();
        }
    }

    private void ReleaseCallBlockIfNeeded()
    {
        if (hasRequestedCallBlock)
        {
            CallApp.ReleaseCallBlock();
            hasRequestedCallBlock = false;
        }
    }

    private void Update()
    {
        // Le joueur peut être sorti de la voiture sans quitter le trigger (ex: il descend
        // pile dans la zone) : on revérifie isDrivenByPlayer à chaque frame, pas seulement à l'entrée.
        if (canStore && currentCar != null && currentCar.isDrivenByPlayer && Input.GetKeyDown(storeKey))
        {
            if (GarageManager.Instance != null)
            {
                bool success = GarageManager.Instance.TryStoreVehicle(currentCar, currentInteraction, playerSafeStandPoint);
                if (success)
                {
                    currentCar = null;
                    currentInteraction = null;
                    canStore = false;
                    if (storePromptUI != null) storePromptUI.SetActive(false);

                    // La voiture est détruite : OnTriggerExit ne se déclenchera jamais pour
                    // elle, donc on relâche le blocage ici plutôt que de le laisser bloqué
                    // pour de bon.
                    ReleaseCallBlockIfNeeded();
                }
            }
        }
    }
}