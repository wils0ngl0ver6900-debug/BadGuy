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
            if (Input.GetKeyDown(KeyCode.E))
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
                    DisplayNextLine();
                }
            }
        }
    }

    public void StartDialogue(Dialogue dialogue)
    {
        if (playerController != null)
        {
            playerController.isDoingQTE = true;
        }

        // --- MASQUER LE HUD POUR L'IMMERSION ---
        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(false);

        currentDialogue = dialogue;
        dialoguePanel.SetActive(true);
        linesQueue.Clear();

        foreach (DialogueLine line in dialogue.lines)
        {
            linesQueue.Enqueue(line);
        }

        DisplayNextLine();
    }

    public void DisplayNextLine()
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

        // --- RÉAFFICHER LE HUD ---
        // Très important de le faire AVANT d'invoquer les événements, 
        // comme ça si ta quête envoie une Notification ("Nouvelle Quête !"), l'écran sera prêt !
        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(true);

        if (currentDialogue.onDialogueEnd != null)
        {
            currentDialogue.onDialogueEnd.Invoke();
        }

        yield return new WaitForEndOfFrame();

        if (playerController != null)
        {
            playerController.isDoingQTE = false;
        }
    }
}