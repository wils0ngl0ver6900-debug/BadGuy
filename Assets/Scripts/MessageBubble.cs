using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MessageBubble : MonoBehaviour
{
    [Header("Composants")]
    public TextMeshProUGUI messageText;
    public Image bubbleBackground;
    public RectTransform bubbleRect; // Le RectTransform du Message_Bubble

    [Header("Le CSS - Thème Joueur 🔵")]
    public Color playerBubbleColor = new Color(0.1f, 0.5f, 0.9f, 1f); // Bleu type iMessage
    public Color playerTextColor = Color.white;

    [Header("Le CSS - Thème PNJ 🔘")]
    public Color npcBubbleColor = new Color(0.2f, 0.2f, 0.2f, 1f); // Gris foncé
    public Color npcTextColor = Color.white;

    public void SetupMessage(string text, bool isPlayer)
    {
        // 1. On injecte le texte (qui supporte le HTML de Unity !)
        messageText.text = text;

        // 2. Le style dynamique (Notre faux CSS)
        if (isPlayer)
        {
            // Bulle du joueur : à droite, bleue
            bubbleBackground.color = playerBubbleColor;
            messageText.color = playerTextColor;
            messageText.alignment = TextAlignmentOptions.Right;

            // On l'ancre à droite
            bubbleRect.pivot = new Vector2(1f, 0.5f);
            // Optionnel: on force l'alignement dans la liste
            SetLayoutAlignment(TextAnchor.UpperRight);
        }
        else
        {
            // Bulle PNJ : à gauche, grise
            bubbleBackground.color = npcBubbleColor;
            messageText.color = npcTextColor;
            messageText.alignment = TextAlignmentOptions.Left;

            // On l'ancre à gauche
            bubbleRect.pivot = new Vector2(0f, 0.5f);
            SetLayoutAlignment(TextAnchor.UpperLeft);
        }
    }

    // Petite astuce pour forcer l'alignement dans une ScrollView
    private void SetLayoutAlignment(TextAnchor anchor)
    {
        var layoutGroup = transform.parent.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.childAlignment = anchor;
        }
    }
}