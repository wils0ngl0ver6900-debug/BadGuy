using UnityEngine;
using System.Collections.Generic;

public class TrafficNode : MonoBehaviour
{
    [Header("Points suivants (Connectez-les ici)")]
    public List<TrafficNode> nextNodes;

    // --- OUTIL PRO : Dessine la route en bleu dans l'éditeur Unity ---
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        if (nextNodes != null)
        {
            foreach (var node in nextNodes)
            {
                if (node != null)
                {
                    // Dessine une ligne vers le prochain point
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(transform.position, node.transform.position);

                    // Petite flèche pour indiquer le sens de circulation
                    Vector3 direction = (node.transform.position - transform.position).normalized;
                    Gizmos.DrawRay(transform.position, direction * 2f);
                }
            }
        }
    }
}