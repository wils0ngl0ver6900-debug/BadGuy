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

    private float stuckTimer = 0f;
    private bool isReversing = false;

    private float obstacleTimer = 0f;
    private float avoidDirection = 1f;
    private bool isAvoidingObstacle = false;

    // --- NOUVEAU : Chrono pour la technique du Bélier (Bump) ---
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

        CheckSensors();

        if (isBraking && !isReversing)
        {
            carController.moveInput = 0f;
            carController.turnInput = 0f;
            carController.isHandbraking = true;
            return;
        }

        carController.isHandbraking = false;
        if (isAvoidingObstacle) return;

        Drive();
    }

    void Drive()
    {
        if (chaseTarget == null && currentNode == null) return;

        // --- LA TECHNIQUE DU BÉLIER (BUMP) ---
        // Si la voiture de flic vient de te percuter, elle recule pour reprendre de l'élan !
        if (ramTimer > 0f)
        {
            ramTimer -= Time.deltaTime;
            carController.moveInput = -1f; // Marche arrière toute
            carController.turnInput = 0f;  // On garde les roues droites pour se dégager
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

        if (rb.linearVelocity.magnitude < 0.5f && !isAvoidingObstacle)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 3f) isReversing = true;
            if (stuckTimer > 6f) { isReversing = false; stuckTimer = 0f; }
        }
        else
        {
            stuckTimer = 0f;
            isReversing = false;
        }

        if (isReversing)
        {
            carController.moveInput = -1f;
            carController.turnInput = Mathf.MoveTowards(carController.turnInput, -targetTurn, Time.deltaTime * steerSmoothing);
            return;
        }

        carController.turnInput = Mathf.MoveTowards(carController.turnInput, targetTurn, Time.deltaTime * steerSmoothing);

        float angleAbs = Mathf.Abs(angle);
        float currentSpeed = rb.linearVelocity.magnitude;

        if (angleAbs > 30f && currentSpeed > 10f) carController.moveInput = -0.8f;
        else if (angleAbs > 15f) carController.moveInput = 0.40f;
        else carController.moveInput = 1f - (Mathf.Abs(carController.turnInput) * 0.2f);
    }

    void CheckSensors()
    {
        isAvoidingObstacle = false;
        isBraking = false;

        Vector3 sensorStartPos = transform.position + (transform.forward * sensorFrontOffset) + (Vector3.up * 0.5f);
        Vector3 frontCenter = transform.forward;
        Vector3 frontLeft = Quaternion.Euler(0, -35, 0) * transform.forward;
        Vector3 frontRight = Quaternion.Euler(0, 35, 0) * transform.forward;

        bool CheckRay(Vector3 dir, float length)
        {
            if (Physics.Raycast(sensorStartPos, dir, out RaycastHit hit, length, obstacleMask))
            {
                if (hit.collider.transform.root == transform.root) return false;

                // --- CORRECTIF PIÉTONS ÉCRASÉS ---
                // On vérifie d'abord si c'est un humain avant d'ignorer la pente !
                NPCBrain npc = hit.collider.GetComponentInParent<NPCBrain>();
                PlayerController pc = hit.collider.GetComponentInParent<PlayerController>();
                bool isHuman = (npc != null || pc != null);

                // Si ce n'est PAS un humain, et que c'est très plat (comme le sol), on ignore.
                if (!isHuman && hit.normal.y > 0.8f) return false;

                // --- CORRECTIF KAMIKAZE (Priorité Cible) ---
                if (chaseTarget != null)
                {
                    // Si on vise le joueur à pied, on lui fonce dessus
                    if (pc != null && hit.collider.CompareTag("Player")) return false;

                    // Si on vise la voiture du joueur, on lui fonce dessus
                    CarController hitCar = hit.collider.GetComponentInParent<CarController>();
                    if (hitCar != null && hitCar.isDrivenByPlayer) return false;
                }

                return true; // Pour tout le reste (civil face à un piéton, flic face à un mur), on freine !
            }
            return false;
        }

        bool hitCenter = CheckRay(frontCenter, sensorLength);
        bool hitLeft = CheckRay(frontLeft, sensorLength * 0.8f);
        bool hitRight = CheckRay(frontRight, sensorLength * 0.8f);

        if (hitCenter || hitLeft || hitRight)
        {
            obstacleTimer += Time.deltaTime;
            if (obstacleTimer > 1.5f)
            {
                isAvoidingObstacle = true;
                carController.moveInput = 0.4f;
                if (hitLeft && !hitRight) avoidDirection = 1f;
                else if (hitRight && !hitLeft) avoidDirection = -1f;
                else if (hitCenter) avoidDirection = 1f;
                carController.turnInput = Mathf.MoveTowards(carController.turnInput, avoidDirection, Time.deltaTime * steerSmoothing * 2f);
            }
            else isBraking = true;
        }
        else obstacleTimer = 0f;
    }

    // --- LE DÉTECTEUR DE CRASH POUR LE RECUL ---
    void OnCollisionEnter(Collision collision)
    {
        if (carController == null || !carController.isDrivenByAI) return;

        if (chaseTarget != null)
        {
            CarController targetCar = collision.collider.GetComponentInParent<CarController>();

            // Si la police vient de taper la voiture que conduit le joueur
            if (targetCar != null && targetCar.isDrivenByPlayer)
            {
                // Si l'impact a été assez fort, on enclenche la marche arrière pour reprendre de l'élan
                if (collision.relativeVelocity.magnitude > 3f)
                {
                    ramTimer = 1.5f;
                }
            }
        }
    }
}