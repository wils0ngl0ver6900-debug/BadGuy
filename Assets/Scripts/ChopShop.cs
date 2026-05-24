using UnityEngine;
using System.Collections.Generic;

public class ChopShop : MonoBehaviour
{
    public static ChopShop Instance;

    [Header("Paramètres du Garage")]
    public int maxSameModelPerDay = 2;
    public float detectionRadius = 6f; // La taille du "Radar" du garage

    private Dictionary<string, int> carsSoldToday = new Dictionary<string, int>();

    private CarController carInZone;
    private bool isWaitingForExit = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Update()
    {
        // On scanne la zone autour du garage
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius);
        bool playerCarInRadius = false;

        foreach (var hitCollider in hitColliders)
        {
            CarController car = hitCollider.GetComponentInParent<CarController>();

            if (car != null)
            {
                // CAS 1 : Le joueur conduit une voiture et entre dans la zone
                if (car.isDrivenByPlayer)
                {
                    playerCarInRadius = true;

                    // Si c'est une nouvelle voiture qu'on détecte
                    if (carInZone != car)
                    {
                        carInZone = car;
                        isWaitingForExit = true;

                        int currentSold = carsSoldToday.ContainsKey(car.carModelName) ? carsSoldToday[car.carModelName] : 0;

                        if (currentSold >= maxSameModelPerDay)
                        {
                            if (UIManager.Instance != null)
                                UIManager.Instance.ShowNotification($"<color=red>On a déjà trop de {car.carModelName} ! Reviens demain.</color>");
                        }
                        else
                        {
                            int price = CalculatePrice(car);
                            if (UIManager.Instance != null)
                                UIManager.Instance.ShowNotification($"Garez-vous et sortez [E] pour vendre ({price}$)");
                        }
                    }
                    break; // On a trouvé le joueur, on arrête de chercher
                }
                // CAS 2 : La voiture qu'on surveillait est là, mais le joueur n'est plus dedans ! (Il vient de sortir)
                else if (car == carInZone && isWaitingForExit && !car.isDrivenByPlayer)
                {
                    SellCar(car);
                    playerCarInRadius = false;
                    break;
                }
            }
        }

        // CAS 3 : Le joueur a fait marche arrière et a quitté la zone
        if (!playerCarInRadius && isWaitingForExit && carInZone != null && carInZone.isDrivenByPlayer)
        {
            carInZone = null;
            isWaitingForExit = false;
            // La notification disparaîtra toute seule après quelques secondes
        }
    }

    private int CalculatePrice(CarController car)
    {
        if (car.maxHealth <= 0) car.maxHealth = 100f;
        if (car.currentHealth <= 0) car.currentHealth = 0f;

        float healthPercentage = car.currentHealth / car.maxHealth;
        return Mathf.Max(0, Mathf.RoundToInt(car.baseValue * healthPercentage));
    }

    private void SellCar(CarController carToSell)
    {
        int currentSold = carsSoldToday.ContainsKey(carToSell.carModelName) ? carsSoldToday[carToSell.carModelName] : 0;

        // Si on a déjà dépassé la limite, on annule la vente et on laisse la voiture vide
        if (currentSold >= maxSameModelPerDay)
        {
            carInZone = null;
            isWaitingForExit = false;
            return;
        }

        int price = CalculatePrice(carToSell);
        carsSoldToday[carToSell.carModelName] = currentSold + 1;

        if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(price);
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=green>Véhicule désossé ! +{price}$</color>");

        // On détruit la voiture du monde (le joueur est déjà sorti, donc il ne sera pas détruit avec !)
        Destroy(carToSell.gameObject);

        // On remet le garage à zéro
        carInZone = null;
        isWaitingForExit = false;
    }

    public void ResetDailyLimits()
    {
        carsSoldToday.Clear();
    }

    // Affiche la zone jaune dans l'éditeur
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}