using UnityEngine;
using TMPro;
using System.Collections;

// Panneau du labo Héroïne. Script séparé et dédié (ne touche pas à WeedLabManager) :
// même mécanique que ton labo weed (drag & drop, feedback, consommation sac/coffre),
// juste avec sa propre recette. HeroinLabSlot pointe directement vers CE manager via
// le champ "manager" de l'Inspector. Un singleton Instance est quand même prévu ici
// (comme WeedLabManager) car Interactable.cs a besoin de faire HeroinLabManager.Instance.OpenLab()
// depuis le déclencheur 3D dans la planque — voir le tuto pour le câblage complet.
public class HeroinLabManager : MonoBehaviour
{
    public static HeroinLabManager Instance;

    [Header("Identité 🏷️")]
    [Tooltip("Juste pour t'y retrouver dans les notifications/erreurs")]
    public string labName = "Labo Héroïne";

    [Header("UI du Labo")]
    public GameObject labUIPanel;

    [Header("Ingrédients de base à verrouiller si absents 🔒")]
    public DraggableItem[] draggableSources;

    [Header("Feedback Visuel 💬")]
    public TextMeshProUGUI textFeedback;

    [HideInInspector] public bool isOpen = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (labUIPanel != null) labUIPanel.SetActive(false);
        if (textFeedback != null) textFeedback.gameObject.SetActive(false);
    }

    public void OpenLab()
    {
        isOpen = true;
        if (labUIPanel != null) labUIPanel.SetActive(true);
        if (textFeedback != null) textFeedback.gameObject.SetActive(false);

        RefreshDraggableItems();

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(false);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void CloseLab()
    {
        isOpen = false;
        if (labUIPanel != null) labUIPanel.SetActive(false);

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseLab();
        }
    }

    public void ShowFeedback(string message, Color color)
    {
        if (textFeedback == null) return;

        textFeedback.text = message;
        textFeedback.color = color;
        textFeedback.gameObject.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(HideFeedbackRoutine());
    }

    private IEnumerator HideFeedbackRoutine()
    {
        yield return new WaitForSeconds(3f);
        if (textFeedback != null) textFeedback.gameObject.SetActive(false);
    }

    public void RefreshDraggableItems()
    {
        if (draggableSources == null) return;

        foreach (DraggableItem source in draggableSources)
        {
            if (source != null && source.itemData != null)
                source.SetInteractable(PlayerOrStashHasItem(source.itemData.itemName));
        }
    }

    private bool PlayerOrStashHasItem(string itemName)
    {
        string target = itemName.Trim().ToLower();

        if (InventoryManager.Instance != null)
        {
            foreach (var slot in InventoryManager.Instance.slots)
            {
                if (slot.item != null && slot.item.itemName.Trim().ToLower() == target) return true;
            }
        }

        if (SafehouseManager.Instance != null)
        {
            foreach (var slot in SafehouseManager.Instance.stashSlots)
            {
                if (slot.item != null && slot.item.itemName.Trim().ToLower() == target) return true;
            }
        }

        return false;
    }

    public void ConsumeItem(ItemData itemToConsume)
    {
        string target = itemToConsume.itemName.Trim().ToLower();

        if (InventoryManager.Instance != null)
        {
            for (int i = 0; i < InventoryManager.Instance.slots.Count; i++)
            {
                var slot = InventoryManager.Instance.slots[i];
                if (slot.item != null && slot.item.itemName.Trim().ToLower() == target)
                {
                    InventoryManager.Instance.RemoveItem(itemToConsume, 1);
                    RefreshDraggableItems();
                    return;
                }
            }
        }

        if (SafehouseManager.Instance != null)
        {
            for (int i = SafehouseManager.Instance.stashSlots.Count - 1; i >= 0; i--)
            {
                var slot = SafehouseManager.Instance.stashSlots[i];
                if (slot.item != null && slot.item.itemName.Trim().ToLower() == target)
                {
                    slot.amount -= 1;
                    if (slot.amount <= 0)
                    {
                        slot.item = null;
                        slot.amount = 0;
                    }
                    FindObjectOfType<StashUI>()?.RefreshUI();
                    RefreshDraggableItems();
                    return;
                }
            }
        }
    }
}