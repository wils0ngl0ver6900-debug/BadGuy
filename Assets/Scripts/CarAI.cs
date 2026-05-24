using UnityEngine;

[RequireComponent(typeof(CarController))]
public class CarAI : MonoBehaviour
{
    [Header("Navigation routière")]
    public TrafficNode currentNode;
    public float waypointThreshold = 3f;

    [Header("Détection d'obstacles (Les Yeux)")]
    public float sensorLength = 6f;
    public LayerMask obstacleMask;

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
            return;
        }

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

        carController.turnInput = Mathf.Clamp(angle / 45f, -1f, 1f);
        carController.moveInput = 1f - (Mathf.Abs(carController.turnInput) * 0.5f);
    }

    void CheckSensors()
    {
        isBraking = false;
        Vector3 sensorStartPos = transform.position + Vector3.up * 0.5f;

        Vector3 frontCenter = transform.forward;
        Vector3 frontLeft = Quaternion.Euler(0, -25, 0) * transform.forward;
        Vector3 frontRight = Quaternion.Euler(0, 25, 0) * transform.forward;

        if (Physics.Raycast(sensorStartPos, frontCenter, sensorLength, obstacleMask) ||
            Physics.Raycast(sensorStartPos, frontLeft, sensorLength * 0.8f, obstacleMask) ||
            Physics.Raycast(sensorStartPos, frontRight, sensorLength * 0.8f, obstacleMask))
        {
            isBraking = true;
        }
    }
}