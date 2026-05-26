using UnityEngine;

public class CarDeformation : MonoBehaviour
{
    [Header("Paramètres de Déformation")]
    [Tooltip("La force minimale du choc pour froisser la tôle")]
    public float minForceToDamage = 3.0f;
    [Tooltip("Le rayon d'impact (taille du cratère)")]
    public float damageRadius = 1.0f;
    [Tooltip("Profondeur maximale de la bosse (pour ne pas aplatir la voiture)")]
    public float maxDeformation = 0.5f;
    [Tooltip("Multiplicateur de force")]
    public float impactMultiplier = 0.05f;

    [Header("Debug")]
    [Tooltip("Laissé vide, le script trouvera la carrosserie tout seul.")]
    public MeshFilter carBodyMeshFilter;

    private Mesh originalMesh;
    private Mesh deformedMesh;
    private Vector3[] originalVertices;
    private Vector3[] displacedVertices;

    private bool isInitialized = false;

    void Start()
    {
        AutoFindCarBody();
        InitializeMesh();
    }

    // --- RECHERCHE AUTOMATIQUE INTÉLLIGENTE ---
    private void AutoFindCarBody()
    {
        // Si tu as quand même mis un truc à la main, on le garde
        if (carBodyMeshFilter != null) return;

        // CORRECTIF : On utilise 'transform.root' pour scanner depuis le sommet du Prefab
        MeshFilter[] allFilters = transform.root.GetComponentsInChildren<MeshFilter>();
        int maxVertices = 0;
        MeshFilter bestFilter = null;

        foreach (MeshFilter mf in allFilters)
        {
            if (mf.sharedMesh == null) continue;

            // On exclut les roues, vitres et phares via leurs noms habituels
            string objName = mf.gameObject.name.ToLower();
            if (objName.Contains("wheel") || objName.Contains("roue") || objName.Contains("tire") ||
                objName.Contains("glass") || objName.Contains("vitre") || objName.Contains("light"))
            {
                continue;
            }

            // La carrosserie est généralement l'objet avec le plus de détails (vertices)
            if (mf.sharedMesh.vertexCount > maxVertices)
            {
                maxVertices = mf.sharedMesh.vertexCount;
                bestFilter = mf;
            }
        }

        if (bestFilter != null)
        {
            carBodyMeshFilter = bestFilter;
            Debug.Log($"<color=green>[CarDeformation] Carrosserie trouvée auto : {bestFilter.gameObject.name}</color>");
        }
    }

    private void InitializeMesh()
    {
        if (carBodyMeshFilter == null)
        {
            Debug.LogError($"[CarDeformation] ÉCHEC : Aucune carrosserie trouvée sur {gameObject.name}.");
            return;
        }

        originalMesh = carBodyMeshFilter.sharedMesh;
        if (originalMesh == null) return;

        if (!originalMesh.isReadable)
        {
            Debug.LogError($"[CarDeformation] ERREUR : Le modèle '{originalMesh.name}' n'est pas modifiable ! Cochez 'Read/Write Enabled' dans l'onglet Model du fichier 3D.");
            return;
        }

        deformedMesh = carBodyMeshFilter.mesh;
        originalVertices = originalMesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];
        System.Array.Copy(originalVertices, displacedVertices, originalVertices.Length);

        isInitialized = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isInitialized) return;

        if (collision.relativeVelocity.magnitude < minForceToDamage) return;

        bool isDeformed = false;

        foreach (ContactPoint contact in collision.contacts)
        {
            Vector3 localContact = transform.InverseTransformPoint(contact.point);
            Vector3 localForce = transform.InverseTransformDirection(collision.relativeVelocity) * impactMultiplier;

            for (int i = 0; i < displacedVertices.Length; i++)
            {
                float dist = Vector3.Distance(localContact, displacedVertices[i]);

                if (dist < damageRadius)
                {
                    float damageEffect = (1f - (dist / damageRadius));
                    Vector3 deformation = localForce * damageEffect;

                    Vector3 totalDeformation = (displacedVertices[i] + deformation) - originalVertices[i];
                    if (totalDeformation.magnitude > maxDeformation)
                    {
                        totalDeformation = totalDeformation.normalized * maxDeformation;
                        displacedVertices[i] = originalVertices[i] + totalDeformation;
                    }
                    else
                    {
                        displacedVertices[i] += deformation;
                    }

                    isDeformed = true;
                }
            }
        }

        if (isDeformed && deformedMesh != null && displacedVertices != null)
        {
            deformedMesh.vertices = displacedVertices;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateBounds();
        }
    }
}