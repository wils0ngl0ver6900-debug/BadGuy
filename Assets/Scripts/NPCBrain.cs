using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class NPCBrain : MonoBehaviour
{
    public enum NPCRole { Civil, Policier, Gang }
    public enum Locomotion { Pieton, Vehicule }
    public enum AIState { Patrouille, Fuite, Poursuite, Panique, Combat, GardeDuCorps }

    [Header("Identité 🪪")]
    public NPCRole role = NPCRole.Civil;
    public TerritoryManager.Faction faction = TerritoryManager.Faction.None;

    [Header("Moteur Physique 🚶/🚗")]
    public Locomotion locomotion = Locomotion.Pieton;
    public AIState currentState = AIState.Patrouille;

    [Header("Paramètres de Déplacement ⚙️")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 5.0f;
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
    [HideInInspector] public Transform leader = null;

    private NavMeshAgent agent;
    private CarController car;
    private Transform player;
    private Animator animator;
    private bool hasSpawnedCops = false;
    private float callPoliceTimer = 0f;
    private float bustTimer = 0f;

    private float followTimer = 0f;
    private Vector3 formationOffset;

    // --- L'AMORTISSEUR (Le secret anti-vibration) ---
    private Quaternion smoothLeaderRot;

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
                agent.acceleration = 40f;
                agent.angularSpeed = 400f;
            }

            animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            formationOffset = new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-3.5f, -1.5f));
            followTimer = Random.Range(0f, 0.2f);
            smoothLeaderRot = transform.rotation;
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

        // Anti-collision physique avec le joueur maintenu !
        if (role == NPCRole.Gang && player != null)
        {
            Collider[] myColliders = GetComponentsInChildren<Collider>();
            Collider[] playerColliders = player.GetComponentsInChildren<Collider>();

            foreach (Collider myCol in myColliders)
            {
                foreach (Collider pCol in playerColliders)
                {
                    Physics.IgnoreCollision(myCol, pCol, true);
                }
            }
        }
    }

    void Update()
    {
        ExecuteStateAction();

        // Lissage visuel de l'animation pour les jambes
        if (animator != null && agent != null && locomotion == Locomotion.Pieton)
        {
            float targetSpeed = agent.velocity.magnitude;
            float currentSpeed = animator.GetFloat("Speed");
            animator.SetFloat("Speed", Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 10f));
        }
    }

    private CarController GetPlayerCar()
    {
        CarController[] allCars = FindObjectsOfType<CarController>();
        foreach (CarController c in allCars)
        {
            if (c != null && c.isDrivenByPlayer) return c;
        }
        return null;
    }

    private IEnumerator BrainTick()
    {
        while (true)
        {
            AnalyzeEnvironment();
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void AnalyzeEnvironment()
    {
        if (currentState == AIState.Panique) return;

        CarController playerCar = GetPlayerCar();
        bool isPlayerInCar = playerCar != null;

        Vector3 actualPlayerPos = isPlayerInCar ? playerCar.transform.position : (player != null ? player.position : transform.position);

        float distToPlayer = Vector3.Distance(transform.position, actualPlayerPos);
        isSeeingPlayer = distToPlayer <= visionRange;

        if (role != NPCRole.Civil)
        {
            Transform bestEnemy = null;
            float minDistance = Mathf.Infinity;

            if (role == NPCRole.Policier && GameManager.Instance != null && GameManager.Instance.wantedLevel > 0 && isSeeingPlayer)
            {
                bestEnemy = isPlayerInCar ? playerCar.transform : player;
                minDistance = distToPlayer;
                if (PoliceManager.Instance != null) PoliceManager.Instance.ReportPlayerSight(actualPlayerPos);
            }

            if (role == NPCRole.Gang && faction != TerritoryManager.Faction.Mafia && faction != TerritoryManager.Faction.None && isSeeingPlayer)
            {
                Transform potentialPlayerTarget = isPlayerInCar ? playerCar.transform : player;
                if (IsTargetAlive(potentialPlayerTarget))
                {
                    bestEnemy = potentialPlayerTarget;
                    minDistance = distToPlayer;
                }
            }

            Collider[] hitColliders = Physics.OverlapSphere(transform.position, visionRange);
            foreach (Collider hit in hitColliders)
            {
                NPCBrain otherNPC = hit.GetComponent<NPCBrain>();
                if (otherNPC != null && otherNPC != this)
                {
                    TargetHealth otherHealth = otherNPC.GetComponent<TargetHealth>();
                    if (otherHealth != null && otherHealth.isDead) continue;

                    bool isEnemy = false;
                    if (role == NPCRole.Policier && otherNPC.role == NPCRole.Gang) isEnemy = true;
                    if (role == NPCRole.Gang && otherNPC.role == NPCRole.Policier) isEnemy = true;
                    if (role == NPCRole.Gang && otherNPC.role == NPCRole.Gang && otherNPC.faction != this.faction) isEnemy = true;

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
            }

            if (bestEnemy != null)
            {
                currentTarget = bestEnemy;

                if (role == NPCRole.Policier && (bestEnemy == player || (isPlayerInCar && bestEnemy == playerCar.transform)))
                {
                    int stars = GameManager.Instance != null ? GameManager.Instance.wantedLevel : 0;
                    if (stars <= 2) ChangeState(AIState.Poursuite);
                    else if (stars >= 3)
                    {
                        if (isPlayerInCar && locomotion == Locomotion.Vehicule) ChangeState(AIState.Poursuite);
                        else ChangeState(AIState.Combat);
                    }
                    return;
                }

                ChangeState(AIState.Combat);
                return;
            }

            currentTarget = null;
        }

        if (role == NPCRole.Civil)
        {
            if (isSeeingPlayer && GameManager.Instance != null && GameManager.Instance.wantedLevel > 0) ChangeState(AIState.Fuite);
            else ChangeState(AIState.Patrouille);
        }
        else if (role == NPCRole.Policier)
        {
            if (GameManager.Instance != null && GameManager.Instance.wantedLevel > 0) ChangeState(AIState.Poursuite);
            else ChangeState(AIState.Patrouille);
        }
        else if (role == NPCRole.Gang)
        {
            if (leader != null) ChangeState(AIState.GardeDuCorps);
            else ChangeState(AIState.Patrouille);
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
            case AIState.GardeDuCorps:
                if (locomotion == Locomotion.Pieton) FollowLeader();
                break;
        }
    }

    // --- LE SUIVI ORGANIQUE SANS VIBRATION ---
    private void FollowLeader()
    {
        if (leader == null || agent == null || !agent.isOnNavMesh) return;

        // 1. L'AMORTISSEUR DE ROTATION
        // Filtre les micro-mouvements de ton personnage pour offrir une cible stable aux PNJ
        smoothLeaderRot = Quaternion.Slerp(smoothLeaderRot, leader.rotation, Time.deltaTime * 2f);
        Vector3 idealPosition = leader.position + (smoothLeaderRot * formationOffset);

        Vector3 finalDestination = leader.position;
        if (NavMesh.SamplePosition(idealPosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            finalDestination = hit.position;
        }

        float distToLeader = Vector3.Distance(transform.position, leader.position);
        float distToFormationPos = Vector3.Distance(transform.position, finalDestination);

        // 2. LA VITESSE PURE (Fini les bridages et les lissages qui faisaient ramper le PNJ)
        agent.speed = runSpeed;
        agent.stoppingDistance = 0.5f;

        // Le freinage d'urgence logiciel (L'anti-poussette en douceur)
        if (distToLeader < 1.5f)
        {
            agent.speed = walkSpeed * 0.5f; // Il ralentit fortement s'il est presque sur toi
        }
        else if (distToLeader > 8.0f)
        {
            agent.speed = runSpeed * 1.2f; // Petit boost s'il est très loin
        }

        if (agent.isStopped) agent.isStopped = false;

        // 3. LA MISE À JOUR DU GPS
        followTimer += Time.deltaTime;
        if (followTimer >= 0.2f)
        {
            followTimer = 0f;
            // On recalcule le chemin que si la cible amortie a vraiment bougé
            if (Vector3.Distance(agent.destination, finalDestination) > 0.5f)
            {
                agent.SetDestination(finalDestination);
            }
        }

        // 4. LE PIVOT DE GARDE DU CORPS (À L'ARRÊT)
        if (distToFormationPos <= agent.stoppingDistance + 0.5f && agent.velocity.sqrMagnitude < 0.2f)
        {
            Vector3 lookDir = leader.forward;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 5f);
            }
        }
    }

    public void ChangeState(AIState newState)
    {
        if (currentState == newState) return;

        if (newState == AIState.Combat && currentState != AIState.Combat)
        {
            nextFireTime = Time.time + Random.Range(0.1f, 0.8f);
        }

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
        TargetHealth myHealth = GetComponent<TargetHealth>();
        if (myHealth != null && (myHealth.isDead || myHealth.isKnockedOut)) return;

        if (locomotion == Locomotion.Vehicule && car != null && !car.isDrivenByAI) return;

        if (role == NPCRole.Policier && currentState == AIState.Poursuite)
        {
            CarController targetCar = GetPlayerCar();
            bool isPlayerInCar = targetCar != null;

            if (!isPlayerInCar && collision.gameObject.CompareTag("Player"))
            {
                if (GameManager.Instance != null) GameManager.Instance.Busted();
            }

            if (locomotion == Locomotion.Vehicule && isPlayerInCar)
            {
                if (targetCar != null && collision.gameObject.transform.root == targetCar.transform.root)
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
            if (leader != null) ChangeState(AIState.GardeDuCorps);
            else ChangeState(AIState.Patrouille);
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
            nextFireTime = Time.time + fireRate * Random.Range(0.8f, 1.2f);

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

        agent.stoppingDistance = 0f;

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

        agent.stoppingDistance = 0f;

        Vector3 directionAway = (transform.position - (player != null ? player.position : transform.position)).normalized;
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

        CarController targetCar = GetPlayerCar();
        bool isPlayerInCar = targetCar != null;

        Vector3 targetDest = transform.position;
        float distToHull = 5f;

        if (isPlayerInCar)
        {
            Transform doorPoint = null;
            Transform[] allChildren = targetCar.GetComponentsInChildren<Transform>();
            foreach (Transform t in allChildren)
            {
                if (t.name.Contains("ExitPoint") || t.name.Contains("DoorTrigger"))
                {
                    doorPoint = t;
                    break;
                }
            }

            if (doorPoint != null) targetDest = doorPoint.position;
            else targetDest = targetCar.transform.position;

            Collider[] allCols = targetCar.GetComponentsInChildren<Collider>();
            float minDist = Mathf.Infinity;
            foreach (Collider col in allCols)
            {
                if (col.isTrigger) continue;
                Vector3 cp = col.ClosestPoint(transform.position);
                float d = Vector3.Distance(transform.position, cp);
                if (d < minDist) minDist = d;
            }
            distToHull = minDist;

            Vector3 localCopPos = targetCar.transform.InverseTransformPoint(transform.position);
            float distToTargetDoor = Vector3.Distance(transform.position, targetDest);

            if (distToHull <= 1.0f && distToTargetDoor > 2.0f)
            {
                Vector3 localDoorPos = targetCar.transform.InverseTransformPoint(targetDest);
                float detourX = localDoorPos.x * 1.6f;
                float detourZ = localCopPos.z;

                if (Mathf.Abs(detourX) < 0.5f) detourX = -2.2f;

                Vector3 localDetour = new Vector3(detourX, 0f, detourZ);
                targetDest = targetCar.transform.TransformPoint(localDetour);
            }
        }
        else
        {
            if (isSeeingPlayer && player != null) targetDest = player.position;
            else if (PoliceManager.Instance != null) targetDest = PoliceManager.Instance.lastKnownPosition;
        }

        bool isTouchingCar = isPlayerInCar && (distToHull <= 0.75f);
        agent.stoppingDistance = isPlayerInCar ? 0.1f : 1.0f;

        if (!isTouchingCar && Vector3.Distance(agent.transform.position, targetDest) > agent.stoppingDistance)
        {
            agent.isStopped = false;
            agent.SetDestination(targetDest);
        }
        else if (isTouchingCar || Vector3.Distance(agent.transform.position, targetDest) <= agent.stoppingDistance)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (role == NPCRole.Policier && isPlayerInCar && isSeeingPlayer && targetCar != null)
        {
            Rigidbody carRb = targetCar.GetComponent<Rigidbody>();

            Transform realDoor = null;
            Transform[] allChildren = targetCar.GetComponentsInChildren<Transform>();
            foreach (Transform t in allChildren)
            {
                if (t.name.Contains("ExitPoint") || t.name.Contains("DoorTrigger")) { realDoor = t; break; }
            }
            Vector3 exactDoorPos = realDoor != null ? realDoor.position : targetCar.transform.position;
            float distanceToRealDoor = Vector3.Distance(transform.position, exactDoorPos);

            if (distanceToRealDoor <= 1.8f && carRb != null && carRb.linearVelocity.magnitude < 5.0f)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;

                Vector3 lookDir = targetCar.transform.position - transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), 0.5f);

                bustTimer += 0.2f;

                if (bustTimer >= 1.4f)
                {
                    if (GameManager.Instance != null) GameManager.Instance.Busted();
                    bustTimer = 0f;
                }
            }
            else
            {
                bustTimer = 0f;
            }
        }
        else
        {
            bustTimer = 0f;
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
        CarController targetCar = GetPlayerCar();
        bool isPlayerInCar = targetCar != null;

        if (car == null || !car.isDrivenByAI || (!isPlayerInCar && player == null)) return;

        float distToPlayer = Vector3.Distance(transform.position, isPlayerInCar ? targetCar.transform.position : player.position);
        int stars = GameManager.Instance != null ? GameManager.Instance.wantedLevel : 0;
        CarAI ai = GetComponent<CarAI>();

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
            ai.chaseTarget = isPlayerInCar ? targetCar.transform : player;
        }
    }

    private void DeployFootCops()
    {
        hasSpawnedCops = true;
        if (copPedestrianPrefab == null || exitDoors == null) return;
        foreach (Transform door in exitDoors) Instantiate(copPedestrianPrefab, door.position, Quaternion.identity);
    }
}