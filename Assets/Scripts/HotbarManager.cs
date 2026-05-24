using UnityEngine;
using UnityEngine.UI;

public class HotbarManager : MonoBehaviour
{
    public static HotbarManager Instance;

    [Header("Configuration UI")]
    public HotbarSlot[] hotbarSlots;
    public RectTransform cadreSelection;

    [Header("État")]
    public int currentSelectedIndex = -1;

    [Header("Système 3D (Mains)")]
    public Transform playerHand;
    private GameObject currentWeaponModel;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // --- CORRECTION DU BOUCLIER INVISIBLE ---
        // On force le cadre à ignorer la souris pour qu'on puisse déposer des objets au travers !
        if (cadreSelection != null)
        {
            Image cadreImg = cadreSelection.GetComponent<Image>();
            if (cadreImg != null) cadreImg.raycastTarget = false;
        }
    }

    void Update()
    {
        // Détection des touches pour sélectionner une case (de 1 à 6)
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SelectSlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) SelectSlot(4);
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) SelectSlot(5);
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= hotbarSlots.Length) return;

        // Désélection si on clique sur la même case
        if (currentSelectedIndex == index)
        {
            currentSelectedIndex = -1;
            if (cadreSelection != null) cadreSelection.gameObject.SetActive(false);

            if (currentWeaponModel != null)
            {
                Destroy(currentWeaponModel);
            }
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateAmmoDisplay(0, 0, false);
            }
            return;
        }

        // Nouvelle sélection
        currentSelectedIndex = index;
        if (cadreSelection != null)
        {
            cadreSelection.gameObject.SetActive(true);
            cadreSelection.position = hotbarSlots[index].transform.position;
        }

        // Nettoyage de l'ancienne arme
        if (currentWeaponModel != null)
        {
            Destroy(currentWeaponModel);
        }

        ItemData item = hotbarSlots[index].itemInSlot;

        // Si on a équipé une arme
        if (item != null && item.isWeapon && item.weaponPrefab != null && playerHand != null)
        {
            currentWeaponModel = Instantiate(item.weaponPrefab, playerHand);

            if (UIManager.Instance != null)
            {
                PlayerCombat combat = FindObjectOfType<PlayerCombat>();
                int ammo = combat != null ? combat.currentAmmo : item.maxAmmo;
                UIManager.Instance.UpdateAmmoDisplay(ammo, item.maxAmmo, true);
            }
        }
        else
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateAmmoDisplay(0, 0, false);
            }
        }
    }

    public ItemData GetEquippedItem()
    {
        if (currentSelectedIndex < 0 || currentSelectedIndex >= hotbarSlots.Length) return null;
        return hotbarSlots[currentSelectedIndex].itemInSlot;
    }

    public void RemoveIllegalItems()
    {
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            InventoryDragDrop itemDansSlot = hotbarSlots[i].GetComponentInChildren<InventoryDragDrop>();

            if (itemDansSlot != null && itemDansSlot.itemReference != null && itemDansSlot.itemReference.isIllegal)
            {
                if (i == currentSelectedIndex && currentWeaponModel != null)
                {
                    Destroy(currentWeaponModel);
                    currentSelectedIndex = -1;
                    if (cadreSelection != null) cadreSelection.gameObject.SetActive(false);
                }

                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.RemoveItem(itemDansSlot.itemReference);
                }

                Destroy(itemDansSlot.gameObject);
                hotbarSlots[i].itemInSlot = null;
            }
        }

        InventoryUI inventoryUI = FindObjectOfType<InventoryUI>();
        if (inventoryUI != null) inventoryUI.RefreshUI();
    }

    // --- NOUVEAU : DÉTRUIT L'OBJET CONSOMMÉ ---
    public void ConsumeEquippedItem()
    {
        if (currentSelectedIndex < 0 || currentSelectedIndex >= hotbarSlots.Length) return;

        InventoryDragDrop itemDansSlot = hotbarSlots[currentSelectedIndex].GetComponentInChildren<InventoryDragDrop>();
        if (itemDansSlot != null)
        {
            // 1. Retire de l'inventaire logique
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.RemoveItem(itemDansSlot.itemReference);
            }

            // 2. Détruit l'icône dans la Hotbar
            Destroy(itemDansSlot.gameObject);
            hotbarSlots[currentSelectedIndex].itemInSlot = null;

            // 3. Rafraîchit l'UI de l'inventaire
            InventoryUI inventoryUI = FindObjectOfType<InventoryUI>();
            if (inventoryUI != null) inventoryUI.RefreshUI();

            // 4. Si l'objet avait un modèle 3D dans la main, on l'efface
            if (currentWeaponModel != null)
            {
                Destroy(currentWeaponModel);
            }

            // On désélectionne la case vide
            currentSelectedIndex = -1;
            if (cadreSelection != null) cadreSelection.gameObject.SetActive(false);

            // On force la mise à jour des munitions (au cas où on vient de jeter un objet)
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateAmmoDisplay(0, 0, false);
            }
        }
    }
}