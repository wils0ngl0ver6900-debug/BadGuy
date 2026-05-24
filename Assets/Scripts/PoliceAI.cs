using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class PoliceAI : MonoBehaviour
{
    public enum CopState { Patrol, Chase }
    public CopState currentState = CopState.Patrol;

    [Header("Mouvement & Patrouille")]
    public Transform[] waypoints;
    public float patrolSpeed = 3f;
    public float chaseSpeed = 6f;
    public float waitTimeAtWaypoint = 2f;

    private int currentWaypointIndex;
    private NavMeshAgent agent;
    private bool isWaiting;

    [Header("Vision & Détection 👁️")]
    public float viewRadius = 15f;
    [Range(0, 360)] public float viewAngle = 90f;
    public LayerMask obstacleMask;

    private Transform playerTarget;
    private bool isSpotted = false;

    // NOUVEAU : Chronos séparés pour la montée des stats et les notifications
    private float crimeDetectionTimer = 0f;
    private float crimeNotificationTimer = 2f; // Initialisé à 2 pour afficher le message dès la 1ère frame

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTarget = p.transform;

        if (waypoints.Length > 0)
        {
            agent.SetDestination(waypoints[0].position);
        }
    }

    void Update()
    {
        if (playerTarget == null) return;

        FieldOfViewCheck();

        if (GameManager.Instance != null && GameManager.Instance.notoriety >= 50)
        {
            currentState = CopState.Chase;
        }
        else
        {
            currentState = CopState.Patrol;
        }

        if (currentState == CopState.Patrol)
        {
            agent.speed = patrolSpeed;
            if (!isWaiting && waypoints.Length > 0)
            {
                PatrolRoutine();
            }
        }
        else if (currentState == CopState.Chase)
        {
            agent.speed = chaseSpeed;
            agent.SetDestination(playerTarget.position);
        }
    }

    private void PatrolRoutine()
    {
        if (agent.remainingDistance < 0.5f && !agent.pathPending)
        {
            StartCoroutine(WaitAtWaypoint());
        }
    }

    private IEnumerator WaitAtWaypoint()
    {
        isWaiting = true;
        yield return new WaitForSeconds(waitTimeAtWaypoint);

        if (waypoints.Length > 0)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }

        isWaiting = false;
    }

    private void FieldOfViewCheck()
    {
        Vector3 targetPos = playerTarget.position + Vector3.up;
        Vector3 rayStart = transform.position + Vector3.up;

        Vector3 directionToPlayer = (targetPos - rayStart).normalized;
        float distanceToPlayer = Vector3.Distance(rayStart, targetPos);

        bool canSeePlayerNow = false;

        if (distanceToPlayer < viewRadius)
        {
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            if (angleToPlayer < viewAngle / 2f)
            {
                if (Physics.Raycast(rayStart, directionToPlayer, out RaycastHit hit, distanceToPlayer, obstacleMask))
                {
                    if (hit.collider.CompareTag("Player"))
                    {
                        canSeePlayerNow = true;
                    }
                }
                else
                {
                    canSeePlayerNow = true;
                }
            }
        }

        if (canSeePlayerNow)
        {
            if (!isSpotted)
            {
                isSpotted = true;
                if (GameManager.Instance != null) GameManager.Instance.spottersCount++;
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Un flic vous observe...</color>");
            }

            CheckPlayerIllegalActions();
        }
        else
        {
            if (isSpotted)
            {
                isSpotted = false;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.spottersCount = Mathf.Max(0, GameManager.Instance.spottersCount - 1);
                }
            }

            // On réinitialise les chronos si le flic nous perd de vue
            crimeDetectionTimer = 0f;
            crimeNotificationTimer = 2f;
        }
    }

    // --- LE SYSTÈME TEMPS RÉEL EST ICI ---
    private void CheckPlayerIllegalActions()
    {
        bool isCommittingCrime = false;

        // 1. Est-ce qu'il tient une arme ?
        if (HotbarManager.Instance != null)
        {
            ItemData equippedItem = HotbarManager.Instance.GetEquippedItem();
            if (equippedItem != null && equippedItem.isWeapon && equippedItem.isIllegal)
            {
                isCommittingCrime = true;
            }
        }

        // 2. Est-ce qu'il porte une cagoule ?
        if (EquipmentManager.Instance != null)
        {
            ItemData headItem = EquipmentManager.Instance.currentEquipment[0];
            if (headItem != null && headItem.isMask)
            {
                isCommittingCrime = true;
            }
        }

        if (isCommittingCrime)
        {
            crimeDetectionTimer += Time.deltaTime;
            crimeNotificationTimer += Time.deltaTime;

            // Ajoute +1 de notoriété toutes les 0.2 secondes (donc +10 en 2 secondes)
            if (crimeDetectionTimer >= 0.2f)
            {
                if (GameManager.Instance != null) GameManager.Instance.IncreaseNotoriety(1);
                crimeDetectionTimer -= 0.2f;
            }

            // Affiche un texte rouge à l'écran seulement toutes les 2 secondes (pour ne pas spammer)
            if (crimeNotificationTimer >= 2f)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification("<color=red>Comportement illégal repéré !</color>");
                crimeNotificationTimer = 0f;
            }
        }
        else
        {
            crimeDetectionTimer = 0f;
            crimeNotificationTimer = 2f;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState == CopState.Chase && collision.gameObject.CompareTag("Player"))
        {
            if (GameManager.Instance != null) GameManager.Instance.Busted();
        }
    }

    private void OnDestroy()
    {
        if (isSpotted && GameManager.Instance != null)
        {
            GameManager.Instance.spottersCount = Mathf.Max(0, GameManager.Instance.spottersCount - 1);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);
        Vector3 viewAngleA = DirFromAngle(transform.eulerAngles.y, -viewAngle / 2);
        Vector3 viewAngleB = DirFromAngle(transform.eulerAngles.y, viewAngle / 2);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);
    }

    private Vector3 DirFromAngle(float eulerY, float angleInDegrees)
    {
        angleInDegrees += eulerY;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}