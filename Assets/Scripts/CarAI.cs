using UnityEngine;

[RequireComponent(typeof(CarController))]
public class CarAI : MonoBehaviour
{
    [Header("Navigation routière")]
    public TrafficNode currentNode;
    public float waypointThreshold = 4f;

    [Header("Détection d'obstacles (Les Yeux)")]
    public float sensorLength = 6f;
    public float sensorFrontOffset = 2.5f;
    public LayerMask obstacleMask;

    [Header("Ajustements IA 🧠")]
    public float steerSmoothing = 4f;

    private CarController carController;
    private Rigidbody rb;
    private bool isBraking = false;

    // Système Anti-Blocage
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
        if (dist < waypointThreshold)
        {
            if (currentNode.nextNodes.Count > 0)
                currentNode = currentNode.nextNodes[Random.Range(0, currentNode.nextNodes.Count)];
        }

        Vector3 localTarget = transform.InverseTransformPoint(currentNode.transform.position);
        float angle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float targetTurn = Mathf.Clamp(angle / 45f, -1f, 1f);

        // --- NOUVEAU : SYSTÈME ANTI-BLOCAGE (MARCHE ARRIÈRE) ---
        if (rb.linearVelocity.magnitude < 1f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 2.5f) isReversing = true; // Bloqué depuis 2.5s ? Marche arrière !
            if (stuckTimer > 5f) // Après 2.5s de marche arrière, on retente d'avancer
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
            // L'IA fait une marche arrière et braque dans le sens inverse pour se dégager
            carController.moveInput = -1f;
            carController.turnInput = Mathf.MoveTowards(carController.turnInput, -targetTurn, Time.deltaTime * steerSmoothing);
            return;
        }

        // --- CONDUITE NORMALE ---
        carController.turnInput = Mathf.MoveTowards(carController.turnInput, targetTurn, Time.deltaTime * steerSmoothing);

        float angleAbs = Mathf.Abs(angle);
        if (angleAbs > 25f)
        {
            carController.moveInput = 0.35f; // On donne un peu plus de jus dans les virages
        }
        else
        {
            carController.moveInput = 1f - (Mathf.Abs(carController.turnInput) * 0.3f);
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

        // On crée une fonction locale pour vérifier si le rayon touche un VRAI obstacle
        bool CheckRay(Vector3 dir, float length)
        {
            RaycastHit hit;
            if (Physics.Raycast(sensorStartPos, dir, out hit, length, obstacleMask))
            {
                // 1. On ignore notre propre voiture
                if (hit.collider.transform.root == transform.root) return false;

                // 2. On ignore le sol (tout ce qui est à peu près plat)
                if (hit.normal.y > 0.5f) return false;

                // DEBUG : Affiche dans la console de Unity CE QUE l'IA regarde
                Debug.Log($"<color=orange>[Pick-It AI]</color> Je freine car je vois : {hit.collider.gameObject.name}");
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