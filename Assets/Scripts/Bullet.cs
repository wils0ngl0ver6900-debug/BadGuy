using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    public float speed = 50f;
    public float lifeTime = 3f;

    [HideInInspector] public int damage;
    [HideInInspector] public bool isEnemyBullet = false;
    [HideInInspector] public GameObject shooter;

    [Header("Effets d'impact")]
    [Tooltip("Laisse vide pour utiliser l'effet de sang généré automatiquement (VFXHelper).")]
    public GameObject customBloodEffectPrefab;

    // Destroy() est différé à la fin de la frame — si la balle chevauche plusieurs colliders
    // du MÊME PNJ en même temps (torse + bras qui se recouvrent, typique d'un rig de ragdoll),
    // OnTriggerEnter peut être appelé plusieurs fois pour une seule balle avant sa destruction
    // réelle. Ce verrou garantit qu'une balle ne touche qu'une fois, peu importe le nombre de
    // colliders superposés.
    private bool hasHit = false;

    void Start()
    {
        GetComponent<Rigidbody>().linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;
        if (hasHit) return; // Déjà traité un impact cette frame-ci, on ignore les colliders en trop

        // La balle ignore son propre tireur
        if (shooter != null)
        {
            if (other.gameObject == shooter || other.transform.IsChildOf(shooter.transform)) return;
        }

        hasHit = true;

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

        // 2. SI LA BALLE TOUCHE UN PNJ ENNEMI (GetComponentInParent : un membre du ragdoll
        // n'a pas forcément TargetHealth directement dessus, seulement la racine du PNJ —
        // comme c'est déjà le cas pour PlayerController/CarController juste au-dessus/en dessous)
        TargetHealth target = other.GetComponentInParent<TargetHealth>();
        bool hitBody = (target != null);
        if (hitBody)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            VFXHelper.SpawnBloodSplatter(hitPoint, transform.forward, customBloodEffectPrefab);

            target.TakeDamage(damage, shooter);
        }

        // 3. SI LA BALLE TOUCHE UNE VOITURE
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null)
        {
            car.TakeDamage(15f);
        }

        // 4. SINON (mur, voiture, tout obstacle qui n'est pas un corps) : étincelles d'impact
        if (!hitBody)
        {
            Vector3 impactPoint = other.ClosestPoint(transform.position);
            VFXHelper.SpawnImpactSparks(impactPoint, -transform.forward);
        }

        Destroy(gameObject);
    }
}