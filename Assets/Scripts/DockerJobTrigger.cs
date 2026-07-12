using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DockerJobTrigger : Interactable
{
    [Header("Discussion avec le Contremaître")]
    public Dialogue foremanDialogue;

    [Header("UI du Choix (Proposition de Job) 📋")]
    public GameObject jobOfferPanel;
    public Button acceptButton;
    public Button declineButton;

    [Header("Mise en place & Cooldown ⏳")]
    public Transform dockerStartPosition;
    [Tooltip("Bloque le job jusqu'au lendemain en jeu")]
    public bool requireNextDay = true;
    private float lastTimeWorked = -9999f;

    private bool isTransitioning = false;
    private PlayerController playerController;

    private void Start()
    {
        // LIGNE TEMPORAIRE POUR TES TESTS (Supprime la protection des 24h)
        lastTimeWorked = -9999f;

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
            float secondsForFullDay = 1440f / TimeManager.Instance.timeScale;
            if (Time.time < lastTimeWorked + secondsForFullDay)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification("Le contremaître : 'T'as déjà fait tes heures. Reviens demain matin !'");
                return;
            }
        }

        foremanDialogue.onDialogueEnd.RemoveAllListeners();
        foremanDialogue.onDialogueEnd.AddListener(ShowJobOffer);

        if (DialogueManager.Instance != null) DialogueManager.Instance.StartDialogue(foremanDialogue);
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
        if (!isTransitioning) StartCoroutine(MorningShiftRoutine());
    }

    public void DeclineJob()
    {
        if (jobOfferPanel != null) jobOfferPanel.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (playerController != null) playerController.enabled = true;
        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = true;

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Vous avez refusé de porter des caisses.");
    }

    private IEnumerator MorningShiftRoutine()
    {
        isTransitioning = true;

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));
        }

        yield return new WaitForSeconds(1.5f);

        // FORCER L'HEURE À 08H00 DU MATIN POUR LE PORT (8h * 60 = 480 minutes)
        if (TimeManager.Instance != null) TimeManager.Instance.currentTimeOfDay = 480f;

        if (dockerStartPosition != null && playerController != null)
        {
            playerController.transform.position = dockerStartPosition.position;
            playerController.transform.rotation = dockerStartPosition.rotation;
        }

        // On lance le job dans le manager !
        if (DockerJobManager.Instance != null) DockerJobManager.Instance.StartJob();

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));
            UIManager.Instance.transitionPanel.SetActive(false);
        }

        lastTimeWorked = Time.time;
        isTransitioning = false;
    }
}