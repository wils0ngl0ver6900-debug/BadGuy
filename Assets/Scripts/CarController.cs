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

    [Header("Propriété 🔑")]
    [Tooltip("Vrai seulement si le joueur a payé ce véhicule (via CarForSale). Sert de garde-fou au garage : une voiture volée ne peut pas y être rangée.")]
    public bool isPlayerOwned = false;

    [Tooltip("Coché : ce véhicule ne subit plus aucun dégât (TakeDamage ne fait rien). Utile pour un prefab dédié à une course, sans affecter les autres voitures.")]
    public bool damageImmune = false;

    [Header("Effets Visuels (Dégâts) 💥")]
    public GameObject smokeEffectPrefab;
    public Transform hoodPosition;

    [Header("Effets Visuels (Drift) 💨")]
    public ParticleSystem[] tireSmokeParticles;
    public TrailRenderer[] skidMarks;

    [HideInInspector] public bool isDrivenByPlayer = false;
    [HideInInspector] public bool isDrivenByAI = false;
    [HideInInspector] public bool inputLocked = false;
    [HideInInspector] public float moveInput;
    [HideInInspector] public float turnInput;
    [HideInInspector] public bool isHandbraking = false;

    private Rigidbody rb;
    [HideInInspector] public bool isEngineDead = false;
    private float spawnProtectionTimer = 2f;
    // Anti-répétition PAR CIBLE (pas un seul minuteur global) : sans ça, percuter plusieurs
    // piétons différents en moins de 0.2s (ex: traverser un petit groupe) sautait le
    // traitement — dégâts, poussée, restitution de vitesse — pour tous sauf le premier,
    // et la voiture encaissait alors une collision physique brute non compensée, comme un
    // mur, sur les suivants.
    private System.Collections.Generic.Dictionary<GameObject, float> lastHitTimeByTarget = new System.Collections.Generic.Dictionary<GameObject, float>();
    // Vitesse mémorisée juste avant que la physique ne résolve les collisions de cette
    // frame — sert de référence "vitesse d'avant impact" dans OnCollisionEnter, pour
    // pouvoir restituer la vitesse perdue contre un piéton (voir OnCollisionEnter).
    private Vector3 velocityBeforePhysicsStep;

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
        else if (inputLocked)
        {
            // Bloque le joueur (ex: compte à rebours avant une course) sans désactiver le
            // script entier — la caméra/HUD "en voiture" restent actifs normalement.
            moveInput = 0;
            turnInput = 0;
            isHandbraking = true;
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

        // Capturé en dernier : c'est la vitesse "juste avant" que Unity ne résolve les
        // collisions du prochain pas de physique.
        velocityBeforePhysicsStep = rb.linearVelocity;
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

    private float lastDirectionMultiplier = 1f;

    private void ProcessSteering()
    {
        if (isEngineDead) return;

        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        float absoluteSpeed = Mathf.Abs(forwardSpeed);

        if (absoluteSpeed > 0.1f)
        {
            // Le signe de forwardSpeed peut basculer d'une frame à l'autre quand la vitesse
            // est proche de zéro (bruit numérique) — typiquement en tournant "sur place",
            // où le freinage/l'adhérence ramènent sans cesse la vitesse près de zéro. On ne
            // met à jour la direction que si la vitesse est clairement au-dessus de ce bruit
            // (0.5 plutôt que 0.1), sinon on garde la dernière direction stable — plutôt que
            // de recalculer un signe qui peut changer sans raison d'une frame à l'autre.
            if (absoluteSpeed > 0.5f)
            {
                lastDirectionMultiplier = Mathf.Sign(forwardSpeed);
            }

            float speedFactor = Mathf.Clamp01(absoluteSpeed / maxSpeed);
            float currentSteerAngle = Mathf.Lerp(lowSpeedSteerAngle, highSpeedSteerAngle, speedFactor);

            float turnAmount = turnInput * currentSteerAngle * lastDirectionMultiplier * Time.fixedDeltaTime;
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
        if (damageImmune || isEngineDead || spawnProtectionTimer > 0f) return;

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

        // ---> AJOUT POUR LA QUÊTE <---
        if (QuestManager.Instance != null)
            QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.DetruireVoiture, 1, carModelName);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (spawnProtectionTimer > 0f) return;
        if (collision.contacts.Length > 0 && collision.contacts[0].normal.y > 0.5f) return;

        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce < 2f) return;

        PlayerController player = collision.gameObject.GetComponentInParent<PlayerController>();
        // TargetHealth est le point commun à TOUS les piétons du jeu (flics, gangs, civils
        // ET clients drogue) — contrairement à NPCBrain, absent sur DrugClientNPC. Avant,
        // seuls les NPCBrain passaient par le chemin "isHuman" (formule d'éjection calibrée
        // + restitution de vitesse de la voiture) ; les clients drogue tombaient dans le
        // chemin générique "isLightObject" sans ces deux traitements, d'où l'impression
        // qu'ils encaissaient/pesaient différemment des flics.
        TargetHealth targetHealth = collision.gameObject.GetComponentInParent<TargetHealth>();

        if (player != null && isDrivenByPlayer) return;

        bool isLightObject = collision.rigidbody != null && collision.rigidbody.mass < 50f;

        bool isHuman = player != null || targetHealth != null;

        if (isHuman || isLightObject)
        {
            // Clé stable pour identifier LA cible touchée (pas juste le collider précis,
            // qui peut être un os différent à chaque frame sur un même PNJ) : la racine du
            // PlayerController, ou celle du TargetHealth, ou à défaut l'objet du Rigidbody.
            GameObject targetKey = player != null ? player.gameObject
                                  : (targetHealth != null ? targetHealth.gameObject
                                  : (collision.rigidbody != null ? collision.rigidbody.gameObject : collision.gameObject));

            if (lastHitTimeByTarget.TryGetValue(targetKey, out float lastHit) && Time.time - lastHit < 0.2f) return;
            lastHitTimeByTarget[targetKey] = Time.time;

            float carDamage = Mathf.Clamp(impactForce * 0.05f, 0f, 5f);
            if (carDamage > 1f) TakeDamage(carDamage);

            if (isHuman)
            {
                int meatDamage = Mathf.RoundToInt(Mathf.Pow(impactForce, 1.4f));

                // Vitesse de la voiture juste avant l'impact (voir FixedUpdate) : sert de
                // référence à la fois pour calibrer la distance d'éjection du piéton et
                // pour restituer à la voiture ce qu'elle a perdu en heurtant un corps —
                // sans ça, un piéton (masse "infinie" pendant qu'il marche, voir NPCBrain)
                // stoppe net la voiture comme un mur au moment précis de l'impact.
                float speedBeforeImpact = velocityBeforePhysicsStep.magnitude;
                Vector3 pushForce = PedestrianImpact.CalculateEjectionVelocity(speedBeforeImpact, velocityBeforePhysicsStep);

                if (player != null)
                {
                    player.TakeDamage(meatDamage);
                    if (player.currentHealth > 0) player.Knockdown(pushForce);
                }
                else if (targetHealth != null)
                {
                    GameObject attacker = isDrivenByPlayer ? GameObject.FindGameObjectWithTag("Player") : this.gameObject;
                    targetHealth.TakeDamage(meatDamage, attacker);

                    if (targetHealth.currentHealth > 0)
                    {
                        targetHealth.TemporaryRagdoll(pushForce);
                    }
                }

                // On restitue la majeure partie de la vitesse perdue (garde un peu de
                // résistance pour que l'impact reste ressenti, sans le coup d'arrêt complet).
                if (velocityBeforePhysicsStep.sqrMagnitude > 0.01f)
                {
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, velocityBeforePhysicsStep, 0.85f);
                }
            }
        }
        else
        {
            if (impactForce > 6f)
            {
                float damage = impactForce * 1.5f;
                TakeDamage(damage);
            }
        }
    }
}