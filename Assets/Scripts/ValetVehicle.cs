using UnityEngine;

// On s'assure que l'objet possède bien ton script CarController
[RequireComponent(typeof(CarController))]
public class ValetVehicle : MonoBehaviour
{
    private CarController carController;
    private float initialHealth;

    private void Awake()
    {
        // On récupère ton script CarController existant
        carController = GetComponent<CarController>();
    }

    private void Start()
    {
        if (carController != null)
        {
            // On mémorise la santé de la voiture au moment où on la confie au voiturier
            initialHealth = carController.currentHealth;
        }
        else
        {
            Debug.LogError("ValetVehicle : Aucun CarController trouvé sur ce véhicule !");
        }
    }

    /// <summary>
    /// Calcule le nombre de points de dégâts subis depuis le début de la course.
    /// </summary>
    public int GetAccumulatedDamage()
    {
        if (carController != null)
        {
            // Les dégâts correspondent à la santé perdue (Santé initiale - Santé actuelle)
            float damageTaken = initialHealth - carController.currentHealth;

            // On s'assure de ne jamais retourner un nombre négatif (au cas où la voiture serait soignée)
            return Mathf.Max(0, Mathf.RoundToInt(damageTaken));
        }

        return 0;
    }
}