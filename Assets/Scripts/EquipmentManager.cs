using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance;
    public ItemData[] currentEquipment = new ItemData[4];
    private PlayerController player;

    [Header("Système de Déguisement 🥷")]
    public int notorietyGainedWithCurrentOutfit = 0;
    private int snapshotPool = 0;
    private bool isCleaningPool = false;
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

        if (notorietyGainedWithCurrentOutfit > GameManager.Instance.notoriety)
        {
            notorietyGainedWithCurrentOutfit = GameManager.Instance.notoriety;
        }

        // Si on est repéré, on ne peut plus se déguiser en cachette
        if (GameManager.Instance.spottersCount > 0)
        {
            isCleaningPool = false;
            snapshotPool = 0;
            for (int i = 0; i < slotChangedThisBreak.Length; i++)
            {
                slotChangedThisBreak[i] = false;
            }
        }
    }

    public void OnNotorietyIncreased(int amount)
    {
        isCleaningPool = false;
        notorietyGainedWithCurrentOutfit = Mathf.Clamp(notorietyGainedWithCurrentOutfit + amount, 0, 100);
    }

    // --- LE MOTEUR UNIVERSEL DE DÉGUISEMENT (25% PAR SLOT) ---
    private void HandleSlotChangeNotoriety(int slotIndex)
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.spottersCount == 0 && notorietyGainedWithCurrentOutfit > 0)
        {
            if (!slotChangedThisBreak[slotIndex])
            {
                slotChangedThisBreak[slotIndex] = true;

                if (!isCleaningPool)
                {
                    isCleaningPool = true;
                    snapshotPool = notorietyGainedWithCurrentOutfit;
                }

                int reduction = Mathf.RoundToInt(snapshotPool * 0.25f);

                GameManager.Instance.notoriety = Mathf.Max(0, GameManager.Instance.notoriety - reduction);
                notorietyGainedWithCurrentOutfit = Mathf.Max(0, notorietyGainedWithCurrentOutfit - reduction);

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.UpdateHUD();
                    string slotName = GetSlotName(slotIndex);
                    UIManager.Instance.ShowNotification($"Silhouette modifiée ({slotName}) : <color=#00FF22>Notoriété -{reduction}%</color>");
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

        // Que ce soit un masque ou non, ça compte comme un changement à 25% !
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

        // --- PUNITION SI ON MET LA CAGOULE DEVANT UN FLIC ---
        if (newItem.isMask && GameManager.Instance != null && GameManager.Instance.spottersCount > 0)
        {
            GameManager.Instance.IncreaseNotoriety(10);
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Masque enfilé devant témoin ! Notoriété +10</color>");
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