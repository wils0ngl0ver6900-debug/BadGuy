using UnityEngine;

[RequireComponent(typeof(CarController))]
public class CarAI : MonoBehaviour
{
    [Header("Navigation routière")]
    public TrafficNode currentNode;
    public float waypointThreshold = 5f;

    [Header("Détection d'obstacles (Les Yeux)")]
    public float sensorLength = 6f;
    public float sensorFrontOffset = 2.5f;
    public LayerMask obstacleMask;

    [Header("Ajustements IA 🧠")]
    public float steerSmoothing = 4f;

    [HideInInspector] public Transform chaseTarget = null;

    private CarController carController;
    private Rigidbody rb;
    private bool isBraking = false;

    // --- MANOEUVRES ET BLOCAGE ---
    private float stuckTimer = 0f;
    private bool isReversing = false;

    // --- ESQUIVE ---
    private float obstacleTimer = 0f;
    private bool isAvoidingObstacle = false;
    private float avoidLockTimer = 0f;
    private float lockedAvoidDirection = 0f;
    private bool lockedHumanDanger = false;
    private float closestObstacleDist = 10f; // NOUVEAU : Mémoire de la distance

    private float ramTimer = 0f;

    void Start()
    {
        carController = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();
        carController.isDrivenByAI = true;
    }

    void Update()
    {
        if (!carController.isDrivenByAI) return;

        // --- 1. GESTION DE LA MARCHE ARRIÈRE (Le Créneau d'urgence) ---
        if (isReversing)
        {
            stuckTimer += Time.deltaTime;

            // On recule pendant exactement 1.5 seconde
            if (stuckTimer > 1.5f)
            {
                isReversing = false;
                stuckTimer = 0f;
                avoidLockTimer = 0.6f; // On force l'esquive en repartant en avant pour contourner
            }
            else
            {
                carController.moveInput = -1f; // Recule !
                // On contre-braque pour dégager le nez de la voiture
                carController.turnInput = Mathf.MoveTowards(carController.turnInput, -lockedAvoidDirection, Time.deltaTime * steerSmoothing);
                carController.isHandbraking = false;
                return; // On ne lit ni les lasers ni la route pendant la manoeuvre !
            }
        }
        else
        {
            // Vérification anti-bug : Si la voiture force contre un trottoir bas non-détecté
            if (Mathf.Abs(carController.moveInput) > 0.1f && rb.linearVelocity.magnitude < 1.0f)
            {
                stuckTimer += Time.deltaTime;
            }
            else
            {
                stuckTimer -= Time.deltaTime;
                stuckTimer = Mathf.Max(0f, stuckTimer);
            }

            // Si elle force dans le vide pendant 1.5s, on déclenche la marche arrière
            if (stuckTimer > 1.5f)
            {
                isReversing = true;
                stuckTimer = 0f;
                avoidLockTimer = 0f;
                isAvoidingObstacle = false;
            }
        }

        // --- 2. LECTURE DES LASERS ---
        CheckSensors();

        // Freinage classique
        if (isBraking && !isAvoidingObstacle)
        {
            carController.moveInput = 0f;
            carController.turnInput = 0f;
            carController.isHandbraking = true;
            return;
        }

        carController.isHandbraking = false;

        // Si on esquive, on laisse CheckSensors piloter
        if (isAvoidingObstacle) return;

        // --- 3. CONDUITE NORMALE ---
        Drive();
    }

    void Drive()
    {
        if (chaseTarget == null && currentNode == null) return;

        // Technique de la police qui percute le joueur
        if (ramTimer > 0f)
        {
            ramTimer -= Time.deltaTime;
            carController.moveInput = -1f;
            carController.turnInput = 0f;
            return;
        }

        Vector3 targetPos;

        if (chaseTarget != null)
        {
            targetPos = chaseTarget.position;
        }
        else
        {
            targetPos = currentNode.transform.position;
            float dist = Vector3.Distance(transform.position, targetPos);
            if (dist < waypointThreshold && currentNode.nextNodes.Count > 0)
            {
                currentNode = currentNode.nextNodes[Random.Range(0, currentNode.nextNodes.Count)];
            }
        }

        Vector3 localTarget = transform.InverseTransformPoint(targetPos);
        float angle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float targetTurn = Mathf.Clamp(angle / 45f, -1f, 1f);

        carController.turnInput = Mathf.MoveTowards(carController.turnInput, targetTurn, Time.deltaTime * steerSmoothing);

        float angleAbs = Mathf.Abs(angle);
        float currentSpeed = rb.linearVelocity.magnitude;

        if (angleAbs > 30f && currentSpeed > 10f) carController.moveInput = -0.8f;
        else if (angleAbs > 15f) carController.moveInput = 0.40f;
        else carController.moveInput = 1f - (Mathf.Abs(carController.turnInput) * 0.2f);
    }

