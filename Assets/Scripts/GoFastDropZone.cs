using UnityEngine;

public class GoFastDropZone : MonoBehaviour
{
    [Header("Configuration du Point de Chute")]
    public float detectionRadius = 5f; // Rayon du radar invisible

    private CarController carInZone;
    private bool isWaitingForExit = false;

    void Update()
    {
        // SÉCURITÉ : On ne fait rien si le joueur n'a pas de contrat Go Fast actif
        if (ContractManager.Instance == null || !ContractManager.Instance.hasActiveContract ||
            ContractManager.Instance.currentContract != ContractManager.ContractType.GoFast)
        {
            carInZone = null;
            isWaitingForExit = false;
            return;
        }

        // Radar de détection autonome (Logique anti-bug multi-colliders)
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius);
        bool targetCarInRadius = false;

        foreach (var hitCollider in hitColliders)
        {
            CarController car = hitCollider.GetComponentInParent<CarController>();

            // On vérifie qu'on a trouvé une voiture ET que son modèle correspond à la commande du ContractManager
            if (car != null && car.carModelName == ContractManager.Instance.targetVehicleModel)
            {
                // CAS 1 : Le joueur entre dans la zone au volant de la bonne voiture
                if (car.isDrivenByPlayer)
                {
                    targetCarInRadius = true;

                    if (carInZone != car)
                    {
                        carInZone = car;
                        isWaitingForExit = true;

                        if (UIManager.Instance != null)
                            UIManager.Instance.ShowNotification($"<color=cyan><b>LIVRAISON GO FAST :</b> Garez-vous et sortez [E] pour exporter la {car.carModelName.ToUpper()}.</color>");
                    }
                    break; // Cible trouvée, on stoppe le scan de cette frame
                }
                // CAS 2 : Le joueur vient de couper le contact et de sortir de la bonne voiture
                else if (car == carInZone && isWaitingForExit && !car.isDrivenByPlayer)
                {
                    DeliverVehicle(car);
                    targetCarInRadius = false;
                    break;
                }
            }
        }

        // CAS 3 : Le joueur quitte la zone de livraison au volant sans valider
        if (!targetCarInRadius && isWaitingForExit && carInZone != null && carInZone.isDrivenByPlayer)
        {
            carInZone = null;
            isWaitingForExit = false;
        }
    }

    private void DeliverVehicle(CarController car)
    {
        // 1. On valide le contrat Go Fast auprès du gestionnaire
        ContractManager.Instance.CompleteContract(ContractManager.ContractType.GoFast);

        // 2. On fait disparaître la voiture (exportation réussie)
        Destroy(car.gameObject);

        // 3. Réinitialisation des variables du radar
        carInZone = null;
        isWaitingForExit = false;
    }

    // Affiche la zone de livraison en bleu turquoise dans l'éditeur
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}