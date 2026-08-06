using UnityEngine;

// Pose ce script SUR LE MÊME OBJET qu'un CarController pour rendre cette voiture achetable.
// Hérite d'Interactable : se glisse directement dans le système "marche dessus + [E]"
// déjà utilisé partout ailleurs (ATM, boutiques, PNJ...), sans avoir besoin de toucher
// à PlayerController.cs — la détection de portée et l'affichage du prompt "[E]" sont
// déjà gérés par le système existant, qui reconnaît n'importe quel composant Interactable
// (et donc aussi ses sous-classes comme celle-ci).
//
// Important : la détection utilise un Collider en Trigger. Le véhicule a déjà un/des
// collider(s) "pleins" pour la carrosserie/la physique — ajoute-en un DEUXIÈME (un
// BoxCollider suffit), un peu plus grand, avec "Is Trigger" coché. Les deux colliders
// peuvent coexister sur le même objet sans se gêner.
public class CarForSale : Interactable
{
    [Header("Vente du véhicule 🚗💰")]
    public int price = 5000;

    [Tooltip("Optionnel : nom affiché dans les messages d'achat. Laisse vide pour reprendre carModelName.")]
    public string displayName = "";

    private CarController car;
    private bool isSold = false;

    private void Awake()
    {
        car = GetComponent<CarController>();
    }

    public override void Interact()
    {
        if (isSold || car == null) return;

        string nom = string.IsNullOrEmpty(displayName) ? car.carModelName : displayName;

        if (GameManager.Instance == null) return;

        if (GameManager.Instance.cleanMoney < price)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification($"<color=red>Pas assez d'argent propre pour {nom} ({price}€).</color>");
            return;
        }

        GameManager.Instance.cleanMoney -= price;
        if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(-price, $"Achat véhicule : {nom}");

        car.isPlayerOwned = true;
        isSold = true;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"<color=green>{nom} achetée ! Elle peut désormais être rangée au garage.</color>");
            UIManager.Instance.UpdateHUD();
        }

        // Une fois vendue, on désactive le(s) collider(s) en Trigger pour que le prompt
        // "[E] pour interagir" arrête de proposer un rachat sur ce véhicule.
        foreach (Collider c in GetComponents<Collider>())
        {
            if (c.isTrigger) c.enabled = false;
        }
    }
}