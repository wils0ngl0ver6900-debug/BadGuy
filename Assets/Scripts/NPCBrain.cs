using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class NPCBrain : MonoBehaviour
{
    public enum NPCRole { Civil, Policier, Gang }
    public enum Locomotion { Pieton, Vehicule }
    public enum AIState { Patrouille, Fuite, Poursuite, Panique }

    [Header("Identité 🪪")]
    public NPCRole role = NPCRole.Civil;
    public TerritoryManager.Faction faction = TerritoryManager.Faction.None;

    [Header("Moteur Physique 🚶/🚗")]
    public Locomotion locomotion = Locomotion.Pieton;
    public AIState currentState = AIState.Patrouille;

    [Header("Paramètres de Déplacement ⚙️")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 4.5f;
    public float visionRange = 15f;
    public TrafficNode currentTrafficNode;

    [Header("Système de Police 🚔")]
    public GameObject copPedestrianPrefab;
    public Transform[] exitDoors;

    private NavMeshAgent agent;
    private CarController car;
    private Transform player;
    private VisionCone vision;

    private bool hasSpawnedCops = false;
    private float callPoliceTimer = 0f;

    // --- NOUVEAU : Pour l'évasion GTA ---
    public bool isSeeingPlayer { get; private set; } // Accessible en lecture seule

    void Awake()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        if (locomotion == Locomotion.Pieton)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.speed = walkSpeed;
        }
        else if (locomotion == Locomotion.Vehicule)
        {
            car = GetComponent<CarController>();
            if (car != null) car.isDrivenByAI = true;
        }

        vision = GetComponent<VisionCone>();
    }

    void Start()
    {
        StartCoroutine(BrainTick());
    }

    private IEnumerator BrainTick()
    {
        while (true)
        {
            AnalyzeEnvironment();
            ExecuteStateAction();
            yield return new WaitForSeconds(0.2f);
        }
    }

    // --- ANALYSE CORRIGÉE ---
    private void AnalyzeEnvironment()
    {
        if (player == null || currentState == AIState.Panique || GameManager.Instance == null) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // On met à jour l'état de vision local
        isSeeingPlayer = distToPlayer <= visionRange;

        if (role == NPCRole.Civil)
        {
            if (isSeeingPlayer && GameManager.Instance.wantedLevel > 0) ChangeState(AIState.Fuite);
        }
        else if (role == NPCRole.Policier)
        {
            // Le flic se contente de se mettre en chasse, c'est tout
            if (isSeeingPlayer && GameManager.Instance.wantedLevel > 0) ChangeState(AIState.Poursuite);
            else if (GameManager.Instance.wantedLevel == 0) ChangeState(AIState.Patrouille);
        }
    }

    private void ExecuteStateAction()
    {
        switch (currentState)
        {
            case AIState.Patrouille:
                if (locomotion == Locomotion.Pieton) PatrolPedestrian();
                else PatrolVehicle();
                break;
            case AIState.Fuite:
                if (locomotion == Locomotion.Pieton) FleePedestrian();
                else FleeVehicle();
                break;
            case AIState.Poursuite:
                if (locomotion == Locomotion.Pieton) ChasePedestrian();
                else ChaseVehicle();
                break;
        }
    }

    public void ChangeState(AIState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        if (locomotion == Locomotion.Pieton && agent != null)
        {
            agent.speed = (newState == AIState.Patrouille) ? walkSpeed : runSpeed;
        }
    }

    // --- LOGIQUE PIÉTONS ---
    private void PatrolPedestrian()
    {
        if (agent == null) return;
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            Vector3 randomDir = Random.insideUnitSphere * 15f + transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, 15f, 1)) agent.SetDestination(hit.position);
        }
    }

    private void FleePedestrian()
    {
        if (agent == null || player == null) return;
        Vector3 directionAway = (transform.position - player.position).normalized;
        agent.SetDestination(transform.position + directionAway * 20f);
        if (role == NPCRole.Civil)
        {
            callPoliceTimer += 0.2f;
            if (callPoliceTimer >= 4f)
            {
                if (GameManager.Instance != null) GameManager.Instance.ReportCrime(15);
                callPoliceTimer = 0f;
            }
        }
    }

    private void ChasePedestrian()
    {
        if (agent != null && player != null) agent.SetDestination(player.position);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (role == NPCRole.Policier && locomotion == Locomotion.Pieton && currentState == AIState.Poursuite)
        {
            if (collision.gameObject.CompareTag("Player") && GameManager.Instance != null) GameManager.Instance.Busted();
        }
    }

    // --- LOGIQUE VÉHICULES ---
    private void PatrolVehicle()
    {
        if (car == null || currentTrafficNode == null || !car.isDrivenByAI) return;
        if (Vector3.Distance(transform.position, currentTrafficNode.transform.position) < 3f && currentTrafficNode.nextNodes.Count > 0)
            currentTrafficNode = currentTrafficNode.nextNodes[Random.Range(0, currentTrafficNode.nextNodes.Count)];
        DriveTowards(currentTrafficNode.transform.position, 0.5f);
    }

    private void FleeVehicle()
    {
        if (car == null || !car.isDrivenByAI || currentTrafficNode == null) return;
        DriveTowards(currentTrafficNode.transform.position, 1f);
    }

    private void ChaseVehicle()
    {
        if (car == null || player == null || !car.isDrivenByAI) return;
        if (Vector3.Distance(transform.position, player.position) < 8f && !hasSpawnedCops)
        {
            car.moveInput = car.turnInput = 0; car.isDrivenByAI = false; DeployFootCops(); return;
        }
        DriveTowards(player.position, 1f);
    }

    private void DriveTowards(Vector3 targetPos, float speedMultiplier)
    {
        Vector3 localTarget = transform.InverseTransformPoint(targetPos);
        float angle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        car.turnInput = Mathf.Clamp(angle / 45f, -1f, 1f);
        car.moveInput = speedMultiplier - (Mathf.Abs(car.turnInput) * 0.4f);
    }

    private void DeployFootCops()
    {
        hasSpawnedCops = true;
        if (copPedestrianPrefab == null || exitDoors == null) return;
        foreach (Transform door in exitDoors) Instantiate(copPedestrianPrefab, door.position, Quaternion.identity);
    }

    public void ForcePanic()
    {
        ChangeState(AIState.Panique);
        if (locomotion == Locomotion.Pieton && agent != null) FleePedestrian();
    }
}