using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class InventoryDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Référence de la Case (Stack)")]
    public InventorySlot slotReference;

    public ItemData itemReference => slotReference?.item;

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
        if (bgImage != null) { originalBgColor = bgImage.color; hasBgColor = true; }
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

        Canvas mainCanvas = GetComponentInParent<Canvas>();
        if (mainCanvas != null) transform.SetParent(mainCanvas.transform);
        else transform.SetParent(transform.root);

        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
        if (UIManager.Instance != null) UIManager.Instance.HideTooltip();
    }

    public void OnDrag(PointerEventData eventData) { if (itemReference != null) rectTransform.position = Input.mousePosition; }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (itemReference == null) return;
        isDragging = false;
        canvasGroup.blocksRaycasts = true;

        Canvas mainCanvas = originalParent != null ? originalParent.GetComponentInParent<Canvas>() : null;
        bool isDirectChildOfCanvas = mainCanvas != null && transform.parent == mainCanvas.transform;

        if (isDirectChildOfCanvas || transform.parent == transform.root)
        {
            if (originalHotbarSlot != null) ReturnToInventoryFromHotbar(originalHotbarSlot);
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
            if (this.GetComponentInParent<HotbarSlot>() != null || droppedItem.GetComponentInParent<HotbarSlot>() != null) return;

            if (InventoryManager.Instance != null)
            {
                int targetListIndex = InventoryManager.Instance.slots.IndexOf(this.slotReference);
                int droppedListIndex = InventoryManager.Instance.slots.IndexOf(droppedItem.slotReference);

                if (targetListIndex >= 0 && droppedListIndex >= 0)
                {
                    InventoryManager.Instance.slots[targetListIndex] = droppedItem.slotReference;
                    InventoryManager.Instance.slots[droppedListIndex] = this.slotReference;
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
            HotbarSlot currentHotbarSlot = GetComponentInParent<HotbarSlot>();
            if (currentHotbarSlot != null) { ReturnToInventoryFromHotbar(currentHotbarSlot); return; }

            if (itemReference != null && itemReference.isClothing)
            {
                if (EquipmentManager.Instance != null)
                {
                    EquipmentManager.Instance.Equip(itemReference);
                    if (UIManager.Instance != null) UIManager.Instance.HideTooltip();

                    InventoryManager.Instance.RemoveItem(itemReference, 1);
                    FindObjectOfType<InventoryUI>().RefreshUI();
                }
            }
        }
    }

    public void SetVisualMode(bool inHotbar)
    {
        TextMeshProUGUI textMesh = GetComponentInChildren<TextMeshProUGUI>();
        if (textMesh != null)
        {
            textMesh.enabled = !inHotbar;
            if (!inHotbar && slotReference != null && slotReference.amount > 1)
                textMesh.text = $"{itemReference.itemName} (x{slotReference.amount})";
        }

        Transform rarityBorder = transform.Find("Bordure_Rarete");
        if (rarityBorder != null) rarityBorder.gameObject.SetActive(!inHotbar);

        Transform iconTransform = transform.Find("Icone");
        if (iconTransform != null)
        {
            Image iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null) { iconImage.enabled = true; iconImage.color = Color.white; }
        }

        Image bgImage = GetComponent<Image>();
        if (bgImage != null)
        {
            bgImage.enabled = true;
            bgImage.color = inHotbar ? new Color(originalBgColor.r, originalBgColor.g, originalBgColor.b, 0f) : (hasBgColor ? originalBgColor : new Color(1f, 1f, 1f, 0.1f));
        }
    }

    private void ReturnToInventoryFromHotbar(HotbarSlot hotbarSlot)
    {
        if (hotbarSlot != null) hotbarSlot.itemInSlot = null;
        if (InventoryManager.Instance != null) InventoryManager.Instance.AddItem(itemReference, slotReference.amount, true);
        InventoryUI inventoryUI = FindObjectOfType<InventoryUI>();
        if (inventoryUI != null) inventoryUI.RefreshUI();
        Destroy(gameObject);
    }
}