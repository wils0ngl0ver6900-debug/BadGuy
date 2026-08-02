using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CarController))]
public class CarExplosionImproved : MonoBehaviour
{
    [Header("Paramètres d'Explosion 💥")]
    public float delayBeforeExplosion = 5f;
    public float explosionForce = 1800f;
    public float explosionRadius = 12f;
    public int explosionDamage = 75;

    [Header("Visuel HDRP 🎨")]
    [Tooltip("Glissez ici le matériau M_Debris_Template (HDRP/Lit avec Émission cochée)")]
    public Material debrisMaterialTemplate;

    [Tooltip("Glissez ici votre Prefab BigExplosion !")]
    public GameObject bigExplosionPrefab;

    [Tooltip("Optionnel : vrais morceaux de carrosserie/pièces détachées. Si vide, utilise des primitives (Cube/Sphère/Cylindre) comme avant.")]
    public GameObject[] debrisPrefabs;

    private CarController car;
    private CarInteraction carInteraction;
    private bool isTriggered = false;

    void Start()
    {
        car = GetComponent<CarController>();

        // NOUVEAU : La tête chercheuse pour trouver ton script de portière !
        carInteraction = FindMyCarInteraction();
    }

    // --- LA TÊTE CHERCHEUSE INFAILLIBLE ---
    private CarInteraction FindMyCarInteraction()
    {
        // 1. On cherche d'abord sur l'objet actuel (au cas où)
        CarInteraction ci = GetComponent<CarInteraction>();
        if (ci != null) return ci;

        // 2. On cherche dans les enfants (la portière, le trigger, etc.)
        ci = GetComponentInChildren<CarInteraction>();
        if (ci != null) return ci;

        // 3. BLINDAGE TOTAL : On fouille toute la scène pour trouver celui relié à CETTE voiture !
        CarInteraction[] allInteractions = FindObjectsOfType<CarInteraction>();
        foreach (CarInteraction interaction in allInteractions)
        {
            if (interaction.carController == this.car)
            {
                return interaction;
            }
        }

        Debug.LogError($"[CarExplosion] Impossible de trouver le script CarInteraction pour la voiture {gameObject.name} !");
        return null;
    }

    void Update()
    {
        if (car != null && car.isEngineDead && !isTriggered)
        {
            isTriggered = true;
            EjectPlayerIfInside();
            StartCoroutine(ExplosionSequence());
        }
    }

