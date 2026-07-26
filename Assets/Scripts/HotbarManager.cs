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
    [Tooltip("Glisse ici le Transform de la main (ex: RightHandProp ou Player_Hand) qui contient les modèles 3D d'armes en enfants.")]
    public Transform playerHand;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (cadreSelection != null)
        {
            Image cadreImg = cadreSelection.GetComponent<Image>();
            if (cadreImg != null) cadreImg.raycastTarget = false;
        }
    }

    private void Start()
    {
        // Masque toutes les armes enfants de la main au démarrage
        HideAllWeapons();
    }

    void Update()
    {
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

        // Si on reclique sur le même slot, on déséquipe tout
        if (currentSelectedIndex == index)
        {
            currentSelectedIndex = -1;
            if (cadreSelection != null) cadreSelection.gameObject.SetActive(false);

            HideAllWeapons();
            if (UIManager.Instance != null) UIManager.Instance.UpdateAmmoDisplay(0, 0, false);
            return;
        }

        currentSelectedIndex = index;
        if (cadreSelection != null)
        {
            cadreSelection.gameObject.SetActive(true);
            cadreSelection.position = hotbarSlots[index].transform.position;
        }

        // On masque l'arme active précédente
        HideAllWeapons();

        ItemData item = hotbarSlots[index].itemInSlot;

        // Si le slot contient une arme, on active le GameObject correspondant déjà présent dans la main
        if (item != null && item.isWeapon && playerHand != null)
        {
            Transform weaponTransform = FindWeaponInHand(item);

            if (weaponTransform != null)
            {
                weaponTransform.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"[HotbarManager] Aucun modèle 3D trouvé dans '{playerHand.name}' pour l'arme '{item.itemName}' !");
            }

            if (UIManager.Instance != null)
            {
                PlayerCombat combat = FindObjectOfType<PlayerCombat>();
                int ammo = combat != null ? combat.currentAmmo : item.maxAmmo;
                UIManager.Instance.UpdateAmmoDisplay(ammo, item.maxAmmo, true);
            }
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.UpdateAmmoDisplay(0, 0, false);
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
                if (i == currentSelectedIndex)
                {
                    HideAllWeapons();
                    currentSelectedIndex = -1;
                    if (cadreSelection != null) cadreSelection.gameObject.SetActive(false);
                }

                Destroy(itemDansSlot.gameObject);
                hotbarSlots[i].itemInSlot = null;
            }
        }
    }

    public void ConsumeEquippedItem()
    {
        if (currentSelectedIndex < 0 || currentSelectedIndex >= hotbarSlots.Length) return;

        InventoryDragDrop itemDansSlot = hotbarSlots[currentSelectedIndex].GetComponentInChildren<InventoryDragDrop>();
        if (itemDansSlot != null)
        {
            itemDansSlot.slotReference.amount--;

            if (itemDansSlot.slotReference.amount <= 0)
            {
                Destroy(itemDansSlot.gameObject);
                hotbarSlots[currentSelectedIndex].itemInSlot = null;
                HideAllWeapons();
                currentSelectedIndex = -1;
                if (cadreSelection != null) cadreSelection.gameObject.SetActive(false);
                if (UIManager.Instance != null) UIManager.Instance.UpdateAmmoDisplay(0, 0, false);
            }
            else
            {
                itemDansSlot.SetVisualMode(true);
            }
        }
    }

    // ========================================================
    // --- GESTION DU RÂTELIER D'ARMES (Membres enfants de playerHand) ---
    // ========================================================

    public void HideAllWeapons()
    {
        if (playerHand == null) return;

        foreach (Transform child in playerHand)
        {
            child.gameObject.SetActive(false);
        }
    }

    private Transform FindWeaponInHand(ItemData item)
    {
        if (playerHand == null || item == null) return null;

        // 1. Cherche un enfant portant le nom exact de l'item (ex: "Pistolet")
        Transform found = playerHand.Find(item.itemName);
        if (found != null) return found;

        // 2. Cherche un enfant portant le nom du prefab associable s'il est défini
        if (item.weaponPrefab != null)
        {
            found = playerHand.Find(item.weaponPrefab.name);
            if (found != null) return found;
        }

        // 3. Recherche tolérante (si le nom contient partiellement le nom de l'item)
        foreach (Transform child in playerHand)
        {
            if (child.name.ToLower().Contains(item.itemName.ToLower()))
            {
                return child;
            }
        }

        return null;
    }
}