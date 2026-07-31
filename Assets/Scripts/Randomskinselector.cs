using UnityEngine;

// Script générique et réutilisable : place-le sur n'importe quel PNJ ayant plusieurs
// variantes de skin en enfants directs (chacune avec son propre squelette/Animator/
// Rigidbody de ragdoll). Au spawn, une seule variante reste active, choisie au hasard,
// toutes les autres sont désactivées.
//
// IMPORTANT : tourne en Awake(), pas en Start(). Unity garantit que TOUS les Awake()
// de la scène s'exécutent avant TOUS les Start() — donc même si TargetHealth est sur
// le même objet et fait des choses au démarrage (DisableRagdoll(), snapshot des os...),
// il ne verra QUE le skin déjà choisi, jamais les 12 en même temps.
public class RandomSkinSelector : MonoBehaviour
{
    [Tooltip("Laisse vide pour utiliser automatiquement tous les enfants directs de cet objet comme variantes de skin.")]
    public GameObject[] skinVariants;

    void Awake()
    {
        GameObject[] pool = (skinVariants != null && skinVariants.Length > 0)
            ? skinVariants
            : GetDirectChildren();

        if (pool.Length == 0) return;

        int chosen = Random.Range(0, pool.Length);

        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] != null) pool[i].SetActive(i == chosen);
        }
    }

    private GameObject[] GetDirectChildren()
    {
        GameObject[] children = new GameObject[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            children[i] = transform.GetChild(i).gameObject;
        }
        return children;
    }
}