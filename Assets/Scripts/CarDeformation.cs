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

    [Header("Le modèle 3D de la carrosserie")]
    [Tooltip("Glissez ici le MeshFilter de la carrosserie de la voiture")]
    public MeshFilter carBodyMeshFilter;

    private Mesh originalMesh;
    private Mesh deformedMesh;
    private Vector3[] originalVertices;
    private Vector3[] displacedVertices;

    void Start()
    {
        if (carBodyMeshFilter != null)
        {
            originalMesh = carBodyMeshFilter.sharedMesh;
            // On clone le mesh pour ne pas déformer le fichier 3D source original de ton projet !
            deformedMesh = carBodyMeshFilter.mesh;

            originalVertices = originalMesh.vertices;
            displacedVertices = new Vector3[originalVertices.Length];
            System.Array.Copy(originalVertices, displacedVertices, originalVertices.Length);
        }
        else
        {
            Debug.LogWarning("CarDeformation : Aucun MeshFilter assigné sur " + gameObject.name);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // On ignore les petits chocs (trottoirs, piétons)
        if (collision.relativeVelocity.magnitude < minForceToDamage) return;
        if (carBodyMeshFilter == null) return;

        bool isDeformed = false;

        // On parcourt tous les points de contact du crash
        foreach (ContactPoint contact in collision.contacts)
        {
            // On convertit le point d'impact en coordonnées locales par rapport à la voiture
            Vector3 localContact = transform.InverseTransformPoint(contact.point);
            Vector3 localForce = transform.InverseTransformDirection(collision.relativeVelocity) * impactMultiplier;

            // On vérifie tous les points 3D de la carrosserie
            for (int i = 0; i < displacedVertices.Length; i++)
            {
                float dist = Vector3.Distance(localContact, displacedVertices[i]);

                // Si le point 3D est proche de l'impact, on l'enfonce
                if (dist < damageRadius)
                {
                    float damageEffect = (1f - (dist / damageRadius)); // Plus on est près du centre, plus ça s'enfonce
                    Vector3 deformation = localForce * damageEffect;

                    // On limite la déformation maximale pour éviter des bugs visuels horribles
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

        // Si la tôle a été touchée, on met à jour le modèle 3D visuel
        if (isDeformed)
        {
            deformedMesh.vertices = displacedVertices;
            deformedMesh.RecalculateNormals(); // Recalcule les reflets de lumière sur la tôle froissée
            deformedMesh.RecalculateBounds();
        }
    }
}