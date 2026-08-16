using UnityEngine;
using UnityEngine.AI;

// Comportement léger du PNJ "client" qui attend dans une DrugDealZone.
[RequireComponent(typeof(NavMeshAgent))]
public class DrugClientNPC : MonoBehaviour
{
    private enum ClientState { Attente, EnCoursDeService, Depart }

    [Header("Errance dans la zone")]
    public float wanderIdleTimeMin = 2f;
    public float wanderIdleTimeMax = 6f;

    private NavMeshAgent agent;
    private TargetHealth targetHealth;
    private Animator anim;
    private ClientState state = ClientState.Attente;

    private Vector3 zoneCenter;
    private float zoneRadius;
    private Transform exitPoint;
    private DrugDealZone parentZone;

    private float nextWanderTime;
    private float leaveTimeoutTimer;
    private const float LEAVE_TIMEOUT = 20f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        targetHealth = GetComponent<TargetHealth>();
        anim = GetComponentInChildren<Animator>();
    }

    public void Initialize(Vector3 center, float radius, Transform exit, DrugDealZone zone)
    {
        zoneCenter = center;
        zoneRadius = radius;
        exitPoint = exit;
        parentZone = zone;
        PickNewWanderTarget();
    }

    void Update()
    {
        if (targetHealth != null && targetHealth.isDead) return;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        if (anim != null)
        {
            anim.SetFloat("Speed", agent.velocity.magnitude);
        }

        switch (state)
        {
            case ClientState.Attente:
                if (!agent.pathPending && agent.remainingDistance < 0.5f && Time.time >= nextWanderTime)
                {
                    PickNewWanderTarget();
                }
                break;

            case ClientState.Depart:
                leaveTimeoutTimer -= Time.deltaTime;
                bool arrived = !agent.pathPending && agent.remainingDistance < 1f;
                if (arrived || leaveTimeoutTimer <= 0f)
                {
                    Destroy(gameObject);
                }
                break;

            case ClientState.EnCoursDeService:
                break;
        }
    }

    // Note : la réaction aux collisions avec une voiture est désormais entièrement gérée
    // par CarController.OnCollisionEnter (détection via TargetHealth, commun à tous les
    // piétons du jeu) et TargetHealth.TemporaryRagdoll (qui désactive/réactive lui-même le
    // NavMeshAgent, l'Animator et le collider). Plus besoin de logique spécifique ici.

    private void PickNewWanderTarget()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        Vector2 randomCircle = Random.insideUnitCircle * zoneRadius;
        Vector3 candidate = zoneCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, zoneRadius + 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        nextWanderTime = Time.time + Random.Range(wanderIdleTimeMin, wanderIdleTimeMax);
    }

    public void OnSaleStarted()
    {
        if (targetHealth != null && targetHealth.isDead) return;
        state = ClientState.EnCoursDeService;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
    }

    public void OnSaleResolved(bool success)
    {
        if (targetHealth != null && targetHealth.isDead) return;

        state = ClientState.Depart;
        leaveTimeoutTimer = LEAVE_TIMEOUT;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        Vector3 destination;
        if (exitPoint != null)
        {
            destination = exitPoint.position;
        }
        else
        {
            Vector2 dir = Random.insideUnitCircle.normalized * (zoneRadius + 15f);
            destination = zoneCenter + new Vector3(dir.x, 0f, dir.y);
        }

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 25f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }

        if (!success) agent.speed *= 1.6f;
    }

    void OnDestroy()
    {
        if (parentZone != null) parentZone.NotifyClientLeft(gameObject);
    }
}