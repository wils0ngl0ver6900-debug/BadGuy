using UnityEngine;
using UnityEngine.Events;

// Trigger générique et réutilisable : pose cet objet n'importe où dans le monde (avec un
// Collider en "Is Trigger" dessus), branche ce que tu veux dans "On Player Enter" — lancer
// une quête, activer un objet, jouer un dialogue, n'importe quelle UnityEvent. Aucune
// logique de jeu propre ici, juste un déclencheur générique.
[RequireComponent(typeof(Collider))]
public class GenericTriggerZone : MonoBehaviour
{
    [Header("Événements")]
    [Tooltip("Déclenché quand le joueur entre dans la zone. Glisse n'importe quel objet + méthode ici (comme pour n'importe quel bouton).")]
    public UnityEvent onPlayerEnter;

    [Tooltip("Optionnel : déclenché quand le joueur ressort de la zone. Laisse vide si pas besoin.")]
    public UnityEvent onPlayerExit;

    [Header("Comportement")]
    [Tooltip("Coché : ne se déclenche qu'une seule fois par partie (typique pour lancer une quête). Décoche pour un déclencheur répétable à chaque passage.")]
    public bool triggerOnce = true;

    [Header("Repère visuel (Scene view uniquement, jamais visible en jeu)")]
    public Color gizmoColor = new Color(0f, 1f, 0.4f, 0.35f);

    private bool hasTriggered = false;

    private void Awake()
    {
        // Sécurité au cas où "Is Trigger" aurait été oublié dans l'Inspector.
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggerOnce && hasTriggered) return;

        hasTriggered = true;
        onPlayerEnter?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        onPlayerExit?.Invoke();
    }

    // Dessine la zone en vert semi-transparent dans l'éditeur (jamais visible en jeu) —
    // sans ça, un GameObject vide avec juste un collider est invisible à placer.
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = gizmoColor;

        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (col is SphereCollider sphere)
        {
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius * scale);
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);
        }
    }
}