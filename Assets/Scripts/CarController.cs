using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Moteur & Vitesse 🏎️")]
    public float maxSpeed = 35f;
    public float accelerationForce = 60f;
    public float reverseForce = 25f;
    public float brakingForce = 40f;

    [Header("Direction Dynamique 🛞")]
    [Tooltip("Braquage max à l'arrêt ou basse vitesse")]
    public float lowSpeedSteerAngle = 70f;
    [Tooltip("Braquage réduit à haute vitesse (évite les têtes-à-queue)")]
    public float highSpeedSteerAngle = 25f;

    [Header("Adhérence (Le secret Pro) 🧲")]
    [Tooltip("1 = Sur des rails (F1), 0 = Caisse à savon sur glace")]
    [Range(0f, 1f)] public float gripLevel = 0.95f;
    [Tooltip("Adhérence quand on tire le frein à main (Espace)")]
    [Range(0f, 1f)] public float driftGrip = 0.3f;

    [Header("Physique Avancée ⚖️")]
    [Tooltip("Plaque la voiture au sol à haute vitesse")]
    public float downforce = 60f;
    public float centerOfMassOffset = -0.5f;

    [Header("Chop Shop & Dégâts 💥")]
    public string carModelName = "Berline Classique";
    public int baseValue = 1500;
    public float maxHealth = 100f;
    public float currentHealth;

    // États
    [HideInInspector] public bool isDrivenByPlayer = false;
    [HideInInspector] public bool isDrivenByAI = false;
    [HideInInspector] public float moveInput;
    [HideInInspector] public float turnInput;
    [HideInInspector] public bool isHandbraking = false;

    private Rigidbody rb;

    void Start()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();

        // 1. Abaisse le centre de gravité pour la stabilité
        rb.centerOfMass = new Vector3(0, centerOfMassOffset, 0);

        // 2. REND LA CAMÉRA FLUIDE ! (Secret de pro)
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        if (isDrivenByPlayer)
        {
            moveInput = Input.GetAxis("Vertical");
            turnInput = Input.GetAxis("Horizontal");
            isHandbraking = Input.GetKey(KeyCode.Space);
        }
        else if (!isDrivenByAI)
        {
            moveInput = 0;
            turnInput = 0;
            isHandbraking = true;
        }
    }

    void FixedUpdate()
    {
        if (!isDrivenByPlayer && !isDrivenByAI && rb.linearVelocity.magnitude < 0.1f) return;

        ProcessEngine();
        ProcessSteering();
        ApplyArcadeGrip(); // La fin de la caisse à savon
        ApplyDownforce();  // Le poids de la voiture
    }

    private void ProcessEngine()
    {
        float speed = rb.linearVelocity.magnitude;

        if (isHandbraking)
        {
            // Frein à main violent
            rb.AddForce(-rb.linearVelocity.normalized * brakingForce, ForceMode.Acceleration);
            return;
        }

        if (Mathf.Abs(moveInput) > 0.1f)
        {
            if (speed < maxSpeed)
            {
                // La puissance du moteur baisse doucement quand on approche de la vitesse max (réalisme)
                float speedFactor = 1f - (speed / maxSpeed);
                float accel = moveInput > 0 ? accelerationForce : reverseForce;

                // On garantit un minimum de 30% de puissance même à fond pour ne pas couper le moteur net
                rb.AddForce(transform.forward * moveInput * accel * Mathf.Max(speedFactor, 0.3f), ForceMode.Acceleration);
            }
        }
        else
        {
            // Frein moteur naturel (la voiture ralentit toute seule si on lâche tout)
            rb.AddForce(-rb.linearVelocity.normalized * (brakingForce * 0.2f), ForceMode.Acceleration);
        }
    }

    private void ProcessSteering()
    {
        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        float absoluteSpeed = Mathf.Abs(forwardSpeed);

        // Impossible de tourner si on est à l'arrêt
        if (absoluteSpeed > 0.5f)
        {
            float directionMultiplier = Mathf.Sign(forwardSpeed);

            // Calcule la sensibilité de la direction en fonction de la vitesse
            float speedFactor = Mathf.Clamp01(absoluteSpeed / maxSpeed);
            float currentSteerAngle = Mathf.Lerp(lowSpeedSteerAngle, highSpeedSteerAngle, speedFactor);

            // Applique la rotation mathématiquement
            float turnAmount = turnInput * currentSteerAngle * directionMultiplier * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }

    private void ApplyArcadeGrip()
    {
        // 1. On calcule exactement à quelle vitesse la voiture "glisse" sur le côté (dérapage)
        float rightSpeed = Vector3.Dot(transform.right, rb.linearVelocity);

        // 2. On choisit l'adhérence (Normal vs Frein à main)
        float activeGrip = isHandbraking ? driftGrip : gripLevel;

        // 3. LA MAGIE : On utilise ForceMode.VelocityChange pour "effacer" instantanément cette glissade
        Vector3 gripForce = -transform.right * rightSpeed * activeGrip;
        rb.AddForce(gripForce, ForceMode.VelocityChange);
    }

    private void ApplyDownforce()
    {
        // Plus on va vite, plus on est lourd sur la route (Aérodynamisme)
        float speed = rb.linearVelocity.magnitude;
        rb.AddForce(-transform.up * downforce * speed, ForceMode.Force);
    }

    private void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce > 4f)
        {
            float damage = impactForce * 1.5f;
            currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);

            if (currentHealth <= 0 && isDrivenByPlayer && UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification("<color=red>Moteur détruit ! Véhicule HS.</color>");
            }
        }
    }
}