    // --- L'ÉJECTION D'URGENCE ---
    private void EjectPlayerIfInside()
    {
        if (carInteraction != null && car != null && car.isDrivenByPlayer)
        {
            // On force ta propre fonction de sortie !
            carInteraction.ExitCar();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification("<color=red>MOTEUR EN FEU ! ÉJECTION !</color>");
            }
        }
    }

    private IEnumerator ExplosionSequence()
    {
        yield return new WaitForSeconds(delayBeforeExplosion);

        if (bigExplosionPrefab != null)
        {
            Instantiate(bigExplosionPrefab, transform.position, Quaternion.identity);
        }

        SetupExplosionLight();
        VFXHelper.SpawnSparkAndSmokeBurst(transform.position);

        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider hit in colliders)
        {
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null && hit.gameObject != this.gameObject)
            {
                rb.AddExplosionForce(explosionForce, transform.position - Vector3.up * 1f, explosionRadius, 3f, ForceMode.Impulse);
            }

            TargetHealth target = hit.GetComponent<TargetHealth>();
            if (target != null) target.TakeDamage(explosionDamage);

            PlayerController player = hit.GetComponentInParent<PlayerController>();
            if (player != null) player.TakeDamage(explosionDamage);
        }

        GenerateImprovedDebris();

        Destroy(gameObject);
    }

    private void SetupExplosionLight()
    {
        GameObject lightObj = new GameObject("ExplosionLight");
        lightObj.transform.position = transform.position + Vector3.up * 1f;
        Light light = lightObj.AddComponent<Light>();

        light.type = LightType.Point;
        light.color = new Color(1f, 0.4f, 0f);
        light.intensity = 150000f;
        light.range = 30f;

        Destroy(lightObj, 0.15f);
    }

    private void GenerateImprovedDebris()
    {
        int debrisCount = Random.Range(8, 15);

        for (int i = 0; i < debrisCount; i++)
        {
            if (debrisPrefabs != null && debrisPrefabs.Length > 0)
            {
                SpawnRealDebrisPiece();
            }
            else
            {
                SpawnPrimitiveDebrisPiece();
            }
        }
    }

    // Utilise de vrais morceaux (si assignés dans l'Inspector) — bien plus "pro" visuellement
    // qu'une primitive, à condition d'avoir des meshes de débris à disposition.
    private void SpawnRealDebrisPiece()
    {
        GameObject prefab = debrisPrefabs[Random.Range(0, debrisPrefabs.Length)];
        GameObject debris = Instantiate(prefab, transform.position + Vector3.up * 1.5f + Random.insideUnitSphere * 1f, Random.rotation);

        Rigidbody rb = debris.GetComponent<Rigidbody>();
        if (rb == null) rb = debris.AddComponent<Rigidbody>();
        rb.mass = Random.Range(15f, 30f);
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.AddExplosionForce(explosionForce * 0.8f, transform.position - Vector3.up * 0.5f, explosionRadius, 1.5f, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 500f, ForceMode.Impulse);

        Destroy(debris, Random.Range(8f, 12f)); // Les vrais morceaux restent plus longtemps au sol (plus discrets qu'un cube qui brille)
    }

    // Solution de secours si aucun vrai morceau n'est fourni : primitives Unity, mais avec
    // plus de variété (ajout du Cylindre) et sans créer 15 instances de Material séparées
    // à chaque explosion (MaterialPropertyBlock à la place — beaucoup plus léger).
    private void SpawnPrimitiveDebrisPiece()
    {
        PrimitiveType[] shapes = { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder };
        GameObject debris = GameObject.CreatePrimitive(shapes[Random.Range(0, shapes.Length)]);
        debris.transform.position = transform.position + Vector3.up * 1.5f + Random.insideUnitSphere * 1f;
        debris.transform.rotation = Random.rotation;

        debris.transform.localScale = new Vector3(
            Random.Range(0.2f, 1.0f),
            Random.Range(0.05f, 0.3f),
            Random.Range(0.3f, 1.5f)
        );

        Renderer rend = debris.GetComponent<Renderer>();
        Color emissionColor = new Color(1f, 0.3f, 0f) * 15f;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        if (debrisMaterialTemplate != null)
        {
            rend.sharedMaterial = debrisMaterialTemplate;
        }
        rend.GetPropertyBlock(block);
        block.SetColor("_EmissiveColor", emissionColor);
        rend.SetPropertyBlock(block);

        Rigidbody rb = debris.AddComponent<Rigidbody>();
        rb.mass = 25f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.AddExplosionForce(explosionForce * 0.8f, transform.position - Vector3.up * 0.5f, explosionRadius, 1.5f, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 500f, ForceMode.Impulse);

        if (debrisMaterialTemplate != null && GameManager.Instance != null)
        {
            GameManager.Instance.StartCoroutine(DebrisCoolingAndCleanup(debris, Random.Range(3.5f, 5f)));
        }
        else
        {
            Destroy(debris, Random.Range(3.5f, 5f));
        }
    }

    private IEnumerator DebrisCoolingAndCleanup(GameObject debris, float lifeTime)
    {
        float elapsed = 0f;
        Color startEmission = new Color(1f, 0.3f, 0f) * 15f;
        Color endEmission = Color.black;
        Renderer rend = debris != null ? debris.GetComponent<Renderer>() : null;
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        while (elapsed < lifeTime && debris != null)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / lifeTime;

            if (rend != null)
            {
                rend.GetPropertyBlock(block);
                block.SetColor("_EmissiveColor", Color.Lerp(startEmission, endEmission, normalizedTime));
                rend.SetPropertyBlock(block);
            }

            yield return null;
        }

        if (debris != null) Destroy(debris);
    }
}