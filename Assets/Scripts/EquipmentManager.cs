using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance;
    public ItemData[] currentEquipment = new ItemData[4];
    private PlayerController player;

    [Header("Système de Déguisement 🥷")]
    private bool[] slotChangedThisBreak = new bool[4];

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        player = FindObjectOfType<PlayerController>();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // Si on est repéré (spottersCount > 0) ou qu'on n'a plus d'étoiles, on remet à zéro la mémoire des vêtements changés
        if (GameManager.Instance.spottersCount > 0 || GameManager.Instance.wantedLevel == 0)
        {
            for (int i = 0; i < slotChangedThisBreak.Length; i++)
            {
                slotChangedThisBreak[i] = false;
            }
        }
    }

    public void OnNotorietyIncreased(int amount)
    {
        // Gardée vide pour éviter les erreurs avec d'autres vieux scripts, 
        // mais n'est plus utile avec le système d'étoiles dynamique.
    }

    // --- LE MOTEUR UNIVERSEL DE DÉGUISEMENT (-1 ÉTOILE) ---
    private void HandleSlotChangeNotoriety(int slotIndex)
    {
        if (GameManager.Instance == null) return;

        // Si personne ne nous voit et qu'on est recherché
        if (GameManager.Instance.spottersCount == 0 && GameManager.Instance.wantedLevel > 0)
        {
            if (!slotChangedThisBreak[slotIndex])
            {
                slotChangedThisBreak[slotIndex] = true;

                GameManager.Instance.DropOneStarFromDisguise();

                if (UIManager.Instance != null)
                {
                    string slotName = GetSlotName(slotIndex);
                    UIManager.Instance.ShowNotification($"Silhouette modifiée ({slotName}) : <color=#00FF22>Recherche réduite !</color>");
                }
            }
        }
    }

    private string GetSlotName(int index)
    {
        switch (index)
        {
            case 0: return "Tête";
            case 1: return "Torse";
            case 2: return "Jambes";
            case 3: return "Pieds";
            default: return "Vêtement";
        }
    }

    public void Equip(ItemData newItem)
    {
        if (newItem == null || !newItem.isClothing) return;

        int slotIndex = (int)newItem.clothingSlot;

        HandleSlotChangeNotoriety(slotIndex);

        if (currentEquipment[slotIndex] != null)
        {
            UnequipInternal(slotIndex);
        }

        currentEquipment[slotIndex] = newItem;

        if (InventoryManager.Instance != null) InventoryManager.Instance.RemoveItem(newItem);

        if (player != null)
        {
            player.maxShield += newItem.armorBonus;
            player.currentShield += newItem.armorBonus;
            player.UpdateClothingSpeedBonus();
            if (UIManager.Instance != null) UIManager.Instance.UpdateHealthDisplay(player.currentHealth, player.maxHealth);
        }

        // --- PUNITION SI ON MET LA CAGOULE DEVANT UN TÉMOIN ---
        if (newItem.isMask && GameManager.Instance != null && GameManager.Instance.spottersCount > 0)
        {
            GameManager.Instance.ReportCrime(10); // FIX ICI
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Masque enfilé devant témoin !</color>");
        }

        if (EquipmentUI.Instance != null) EquipmentUI.Instance.RefreshUI();
        InventoryUI invUI = FindObjectOfType<InventoryUI>();
        if (invUI != null) invUI.RefreshUI();
    }

    public void Unequip(int slotIndex)
    {
        if (currentEquipment[slotIndex] == null) return;

        HandleSlotChangeNotoriety(slotIndex);
        UnequipInternal(slotIndex);

        if (EquipmentUI.Instance != null) EquipmentUI.Instance.RefreshUI();
        InventoryUI invUI = FindObjectOfType<InventoryUI>();
        if (invUI != null) invUI.RefreshUI();
    }

    private void UnequipInternal(int slotIndex)
    {
        ItemData oldItem = currentEquipment[slotIndex];

        if (InventoryManager.Instance != null) InventoryManager.Instance.AddItem(oldItem);

        if (player != null)
        {
            player.maxShield -= oldItem.armorBonus;
            player.currentShield = Mathf.Clamp(player.currentShield - oldItem.armorBonus, 0, player.maxShield);
            player.UpdateClothingSpeedBonus();
            if (UIManager.Instance != null) UIManager.Instance.UpdateHealthDisplay(player.currentHealth, player.maxHealth);
        }

        currentEquipment[slotIndex] = null;
    }
}