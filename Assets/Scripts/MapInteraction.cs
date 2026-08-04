using UnityEngine;
using UnityEngine.UI;

public class MapInteraction : MonoBehaviour
{
    [Header("Zoom (Molette)")]
    public float zoomSpeed = 0.5f;
    public float minZoom = 0.2f;  // J'ai baissé un peu pour te laisser reculer plus loin dans l'espace
    public float maxZoom = 5f;

    [Header("Navigation (Glisser avec clic)")]
    public float panSpeed = 1f;
    public float maxPanDistance = 1500f;

    [Header("Transition Google Earth 🌍")]
    [Tooltip("Le CanvasGroup de ta map (FondCarte) pour l'effacer")]
    public CanvasGroup cityCanvasGroup;
    [Tooltip("Le CanvasGroup de ta Planète/Mapemonde pour l'afficher")]
    public CanvasGroup globeCanvasGroup;

    [Tooltip("Niveau de zoom où la transition commence")]
    public float transitionStartZoom = 1.0f;
    [Tooltip("Niveau de zoom où on ne voit PLUS DU TOUT la ville")]
    public float transitionEndZoom = 0.5f;
    [Tooltip("Vitesse de recentrage auto vers le globe")]
    public float autoCenterSpeed = 5f;

    [Header("Effet Zoom Spatial 🚀")]
    [Tooltip("Taille du globe quand la ville est à l'écran (doit être énorme pour remplir l'écran)")]
    public float globeScaleStart = 4f;
    [Tooltip("Taille normale du globe quand la transition est terminée")]
    public float globeScaleEnd = 1f;

    private RectTransform mapRect;
    private Vector3 lastMousePosition;
    private Vector2 startPosition;

    void Awake()
    {
        mapRect = GetComponent<RectTransform>();
        startPosition = mapRect.anchoredPosition;
    }

    void OnEnable()
    {
        if (mapRect != null)
        {
            mapRect.localScale = Vector3.one;
            mapRect.anchoredPosition = startPosition;
            UpdateGlobeFade();
        }
    }

    void Update()
    {
        if (MapApp.Instance == null || !MapApp.Instance.isOpen) return;

        HandleZoom();
        HandlePan();
        UpdateGlobeFade();
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            float currentScale = mapRect.localScale.x;
            float newScale = currentScale + (scroll * zoomSpeed);
            newScale = Mathf.Clamp(newScale, minZoom, maxZoom);
            mapRect.localScale = new Vector3(newScale, newScale, 1f);
        }
    }

    private void HandlePan()
    {
        // On empêche de glisser la ville si le globe est à 100% visible
        if (globeCanvasGroup != null && globeCanvasGroup.alpha >= 0.99f) return;

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            Vector2 newPos = mapRect.anchoredPosition + new Vector2(delta.x, delta.y) * panSpeed;

            float currentLimit = maxPanDistance * mapRect.localScale.x;
            newPos.x = Mathf.Clamp(newPos.x, startPosition.x - currentLimit, startPosition.x + currentLimit);
            newPos.y = Mathf.Clamp(newPos.y, startPosition.y - currentLimit, startPosition.y + currentLimit);

            mapRect.anchoredPosition = newPos;
            lastMousePosition = Input.mousePosition;
        }
    }

    private void UpdateGlobeFade()
    {
        float currentZoom = mapRect.localScale.x;

        // Calcul du fondu (0 = Ville pure, 1 = Globe pur)
        float alpha = Mathf.InverseLerp(transitionStartZoom, transitionEndZoom, currentZoom);

        // On gère l'opacité ET la taille du globe
        if (globeCanvasGroup != null)
        {
            globeCanvasGroup.alpha = alpha;

            // --- MAGIE DU ZOOM SPATIAL ---
            float globeScale = 1f;

            if (currentZoom >= transitionEndZoom)
            {
                // ÉTAPE 1 : On passe de très gros (sur la ville) à une taille normale
                float transitionProgress = (currentZoom - transitionEndZoom) / (transitionStartZoom - transitionEndZoom);
                globeScale = Mathf.Lerp(globeScaleEnd, globeScaleStart, transitionProgress);
            }
            else
            {
                // ÉTAPE 2 : La transition est finie, mais on continue de dézoomer dans le vide spatial
                globeScale = globeScaleEnd * (currentZoom / transitionEndZoom);
            }

            // On applique cette taille à l'image 2D qui diffuse le globe
            globeCanvasGroup.transform.localScale = new Vector3(globeScale, globeScale, 1f);
        }

        // On efface la ville progressivement
        if (cityCanvasGroup != null)
        {
            cityCanvasGroup.alpha = 1f - alpha;
        }

        // Recentrage auto
        if (alpha > 0f)
        {
            mapRect.anchoredPosition = Vector2.Lerp(mapRect.anchoredPosition, startPosition, Time.unscaledDeltaTime * autoCenterSpeed * alpha);
        }
    }
}