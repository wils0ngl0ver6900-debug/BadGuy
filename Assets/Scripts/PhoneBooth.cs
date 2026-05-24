using UnityEngine;
using System.Collections;

public class PhoneBooth : MonoBehaviour
{
    [Header("Paramètres d'appel")]
    public float timeBetweenCalls = 45f; // Sonne toutes les 45 secondes
    public float ringDuration = 10f;     // Sonne pendant 10 secondes avant de raccrocher

    private bool isRinging = false;
    private float callTimer = 0f;

    void Update()
    {
        // Si on a déjà un contrat, la cabine ne sonne pas
        if (ContractManager.Instance != null && ContractManager.Instance.hasActiveContract) return;

        callTimer += Time.deltaTime;

        if (callTimer >= timeBetweenCalls && !isRinging)
        {
            StartCoroutine(RingRoutine());
        }
    }

    IEnumerator RingRoutine()
    {
        isRinging = true;
        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("Une cabine sonne au loin...");

        // ICI : Tu pourras rajouter un AudioSource.Play() pour le son de sonnerie !

        yield return new WaitForSeconds(ringDuration);

        isRinging = false;
        callTimer = 0f;
    }

    // Le joueur est dans la zone de la cabine
    private void OnTriggerStay(Collider other)
    {
        if (isRinging && other.CompareTag("Player"))
        {
            // Affiche l'indication pour décrocher
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("Appuyez sur [E] pour décrocher");

            if (Input.GetKeyDown(KeyCode.E))
            {
                isRinging = false;
                callTimer = 0f;
                ContractManager.Instance.AssignContract(); // Donne la mission !
            }
        }
    }
}