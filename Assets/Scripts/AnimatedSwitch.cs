using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[RequireComponent(typeof(Toggle))]
public class AnimatedSwitch : MonoBehaviour
{
    private Toggle toggle;

    [Header("Éléments UI")]
    public RectTransform handle; // Le cercle blanc
    public TextMeshProUGUI statusText; // Le texte "ON" ou "OFF"
    public Image backgroundImage; // La pilule noire/grise

    [Header("Paramètres d'Animation")]
    public Vector2 handlePositionOff = new Vector2(-20f, 0f);
    public Vector2 handlePositionOn = new Vector2(20f, 0f);
    public Color bgOffColor = new Color(0.2f, 0.2f, 0.2f, 1f); // Gris foncé
    public Color bgOnColor = Color.black; // Noir pur
    public float animationDuration = 0.15f; // Vitesse du glissement

    private Coroutine animateCoroutine;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
    }

    private void Start()
    {
        // On écoute quand le joueur clique sur le bouton
        toggle.onValueChanged.AddListener(OnSwitch);

        // Initialisation instantanée sans animation au lancement
        UpdateUI(toggle.isOn, true);
    }

    private void OnSwitch(bool isOn)
    {
        if (animateCoroutine != null) StopCoroutine(animateCoroutine);
        animateCoroutine = StartCoroutine(AnimateSwitch(isOn));
    }

    private IEnumerator AnimateSwitch(bool isOn)
    {
        float elapsed = 0f;
        Vector2 startPos = handle.anchoredPosition;
        Vector2 targetPos = isOn ? handlePositionOn : handlePositionOff;

        Color startColor = backgroundImage.color;
        Color targetColor = isOn ? bgOnColor : bgOffColor;

        // On change le texte tout de suite
        UpdateText(isOn);

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            // Calcul mathématique pour un glissement fluide (Ease-in-out)
            float t = elapsed / animationDuration;
            t = t * t * (3f - 2f * t);

            handle.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            backgroundImage.color = Color.Lerp(startColor, targetColor, t);

            yield return null;
        }

        handle.anchoredPosition = targetPos;
        backgroundImage.color = targetColor;
    }

    private void UpdateUI(bool isOn, bool instant)
    {
        handle.anchoredPosition = isOn ? handlePositionOn : handlePositionOff;
        backgroundImage.color = isOn ? bgOnColor : bgOffColor;
        UpdateText(isOn);
    }

    private void UpdateText(bool isOn)
    {
        if (statusText != null)
        {
            // On change uniquement le mot, on ne touche plus à sa position !
            statusText.text = isOn ? "ON" : "OFF";
        }
    }
}