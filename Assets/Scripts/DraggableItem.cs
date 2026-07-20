using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("Glisse le ScriptableObject correspondant ici (ex: Item_Terre)")]
    public ItemData itemData;

    private Transform parentOriginal;
    private Vector2 startPosition; // <--- On ajoute une mémoire pour la position !

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    [HideInInspector] public bool isLocked = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void SetInteractable(bool state)
    {
        isLocked = !state;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = state ? 1f : 0.4f;
            canvasGroup.blocksRaycasts = state;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked) return;

        parentOriginal = transform.parent;
        startPosition = rectTransform.anchoredPosition; // <--- On sauvegarde où l'objet était posé

        transform.SetParent(transform.root);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isLocked) return;
        rectTransform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(parentOriginal);

        // <--- On force le retour à sa position initiale exacte !
        rectTransform.anchoredPosition = startPosition;

        if (!isLocked)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }
    }
}