using UnityEngine;

public class PhoneManager : MonoBehaviour
{
    public static PhoneManager Instance;

    [Header("Configuration Objet 📱")]
    public bool requiresItemToUse = true;
    public string requiredPhoneItemName = "Smartphone";

    [Header("UI Téléphone 📱")]
    public RectTransform phonePanel;
    public float slideSpeed = 12f;

    [Header("Positions sur l'écran (Y)")]
    public float hiddenPosY = -800f;
    public float visiblePosY = 0f;

    [HideInInspector] public bool isPhoneOpen = false;

    private Vector2 targetPosition;
    private PlayerController playerController;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();

        if (phonePanel != null)
        {
            targetPosition = new Vector2(phonePanel.anchoredPosition.x, hiddenPosY);
            phonePanel.anchoredPosition = targetPosition;
        }
    }

    private void Update()
    {
        if (playerController != null && (playerController.isDoingQTE || playerController.currentHealth <= 0))
        {
            if (isPhoneOpen) TogglePhone();
            return;
        }

        if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.P))
        {
            if (!isPhoneOpen && requiresItemToUse)
            {
                if (!CheckPlayerHasPhone())
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowNotification("Vous n'avez pas de téléphone sur vous !");
                    return;
                }
            }

            TogglePhone();
        }

        if (phonePanel != null)
        {
            phonePanel.anchoredPosition = Vector2.Lerp(phonePanel.anchoredPosition, targetPosition, Time.deltaTime * slideSpeed);
        }
    }

    private bool CheckPlayerHasPhone()
    {
        string targetName = requiredPhoneItemName.Trim().ToLower();

        if (HotbarManager.Instance != null)
        {
            foreach (HotbarSlot slot in HotbarManager.Instance.hotbarSlots)
            {
                if (slot.itemInSlot != null && slot.itemInSlot.itemName.Trim().ToLower() == targetName)
                    return true;
            }
        }

        if (InventoryManager.Instance != null)
        {
            foreach (InventorySlot slot in InventoryManager.Instance.slots)
            {
                if (slot.item != null && slot.item.itemName.Trim().ToLower() == targetName)
                    return true;
            }
        }

        return false;
    }

    public void TogglePhone()
    {
        isPhoneOpen = !isPhoneOpen;

        if (isPhoneOpen)
        {
            targetPosition = new Vector2(phonePanel.anchoredPosition.x, visiblePosY);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            targetPosition = new Vector2(phonePanel.anchoredPosition.x, hiddenPosY);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;
        }
    }
}