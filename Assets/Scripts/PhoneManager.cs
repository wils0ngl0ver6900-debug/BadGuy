using UnityEngine;

public class PhoneManager : MonoBehaviour
{
    public static PhoneManager Instance;

    [Header("Configuration Objet 📱")]
    public bool requiresItemToUse = true; // Coche/décoche pour activer la vérification
    public string requiredPhoneItemName = "Smartphone"; // Le nom EXACT de ton ItemData

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
            // Au démarrage, le téléphone est rangé
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
            // Si le téléphone est fermé, on vérifie si le joueur possède l'objet avant de l'ouvrir
            if (!isPhoneOpen && requiresItemToUse)
            {
                if (!CheckPlayerHasPhone())
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowNotification("Vous n'avez pas de téléphone sur vous !");
                    return; // On bloque l'ouverture
                }
            }

            TogglePhone();
        }

        // L'animation
        if (phonePanel != null)
        {
            phonePanel.anchoredPosition = Vector2.Lerp(phonePanel.anchoredPosition, targetPosition, Time.deltaTime * slideSpeed);
        }
    }

    // --- LA FONCTION DE FOUILLE ---
    private bool CheckPlayerHasPhone()
    {
        string targetName = requiredPhoneItemName.Trim().ToLower();

        // 1. Vérification dans la Hotbar
        if (HotbarManager.Instance != null)
        {
            foreach (HotbarSlot slot in HotbarManager.Instance.hotbarSlots)
            {
                if (slot.itemInSlot != null && slot.itemInSlot.itemName.Trim().ToLower() == targetName)
                    return true;
            }
        }

        // 2. Vérification dans l'inventaire principal
        if (InventoryManager.Instance != null)
        {
            foreach (ItemData item in InventoryManager.Instance.items)
            {
                if (item != null && item.itemName.Trim().ToLower() == targetName)
                    return true;
            }
        }

        return false; // L'objet n'a pas été trouvé
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