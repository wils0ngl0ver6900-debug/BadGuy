using UnityEngine;

public class FixIconRotation : MonoBehaviour
{
    private Quaternion initialRotation;
    private Transform targetToFollow;

    void Start()
    {
        // On mémorise la rotation de départ (X=90, Y=0, Z=0)
        initialRotation = transform.rotation;

        // Si l'icône est accrochée à un objet (Joueur ou Voiture)
        if (transform.parent != null)
        {
            // On mémorise qui on doit suivre
            targetToFollow = transform.parent;

            // On se détache ! Ainsi, on ne subit plus les déformations de taille (Scale)
            transform.SetParent(null);
        }
    }

    void LateUpdate()
    {
        // 1. On force la rotation parfaite vers le Nord
        transform.rotation = initialRotation;

        // 2. On suit la position de notre cible de haut
        if (targetToFollow != null)
        {
            // On se place exactement sur la cible, mais 15 mètres plus haut
            transform.position = targetToFollow.position + Vector3.up * 15f;
        }
    }
}