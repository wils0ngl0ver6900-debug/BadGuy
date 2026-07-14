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

            // Coupure du téléphone propre
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

        // Retour à la normale
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        if (playerController != null) playerController.enabled = true;
        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = true;

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Vous avez refusé de porter des caisses.");
    }

    private IEnumerator MorningShiftRoutine()
    {
        isTransitioning = true;

        // 1. Écran Noir
        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(1f));
        }

        yield return new WaitForSeconds(0.5f);

        // 2. TP et Changement d'heure (Ex: 08h00 du matin)
        if (TimeManager.Instance != null) TimeManager.Instance.currentTimeOfDay = 480f;

        if (dockerStartPosition != null && playerController != null)
        {
            playerController.transform.position = dockerStartPosition.position;
            playerController.transform.rotation = dockerStartPosition.rotation;
        }

        // 3. Rallumer la lumière
        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));
            UIManager.Instance.transitionPanel.SetActive(false);
        }

        // 4. Libérer le joueur
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
        if (playerController != null) playerController.enabled = true;

        // 5. Lancer le Job
        if (DockerJobManager.Instance != null) DockerJobManager.Instance.StartJob();

        lastTimeWorked = Time.time;
        isTransitioning = false;
    }
}