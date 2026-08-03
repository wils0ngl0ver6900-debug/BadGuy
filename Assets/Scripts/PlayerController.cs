using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class PlayerController : MonoBehaviour
{
    [Header("Santé & Survie ❤️")]
    public int maxHealth = 100;
    public int currentHealth;

    [HideInInspector] public int maxShield = 0;
    [HideInInspector] public int currentShield = 0;
    private float clothingSpeedBonus = 0f;

    [Header("Mouvement")]
    public float moveSpeed = 5f;
    private float originalMoveSpeed;
    private bool isSpeedBoosted = false;
    private bool isInComedown = false;
    private bool currentInvertControls = false;
    private bool isTimeSlowed = false;

    [Header("Inventaire UI")]
    public GameObject inventoryPanel;

    [HideInInspector] public bool isDoingQTE = false;
    [HideInInspector] public bool isKnockedDown = false;

    private Rigidbody rb;
    private Animator anim; // --- AJOUT : Variable globale pour l'Animator ---
    private Vector3 moveInput;
    private Interactable currentInteractable;

    private Volume drogueVolume;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>(); // --- AJOUT : Initialisation de l'Animator au lancement ---
        originalMoveSpeed = moveSpeed;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealthDisplay(currentHealth, maxHealth);
        }

        SetupPostProcessing();
        SetupRagdollColliderIgnores();
    }

    // Les colliders ajoutés par l'Assistant Ragdoll sur les os (bras, jambes, bassin...)
    // entrent en collision avec le collider principal ET entre eux en permanence pendant
    // que le perso est vivant et animé — d'où les tremblements et les poussées quand la
    // caméra/visée fait tourner le squelette. Il faut leur dire de s'ignorer mutuellement
    // tant qu'ils ne servent pas encore de vrai ragdoll (géré séparément par GameManager
    // à la mort, qui active la physique dessus — l'ignore-collision configuré ici reste
    // actif même à ce moment-là, ce qui est correct : les os du ragdoll n'ont pas besoin
    // de se cogner contre l'ancien collider principal, désactivé de toute façon à la mort).
    private void SetupRagdollColliderIgnores()
    {
        Collider mainCollider = GetComponent<Collider>();
        Collider[] allColliders = GetComponentsInChildren<Collider>();

        // Force tous les colliders des os sur le MÊME calque que la racine. L'Assistant
        // Ragdoll crée souvent ses colliders sur un calque par défaut différent de celui
        // du personnage — s'il ne collisionne pas avec le sol dans la matrice de collision
        // du projet, les os tombent à travers le monde indéfiniment au lieu de s'arrêter.
        int rootLayer = gameObject.layer;
        foreach (Collider col in allColliders)
        {
            col.gameObject.layer = rootLayer;
        }

        for (int i = 0; i < allColliders.Length; i++)
        {
            if (mainCollider != null && allColliders[i] != mainCollider)
            {
                Physics.IgnoreCollision(mainCollider, allColliders[i], true);
            }

            for (int j = i + 1; j < allColliders.Length; j++)
            {
                Physics.IgnoreCollision(allColliders[i], allColliders[j], true);
            }
        }

        // MANQUAIT DEPUIS LE DÉBUT : tant que le joueur est vivant, les Rigidbody des os
        // doivent être kinematic (contrôlés par l'Animator, pas par la physique). Sans ça,
        // un os non-kinematic déplacé par l'animation (bras qui swing en marchant) est
        // interprété par PhysX comme un déplacement physique réel à très haute vitesse —
        // ce qui explique un simple contact avec une voiture À L'ARRÊT calculant un impact
        // énorme et tuant d'un coup, alors que la voiture, elle, ne bouge pas.
        Rigidbody[] boneRigidbodies = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody boneRb in boneRigidbodies)
        {
            if (boneRb.gameObject == gameObject) continue; // La racine reste non-kinematic (mouvement normal)
            boneRb.isKinematic = true;
        }
    }

    private void SetupPostProcessing()
    {
        GameObject volumeObj = GameObject.FindWithTag("GameController");
        if (volumeObj != null)
        {
            drogueVolume = volumeObj.GetComponent<Volume>();
            if (drogueVolume != null && drogueVolume.profile != null)
            {
                drogueVolume.profile.TryGet(out chromaticAberration);
                drogueVolume.profile.TryGet(out lensDistortion);
                drogueVolume.profile.TryGet(out vignette);
                drogueVolume.profile.TryGet(out colorAdjustments);
            }
        }
    }

    void Update()
    {
        // --- NOUVEAU VERROU : On bloque tout accès si le coffre OU la plantation sont ouverts ---
        if ((SafehouseManager.Instance != null && SafehouseManager.Instance.isOpen) ||
            (WeedLabManager.Instance != null && WeedLabManager.Instance.isOpen))
        {
            moveInput = Vector3.zero;
            UpdateAnimator(); // --- AJOUT : Force le retour à l'animation "Idle" ---
            return;
        }

        if (isDoingQTE || isKnockedDown)
        {
            moveInput = Vector3.zero;
            UpdateAnimator(); // --- AJOUT : Force le retour à l'animation "Idle" ---
            return;
        }

        bool isUIOpen = (inventoryPanel != null && inventoryPanel.activeSelf) ||
                         (ShopManager.Instance != null && ShopManager.Instance.shopPanel != null && ShopManager.Instance.shopPanel.activeSelf) ||
                         (LaundromatManager.Instance != null && LaundromatManager.Instance.laundromatPanel != null && LaundromatManager.Instance.laundromatPanel.activeSelf);

        if (isUIOpen)
        {
            moveInput = Vector3.zero;
            UpdateAnimator(); // --- AJOUT : Force le retour à l'animation "Idle" ---

            if (Input.GetKeyDown(KeyCode.I) && inventoryPanel != null && inventoryPanel.activeSelf)
            {
                inventoryPanel.SetActive(false);
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Confined;
                if (UIManager.Instance != null) UIManager.Instance.HideTooltip();
            }
            return;
        }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        if (isInComedown && currentInvertControls)
        {
            moveX = -moveX;
            moveZ = -moveZ;
        }

        moveInput = new Vector3(moveX, 0f, moveZ).normalized;

        // --- AJOUT : Envoi constant de la vitesse de déplacement à l'Animator ---
        UpdateAnimator();

        if (Input.GetKeyDown(KeyCode.I))
        {
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(true);
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            // --- CORRECTION : l'interaction (PNJ, ATM, boutique...) passe désormais AVANT la
            // consommation d'un objet équipé. Avant ce changement, avoir une drogue équipée en
            // s'approchant d'un Interactable la faisait consommer sur soi au lieu de déclencher
            // l'interaction — bloquant notamment toute vente de drogue à un PNJ, puisqu'il faut
            // justement avoir la drogue "en main" pour s'en approcher.
            if (currentInteractable != null)
            {
                currentInteractable.Interact();
            }
            else
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
                        if (!isSpeedBoosted && !isInComedown && !isTimeSlowed)
                        {
                            string itemNameLower = equippedItem.itemName.ToLower();

                            if (itemNameLower.Contains("weed"))
                            {
                                StartCoroutine(WeedEffectRoutine());
                            }
                            else if (itemNameLower.Contains("héro") || itemNameLower.Contains("hero"))
                            {
                                StartCoroutine(HeroinEffectRoutine(equippedItem));
                            }
                            else
                            {
                                StartCoroutine(DrugDoubleEffectRoutine(equippedItem));
                            }

                            itemHasBeenUsed = true;
                        }
                        else
                        {
                            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Votre corps ne supporterait pas une dose supplémentaire !");
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
            }
        }
    }

    // --- MISE A JOUR : Méthode personnalisée pour mettre à jour l'Animator proprement en 2D ---
    private void UpdateAnimator()
    {
        if (anim != null)
        {
            // Convertit le mouvement global en mouvement local (par rapport à la visée)
            Vector3 localMove = transform.InverseTransformDirection(moveInput);

            // Envoie les valeurs X et Z au Blend Tree 2D
            anim.SetFloat("InputX", localMove.x);
            anim.SetFloat("InputY", localMove.z);
        }
    }

    void FixedUpdate()
    {
        // --- MISE À JOUR : On inclut la plantation dans la vérification de l'UI pour stopper la physique ---
        bool isUIOpen = (inventoryPanel != null && inventoryPanel.activeSelf) ||
                         (ShopManager.Instance != null && ShopManager.Instance.shopPanel != null && ShopManager.Instance.shopPanel.activeSelf) ||
                         (LaundromatManager.Instance != null && LaundromatManager.Instance.laundromatPanel != null && LaundromatManager.Instance.laundromatPanel.activeSelf) ||
                         (SafehouseManager.Instance != null && SafehouseManager.Instance.isOpen) ||
                         (WeedLabManager.Instance != null && WeedLabManager.Instance.isOpen);

        if (isUIOpen || isDoingQTE || isKnockedDown)
        {
            if (!isKnockedDown) rb.linearVelocity = Vector3.zero;
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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Wasted();
        }
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

    // ========================================================
    // --- LES 3 TYPES DE DROGUES ---
    // ========================================================

    // 1. LA WEED (Bullet Time centré sur le monde + Filtre Vert Léger)
    private IEnumerator WeedEffectRoutine()
    {
        isTimeSlowed = true;
        isSpeedBoosted = true;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("Le monde ralentit... (30s)");

        // --- MODIFICATION : Utilisation de la variable globale au lieu de chercher le composant à nouveau ---
        if (anim != null) anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        Time.timeScale = 0.4f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        moveSpeed = originalMoveSpeed * 2.5f;

        // --- FADE IN VERT LÉGER ---
        float elapsed = 0f;
        Color normalColor = Color.white;
        Color weedColor = new Color(0.8f, 1f, 0.8f); // Vert très doux

        // On utilise unscaledDeltaTime car le jeu est ralenti ! 
        // Ça permet au fondu de se faire en 1 seconde "réelle".
        while (elapsed < 1f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (colorAdjustments != null) colorAdjustments.colorFilter.value = Color.Lerp(normalColor, weedColor, elapsed);
            yield return null;
        }

        // On attend 29 secondes IRL (30 - la seconde du fondu)
        yield return new WaitForSecondsRealtime(29f);

        // --- FADE OUT (Retour à la normale) ---
        elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (colorAdjustments != null) colorAdjustments.colorFilter.value = Color.Lerp(weedColor, normalColor, elapsed);
            yield return null;
        }

        // Restauration de la physique et vitesse
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        moveSpeed = originalMoveSpeed;
        if (anim != null) anim.updateMode = AnimatorUpdateMode.Normal;
        if (colorAdjustments != null) colorAdjustments.colorFilter.value = normalColor; // Sécurité

        isTimeSlowed = false;
        isSpeedBoosted = false;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("Le temps reprend son cours normal.");
    }

    // 2. L'HÉROÏNE (Buff 15s + Écran Rouge)
    private IEnumerator HeroinEffectRoutine(ItemData drug)
    {
        isSpeedBoosted = true;

        float buffMult = drug.speedBoostMultiplier > 0 ? drug.speedBoostMultiplier : 1.2f;
        moveSpeed = originalMoveSpeed * buffMult;

        if (drug.healAmount > 0) Heal(drug.healAmount);

        float buffTime = 15f;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"Flash d'euphorie ! ({buffTime}s)");

        yield return new WaitForSeconds(buffTime);

        // --- LA DESCENTE ---
        isSpeedBoosted = false;
        isInComedown = true;
        currentInvertControls = drug.invertControlsDuringComedown;

        float comedownMult = Mathf.Abs(drug.comedownSpeedMultiplier);
        moveSpeed = originalMoveSpeed * (comedownMult > 0 ? comedownMult : 0.5f);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("⚠️ BAD TRIP À L'HÉROÏNE ⚠️");

        float elapsed = 0f;
        Color normalColor = Color.white;
        Color tripColor = new Color(1f, 0.2f, 0.2f); // Écran teinté en rouge sang

        while (elapsed < 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 2f;
            if (chromaticAberration != null) chromaticAberration.intensity.value = Mathf.Lerp(0f, 1f, t);
            if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(0f, -0.6f, t);
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, 0.5f, t);

            if (colorAdjustments != null) colorAdjustments.colorFilter.value = Color.Lerp(normalColor, tripColor, t);

            yield return null;
        }

        yield return new WaitForSeconds(drug.comedownDuration > 2f ? drug.comedownDuration - 2f : 15f);

        elapsed = 0f;
        while (elapsed < 3f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 3f;
            if (chromaticAberration != null) chromaticAberration.intensity.value = Mathf.Lerp(1f, 0f, t);
            if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(-0.6f, 0f, t);
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0.5f, 0f, t);
            if (colorAdjustments != null) colorAdjustments.colorFilter.value = Color.Lerp(tripColor, normalColor, t);
            yield return null;
        }

        moveSpeed = originalMoveSpeed;
        isInComedown = false;
        currentInvertControls = false;
        if (colorAdjustments != null) colorAdjustments.colorFilter.value = normalColor;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("Les effets de l'héroïne se sont dissipés.");
    }

    // 3. LA COCAÏNE (Déformation classique)
    private IEnumerator DrugDoubleEffectRoutine(ItemData drug)
    {
        isSpeedBoosted = true;
        moveSpeed = originalMoveSpeed * drug.speedBoostMultiplier;

        if (drug.healAmount > 0) Heal(drug.healAmount);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"Substance absorbée ! Énergie maximale ({drug.buffDuration}s) !");

        yield return new WaitForSeconds(drug.buffDuration);

        isSpeedBoosted = false;
        isInComedown = true;
        currentInvertControls = drug.invertControlsDuringComedown;
        moveSpeed = originalMoveSpeed * drug.comedownSpeedMultiplier;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("⚠️ VOUS ÊTES EN PLEINE DESCENTE ! ⚠️");

        float elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            if (chromaticAberration != null) chromaticAberration.intensity.value = Mathf.Lerp(0f, 1f, elapsed);
            if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(0f, -0.4f, elapsed);
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, 0.45f, elapsed);
            yield return null;
        }

        yield return new WaitForSeconds(drug.comedownDuration - 1f);

        elapsed = 0f;
        while (elapsed < 2f)
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

    // ========================================================

    public void Knockdown(Vector3 pushForce)
    {
        if (currentHealth <= 0 || isKnockedDown) return;
        StartCoroutine(PlayerKnockdownRoutine(pushForce));
    }

    private IEnumerator PlayerKnockdownRoutine(Vector3 pushForce)
    {
        isKnockedDown = true;
        this.enabled = false;

        // Réutilise le vrai système de ragdoll (les os) plutôt que de faire tourner la seule
        // capsule racine pendant que l'Animator continue d'animer par-dessus — c'est ce
        // conflit-là qui causait le bazar visuel/collisions bizarres avec les véhicules
        // depuis l'ajout du rig de ragdoll.
        if (GameManager.Instance != null) GameManager.Instance.EnablePlayerRagdoll(gameObject);

        Rigidbody[] boneRbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody boneRb in boneRbs)
        {
            if (boneRb.gameObject == gameObject) continue; // Racine déjà gérée par EnablePlayerRagdoll
            boneRb.AddForce(pushForce, ForceMode.Impulse);
            boneRb.AddTorque(Random.insideUnitSphere * pushForce.magnitude, ForceMode.Impulse);
        }

        yield return new WaitForSeconds(3f);

        if (currentHealth > 0)
        {
            if (GameManager.Instance != null) GameManager.Instance.DisablePlayerRagdoll(gameObject);
            this.enabled = true;
        }

        isKnockedDown = false;
    }
}