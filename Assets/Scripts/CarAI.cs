using UnityEngine;

[RequireComponent(typeof(CarController))]
public class CarAI : MonoBehaviour
{
    [Header("Navigation routière")]
    public TrafficNode currentNode;
    public float waypointThreshold = 4f;

    [Header("Détection d'obstacles (Les Yeux)")]
    public float sensorLength = 6f;
    [Tooltip("Décale les lasers vers l'avant. Ajuste ça pour que les lasers rouges sortent DU PARE-CHOCS dans la vue Scene.")]
    public float sensorFrontOffset = 2.5f;
    public LayerMask obstacleMask;

    [Header("Ajustements IA 🧠")]
    public float steerSmoothing = 4f;

    private CarController carController;
    private bool isBraking = false;

    void Start()
    {
        carController = GetComponent<CarController>();
        carController.isDrivenByAI = true;
    }

    void Update()
    {
        if (!carController.isDrivenByAI) return;

        CheckSensors();

        if (isBraking)
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
        carController.turnInput = Mathf.MoveTowards(carController.turnInput, targetTurn, Time.deltaTime * steerSmoothing);

        float angleAbs = Mathf.Abs(angle);
        if (angleAbs > 25f)
        {
            carController.moveInput = 0.25f;
        }
        else
        {
            carController.moveInput = 1f - (Mathf.Abs(carController.turnInput) * 0.3f);
        }
    }

    void CheckSensors()
    {
        isBraking = false;

        // On utilise la nouvelle variable pour sortir du capot
        Vector3 sensorStartPos = transform.position + (transform.forward * sensorFrontOffset) + (Vector3.up * 0.5f);

        Vector3 frontCenter = transform.forward;
        Vector3 frontLeft = Quaternion.Euler(0, -25, 0) * transform.forward;
        Vector3 frontRight = Quaternion.Euler(0, 25, 0) * transform.forward;

        Debug.DrawRay(sensorStartPos, frontCenter * sensorLength, Color.red);
        Debug.DrawRay(sensorStartPos, frontLeft * (sensorLength * 0.8f), Color.red);
        Debug.DrawRay(sensorStartPos, frontRight * (sensorLength * 0.8f), Color.red);

        if (Physics.Raycast(sensorStartPos, frontCenter, sensorLength, obstacleMask) ||
            Physics.Raycast(sensorStartPos, frontLeft, sensorLength * 0.8f, obstacleMask) ||
            Physics.Raycast(sensorStartPos, frontRight, sensorLength * 0.8f, obstacleMask))
        {
            isBraking = true;
        }
    }
}