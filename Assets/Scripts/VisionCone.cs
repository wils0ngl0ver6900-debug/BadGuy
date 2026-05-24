using UnityEngine;

public class VisionCone : MonoBehaviour
{
    [Header("Paramètres de Vision")]
    public float viewRadius = 8f;
    [Range(0, 360)] public float viewAngle = 90f;

    [Header("Obstacles")]
    public LayerMask obstacleMask;

    private Transform player;
    public bool isSeeingPlayer = false;

    // LE FIX : On trouve le joueur dans Awake !
    void Awake()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (player == null) return;

        bool canSee = CheckVisibility();

        if (canSee && !isSeeingPlayer)
        {
            isSeeingPlayer = true;
            if (GameManager.Instance != null) GameManager.Instance.spottersCount++;
        }
        else if (!canSee && isSeeingPlayer)
        {
            isSeeingPlayer = false;
            if (GameManager.Instance != null) GameManager.Instance.spottersCount--;
        }
    }

    bool CheckVisibility()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= viewRadius)
        {
            Vector3 directionToPlayer = (player.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, directionToPlayer) < viewAngle / 2)
            {
                if (!Physics.Raycast(transform.position + Vector3.up, directionToPlayer, distanceToPlayer, obstacleMask))
                {
                    return true;
                }
            }
        }
        return false;
    }

    void OnDestroy()
    {
        if (isSeeingPlayer && GameManager.Instance != null)
        {
            GameManager.Instance.spottersCount--;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 viewAngleA = DirFromAngle(transform.eulerAngles.y, -viewAngle / 2);
        Vector3 viewAngleB = DirFromAngle(transform.eulerAngles.y, viewAngle / 2);

        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);

        if (isSeeingPlayer && player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up, player.position + Vector3.up);
        }
    }

    private Vector3 DirFromAngle(float eulerY, float angleInDegrees)
    {
        angleInDegrees += eulerY;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}