using UnityEngine;

[RequireComponent(typeof(CarController))]
public class CarAI : MonoBehaviour
{
    [Header("Navigation routière")]
    public TrafficNode currentNode;
    public float waypointThreshold = 4f; // Légèrement augmenté pour les véhicules lourds

    [Header("Détection d'obstacles (Les Yeux)")]
    public float sensorLength = 6f;
    public LayerMask obstacleMask;

    [Header("Ajustements IA 🧠")]
    [Tooltip("Vitesse à laquelle l'IA tourne le volant. Plus c'est bas, plus c'est fluide.")]
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
            carController.isHandbraking = true; // Active le frein pour s'arrêter net
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

        // Calcul de l'angle vers la cible
        Vector3 localTarget = transform.InverseTransformPoint(currentNode.transform.position);
        float angle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

        // 1. CORRECTION DES EMBARDÉES : Rotation progressive du volant
        float targetTurn = Mathf.Clamp(angle / 45f, -1f, 1f);
        carController.turnInput = Mathf.MoveTowards(carController.turnInput, targetTurn, Time.deltaTime * steerSmoothing);

        // 2. ANTICIPATION DU VIRAGE : On ralentit si l'angle est trop prononcé
        float angleAbs = Mathf.Abs(angle);
        if (angleAbs > 25f)
        {
            // Le virage est serré, on force la voiture à ralentir pour ne pas glisser hors de la route
            carController.moveInput = 0.25f;
        }
        else
        {
            // Ligne droite ou courbe légère : on accélère de façon saine
            carController.moveInput = 1f - (Mathf.Abs(carController.turnInput) * 0.3f);
        }
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