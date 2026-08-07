using UnityEngine;

// Zone à poser dans un garage de tuning : le joueur roule dedans au volant d'une voiture
// ACHETÉE, un prompt apparaît, [T] ouvre le menu de modification (TuningShopManager).
// Même pattern que GarageStoreZone (OnTriggerEnter/Exit + GetComponentInParent), avec le
// même garde-fou contre les doubles blocages d'appel (une voiture a plusieurs colliders,
// donc plusieurs OnTriggerEnter pour une seule entrée dans la zone).
public class TuningShopZone : MonoBehaviour
{
    [Header("UI de Prompt")]
    public GameObject tuningPromptUI;

    [Header("Touche pour ouvrir le menu")]
    public KeyCode openKey = KeyCode.T;

    private CarController currentCar;
    private bool canOpen = false;
    private bool hasRequestedCallBlock = false;

    private void Start()
    {
        if (tuningPromptUI != null) tuningPromptUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null && car.isDrivenByPlayer)
        {
            currentCar = car;
            canOpen = true;
            if (tuningPromptUI != null) tuningPromptUI.SetActive(true);

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
            canOpen = false;
            if (tuningPromptUI != null) tuningPromptUI.SetActive(false);

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

    private void OnDisable()
    {
        // Sécurité si l'objet est désactivé/la scène change pendant qu'une voiture est
        // encore dans la zone : on ne laisse jamais un blocage orphelin derrière nous.
        ReleaseCallBlockIfNeeded();
    }

    private void Update()
    {
        if (canOpen && currentCar != null && currentCar.isDrivenByPlayer && Input.GetKeyDown(openKey))
        {
            // La peinture doit rester accessible même sur une voiture volée (ça fait
            // perdre une étoile de recherche, voir TuningShopManager.SelectColor) — donc
            // on n'interdit plus l'ouverture du shop ici. Seules les 4 améliorations
            // mécaniques restent réservées aux véhicules achetés, vérifié à l'achat de
            // chacune dans TuningShopManager.
            if (TuningShopManager.Instance != null)
            {
                TuningShopManager.Instance.OpenShopFor(currentCar);
            }
        }
    }
}