using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class NPCBrain : MonoBehaviour
{
    public enum NPCRole { Civil, Policier, Gang }
    public enum Locomotion { Pieton, Vehicule }
    public enum AIState { Patrouille, Fuite, Poursuite, Panique, Combat }

    [Header("Identité 🪪")]
    public NPCRole role = NPCRole.Civil;
    public TerritoryManager.Faction faction = TerritoryManager.Faction.None;

    [Header("Moteur Physique 🚶/🚗")]
    public Locomotion locomotion = Locomotion.Pieton;
    public AIState currentState = AIState.Patrouille;

    [Header("Paramètres de Déplacement ⚙️")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 6.0f; // BOOSTÉ : Le flic court plus vite que le joueur (5.0f)
    public float visionRange = 25f;
    public TrafficNode currentTrafficNode;

    [Header("Système de Combat 🔫")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.5f;
    public int attackDamage = 15;

    public Light muzzleFlashLight;

    private float nextFireTime = 0f;
    private Transform currentTarget;

    [Header("Système de Police 🚔")]
    public GameObject copPedestrianPrefab;
    public Transform[] exitDoors;

    [HideInInspector] public bool isSeeingPlayer = false;

    private NavMeshAgent agent;
    private CarController car;
    private Transform player;
    private bool hasSpawnedCops = false;
    private float callPoliceTimer = 0f;

    void Awake()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        if (locomotion == Locomotion.Pieton)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed = walkSpeed;
                agent.stoppingDistance = 1.5f; // ANTI-SACCADE : Il s'arrête poliment à 1.5m de toi !
            }
        }
        else if (locomotion == Locomotion.Vehicule)
        {
            car = GetComponent<CarController>();
            if (car != null) car.isDrivenByAI = true;
        }

        if (muzzleFlashLight != null) muzzleFlashLight.enabled = false;
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

    private void AnalyzeEnvironment()
    {
        if (player == null || currentState == AIState.Panique) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        isSeeingPlayer = distToPlayer <= visionRange;

        if (role != NPCRole.Civil)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, visionRange);
            Transform bestEnemy = null;
            float minDistance = Mathf.Infinity;

            foreach (Collider hit in hitColliders)
            {
                TargetHealth health = hit.GetComponent<TargetHealth>();
                PlayerController playerHealth = hit.GetComponent<PlayerController>();

                if (health != null && health.isDead) continue;
                if (playerHealth != null && playerHealth.currentHealth <= 0) continue;

                if (health == null && playerHealth == null) continue;

                NPCBrain otherNPC = hit.GetComponent<NPCBrain>();
                bool isEnemy = false;

                if (role == NPCRole.Policier)
                {
                    if (otherNPC != null && otherNPC.role == NPCRole.Gang) isEnemy = true;
                    if (hit.CompareTag("Player") && GameManager.Instance != null && GameManager.Instance.wantedLevel > 0)
                    {
                        isEnemy = true;
                        if (PoliceManager.Instance != null) PoliceManager.Instance.ReportPlayerSight(player.position);
                    }
                }
                else if (role == NPCRole.Gang)
                {
                    if (otherNPC != null && otherNPC.role == NPCRole.Policier) isEnemy = true;
                    if (otherNPC != null && otherNPC.role == NPCRole.Gang && otherNPC.faction != this.faction) isEnemy = true;
                }

                if (isEnemy)
                {
                    float dist = Vector3.Distance(transform.position, hit.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestEnemy = hit.transform;
                    }
                }
            }

            if (bestEnemy != null)
            {
                currentTarget = bestEnemy;

                if (role == NPCRole.Policier && bestEnemy.CompareTag("Player"))
                {
                    int stars = GameManager.Instance != null ? GameManager.Instance.wantedLevel : 0;
                    bool isPlayerInCar = bestEnemy.GetComponentInParent<CarController>() != null;

                    if (stars <= 2)
                    {
                        ChangeState(AIState.Poursuite);
                        return;
                    }
                    else if (stars == 3)
                    {
                        if (isPlayerInCar && locomotion == Locomotion.Vehicule)
                        {
                            ChangeState(AIState.Poursuite);
                            return;
                        }
                        else
                        {
                            ChangeState(AIState.Combat);
                            return;
                        }
                    }
                    else
                    {
                        ChangeState(AIState.Combat);
                        return;
                    }
                }

                ChangeState(AIState.Combat);
                return;
            }
            else
            {
                currentTarget = null;
            }
        }

        if (role == NPCRole.Civil)
        {
            if (isSeeingPlayer && GameManager.Instance != null && GameManager.Instance.wantedLevel > 0) ChangeState(AIState.Fuite);
            else ChangeState(AIState.Patrouille);
        }
        else if (role == NPCRole.Policier)
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.wantedLevel == 0) ChangeState(AIState.Patrouille);
                else ChangeState(AIState.Poursuite);
            }
        }
        else if (role == NPCRole.Gang)
        {
            ChangeState(AIState.Patrouille);
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
            case AIState.Combat:
                if (locomotion == Locomotion.Pieton) CombatPedestrian();
                else CombatVehicle();
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
            if (newState == AIState.Combat) agent.isStopped = true;
            else agent.isStopped = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (role == NPCRole.Policier && currentState == AIState.Poursuite)
        {
            bool isPlayerInCar = player != null && player.GetComponentInParent<CarController>() != null;

            if (collision.gameObject.CompareTag("Player"))
            {
                if (locomotion == Locomotion.Pieton || (locomotion == Locomotion.Vehicule && !isPlayerInCar))
                {
                    if (GameManager.Instance != null) GameManager.Instance.Busted();
                }
            }

            if (locomotion == Locomotion.Vehicule && isPlayerInCar)
            {
                CarController targetCar = collision.gameObject.GetComponentInParent<CarController>();
                if (targetCar != null && collision.gameObject.transform.root == player.root)
                {
                    Rigidbody myRb = GetComponent<Rigidbody>();
                    if (myRb != null && myRb.linearVelocity.magnitude > 8f)
                    {
                        targetCar.TakeDamage(10f);
                    }
                }
            }
        }
    }

    private bool IsTargetAlive(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;

        TargetHealth th = target.GetComponent<TargetHealth>();
        if (th != null && th.isDead) return false;

        PlayerController pc = target.GetComponent<PlayerController>();
        if (pc != null && pc.currentHealth <= 0) return false;

        return true;
    }

    private void CombatPedestrian()
    {
        if (currentTarget == null || !IsTargetAlive(currentTarget))
        {
            currentTarget = null;
            ChangeState(AIState.Patrouille);
            return;
        }

        Vector3 lookDir = currentTarget.position - transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);

        ShootAtTarget();
    }

    private void CombatVehicle()
    {
        if (car != null && car.isEngineDead)
        {
            ChangeState(AIState.Panique);
            return;
        }

        if (currentTarget == null || !IsTargetAlive(currentTarget))
        {
            currentTarget = null;
            ChangeState(AIState.Patrouille);
            return;
        }

        if (role == NPCRole.Policier && hasSpawnedCops)
        {
            ChaseVehicle();
            return;
        }

        ShootAtTarget();
        ChaseVehicle();
    }

    private void ShootAtTarget()
    {
        if (Time.time >= nextFireTime && bulletPrefab != null && firePoint != null && currentTarget != null)
        {
            nextFireTime = Time.time + fireRate;
            Vector3 aimDir = (currentTarget.position + Vector3.up * 1f) - firePoint.position;

            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(aimDir));

            Bullet b = bullet.GetComponent<Bullet>();
            if (b != null)
            {
                b.isEnemyBullet = true;
                b.damage = attackDamage;
                b.shooter = this.gameObject;
            }

            if (muzzleFlashLight != null) StartCoroutine(FlashMuzzleLight());
        }
    }

    private IEnumerator FlashMuzzleLight()
    {
        muzzleFlashLight.enabled = true;
        yield return new WaitForSeconds(0.05f);
        muzzleFlashLight.enabled = false;
    }

    private void PatrolPedestrian()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            Vector3 randomDir = Random.insideUnitSphere * 15f + transform.position;
            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 15f, 1)) agent.SetDestination(hit.position);
        }
    }

    private void PatrolVehicle()
    {
        CarAI ai = GetComponent<CarAI>();
        if (ai != null) ai.chaseTarget = null;
    }

    private void FleePedestrian()
    {
        if (agent == null || player == null || !agent.isOnNavMesh) return;
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
        if (agent == null || !agent.isOnNavMesh) return;

        Vector3 targetDest = transform.position;

        if (isSeeingPlayer && player != null)
        {
            targetDest = player.position;
        }
        else if (PoliceManager.Instance != null)
        {
            targetDest = PoliceManager.Instance.lastKnownPosition;
        }

        // ANTI-SACCADES : On ne modifie la destination que si tu as bougé d'un mètre !
        if (Vector3.Distance(agent.destination, targetDest) > 1.0f)
        {
            agent.SetDestination(targetDest);
        }
    }

    public void ForcePanic()
    {
        if (role != NPCRole.Civil) return;

        ChangeState(AIState.Panique);
        if (locomotion == Locomotion.Pieton && agent != null) FleePedestrian();
    }

    private void FleeVehicle()
    {
        if (car == null || !car.isDrivenByAI) return;
        car.moveInput = 1f;
    }

    private void ChaseVehicle()
    {
        if (car == null || player == null || !car.isDrivenByAI) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        int stars = GameManager.Instance != null ? GameManager.Instance.wantedLevel : 0;
        CarAI ai = GetComponent<CarAI>();

        bool isPlayerInCar = player.GetComponentInParent<CarController>() != null;

        if (stars <= 2 && !isPlayerInCar && distToPlayer < 15f && !hasSpawnedCops)
        {
            if (ai != null) ai.enabled = false;
            car.moveInput = 0;
            car.turnInput = 0;
            car.isHandbraking = true;
            car.isDrivenByAI = false;
            DeployFootCops();
            return;
        }

        if (ai != null)
        {
            ai.chaseTarget = player;
        }
    }

    private void DeployFootCops()
    {
        hasSpawnedCops = true;
        if (copPedestrianPrefab == null || exitDoors == null) return;
        foreach (Transform door in exitDoors) Instantiate(copPedestrianPrefab, door.position, Quaternion.identity);
    }
}