using UnityEngine;
using System.Collections;

public class ValetJobTrigger : Interactable
{
    [Header("Discussion avec le Gérant")]
    public Dialogue bossDialogue;

    [Header("Mise en place & Cooldown ⏳")]
    public bool requireNextDay = true;
    private float lastTimeWorked = -9999f;

    public override void Interact()
    {
        // On empêche d'interagir si le joueur est déjà en train de travailler
        if (ValetJobManager.Instance != null && ValetJobManager.Instance.isJobActive) return;

        // --- SÉCURITÉ ANTI-POLICE ---
        if (GameManager.Instance != null && GameManager.Instance.wantedLevel > 0)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("Le gérant : 'Tu as les flics aux trousses ! Ne les ramène pas devant mon casino.'");
            return;
        }

        // --- COOLDOWN (Vérifie si une journée est passée) ---
        if (requireNextDay && TimeManager.Instance != null)
        {
            float secondsForFullDay = 1440f / TimeManager.Instance.timeScale;
            if (Time.time < lastTimeWorked + secondsForFullDay)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification("Le gérant : 'Le parking est calme, on n'a plus besoin de toi aujourd'hui. Reviens demain !'");
                return;
            }
        }

        // --- LANCEMENT DU DIALOGUE ---
        bossDialogue.onDialogueEnd.RemoveAllListeners();
        bossDialogue.onDialogueEnd.AddListener(OnDialogueFinished);

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(bossDialogue);
        }
    }

    private void OnDialogueFinished()
    {
        // On enregistre l'heure pour le cooldown
        lastTimeWorked = Time.time;

        // On appelle la fonction ShowJobOffer que l'on a créée dans le ValetJobManager
        if (ValetJobManager.Instance != null)
        {
            ValetJobManager.Instance.ShowJobOffer();
        }
    }
}