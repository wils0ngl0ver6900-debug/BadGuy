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

    private CarController car;
    private bool isTriggered = false;

    void Start()
    {
        car = GetComponent<CarController>();
    }

    void Update()
    {
        if (car != null && car.isEngineDead && !isTriggered)
        {
            isTriggered = true;
            StartCoroutine(ExplosionSequence());
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
            GameObject debris = GameObject.CreatePrimitive(Random.value > 0.5f ? PrimitiveType.Cube : PrimitiveType.Sphere);
            debris.transform.position = transform.position + Vector3.up * 1.5f + Random.insideUnitSphere * 1f;

            debris.transform.localScale = new Vector3(
                Random.Range(0.2f, 1.0f),
                Random.Range(0.05f, 0.3f),
                Random.Range(0.3f, 1.5f)
            );

            Renderer rend = debris.GetComponent<Renderer>();

            Material mat = null;
            if (debrisMaterialTemplate != null)
            {
                mat = new Material(debrisMaterialTemplate);
                mat.SetColor("_EmissiveColor", new Color(1f, 0.3f, 0f) * 15f);
                rend.material = mat;
            }

            Rigidbody rb = debris.AddComponent<Rigidbody>();
            rb.mass = 25f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            rb.AddExplosionForce(explosionForce * 0.8f, transform.position - Vector3.up * 0.5f, explosionRadius, 1.5f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 500f, ForceMode.Impulse);

            if (mat != null && GameManager.Instance != null)
            {
                // CORRECTIF : On passe le relais au GameManager car cette voiture va mourir dans une milliseconde !
                GameManager.Instance.StartCoroutine(DebrisCoolingAndCleanup(debris, mat, Random.Range(3.5f, 5f)));
            }
            else
            {
                Destroy(debris, Random.Range(3.5f, 5f));
            }
        }
    }

    private IEnumerator DebrisCoolingAndCleanup(GameObject debris, Material mat, float lifeTime)
    {
        float elapsed = 0f;
        Color startEmission = new Color(1f, 0.3f, 0f) * 15f;
        Color endEmission = Color.black;

        while (elapsed < lifeTime && debris != null)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / lifeTime;

            if (mat != null)
            {
                mat.SetColor("_EmissiveColor", Color.Lerp(startEmission, endEmission, normalizedTime));
            }

            yield return null;
        }

        // Les débris s'effaceront enfin !
        if (debris != null) Destroy(debris);
    }
}