using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Réglages Moteur 🏎️")]
    public float acceleration = 30f;
    public float steering = 80f;

    [Header("Chop Shop & Dégâts 💥")]
    public string carModelName = "Berline Classique";
    public int baseValue = 1500; // Prix si la voiture est intacte
    public float maxHealth = 100f;
    public float currentHealth;

    // États de conduite
    [HideInInspector] public bool isDrivenByPlayer = false;
    [HideInInspector] public bool isDrivenByAI = false;

    // Inputs utilisés par le Joueur ou l'IA
    [HideInInspector] public float moveInput;
    [HideInInspector] public float turnInput;

    private Rigidbody rb;

    void Start()
    {
        currentHealth = maxHealth; // La voiture spawn avec 100% de vie
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -1f, 0); // Évite de faire des tonneaux
        rb.linearDamping = 2f; // Anciennement rb.drag
        rb.angularDamping = 2f;
    }

    void Update()
    {
        // Si c'est le joueur, on écoute le clavier
        if (isDrivenByPlayer)
        {
            moveInput = Input.GetAxis("Vertical");
            turnInput = Input.GetAxis("Horizontal");
        }
        // Si personne ne conduit, on met les pédales à 0
        else if (!isDrivenByAI)
        {
            moveInput = 0;
            turnInput = 0;
        }
        // Si c'est l'IA, on laisse le script CarAI dicter les variables
    }

    void FixedUpdate()
    {
        if (!isDrivenByPlayer && !isDrivenByAI) return;

        if (Mathf.Abs(moveInput) > 0.1f)
        {
            rb.AddRelativeForce(Vector3.forward * moveInput * acceleration, ForceMode.Acceleration);

            float turnMultiplier = moveInput > 0 ? 1 : -1;
            Quaternion turnRotation = Quaternion.Euler(0f, turnInput * steering * turnMultiplier * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }
    // Calcule la violence des chocs pour réduire le prix de revente
    private void OnCollisionEnter(Collision collision)
    {
        // On ignore les frottements légers, on ne compte que les vrais chocs
        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce > 3f)
        {
            float damage = impactForce * 2f; // Plus on tape fort, plus ça coûte cher
            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // Bloque entre 0 et 100
        }
    }
}