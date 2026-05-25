using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    public float speed = 50f; // Vitesse de la balle
    public float lifeTime = 3f; // Temps avant de disparaître (pour ne pas polluer le jeu)

    [HideInInspector] public int damage; // Défini par l'arme qui a tiré

    void Start()
    {
        // On propulse la balle en avant dès qu'elle apparaît
        GetComponent<Rigidbody>().linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifeTime); // Auto-destruction
    }

    // Quand la balle touche quelque chose
    private void OnTriggerEnter(Collider other)
    {
        // --- SÉCURITÉ : On empêche la balle de toucher le joueur lui-même ---
        if (other.CompareTag("Player")) return;

        // (Optionnel) Empêche la balle d'exploser sur des zones invisibles comme les zones de détection des PNJ
        if (other.isTrigger) return;

        // 1. Cherche si l'objet touché est un PNJ ou un joueur (TargetHealth)
        TargetHealth target = other.GetComponent<TargetHealth>();
        if (target != null)
        {
            target.TakeDamage(damage);
        }

        // 2. NOUVEAU : Cherche si l'objet touché est une voiture (CarController)
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null)
        {
            // On inflige 15 points de dégâts (environ 7 balles pour détruire une voiture à 100 PV)
            car.TakeDamage(15f);
        }

        // On détruit la balle au premier vrai contact (mur, ennemi, voiture, etc.)
        Destroy(gameObject);
    }
}