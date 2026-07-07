using UnityEngine;

public class MessageTrigger : MonoBehaviour
{
    [Header("Configuration du SMS ✉️")]
    public string contactName = "Boss";

    [TextArea(3, 6)]
    public string messageContent = "Va au point de rendez-vous.";

    public bool isFromPlayer = false;

    [Header("Paramètres de Déclenchement ⚙️")]
    [Tooltip("Cochez pour que le message ne s'envoie qu'une seule fois dans toute la partie.")]
    public bool triggerOnlyOnce = true;

    [Tooltip("COCHEZ pour déclencher au toucher (zone invisible). DÉCOCHEZ pour déclencher manuellement (ex: Voler une voiture).")]
    public bool triggerOnCollision = false; // <-- LE NOUVEAU RÉGLAGE EST LÀ

    private bool hasBeenTriggered = false;

    // La fonction qui envoie réellement le message
    public void SendTheMessage()
    {
        if (triggerOnlyOnce && hasBeenTriggered) return; // Sécurité anti-spam

        if (MessageApp.Instance != null)
        {
            MessageApp.Instance.ReceiveMessage(contactName, messageContent, isFromPlayer);
            hasBeenTriggered = true;
        }
    }

    // La fonction qui gère les collisions
    private void OnTriggerEnter(Collider other)
    {
        // Ne se déclenche QUE SI la case est cochée !
        if (triggerOnCollision && other.CompareTag("Player"))
        {
            SendTheMessage();
        }
    }
}