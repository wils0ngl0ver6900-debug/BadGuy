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
    [Header("Que se passe-t-il à la fin ?")]
    public UnityEvent onDialogueEnd;
}

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI Éléments")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    public Image portraitImage;
    public GameObject continuePrompt;

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
    }

    private void Start()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        playerController = FindObjectOfType<PlayerController>();
    }

    private void Update()
    {
        if (dialoguePanel != null && dialoguePanel.activeSelf)
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
            EndDialogue();
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

    private void EndDialogue()
    {
        StartCoroutine(EndDialogueRoutine());
    }

    private IEnumerator EndDialogueRoutine()
    {
        dialoguePanel.SetActive(false);

        // On réaffiche le HUD (et on range le téléphone si besoin)
        if (UIManager.Instance != null)
            UIManager.Instance.ToggleHUD(true);

        if (playerController != null && !isCurrentDialogueAPhoneCall)
        {
            // On libère le joueur (si ce n'était pas un appel)
            playerController.isDoingQTE = false;
            playerController.enabled = true;
        }

        // Si c'était un appel, on dit à l'application téléphone de raccrocher
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