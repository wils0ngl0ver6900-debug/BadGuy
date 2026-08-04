using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class ScrollBackground : MonoBehaviour
{
    [Tooltip("Vitesse de déplacement des étoiles (X = Horizontal, Y = Vertical)")]
    public Vector2 scrollSpeed = new Vector2(0.01f, 0.005f);

    private RawImage background;

    void Awake()
    {
        background = GetComponent<RawImage>();
    }

    void Update()
    {
        // On récupère le cadrage actuel de l'image
        Rect currentRect = background.uvRect;

        // On décale le cadrage. 
        // L'astuce "unscaledDeltaTime" permet à l'espace de bouger même si ton jeu est en pause !
        currentRect.x += scrollSpeed.x * Time.unscaledDeltaTime;
        currentRect.y += scrollSpeed.y * Time.unscaledDeltaTime;

        // On applique le nouveau cadrage
        background.uvRect = currentRect;
    }
}