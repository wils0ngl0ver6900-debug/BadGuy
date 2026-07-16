using UnityEngine;
using TMPro;

public class ValetJobStarter : MonoBehaviour
{
    [Header("UI d'interaction")]
    public GameObject interactPromptUI; // Un petit texte "Appuyez sur [E] pour travailler"

    private bool isPlayerInZone = false;

    private void Start()
    {
        if (interactPromptUI != null) interactPromptUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInZone = true;
            if (interactPromptUI != null) interactPromptUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInZone = false;
            if (interactPromptUI != null) interactPromptUI.SetActive(false);
        }
    }

    private void Update()
    {
        // Si le joueur est dans la zone et appuie sur E, on lance le job
        if (isPlayerInZone && Input.GetKeyDown(KeyCode.E))
        {
            if (ValetJobManager.Instance != null && !ValetJobManager.Instance.isJobActive)
            {
                ValetJobManager.Instance.StartJob();
                if (interactPromptUI != null) interactPromptUI.SetActive(false);
            }
        }
    }
}