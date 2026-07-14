using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BouncerJobTrigger : Interactable
{
    [Header("Discussion avec le Patron (Job Videur)")]
    public Dialogue bossDialogue;

    [Header("UI du Choix (Proposition de Job) 📋")]
    public GameObject jobOfferPanel;
    public Button acceptButton;
    public Button declineButton;

    [Header("Mise en place & Cooldown ⏳")]
    public Transform bouncerStandPosition;
    [Tooltip("Bloque le job jusqu'au lendemain en jeu")]
    public bool requireNextDay = true;
    private float lastTimeWorked = -9999f;

    private bool isTransitioning = false;
    private PlayerController playerController;

    private void Start()
    {
        if (jobOfferPanel != null) jobOfferPanel.SetActive(false);
        if (acceptButton != null) acceptButton.onClick.AddListener(AcceptJob);
        if (declineButton != null) declineButton.onClick.AddListener(DeclineJob);

        playerController = FindObjectOfType<PlayerController>();
    }

    public override void Interact()
    {
        if (isTransitioning) return;

        // --- GESTION DU COOLDOWN (24 Heures In-Game) ---
        if (requireNextDay && TimeManager.Instance != null)
        {
            // Calcule combien de secondes IRL durent 24h dans ton jeu
            float secondsForFullDay = 1440f / TimeManager.Instance.timeScale;

            if (Time.time < lastTimeWorked + secondsForFullDay)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification("Le patron : 'T'as déjà fait ton service. Reviens demain soir !'");
                return;
            }
        }

        bossDialogue.onDialogueEnd.RemoveAllListeners();
        bossDialogue.onDialogueEnd.AddListener(ShowJobOffer);

        if (DialogueManager.Instance != null) DialogueManager.Instance.StartDialogue(bossDialogue);
    }

    private void ShowJobOffer()
    {
        if (jobOfferPanel != null)
        {
            jobOfferPanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (playerController != null) playerController.enabled = false;
            if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = false;
        }
    }

    public void AcceptJob()
    {
        if (jobOfferPanel != null) jobOfferPanel.SetActive(false);
        if (!isTransitioning) StartCoroutine(NightShiftRoutine());
    }

    public void DeclineJob()
    {
        if (jobOfferPanel != null) jobOfferPanel.SetActive(false);

        // LE CORRECTIF EST ICI : Confined au lieu de Locked
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        if (playerController != null) playerController.enabled = true;
        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = true;

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Vous avez refusé le job pour l'instant.");
    }

    private IEnumerator NightShiftRoutine()
    {
        isTransitioning = true;

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));
        }

        yield return new WaitForSeconds(1.5f);

        // FORCER L'HEURE À 23H00
        if (TimeManager.Instance != null) TimeManager.Instance.currentTimeOfDay = 1380f;

        if (bouncerStandPosition != null && playerController != null)
        {
            playerController.transform.position = bouncerStandPosition.position;
            playerController.transform.rotation = bouncerStandPosition.rotation;
        }

        if (BouncerJobManager.Instance != null) BouncerJobManager.Instance.StartJob();

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));
            UIManager.Instance.transitionPanel.SetActive(false);
        }

        lastTimeWorked = Time.time;
        isTransitioning = false;
    }
}