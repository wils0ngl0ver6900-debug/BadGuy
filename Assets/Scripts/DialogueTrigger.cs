using UnityEngine;

// On hérite de "Interactable" pour que ton PlayerController le détecte automatiquement avec la touche E
public class DialogueTrigger : Interactable
{
    [Header("Scénario")]
    public Dialogue dialogue;

    // Cette fonction est appelée automatiquement par PlayerController.cs quand on s'approche et qu'on appuie sur E !
    public override void Interact() // Retire le mot "override" si tu as une erreur de compilation avec ton script Interactable de base
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(dialogue);

            // Cache le tooltip "Appuyez sur E"
            if (UIManager.Instance != null) UIManager.Instance.HideNotification();
        }
    }
}