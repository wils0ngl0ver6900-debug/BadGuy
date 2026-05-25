using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    public float speed = 50f;
    public float lifeTime = 3f;

    [HideInInspector] public int damage;
    [HideInInspector] public bool isEnemyBullet = false;
    [HideInInspector] public GameObject shooter;

    void Start()
    {
        GetComponent<Rigidbody>().linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;

        // La balle ignore son propre tireur
        if (shooter != null)
        {
            if (other.gameObject == shooter || other.transform.IsChildOf(shooter.transform)) return;
        }

        // 1. SI LA BALLE TOUCHE LE JOUEUR
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            if (!isEnemyBullet) return;

            // CORRECTIF : La balle dit juste "Fais les dégâts". Le PlayerController s'occupe du reste (bouclier, mort, etc.)
            pc.TakeDamage(damage);

            Destroy(gameObject);
            return;
        }

        // 2. SI LA BALLE TOUCHE UN PNJ ENNEMI
        TargetHealth target = other.GetComponent<TargetHealth>();
        if (target != null)
        {
            target.TakeDamage(damage, shooter);
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