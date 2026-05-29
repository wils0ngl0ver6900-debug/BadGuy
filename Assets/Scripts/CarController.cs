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

    [HideInInspector] public bool isDrivenByPlayer = false;
    [HideInInspector] public bool isDrivenByAI = false;
    [HideInInspector] public float moveInput;
    [HideInInspector] public float turnInput;
    [HideInInspector] public bool isHandbraking = false;

    private Rigidbody rb;
    [HideInInspector] public bool isEngineDead = false;
    private float spawnProtectionTimer = 2f;

    private float lastHumanHitTime = 0f;

    void Start()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, centerOfMassOffset, 0);
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

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
        if (spawnProtectionTimer > 0f) spawnProtectionTimer -= Time.deltaTime;

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

        if (rb.linearVelocity.magnitude > maxSpeed * 1.5f)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

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
        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);

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

        if (moveInput < -0.1f)
        {
            if (forwardSpeed > 1f)
            {
                rb.AddForce(-rb.linearVelocity.normalized * brakingForce, ForceMode.Acceleration);
            }
            else
            {
                float speedFactor = 1f - (speed / (maxSpeed * 0.5f));
                rb.AddForce(transform.forward * moveInput * reverseForce * Mathf.Max(speedFactor, 0.3f), ForceMode.Acceleration);
            }
        }
        else if (moveInput > 0.1f)
        {
            if (forwardSpeed < -1f)
            {
                rb.AddForce(-rb.linearVelocity.normalized * brakingForce, ForceMode.Acceleration);
            }
            else
            {
                float speedFactor = 1f - (speed / maxSpeed);
                rb.AddForce(transform.forward * moveInput * accelerationForce * Mathf.Max(speedFactor, 0.3f), ForceMode.Acceleration);
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

        if (absoluteSpeed > 0.1f)
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
        float clampedSpeed = Mathf.Clamp(speed, 0f, maxSpeed);
        rb.AddForce(Vector3.down * downforce * clampedSpeed, ForceMode.Force);
    }

    private void AutoRighting()
    {
        if (transform.up.y < 0.1f && rb.linearVelocity.magnitude < 5f)
        {
            Quaternion targetRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 2f);
        }
    }

    public void TakeDamage(float amount)
    {
        if (isEngineDead || spawnProtectionTimer > 0f) return;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
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

    private void OnCollisionEnter(Collision collision)
    {
        if (spawnProtectionTimer > 0f) return;
        if (collision.contacts.Length > 0 && collision.contacts[0].normal.y > 0.5f) return;

        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce < 2f) return;

        NPCBrain npc = collision.gameObject.GetComponentInParent<NPCBrain>();
        PlayerController player = collision.gameObject.GetComponentInParent<PlayerController>();

        // --- LE CORRECTIF EST ICI : L'ANTI-COUP DU LAPIN ---
        // Si la voiture percute le joueur, mais que ce joueur est LE CONDUCTEUR actuel de la voiture, on ignore l'impact !
        if (player != null && isDrivenByPlayer) return;

        bool isLightObject = collision.rigidbody != null && collision.rigidbody.mass < 50f;

        bool isHuman = false;
        if (player != null) isHuman = true;
        if (npc != null && npc.locomotion == NPCBrain.Locomotion.Pieton) isHuman = true;

        if (isHuman || isLightObject)
        {
            if (Time.time - lastHumanHitTime < 0.2f) return;
            lastHumanHitTime = Time.time;

            float carDamage = Mathf.Clamp(impactForce * 0.05f, 0f, 5f);
            if (carDamage > 1f) TakeDamage(carDamage);

            if (isHuman)
            {
                int meatDamage = Mathf.RoundToInt(Mathf.Pow(impactForce, 1.4f));
                Vector3 pushForce = (rb.linearVelocity.normalized + (Vector3.up * 0.4f)) * impactForce * 0.4f;

                if (player != null)
                {
                    player.TakeDamage(meatDamage);
                    if (player.currentHealth > 0) player.Knockdown(pushForce);
                }
                if (npc != null)
                {
                    TargetHealth health = npc.GetComponent<TargetHealth>();
                    if (health != null)
                    {
                        GameObject attacker = isDrivenByPlayer ? GameObject.FindGameObjectWithTag("Player") : this.gameObject;
                        health.TakeDamage(meatDamage, attacker);

                        if (health.currentHealth > 0)
                        {
                            health.TemporaryRagdoll(pushForce);
                        }
                    }
                }
            }
        }
        else
        {
            // Les accidents contre les murs endommagent toujours la voiture
            if (impactForce > 6f)
            {
                float damage = impactForce * 1.5f;
                TakeDamage(damage);
            }
        }
    }
}