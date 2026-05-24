using UnityEngine;

public class EquipmentUI : MonoBehaviour
{
    public static EquipmentUI Instance;

    [Header("Les 4 cases (Tete, Torse, Jambes, Pieds)")]
    public EquipmentSlotUI[] equipmentSlots;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void RefreshUI()
    {
        if (EquipmentManager.Instance == null) return;

        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            // Assigne l'index (0, 1, 2 ou 3) à chaque case
            equipmentSlots[i].slotIndex = i;
            equipmentSlots[i].RefreshSlot(EquipmentManager.Instance.currentEquipment[i]);
        }
    }
}