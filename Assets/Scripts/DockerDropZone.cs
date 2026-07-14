using UnityEngine;

public class DockerDropZone : MonoBehaviour
{
    [Header("Type de Zone")]
    public bool isIllegalZone = false;

    [Header("Marqueur Visuel (Hologramme)")]
    public GameObject markerVisual;

    private void Start()
    {
        if (markerVisual != null) markerVisual.SetActive(false);
    }

    private void Update()
    {
        if (markerVisual != null && DockerJobManager.Instance != null)
        {
            bool shouldShow = DockerJobManager.Instance.isCarryingCrate;

            if (shouldShow)
            {
                if (isIllegalZone)
                {
                    // Le receleur n'accepte de s'afficher que si tu as la bonne came
                    shouldShow = DockerJobManager.Instance.isCurrentCrateIllegal;
                }
                else
                {
                    // Le camion légal accepte de réceptionner n'importe quoi
                    shouldShow = true;
                }
            }

            if (markerVisual.activeSelf != shouldShow)
            {
                markerVisual.SetActive(shouldShow);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (DockerJobManager.Instance != null && DockerJobManager.Instance.isCarryingCrate)
            {
                DockerJobManager.Instance.DeliverCrate(isIllegalZone);
            }
        }
    }
}