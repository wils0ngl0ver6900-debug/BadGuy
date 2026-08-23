using UnityEngine;

// Circuit de course explicite, INDÉPENDANT du graphe TrafficNode utilisé par le trafic
// normal de la ville. Élimine tout risque d'embranchement mal configuré (plusieurs Next
// Nodes, boucle accidentelle...) : ici, c'est juste une liste ordonnée de points, le
// suivant est TOUJOURS le suivant dans la liste, sans ambiguïté possible.
//
// Mise en place : glisse les points du circuit dans "Waypoints", DANS L'ORDRE. Le premier
// sert de ligne de départ/arrivée. Le circuit se referme automatiquement du dernier point
// vers le premier (pas besoin de le remettre à la fin).
//
// Repère visuel : sélectionne cet objet dans la Hierarchy, les points et les segments qui
// les relient s'affichent en cyan dans la Scene view — un coup d'œil suffit pour repérer un
// point mal placé ou un ordre incorrect (contrairement au graphe TrafficNode, où une
// mauvaise connexion est invisible tant qu'on n'a pas cliqué sur chaque noeud un par un).
public class RaceCircuit : MonoBehaviour
{
    [Tooltip("Les points du circuit, DANS L'ORDRE. Le premier sert de ligne de départ/arrivée.")]
    public Transform[] waypoints;

    public int Count => waypoints != null ? waypoints.Length : 0;

    public Vector3 GetPoint(int index)
    {
        if (waypoints == null || waypoints.Length == 0) return transform.position;
        int wrapped = ((index % waypoints.Length) + waypoints.Length) % waypoints.Length;
        return waypoints[wrapped] != null ? waypoints[wrapped].position : transform.position;
    }

    public Transform StartFinish => (waypoints != null && waypoints.Length > 0 && waypoints[0] != null) ? waypoints[0] : transform;

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.color = (i == 0) ? Color.green : Color.cyan; // le premier point (départ/arrivée) ressort en vert
            Gizmos.DrawSphere(waypoints[i].position, 1f);

            Transform next = waypoints[(i + 1) % waypoints.Length];
            if (next != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }
    }
}