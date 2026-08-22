using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class DialogueLine
{
    public string speakerName;
    [TextArea(3, 10)] public string sentence;
    public Sprite portrait;
}

[System.Serializable]
public class Dialogue
{
    public DialogueLine[] lines;
    public UnityEvent onDialogueEnd;

    [Header("Choix Oui/Non (optionnel, affiche apres la derniere replique)")]
    public bool hasYesNoChoice = false;
    public string yesLabel = "Oui";
    public string noLabel = "Non";
    public UnityEvent onYesChoice;
    public UnityEvent onNoChoice;
}

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI �l�ments")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    public Image portraitImage;
    public GameObject continuePrompt;

    [Header("Choix Oui/Non")]
    public GameObject yesNoChoicePanel;
    public Button yesButton;
    public Button noButton;
    public TextMeshProUGUI yesButtonText;
    public TextMeshProUGUI noButtonText;

    private Queue<DialogueLine> linesQueue;
    private Dialogue currentDialogue;
    private bool isTyping = false;
    private string fullSentence = "";

    private PlayerController playerController;
    private bool isCurrentDialogueAPhoneCall = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        linesQueue = new Queue<DialogueLine>();

        if (yesButton != null) yesButton.onClick.AddListener(OnYesClicked);
        if (noButton != null) noButton.onClick.AddListener(OnNoClicked);
    }

    private void Start()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (yesNoChoicePanel != null) yesNoChoicePanel.SetActive(false);
        playerController = FindObjectOfType<PlayerController>();
    }

    private void Update()
    {
        if (dialoguePanel != null && dialoguePanel.activeSelf
            && (yesNoChoicePanel == null || !yesNoChoicePanel.activeSelf))
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                if (isTyping)
                {
                    StopAllCoroutines();
                    dialogueText.text = fullSentence;
                    isTyping = false;
                    if (continuePrompt != null) continuePrompt.SetActive(true);
                }
                else
                {
                    DisplayNextSentence();
                }
            }
        }
    }

    public void StartDialogue(Dialogue dialogue, bool isPhoneCall = false)
    {
        isCurrentDialogueAPhoneCall = isPhoneCall;

        // --- C'EST ICI LA MAGIE ---
        // Le UIManager s'occupe de faire le tri dynamiquement !
        if (UIManager.Instance != null)
            UIManager.Instance.ToggleHUD(false, isPhoneCall);

        if (playerController != null && !isPhoneCall)
        {
            // On fige le joueur seulement si ce n'est PAS un appel
            playerController.isDoingQTE = true;
            playerController.enabled = false;
        }

        dialoguePanel.SetActive(true);
        currentDialogue = dialogue;
        linesQueue.Clear();

        foreach (DialogueLine line in dialogue.lines)
        {
            linesQueue.Enqueue(line);
        }

        DisplayNextSentence();
    }

    public void DisplayNextSentence()
    {
        if (linesQueue.Count == 0)
        {
            if (currentDialogue.hasYesNoChoice)
            {
                ShowYesNoChoice();
            }
            else
            {
                EndDialogue();
            }
            return;
        }

        DialogueLine line = linesQueue.Dequeue();
        nameText.text = line.speakerName;

        if (line.portrait != null)
        {
            portraitImage.sprite = line.portrait;
            portraitImage.gameObject.SetActive(true);
        }
        else
        {
            portraitImage.gameObject.SetActive(false);
        }

        StopAllCoroutines();
        StartCoroutine(TypeSentence(line.sentence));
    }

    private IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;
        fullSentence = sentence;
        dialogueText.text = "";
        if (continuePrompt != null) continuePrompt.SetActive(false);

        foreach (char letter in sentence.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(0.02f);
        }

        isTyping = false;
        if (continuePrompt != null) continuePrompt.SetActive(true);
    }

    private void ShowYesNoChoice()
    {
        // Le prompt "appuie pour continuer" n'a plus lieu d'être, le choix prend le relais.
        if (continuePrompt != null) continuePrompt.SetActive(false);
        if (yesNoChoicePanel != null) yesNoChoicePanel.SetActive(true);
        if (yesButtonText != null) yesButtonText.text = currentDialogue.yesLabel;
        if (noButtonText != null) noButtonText.text = currentDialogue.noLabel;
    }

    private void OnYesClicked()
    {
        if (yesNoChoicePanel != null) yesNoChoicePanel.SetActive(false);
        if (currentDialogue.onYesChoice != null) currentDialogue.onYesChoice.Invoke();
        EndDialogue();
    }

    private void OnNoClicked()
    {
        if (yesNoChoicePanel != null) yesNoChoicePanel.SetActive(false);
        if (currentDialogue.onNoChoice != null) currentDialogue.onNoChoice.Invoke();
        EndDialogue();
    }

    private void EndDialogue()
    {
        StartCoroutine(EndDialogueRoutine());
    }

    private IEnumerator EndDialogueRoutine()
    {
        dialoguePanel.SetActive(false);

        // On r�affiche le HUD (et on range le t�l�phone si besoin)
        if (UIManager.Instance != null)
            UIManager.Instance.ToggleHUD(true);

        if (playerController != null && !isCurrentDialogueAPhoneCall)
        {
            // On lib�re le joueur (si ce n'�tait pas un appel)
            playerController.isDoingQTE = false;
            playerController.enabled = true;
        }

        // Si c'�tait un appel, on dit � l'application t�l�phone de raccrocher
        if (isCurrentDialogueAPhoneCall && CallApp.Instance != null)
        {
            CallApp.Instance.EndCall();
        }

        if (currentDialogue.onDialogueEnd != null)
        {
            currentDialogue.onDialogueEnd.Invoke();
        }

        yield return null;
    }
}