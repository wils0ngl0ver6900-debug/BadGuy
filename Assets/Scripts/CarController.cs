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
    public float lowSpeedSteerAngle = 70f;
    public float highSpeedSteerAngle = 25f;

    [Header("Adhérence (Le secret Pro) 🧲")]
    [Range(0f, 1f)] public float gripLevel = 0.95f;
    [Range(0f, 1f)] public float driftGrip = 0.3f;

    [Header("Physique Avancée ⚖️")]
    public float downforce = 60f;
    public float centerOfMassOffset = -0.5f;

    [Header("Chop Shop & Dégâts 💥")]
    public string carModelName = "Berline Classique";
    public int baseValue = 1500;
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Effets Visuels (Dégâts) 💥")]
    public GameObject smokeEffectPrefab;
    public Transform hoodPosition;

    [Header("Effets Visuels (Drift) 💨")]
    public ParticleSystem[] tireSmokeParticles;
    public TrailRenderer[] skidMarks;

    // États
    [HideInInspector] public bool isDrivenByPlayer = false;
    [HideInInspector] public bool isDrivenByAI = false;
    [HideInInspector] public float moveInput;
    [HideInInspector] public float turnInput;
    [HideInInspector] public bool isHandbraking = false;

    private Rigidbody rb;
    private bool isEngineDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, centerOfMassOffset, 0);
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // --- CORRECTION DE LA FUMÉE FANTÔME ---
        // On coupe tout, on nettoie les particules résiduelles et on ferme la vanne
        foreach (ParticleSystem smoke in tireSmokeParticles)
        {
            if (smoke != null)
            {
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var em = smoke.emission;
                em.enabled = false;
            }
        }

        foreach (TrailRenderer trail in skidMarks)
        {
            if (trail != null) trail.emitting = false;
        }
    }

    void Update()
    {
        if (isEngineDead)
        {
            moveInput = 0;
            turnInput = 0;
            isHandbraking = false;
        }
        else if (isDrivenByPlayer)
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

        HandleEffects();
    }

    void FixedUpdate()
    {
        if (!isDrivenByPlayer && !isDrivenByAI && rb.linearVelocity.magnitude < 0.1f && !isEngineDead) return;

        ProcessEngine();
        ProcessSteering();
        ApplyArcadeGrip();
        ApplyDownforce();
        AutoRighting();
    }

    private void HandleEffects()
    {
        if (isEngineDead)
        {
            SetTireEffects(false);
            return;
        }

        bool isDrifting = false;

        // Détection propre du drift
        if ((isDrivenByPlayer || isDrivenByAI) && rb.linearVelocity.magnitude > 2f)
        {
            float rightSpeed = Mathf.Abs(Vector3.Dot(transform.right, rb.linearVelocity));
            float forwardSpeed = rb.linearVelocity.magnitude;

            isDrifting = (isHandbraking && forwardSpeed > 5f) || rightSpeed > 3f;
        }

        SetTireEffects(isDrifting);
    }

    private void SetTireEffects(bool active)
    {
        foreach (TrailRenderer trail in skidMarks)
        {
            if (trail != null) trail.emitting = active;
        }

        // --- GESTION PROPRE DU PLAY / STOP ---
        foreach (ParticleSystem smoke in tireSmokeParticles)
        {
            if (smoke != null)
            {
                var emission = smoke.emission;
                if (emission.enabled != active)
                {
                    emission.enabled = active;
                    if (active) smoke.Play();
                    else smoke.Stop();
                }
            }
        }
    }

    private void ProcessEngine()
    {
        float speed = rb.linearVelocity.magnitude;

        if (isEngineDead)
        {
            rb.AddForce(-rb.linearVelocity.normalized * (brakingForce * 0.5f), ForceMode.Acceleration);
            return;
        }

        if (isHandbraking)
        {
            rb.AddForce(-rb.linearVelocity.normalized * brakingForce, ForceMode.Acceleration);
            return;
        }

        if (Mathf.Abs(moveInput) > 0.1f)
        {
            if (speed < maxSpeed)
            {
                float speedFactor = 1f - (speed / maxSpeed);
                float accel = moveInput > 0 ? accelerationForce : reverseForce;
                rb.AddForce(transform.forward * moveInput * accel * Mathf.Max(speedFactor, 0.3f), ForceMode.Acceleration);
            }
        }
        else
        {
            rb.AddForce(-rb.linearVelocity.normalized * (brakingForce * 0.2f), ForceMode.Acceleration);
        }
    }

    private void ProcessSteering()
    {
        if (isEngineDead) return;

        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        float absoluteSpeed = Mathf.Abs(forwardSpeed);

        if (absoluteSpeed > 0.5f)
        {
            float directionMultiplier = Mathf.Sign(forwardSpeed);
            float speedFactor = Mathf.Clamp01(absoluteSpeed / maxSpeed);
            float currentSteerAngle = Mathf.Lerp(lowSpeedSteerAngle, highSpeedSteerAngle, speedFactor);

            float turnAmount = turnInput * currentSteerAngle * directionMultiplier * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }

    private void ApplyArcadeGrip()
    {
        float rightSpeed = Vector3.Dot(transform.right, rb.linearVelocity);
        float activeGrip = isHandbraking ? driftGrip : gripLevel;
        Vector3 gripForce = -transform.right * rightSpeed * activeGrip;
        rb.AddForce(gripForce, ForceMode.VelocityChange);
    }

    private void ApplyDownforce()
    {
        float speed = rb.linearVelocity.magnitude;
        rb.AddForce(Vector3.down * downforce * speed, ForceMode.Force);
    }

    private void AutoRighting()
    {
        if (transform.up.y < 0.1f && rb.linearVelocity.magnitude < 5f)
        {
            Quaternion targetRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 2f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // --- CORRECTION DU BUG DE DÉGÂT SUR LE FREIN À MAIN ---
        // Si le choc vient d'en bas (la route / le sol), on IGNORE complétement !
        if (collision.contacts.Length > 0 && collision.contacts[0].normal.y > 0.5f)
        {
            return;
        }

        float impactForce = collision.relativeVelocity.magnitude;

        // J'ai augmenté la tolérance de 4f à 6f pour éviter de casser la voiture au moindre poteau
        if (impactForce > 6f)
        {
            float damage = impactForce * 1.5f;
            currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);

            if (currentHealth <= 0 && !isEngineDead)
            {
                isEngineDead = true;

                if (isDrivenByPlayer && UIManager.Instance != null)
                {
                    UIManager.Instance.ShowNotification("<color=red>Moteur détruit ! Véhicule HS.</color>");
                }

                if (smokeEffectPrefab != null && hoodPosition != null)
                {
                    GameObject smoke = Instantiate(smokeEffectPrefab, hoodPosition);
                    smoke.transform.localPosition = Vector3.zero;
                    smoke.transform.localRotation = smokeEffectPrefab.transform.rotation;
                }
            }
        }
    }
}