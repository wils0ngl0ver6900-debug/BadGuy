using UnityEngine;
using System.Collections;

public class VisionCone : MonoBehaviour
{
    [Header("Param�tres de Vision")]
    public float viewRadius = 15f;
    [Range(0, 360)] public float viewAngle = 90f;
    public LayerMask obstacleMask; // N'oublie pas de cocher les murs/d�cors dans Unity !

    [HideInInspector] public bool isSeeingPlayer = false;
    private Transform playerTarget;

    [Header("Discretion (Masque)")]
    [Tooltip("Multiplie le rayon de detection quand le joueur porte un objet marque isMask. 0.5 = distance de detection divisee par 2.")]
    [Range(0.1f, 1f)] public float maskDetectionMultiplier = 0.5f;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTarget = p.transform;

        // Optimisation : Le c�ne de vision scanne 5 fois par seconde au lieu de 60
        StartCoroutine(FindPlayerWithDelay(0.2f));
    }

    IEnumerator FindPlayerWithDelay(float delay)
    {
        while (true)
        {
            yield return new WaitForSeconds(delay);
            FindVisiblePlayer();
        }
    }

    void FindVisiblePlayer()
    {
        if (playerTarget == null) return;

        isSeeingPlayer = false; // Par d�faut, on ne le voit pas
        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        // Un masque ne rend pas invisible : il reduit juste la distance a laquelle on te
        // repere, comme si tu te fondais mieux dans la foule / etais moins reconnaissable.
        float effectiveRadius = viewRadius;
        if (EquipmentManager.Instance != null && EquipmentManager.Instance.IsWearingMask())
        {
            effectiveRadius *= maskDetectionMultiplier;
        }
        if (distToPlayer < effectiveRadius)
        {
            Vector3 dirToPlayer = (playerTarget.position - transform.position).normalized;

            // Le joueur est-il dans l'angle de vision ?
            if (Vector3.Angle(transform.forward, dirToPlayer) < viewAngle / 2f)
            {
                // Un mur nous bloque-t-il la vue ?
                if (!Physics.Raycast(transform.position + Vector3.up, dirToPlayer, distToPlayer, obstacleMask))
                {
                    isSeeingPlayer = true; // Le joueur est visible !
                }
            }
        }
    }

    // --- DESSINE LE C�NE DANS L'�DITEUR ---
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