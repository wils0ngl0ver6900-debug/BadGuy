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
                }
            }
        }
    }
}