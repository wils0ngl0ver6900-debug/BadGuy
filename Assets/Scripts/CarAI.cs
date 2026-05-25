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

    private CarController carController;
    private Rigidbody rb;
    private bool isBraking = false;

    // Systèmes de Secours (Blocage et Contournement)
    private float stuckTimer = 0f;
    private bool isReversing = false;

    private float obstacleTimer = 0f;
    private float avoidDirection = 1f;
    private bool isAvoidingObstacle = false;

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

        // Si on est en train de contourner un obstacle, on ignore le chemin classique temporairement
        if (isAvoidingObstacle) return;

        if (currentNode != null) Drive();
    }

    void Drive()
    {
        float dist = Vector3.Distance(transform.position, currentNode.transform.position);

        if (dist < waypointThreshold)
        {
            if (currentNode.nextNodes.Count > 0)
                currentNode = currentNode.nextNodes[Random.Range(0, currentNode.nextNodes.Count)];
        }

        Vector3 localTarget = transform.InverseTransformPoint(currentNode.transform.position);
        float angle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float targetTurn = Mathf.Clamp(angle / 45f, -1f, 1f);

        // Système Anti-Blocage Total (Marche Arrière)
        if (rb.linearVelocity.magnitude < 0.5f && !isAvoidingObstacle)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 3f) isReversing = true;
            if (stuckTimer > 6f)
            {
                isReversing = false;
                stuckTimer = 0f;
            }
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

        if (angleAbs > 30f && currentSpeed > 10f)
        {
            carController.moveInput = -0.8f;
        }
        else if (angleAbs > 15f)
        {
            carController.moveInput = 0.40f;
        }
        else
        {
            carController.moveInput = 1f - (Mathf.Abs(carController.turnInput) * 0.2f);
        }
    }

    void CheckSensors()
    {
        isAvoidingObstacle = false;
        isBraking = false;

        Vector3 sensorStartPos = transform.position + (transform.forward * sensorFrontOffset) + (Vector3.up * 0.5f);

        Vector3 frontCenter = transform.forward;
        Vector3 frontLeft = Quaternion.Euler(0, -35, 0) * transform.forward; // J'ai écarté un peu les capteurs (35 degrés)
        Vector3 frontRight = Quaternion.Euler(0, 35, 0) * transform.forward;

        Debug.DrawRay(sensorStartPos, frontCenter * sensorLength, Color.red);
        Debug.DrawRay(sensorStartPos, frontLeft * (sensorLength * 0.8f), Color.red);
        Debug.DrawRay(sensorStartPos, frontRight * (sensorLength * 0.8f), Color.red);

        bool CheckRay(Vector3 dir, float length)
        {
            if (Physics.Raycast(sensorStartPos, dir, out RaycastHit hit, length, obstacleMask))
            {
                if (hit.collider.transform.root == transform.root) return false;
                if (hit.normal.y > 0.5f) return false;
                return true;
            }
            return false;
        }

        bool hitCenter = CheckRay(frontCenter, sensorLength);
        bool hitLeft = CheckRay(frontLeft, sensorLength * 0.8f);
        bool hitRight = CheckRay(frontRight, sensorLength * 0.8f);

        if (hitCenter || hitLeft || hitRight)
        {
            obstacleTimer += Time.deltaTime;

            // SI ÇA FAIT PLUS DE 1.5 SECONDE QU'ON EST BLOQUÉ -> MODE CONTOURNEMENT
            if (obstacleTimer > 1.5f)
            {
                isAvoidingObstacle = true;
                carController.moveInput = 0.4f; // On donne un petit coup de gaz pour forcer le passage

                // On cherche la voie libre !
                if (hitLeft && !hitRight) avoidDirection = 1f; // Obstacle à gauche ? Tourne à droite !
                else if (hitRight && !hitLeft) avoidDirection = -1f; // Obstacle à droite ? Tourne à gauche !
                else if (hitCenter) avoidDirection = 1f; // Par défaut, esquive par la droite

                carController.turnInput = Mathf.MoveTowards(carController.turnInput, avoidDirection, Time.deltaTime * steerSmoothing * 2f);
            }
            else
            {
                // FREINAGE CLASSIQUE
                isBraking = true;
            }
        }
        else
        {
            // Voie libre !
            obstacleTimer = 0f;
        }
    }
}