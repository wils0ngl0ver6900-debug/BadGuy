using UnityEngine;
using System.Collections;
using UnityEngine.Rendering; // INDISPENSABLE pour manipuler les Volumes
using UnityEngine.Rendering.HighDefinition; // INDISPENSABLE pour HDRP

public class PlayerController : MonoBehaviour
{
    [Header("Santé & Survie ❤️")]
    public int maxHealth = 100;
    public int currentHealth;

    [HideInInspector] public int maxShield = 0;
    [HideInInspector] public int currentShield = 0;
    private float clothingSpeedBonus = 0f; // Stocke le bonus des chaussures/pantalons

    [Header("Mouvement")]
    public float moveSpeed = 5f;
    private float originalMoveSpeed;
    private bool isSpeedBoosted = false;

    private bool isInComedown = false;
    private bool currentInvertControls = false;

    [Header("Inventaire UI")]
    public GameObject inventoryPanel;

    [HideInInspector] public bool isDoingQTE = false;

    private Rigidbody rb;
    private Vector3 moveInput;
    private Interactable currentInteractable;

    // --- VARIABLES POST-PROCESSING HDRP ---
    private Volume drogueVolume;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private Vignette vignette;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        originalMoveSpeed = moveSpeed;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealthDisplay(currentHealth, maxHealth);
        }

        // Configuration automatique du Post-Processing HDRP
        SetupPostProcessing();
    }

    private void SetupPostProcessing()
    {
        // On cherche le Global Volume via son Tag
        GameObject volumeObj = GameObject.FindWithTag("GameController");
        if (volumeObj != null)
        {
            drogueVolume = volumeObj.GetComponent<Volume>();
            if (drogueVolume != null && drogueVolume.profile != null)
            {
                // On extrait les composants HDRP du profil pour pouvoir les modifier par code
                drogueVolume.profile.TryGet(out chromaticAberration);
                drogueVolume.profile.TryGet(out lensDistortion);
                drogueVolume.profile.TryGet(out vignette);
            }
        }
        else
        {
            Debug.LogWarning("DrogueVolume introuvable ! Assurez-vous d'avoir mis le tag 'GameController' sur votre Global Volume.");
        }
    }

    void Update()
    {
        if (isDoingQTE)
        {
            moveInput = Vector3.zero;
            return;
        }

        bool isUIOpen = (inventoryPanel != null && inventoryPanel.activeSelf) ||
                         (ShopManager.Instance != null && ShopManager.Instance.shopPanel != null && ShopManager.Instance.shopPanel.activeSelf) ||
                         (LaundromatManager.Instance != null && LaundromatManager.Instance.laundromatPanel != null && LaundromatManager.Instance.laundromatPanel.activeSelf);

        if (isUIOpen)
        {
            moveInput = Vector3.zero;

            if (Input.GetKeyDown(KeyCode.I) && inventoryPanel != null && inventoryPanel.activeSelf)
            {
                inventoryPanel.SetActive(false);
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Confined;
                if (UIManager.Instance != null) UIManager.Instance.HideTooltip();
            }
            return;
        }

        // --- DÉPLACEMENT ---
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        if (isInComedown && currentInvertControls)
        {
            moveX = -moveX;
            moveZ = -moveZ;
        }

        moveInput = new Vector3(moveX, 0f, moveZ).normalized;

        // --- OUVRIR L'INVENTAIRE ---
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(true);
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        // --- ACTION PRINCIPALE : TOUCHE [E] ---
        if (Input.GetKeyDown(KeyCode.E))
        {
            ItemData equippedItem = null;
            if (HotbarManager.Instance != null)
            {
                equippedItem = HotbarManager.Instance.GetEquippedItem();
            }

            if (equippedItem != null && equippedItem.isConsumable)
            {
                bool itemHasBeenUsed = false;

                if (equippedItem.isDrugWithComedown)
                {
                    if (!isSpeedBoosted && !isInComedown)
                    {
                        StartCoroutine(DrugDoubleEffectRoutine(equippedItem));
                        itemHasBeenUsed = true;
                    }
                    else
                    {
                        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Votre corps ne supporterait pas une autre dose !");
                    }
                }
                else
                {
                    if (equippedItem.healAmount > 0)
                    {
                        if (currentHealth < maxHealth)
                        {
                            Heal(equippedItem.healAmount);
                            itemHasBeenUsed = true;
                        }
                        else if (equippedItem.speedBoostMultiplier == 0)
                        {
                            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Santé déjà au maximum !");
                        }
                    }

                    if (equippedItem.speedBoostMultiplier > 0)
                    {
                        if (!isSpeedBoosted && !isInComedown)
                        {
                            StartCoroutine(SimpleSpeedBoostRoutine(equippedItem.speedBoostMultiplier, equippedItem.buffDuration));
                            itemHasBeenUsed = true;
                        }
                    }
                }

                if (itemHasBeenUsed)
                {
                    HotbarManager.Instance.ConsumeEquippedItem();
                }
            }
            else if (currentInteractable != null)
            {
                currentInteractable.Interact();
            }
        }
    }

    void FixedUpdate()
    {
        bool isUIOpen = (inventoryPanel != null && inventoryPanel.activeSelf) ||
                         (ShopManager.Instance != null && ShopManager.Instance.shopPanel != null && ShopManager.Instance.shopPanel.activeSelf) ||
                         (LaundromatManager.Instance != null && LaundromatManager.Instance.laundromatPanel != null && LaundromatManager.Instance.laundromatPanel.activeSelf) ||
                         (SafehouseManager.Instance != null && SafehouseManager.Instance.safehousePanel != null && SafehouseManager.Instance.safehousePanel.activeSelf);

        if (isUIOpen || isDoingQTE)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        rb.MovePosition(rb.position + moveInput * (moveSpeed + clothingSpeedBonus) * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Interactable>(out Interactable interactable))
        {
            currentInteractable = interactable;
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Appuyez sur [E] pour interagir");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentInteractable != null && other.GetComponent<Interactable>() == currentInteractable)
        {
            currentInteractable = null;
            if (UIManager.Instance != null) UIManager.Instance.HideNotification();
        }
    }

    public void TakeDamage(int amount)
    {
        // Le bouclier encaisse d'abord !
        if (currentShield > 0)
        {
            if (amount <= currentShield)
            {
                currentShield -= amount;
                amount = 0;
            }
            else
            {
                amount -= currentShield;
                currentShield = 0;
            }
        }

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (UIManager.Instance != null) UIManager.Instance.UpdateHealthDisplay(currentHealth, maxHealth);

        if (currentHealth <= 0) Die();
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (UIManager.Instance != null) UIManager.Instance.UpdateHealthDisplay(currentHealth, maxHealth);
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"+{amount} PV");
    }

    private void Die()
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("VOUS ÊTES MORT !");
        gameObject.SetActive(false);
    }
    
    public void UpdateClothingSpeedBonus()
    {
        clothingSpeedBonus = 0f;
        if (EquipmentManager.Instance != null)
        {
            foreach (var item in EquipmentManager.Instance.currentEquipment)
            {
                if (item != null) clothingSpeedBonus += item.speedBonus;
            }
        }
    }
    private IEnumerator SimpleSpeedBoostRoutine(float multiplier, float duration)
    {
        isSpeedBoosted = true;
        moveSpeed = originalMoveSpeed * multiplier;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Boost actif ({duration}s) !");
        yield return new WaitForSeconds(duration);
        moveSpeed = originalMoveSpeed;
        isSpeedBoosted = false;
    }

    // --- LE MINUTEUR À DOUBLE EFFET AVEC EFFETS VISUELS HDRP ---
    private IEnumerator DrugDoubleEffectRoutine(ItemData drug)
    {
        // ==========================================
        // ETAPE 1 : LE HIGH (LE GROS BOOST)
        // ==========================================
        isSpeedBoosted = true;
        moveSpeed = originalMoveSpeed * drug.speedBoostMultiplier;

        if (drug.healAmount > 0) Heal(drug.healAmount);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"Substance absorbée ! Énergie maximale ({drug.buffDuration}s) !");

        yield return new WaitForSeconds(drug.buffDuration);

        // ==========================================
        // ETAPE 2 : LA DESCENTE (LE BAD TRIP HDRP)
        // ==========================================
        isSpeedBoosted = false;
        isInComedown = true;
        currentInvertControls = drug.invertControlsDuringComedown;
        moveSpeed = originalMoveSpeed * drug.comedownSpeedMultiplier;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("⚠️ VOUS ÊTES EN PLEINE DESCENTE ! ⚠️");

        // ---> ANIMATION DU POST-PROCESSING (On fait monter l'effet en 1 seconde) <---
        float elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            if (chromaticAberration != null) chromaticAberration.intensity.value = Mathf.Lerp(0f, 1f, elapsed);
            if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(0f, -0.4f, elapsed); // Déforme l'écran vers l'intérieur
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, 0.45f, elapsed); // Assombrit les bords
            yield return null;
        }

        // On attend que la descente se termine
        yield return new WaitForSeconds(drug.comedownDuration - 1f);

        // ==========================================
        // ETAPE 3 : RETOUR À LA NORMALE (On dissipe l'effet)
        // ==========================================
        elapsed = 0f;
        while (elapsed < 2f) // Le retour à la normale prend 2 secondes
        {
            elapsed += Time.deltaTime;
            if (chromaticAberration != null) chromaticAberration.intensity.value = Mathf.Lerp(1f, 0f, elapsed / 2f);
            if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(-0.4f, 0f, elapsed / 2f);
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0.45f, 0f, elapsed / 2f);
            yield return null;
        }

        moveSpeed = originalMoveSpeed;
        isInComedown = false;
        currentInvertControls = false;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("L'effet de la substance s'est totalement dissipé.");
    }
}