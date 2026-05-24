using UnityEngine;

[RequireComponent(typeof(CarController))]
public class CarAI : MonoBehaviour
{
    [Header("Navigation routière")]
    public TrafficNode currentNode;
    public float waypointThreshold = 5f; // Recommandé : 5 pour les camions

    [Header("Détection d'obstacles (Les Yeux)")]
    public float sensorLength = 6f;
    public float sensorFrontOffset = 2.5f;
    public LayerMask obstacleMask;

    [Header("Ajustements IA 🧠")]
    public float steerSmoothing = 4f;

    private CarController carController;
    private Rigidbody rb;
    private bool isBraking = false;

    private float stuckTimer = 0f;
    private bool isReversing = false;

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

        if (currentNode != null) Drive();
    }

    void Drive()
    {
        float dist = Vector3.Distance(transform.position, currentNode.transform.position);

        // Validation du noeud de trafic
        if (dist < waypointThreshold)
        {
            if (currentNode.nextNodes.Count > 0)
                currentNode = currentNode.nextNodes[Random.Range(0, currentNode.nextNodes.Count)];
        }

        Vector3 localTarget = transform.InverseTransformPoint(currentNode.transform.position);
        float angle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float targetTurn = Mathf.Clamp(angle / 45f, -1f, 1f);

        // Système Anti-Blocage
        if (rb.linearVelocity.magnitude < 0.5f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 2.5f) isReversing = true;
            if (stuckTimer > 5f)
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

        // Le volant tourne vers la cible
        carController.turnInput = Mathf.MoveTowards(carController.turnInput, targetTurn, Time.deltaTime * steerSmoothing);

        // --- NOUVELLE LOGIQUE DE VIRAGE PRO (Freinage et Filet de gaz) ---
        float angleAbs = Mathf.Abs(angle);
        float currentSpeed = rb.linearVelocity.magnitude;

        if (angleAbs > 30f && currentSpeed > 10f)
        {
            // VIRAGE BRUTAL + VITESSE ÉLEVÉE : L'IA écrase le frein !
            carController.moveInput = -0.8f;
        }
        else if (angleAbs > 15f)
        {
            // VIRAGE CLASSIQUE : Filet de gaz (40%) pour garder de l'élan sans déborder
            carController.moveInput = 0.40f;
        }
        else
        {
            // LIGNE DROITE : On accélère à fond (proportionnellement à la ligne droite)
            carController.moveInput = 1f - (Mathf.Abs(carController.turnInput) * 0.2f);
        }
    }

    void CheckSensors()
    {
        isBraking = false;
        Vector3 sensorStartPos = transform.position + (transform.forward * sensorFrontOffset) + (Vector3.up * 0.5f);

        Vector3 frontCenter = transform.forward;
        Vector3 frontLeft = Quaternion.Euler(0, -25, 0) * transform.forward;
        Vector3 frontRight = Quaternion.Euler(0, 25, 0) * transform.forward;

        Debug.DrawRay(sensorStartPos, frontCenter * sensorLength, Color.red);
        Debug.DrawRay(sensorStartPos, frontLeft * (sensorLength * 0.8f), Color.red);
        Debug.DrawRay(sensorStartPos, frontRight * (sensorLength * 0.8f), Color.red);

        bool CheckRay(Vector3 dir, float length)
        {
            RaycastHit hit;
            if (Physics.Raycast(sensorStartPos, dir, out hit, length, obstacleMask))
            {
                if (hit.collider.transform.root == transform.root) return false;
                if (hit.normal.y > 0.5f) return false;
                return true;
            }
            return false;
        }

        if (CheckRay(frontCenter, sensorLength) ||
            CheckRay(frontLeft, sensorLength * 0.8f) ||
            CheckRay(frontRight, sensorLength * 0.8f))
        {
            isBraking = true;
        }
    }
}