using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class InventoryDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Référence de l'objet")]
    public ItemData itemReference;

    [HideInInspector] public HotbarSlot originalHotbarSlot;
    [HideInInspector] public bool isSwapped = false;

    [HideInInspector] public Transform originalParent;
    [HideInInspector] public int originalSiblingIndex;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private bool isDragging = false;

    private Color originalBgColor;
    private bool hasBgColor = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Image bgImage = GetComponent<Image>();
        if (bgImage != null)
        {
            originalBgColor = bgImage.color;
            hasBgColor = true;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!Cursor.visible) return;
        if (!isDragging && itemReference != null && UIManager.Instance != null)
            UIManager.Instance.ShowTooltip(itemReference);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (UIManager.Instance != null) UIManager.Instance.HideTooltip();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemReference == null) return;
        isDragging = true;

        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalHotbarSlot = GetComponentInParent<HotbarSlot>();

        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;

        if (UIManager.Instance != null) UIManager.Instance.HideTooltip();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (itemReference == null) return;
        rectTransform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (itemReference == null) return;
        isDragging = false;
        canvasGroup.blocksRaycasts = true;

        if (transform.parent == transform.root)
        {
            if (originalHotbarSlot != null)
            {
                ReturnToInventoryFromHotbar(originalHotbarSlot);
            }
            else
            {
                transform.SetParent(originalParent);
                transform.SetSiblingIndex(originalSiblingIndex);
                rectTransform.localPosition = Vector3.zero;
                SetVisualMode(false);
            }
        }

        isSwapped = false;
    }

    public void OnDrop(PointerEventData eventData)
    {
        InventoryDragDrop droppedItem = eventData.pointerDrag?.GetComponent<InventoryDragDrop>();
        if (droppedItem != null && droppedItem != this)
        {
            if (this.GetComponentInParent<HotbarSlot>() != null || droppedItem.GetComponentInParent<HotbarSlot>() != null)
                return;

            if (InventoryManager.Instance != null)
            {
                int targetListIndex = InventoryManager.Instance.items.IndexOf(this.itemReference);
                int droppedListIndex = InventoryManager.Instance.items.IndexOf(droppedItem.itemReference);

                if (targetListIndex >= 0 && droppedListIndex >= 0)
                {
                    InventoryManager.Instance.items[targetListIndex] = droppedItem.itemReference;
                    InventoryManager.Instance.items[droppedListIndex] = this.itemReference;
                }
            }

            int myIndex = transform.GetSiblingIndex();
            transform.SetSiblingIndex(droppedItem.originalSiblingIndex);

            droppedItem.originalSiblingIndex = myIndex;
            droppedItem.originalParent = transform.parent;

            droppedItem.isSwapped = true;
            this.isSwapped = true;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // CAS 1 : L'objet est dans la Hotbar -> Retour à l'inventaire
            HotbarSlot currentHotbarSlot = GetComponentInParent<HotbarSlot>();
            if (currentHotbarSlot != null)
            {
                ReturnToInventoryFromHotbar(currentHotbarSlot);
                return;
            }

            // CAS 2 : L'objet est dans l'Inventaire ET c'est un Vêtement -> On l'équipe !
            if (itemReference != null && itemReference.isClothing)
            {
                if (EquipmentManager.Instance != null)
                {
                    EquipmentManager.Instance.Equip(itemReference);

                    if (UIManager.Instance != null) UIManager.Instance.HideTooltip();
                    Destroy(gameObject);
                }
            }
        }
    }

    public void SetVisualMode(bool inHotbar)
    {
        TextMeshProUGUI textMesh = GetComponentInChildren<TextMeshProUGUI>();
        if (textMesh != null) textMesh.enabled = !inHotbar;

        Transform rarityBorder = transform.Find("Bordure_Rarete");
        if (rarityBorder != null) rarityBorder.gameObject.SetActive(!inHotbar);

        Transform iconTransform = transform.Find("Icone");
        if (iconTransform != null)
        {
            Image iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.color = Color.white;
            }
        }

        Image bgImage = GetComponent<Image>();
        if (bgImage != null)
        {
            bgImage.enabled = true;

            if (inHotbar)
            {
                bgImage.color = new Color(originalBgColor.r, originalBgColor.g, originalBgColor.b, 0f);
            }
            else
            {
                bgImage.color = hasBgColor ? originalBgColor : new Color(1f, 1f, 1f, 0.1f);
            }
        }
    }

    private void ReturnToInventoryFromHotbar(HotbarSlot hotbarSlot)
    {
        if (hotbarSlot != null) hotbarSlot.itemInSlot = null;

        if (InventoryManager.Instance != null && !InventoryManager.Instance.items.Contains(itemReference))
            InventoryManager.Instance.items.Add(itemReference);

        InventoryUI inventoryUI = FindObjectOfType<InventoryUI>();
        if (inventoryUI != null) inventoryUI.RefreshUI();

        Destroy(gameObject);
    }
}