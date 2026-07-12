using UnityEngine;

public class DockerDropZone : MonoBehaviour
{
    [Header("Type de Zone")]
    public bool isIllegalZone = false; // Coche cette case pour la zone de contrebande

    private void OnTriggerEnter(Collider other)
    {
        // Si c'est le joueur qui entre dans la zone avec son corps
        if (other.CompareTag("Player"))
        {
            if (DockerJobManager.Instance != null && DockerJobManager.Instance.isCarryingCrate)
            {
                DockerJobManager.Instance.DeliverCrate(isIllegalZone);
            }
        }
    }
}