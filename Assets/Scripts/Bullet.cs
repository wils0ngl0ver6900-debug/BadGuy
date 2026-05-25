using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    public float speed = 50f;
    public float lifeTime = 3f;

    [HideInInspector] public int damage;
    [HideInInspector] public bool isEnemyBullet = false;

    void Start()
    {
        GetComponent<Rigidbody>().linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;

        // 1. SI LA BALLE TOUCHE LE JOUEUR (On cherche directement le PlayerController)
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            if (!isEnemyBullet) return; // Sécurité : on ne se tire pas dessus nous-même

            // Baisse le bouclier, puis la vie
            if (pc.currentShield > 0)
            {
                pc.currentShield -= damage;
                if (pc.currentShield < 0)
                {
                    pc.currentHealth += pc.currentShield;
                    pc.currentShield = 0;
                }
            }
            else
            {
                pc.currentHealth -= damage;
            }

            if (UIManager.Instance != null) UIManager.Instance.UpdateHealthDisplay((int)pc.currentHealth, (int)pc.maxHealth);

            Destroy(gameObject);
            return; // IMPORTANT : On détruit la balle ici pour éviter que d'autres scripts affichent "-0 PV" !
        }

        // 2. SI LA BALLE TOUCHE UN PNJ ENNEMI
        TargetHealth target = other.GetComponent<TargetHealth>();
        if (target != null)
        {
            target.TakeDamage(damage);
        }

        // 3. SI LA BALLE TOUCHE UNE VOITURE
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null)
        {
            car.TakeDamage(15f);
        }

        Destroy(gameObject);
    }
}