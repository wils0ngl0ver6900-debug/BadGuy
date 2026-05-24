using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class NPCBrain : MonoBehaviour
{
    // --- LES LISTES DÉROULANTES DE L'INSPECTEUR ---
    public enum NPCRole { Civil, Policier, Gang }
    public enum Locomotion { Pieton, Vehicule }
    public enum AIState { Patrouille, Fuite, Poursuite, Panique }

    [Header("Identité 🪪")]
    [Tooltip("Qui est ce PNJ ?")]
    public NPCRole role = NPCRole.Civil;
    public TerritoryManager.Faction faction = TerritoryManager.Faction.None; // Utilisé si c'est un Gang

    [Header("Moteur Physique 🚶/🚗")]
    [Tooltip("Comment se déplace-t-il ?")]
    public Locomotion locomotion = Locomotion.Pieton;
    public AIState currentState = AIState.Patrouille;

    [Header("Paramètres de Déplacement ⚙️")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 4.5f;
    public float visionRange = 15f;
    public TrafficNode currentTrafficNode; // Uniquement si Locomotion = Vehicule

    [Header("Système de Police 🚔")]
    public GameObject copPedestrianPrefab; // Si un véhicule de police doit faire descendre des flics
    public Transform[] exitDoors;

    // Références internes (Le Corps)
    private NavMeshAgent agent;
    private CarController car;
    private Transform player;
    private VisionCone vision;

    // Variables de logique
    private bool hasSpawnedCops = false;
    private float callPoliceTimer = 0f;

    void Awake()
    {
        // On récupère le joueur
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        // On connecte le corps au cerveau
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
        // Optimisation Pro : On ne vérifie pas l'IA à chaque frame (Update), mais tous les 0.2s
        StartCoroutine(BrainTick());
    }

    // --- LE MOTEUR DE RÉFLEXION (Tourne 5 fois par seconde au lieu de 60) ---
    private IEnumerator BrainTick()
    {
        while (true)
        {
            AnalyzeEnvironment();
            ExecuteStateAction();
            yield return new WaitForSeconds(0.2f);
        }
    }

    // --- 1. ANALYSE (Que se passe-t-il autour de moi ?) ---
    private void AnalyzeEnvironment()
    {
            if (player == null || currentState == AIState.Panique) return;

            float distToPlayer = Vector3.Distance(transform.position, player.position);
            bool canSeePlayer = distToPlayer <= visionRange;

            // Si le PNJ est un CIVIL
            if (role == NPCRole.Civil)
        {
            if (canSeePlayer && GameManager.Instance != null && GameManager.Instance.wantedLevel > 0)
            {
                ChangeState(AIState.Fuite);
            }
        }
        // Si le PNJ est un FLIC
        else if (role == NPCRole.Policier)
        {
            // Le flic communique avec le GameManager pour dire "Je le vois !"
            if (canSeePlayer && GameManager.Instance.wantedLevel > 0)
            {
                // S'il te voit, il empêche ton évasion de baisser
                GameManager.Instance.spottersCount = 1;
                ChangeState(AIState.Poursuite);
            }
            else if (GameManager.Instance.wantedLevel == 0)
            {
                ChangeState(AIState.Patrouille);
            }
        }
    }

    // --- 2. ACTION (Qu'est-ce que je fais en fonction de mon état ?) ---
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

    // --- CHANGEMENT D'ÉTAT SÉCURISÉ ---
    public void ChangeState(AIState newState)
    {
        if (currentState == newState) return;
        currentState = newState;

        // Reset spécifique selon l'état
        if (locomotion == Locomotion.Pieton && agent != null)
        {
            agent.speed = (newState == AIState.Patrouille) ? walkSpeed : runSpeed;
        }
    }

    // ==========================================
    // LOGIQUE PIÉTONS (NavMesh)
    // ==========================================
    private void PatrolPedestrian()
    {
        if (agent == null) return;
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            // Trouve un point au hasard
            Vector3 randomDir = Random.insideUnitSphere * 15f;
            randomDir += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, 15f, 1))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    private void FleePedestrian()
    {
        if (agent == null || player == null) return;
        Vector3 directionAway = (transform.position - player.position).normalized;
        agent.SetDestination(transform.position + directionAway * 20f);

        // Mécanique de snitch : Appelle la police en fuyant
        if (role == NPCRole.Civil)
        {
            callPoliceTimer += 0.2f; // On ajoute le temps du BrainTick
            if (callPoliceTimer >= 4f)
            {
                if (GameManager.Instance != null) GameManager.Instance.ReportCrime(15);
                callPoliceTimer = 0f; // Reset
            }
        }
    }

    private void ChasePedestrian()
    {
        if (agent != null && player != null)
        {
            agent.SetDestination(player.position);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Un flic à pied arrête le joueur s'il le touche en pleine poursuite
        if (role == NPCRole.Policier && locomotion == Locomotion.Pieton && currentState == AIState.Poursuite)
        {
            if (collision.gameObject.CompareTag("Player") && GameManager.Instance != null)
            {
                GameManager.Instance.Busted();
            }
        }
    }

    // ==========================================
    // LOGIQUE VÉHICULES (CarController)
    // ==========================================
    private void PatrolVehicle()
    {
        if (car == null || currentTrafficNode == null || !car.isDrivenByAI) return;

        float dist = Vector3.Distance(transform.position, currentTrafficNode.transform.position);
        if (dist < 3f && currentTrafficNode.nextNodes.Count > 0)
        {
            currentTrafficNode = currentTrafficNode.nextNodes[Random.Range(0, currentTrafficNode.nextNodes.Count)];
        }

        DriveTowards(currentTrafficNode.transform.position, 0.5f);
    }

    private void FleeVehicle()
    {
        if (car == null || !car.isDrivenByAI) return;
        // Le civil panique au volant, il accélère à fond sur son noeud actuel !
        if (currentTrafficNode != null) DriveTowards(currentTrafficNode.transform.position, 1f);
    }

    private void ChaseVehicle()
    {
        if (car == null || player == null || !car.isDrivenByAI) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // Si la voiture de flic rattrape le joueur (moins de 8m), elle pile et déploie les flics !
        if (distToPlayer < 8f && !hasSpawnedCops)
        {
            car.moveInput = 0;
            car.turnInput = 0;
            car.isDrivenByAI = false;
            DeployFootCops();
            return;
        }

        DriveTowards(player.position, 1f); // Vitesse max
    }

    private void DriveTowards(Vector3 targetPos, float speedMultiplier)
    {
        Vector3 localTarget = transform.InverseTransformPoint(targetPos);
        float angle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

        car.turnInput = Mathf.Clamp(angle / 45f, -1f, 1f);
        car.moveInput = speedMultiplier - (Mathf.Abs(car.turnInput) * 0.4f); // Ralentit un peu pour tourner
    }

    private void DeployFootCops()
    {
        hasSpawnedCops = true;
        if (copPedestrianPrefab == null || exitDoors == null) return;

        foreach (Transform door in exitDoors)
        {
            Instantiate(copPedestrianPrefab, door.position, Quaternion.identity);
        }
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=blue>POLICE : Unité à pied déployée !</color>");
    }

    // --- ACCESSOIRE POUR LE CARJACKING ---
    public void ForcePanic()
    {
        ChangeState(AIState.Panique);
        if (locomotion == Locomotion.Pieton && agent != null)
        {
            FleePedestrian(); // Force la course immédiate
        }
    }
}