using UnityEngine;

// Zone à poser dans un garage de tuning. Deux comportements bien séparés :
// - Voiture ACHETÉE : prompt "[T] pour Tuning" comme avant, ouvre le menu complet
//   (couleur au choix + 4 améliorations mécaniques).
// - Voiture VOLÉE : AUCUNE touche, AUCUN menu. Dès qu'elle entre dans la zone, elle se
//   fait repeindre automatiquement (couleur aléatoire) et perd une étoile de recherche
//   si elle en a — comme un "car wash à planque", pas un vrai atelier de tuning.
// Même garde-fou contre les doubles blocages d'appel que GarageStoreZone (une voiture a
// plusieurs colliders, donc plusieurs OnTriggerEnter pour une seule entrée dans la zone).
public class TuningShopZone : MonoBehaviour
{
    [Header("UI de Prompt (voitures achetées uniquement)")]
    public GameObject tuningPromptUI;

    [Header("Touche pour ouvrir le menu (voitures achetées uniquement)")]
    public KeyCode openKey = KeyCode.T;

    private CarController currentCar;
    private bool canOpen = false;
    private bool hasRequestedCallBlock = false;

    // Une seule repeinture automatique par entrée dans la zone (sinon ça repeindrait/
    // ferait baisser une étoile en boucle tant que la voiture reste dedans).
    private bool hasAutoRepaintedThisVisit = false;

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

            if (!hasRequestedCallBlock)
            {
                CallApp.RequestCallBlock();
                hasRequestedCallBlock = true;
            }

            // On reporte la décision achetée/volée à la frame suivante : si plusieurs
            // colliders du même véhicule déclenchent cet événement quasi-simultanément,
            // on évite de traiter le premier (qui pourrait arriver avant que isPlayerOwned
            // soit à jour) et de déclencher par erreur le service automatique sur une
            // voiture achetée qui n'est pas encore reconnue comme telle.
            StartCoroutine(HandleCarEnteredNextFrame(car));
        }
    }

    private System.Collections.IEnumerator HandleCarEnteredNextFrame(CarController car)
    {
        yield return null; // attend la fin de la frame en cours

        // La voiture a pu sortir ou changer pendant ce délai d'une frame
        if (car == null || !car.isDrivenByPlayer || currentCar != car) yield break;

        if (car.isPlayerOwned)
        {
            canOpen = true;
            if (tuningPromptUI != null) tuningPromptUI.SetActive(true);
        }
        else
        {
            canOpen = false;
            if (tuningPromptUI != null) tuningPromptUI.SetActive(false);

            if (!hasAutoRepaintedThisVisit && TuningShopManager.Instance != null)
            {
                TuningShopManager.Instance.AutoServiceStolenCar(car);
                hasAutoRepaintedThisVisit = true;
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
            hasAutoRepaintedThisVisit = false; // ressort et rerentre = ça peut se redéclencher
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
        // Uniquement pour les voitures achetées désormais (canOpen ne passe jamais à true
        // pour une voiture volée, voir OnTriggerEnter).
        if (canOpen && currentCar != null && currentCar.isDrivenByPlayer && Input.GetKeyDown(openKey))
        {
            if (TuningShopManager.Instance != null)
            {
                TuningShopManager.Instance.OpenShopFor(currentCar);
                // Le prompt "[T] pour Tuning" n'a plus lieu d'être une fois le menu ouvert —
                // avant, il restait affiché par-dessus toute la session de tuning.
                if (tuningPromptUI != null) tuningPromptUI.SetActive(false);
            }
        }
    }
}