    void CheckSensors()
    {
        if (avoidLockTimer > 0f)
        {
            avoidLockTimer -= Time.deltaTime;
            isAvoidingObstacle = true;
            isBraking = false;

            carController.moveInput = lockedHumanDanger ? -1f : 0.4f;
            carController.turnInput = Mathf.Lerp(carController.turnInput, lockedAvoidDirection, Time.deltaTime * steerSmoothing * 1.5f);
            return;
        }

        isAvoidingObstacle = false;
        isBraking = false;
        closestObstacleDist = sensorLength; // On réinitialise la distance

        Vector3 sensorStartPos = transform.position + (transform.forward * sensorFrontOffset) + (Vector3.up * 0.5f);

        Vector3 frontCenter = transform.forward;
        Vector3 frontLeftInner = Quaternion.Euler(0, -15, 0) * transform.forward;
        Vector3 frontRightInner = Quaternion.Euler(0, 15, 0) * transform.forward;
        Vector3 frontLeftOuter = Quaternion.Euler(0, -35, 0) * transform.forward;
        Vector3 frontRightOuter = Quaternion.Euler(0, 35, 0) * transform.forward;

        bool humanInDanger = false;

        bool CheckRay(Vector3 dir, float length, out bool isHumanObstacle)
        {
            isHumanObstacle = false;

            if (Physics.Raycast(sensorStartPos, dir, out RaycastHit hit, length, obstacleMask))
            {
                if (hit.collider.transform.root == transform.root) return false;

                NPCBrain npc = hit.collider.GetComponentInParent<NPCBrain>();
                PlayerController pc = hit.collider.GetComponentInParent<PlayerController>();
                bool isHuman = (npc != null || pc != null);

                if (isHuman) isHumanObstacle = true;
                if (!isHuman && hit.normal.y > 0.8f) return false;

                if (chaseTarget != null)
                {
                    if (pc != null && hit.collider.CompareTag("Player")) return false;
                    CarController hitCar = hit.collider.GetComponentInParent<CarController>();
                    if (hitCar != null && hitCar.isDrivenByPlayer) return false;
                }

                // NOUVEAU : On enregistre la distance la plus courte détectée !
                if (hit.distance < closestObstacleDist) closestObstacleDist = hit.distance;

                return true;
            }
            return false;
        }

        bool centerHuman, leftInnerHuman, rightInnerHuman, leftOuterHuman, rightOuterHuman;

        bool hitCenter = CheckRay(frontCenter, sensorLength, out centerHuman);
        bool hitLeftInner = CheckRay(frontLeftInner, sensorLength * 0.9f, out leftInnerHuman);
        bool hitRightInner = CheckRay(frontRightInner, sensorLength * 0.9f, out rightInnerHuman);
        bool hitLeftOuter = CheckRay(frontLeftOuter, sensorLength * 0.75f, out leftOuterHuman);
        bool hitRightOuter = CheckRay(frontRightOuter, sensorLength * 0.75f, out rightOuterHuman);

        humanInDanger = centerHuman || leftInnerHuman || rightInnerHuman || leftOuterHuman || rightOuterHuman;

        bool hitAnyLeft = hitLeftInner || hitLeftOuter;
        bool hitAnyRight = hitRightInner || hitRightOuter;

        if (hitCenter || hitAnyLeft || hitAnyRight)
        {
            float reactionTime = humanInDanger ? 0.05f : 0.4f;
            obstacleTimer += Time.deltaTime;

            if (obstacleTimer > reactionTime)
            {
                if (hitAnyLeft && !hitAnyRight) lockedAvoidDirection = 1f;
                else if (hitAnyRight && !hitAnyLeft) lockedAvoidDirection = -1f;
                else if (hitCenter) lockedAvoidDirection = Random.value > 0.5f ? 1f : -1f;

                lockedHumanDanger = humanInDanger;

                // LE SECRET EST ICI : Si le mur est trop près (< 2 mètres), on ne force pas !
                if (!humanInDanger && closestObstacleDist < 2.0f && !isReversing)
                {
                    isReversing = true;     // Déclenche la marche arrière tout de suite
                    stuckTimer = 0f;
                    avoidLockTimer = 0f;
                    isAvoidingObstacle = false;
                }
                else
                {
                    avoidLockTimer = 0.7f;
                }
                obstacleTimer = 0f;
            }
            else
            {
                isBraking = true; // On freine le temps de réagir
            }
        }
        else
        {
            obstacleTimer = 0f;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (carController == null || !carController.isDrivenByAI) return;

        if (chaseTarget != null)
        {
            CarController targetCar = collision.collider.GetComponentInParent<CarController>();

            if (targetCar != null && targetCar.isDrivenByPlayer)
            {
                if (collision.relativeVelocity.magnitude > 3f)
                {
                    ramTimer = 1.5f;
                }
            }
        }
    }
}