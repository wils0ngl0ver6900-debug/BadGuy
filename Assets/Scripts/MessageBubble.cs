using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MessageBubble : MonoBehaviour
{
    [Header("Composants")]
    public TextMeshProUGUI messageText;
    public Image bubbleBackground;
    public HorizontalLayoutGroup parentLayoutGroup; // C'est lui qui va bouger la bulle !

    [Header("Le CSS - Thème Joueur 🔵")]
    public Color playerBubbleColor = new Color(0.1f, 0.5f, 0.9f, 1f);
    public Color playerTextColor = Color.white;

    [Header("Le CSS - Thème PNJ 🔘")]
    public Color npcBubbleColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color npcTextColor = Color.white;

    public void SetupMessage(string text, bool isPlayer)
    {
        messageText.text = text;

        if (isPlayer)
        {
            // Thème Joueur
            bubbleBackground.color = playerBubbleColor;
            messageText.color = playerTextColor;
            messageText.alignment = TextAlignmentOptions.Left; // Le texte reste lisible de gauche à droite à l'intérieur de la bulle

            // On colle la bulle à droite de l'écran
            if (parentLayoutGroup != null)
                parentLayoutGroup.childAlignment = TextAnchor.MiddleRight;
        }
        else
        {
            // Thème PNJ
            bubbleBackground.color = npcBubbleColor;
            messageText.color = npcTextColor;
            messageText.alignment = TextAlignmentOptions.Left;

            // On colle la bulle à gauche de l'écran
            if (parentLayoutGroup != null)
                parentLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        }
    }
}