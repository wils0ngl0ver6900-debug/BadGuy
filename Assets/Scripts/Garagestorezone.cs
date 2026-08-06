using UnityEngine;

// Zone à poser à l'entrée du garage : le joueur roule dedans au volant d'une voiture,
// un prompt apparaît, [G] range le véhicule dans GarageManager. Même pattern que
// ValetParkingZone (OnTriggerEnter/Exit + GetComponentInParent) mais sans minigame
// d'alignement, ici on veut juste garer sa propre bagnole.
public class GarageStoreZone : MonoBehaviour
{
    [Header("UI de Prompt")]
    public GameObject storePromptUI;

    [Header("Touche pour garer")]
    public KeyCode storeKey = KeyCode.G;

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
            currentInteraction = car.GetComponent<CarInteraction>();
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
                bool success = GarageManager.Instance.TryStoreVehicle(currentCar, currentInteraction);
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