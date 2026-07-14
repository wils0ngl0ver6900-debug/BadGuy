using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CashierJobTrigger : Interactable
{
    [Header("Discussion avec le Gérant")]
    public Dialogue managerDialogue;

    [Header("UI du Choix (Proposition de Job) 📋")]
    public GameObject jobOfferPanel;
    public Button acceptButton;
    public Button declineButton;

    [Header("Mise en place & Cooldown ⏳")]
    public Transform cashierStandPosition;
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

        // --- SÉCURITÉ ANTI-POLICE ---
        if (GameManager.Instance != null && GameManager.Instance.wantedLevel > 0)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("Le gérant : 'T'es fou ?! Ramène pas les flics dans ma station !'");
            return;
        }

        // --- GESTION DU COOLDOWN (24 Heures In-Game) ---
        if (requireNextDay && TimeManager.Instance != null)
        {
            float secondsForFullDay = 1440f / TimeManager.Instance.timeScale;
            if (Time.time < lastTimeWorked + secondsForFullDay)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification("Le gérant : 'T'as déjà fait la fermeture. Reviens la nuit prochaine.'");
                return;
            }
        }

        managerDialogue.onDialogueEnd.RemoveAllListeners();
        managerDialogue.onDialogueEnd.AddListener(ShowJobOffer);

        if (DialogueManager.Instance != null) DialogueManager.Instance.StartDialogue(managerDialogue);
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

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        if (playerController != null) playerController.enabled = true;
        if (PhoneManager.Instance != null) PhoneManager.Instance.enabled = true;

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Vous avez refusé le job.");
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

        // FORCER L'HEURE À MINUIT (Nuit noire)
        if (TimeManager.Instance != null) TimeManager.Instance.currentTimeOfDay = 0f;

        if (cashierStandPosition != null && playerController != null)
        {
            playerController.transform.position = cashierStandPosition.position;
            playerController.transform.rotation = cashierStandPosition.rotation;
        }

        if (CashierJobManager.Instance != null) CashierJobManager.Instance.StartJob();

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(1f));
            UIManager.Instance.transitionPanel.SetActive(false);
        }

        lastTimeWorked = Time.time;
        isTransitioning = false;
    }
}