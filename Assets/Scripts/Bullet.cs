using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    public float speed = 50f; // Vitesse de la balle
    public float lifeTime = 3f; // Temps avant de dispara�tre (pour ne pas polluer le jeu)

    [HideInInspector] public int damage; // D�fini par l'arme qui a tir�

    void Start()
    {
        // On propulse la balle en avant d�s qu'elle appara�t
        GetComponent<Rigidbody>().linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifeTime); // Auto-destruction
    }

    // Quand la balle touche quelque chose
    // Quand la balle touche quelque chose
    private void OnTriggerEnter(Collider other)
    {
        // --- SÉCURITÉ : On empêche la balle de toucher le joueur lui-même ---
        if (other.CompareTag("Player")) return;

        // (Optionnel) Empêche la balle d'exploser sur des zones invisibles comme les zones de détection des PNJ
        if (other.isTrigger) return;

        // Cherche si l'objet touché a de la vie
        TargetHealth target = other.GetComponent<TargetHealth>();
        if (target != null)
        {
            target.TakeDamage(damage);
        }

        // On détruit la balle au premier vrai contact (mur, ennemi, etc.)
        Destroy(gameObject);
    }
}