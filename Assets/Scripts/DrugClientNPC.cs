using UnityEngine;
using UnityEngine.AI;

// Comportement léger du PNJ "client" qui attend dans une DrugDealZone.
// Volontairement séparé de NPCBrain (726 lignes, rôle Policier/Gang/Civil déjà complexe) :
// ce PNJ n'a besoin ni de vision, ni de fuite face à la police, ni de combat — juste
// d'attendre dans une petite zone, de se faire servir, puis de partir. Le coupler à
// NPCBrain aurait ajouté de la complexité et du risque pour un besoin très ciblé.
//
// Doit être placé sur le même GameObject qu'un Interactable (type = SellDrugs) et un
// NavMeshAgent + un Collider (isTrigger = true) pour être détectable par PlayerController.
[RequireComponent(typeof(NavMeshAgent))]
public class DrugClientNPC : MonoBehaviour
{
    private enum ClientState { Attente, EnCoursDeService, Depart }

    [Header("Errance dans la zone")]
    public float wanderIdleTimeMin = 2f;
    public float wanderIdleTimeMax = 6f;

    private NavMeshAgent agent;
    private ClientState state = ClientState.Attente;

    private Vector3 zoneCenter;
    private float zoneRadius;
    private Transform exitPoint;
    private DrugDealZone parentZone;

    private float nextWanderTime;
    private float leaveTimeoutTimer;
    private const float LEAVE_TIMEOUT = 20f; // Sécurité : si le NPC reste coincé, on le détruit quand même

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Appelé par DrugDealZone juste après Instantiate()
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
                    if (parentZone != null) parentZone.NotifyClientLeft(gameObject);
                    Destroy(gameObject);
                }
                break;

            case ClientState.EnCoursDeService:
                // Immobile, ne fait rien de spécial ici — géré par Interactable.SellDrugRoutine()
                break;
        }
    }

    private void PickNewWanderTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * zoneRadius;
        Vector3 candidate = zoneCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, zoneRadius + 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        nextWanderTime = Time.time + Random.Range(wanderIdleTimeMin, wanderIdleTimeMax);
    }

    // --- Appelé par Interactable.SellDrugRoutine() ---

    public void OnSaleStarted()
    {
        state = ClientState.EnCoursDeService;
        agent.ResetPath();
    }

    public void OnSaleResolved(bool success)
    {
        state = ClientState.Depart;
        leaveTimeoutTimer = LEAVE_TIMEOUT;

        Vector3 destination;
        if (exitPoint != null)
        {
            destination = exitPoint.position;
        }
        else
        {
            // Pas de point de sortie assigné : on part loin dans une direction aléatoire hors zone
            Vector2 dir = Random.insideUnitCircle.normalized * (zoneRadius + 15f);
            destination = zoneCenter + new Vector3(dir.x, 0f, dir.y);
        }

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 25f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }

        // Si la vente a échoué (client méfiant qui appelle les flics), on accélère un peu son départ
        if (!success) agent.speed *= 1.6f;
    }
}
