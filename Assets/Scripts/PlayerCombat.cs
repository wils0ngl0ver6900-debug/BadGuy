using UnityEngine;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    [Header("Configuration")]
    public Transform playerCamera;
    public Transform firePoint;

    [Header("Effets Visuels 💥")]
    public Light muzzleFlashLight;
    public float flashDuration = 0.05f;

    [Header("Munitions")]
    public int currentAmmo;
    private float nextFireTime = 0f;

    [Header("Combat à mains nues 👊")]
    public float punchRange = 1.0f;
    public int punchDamage = 15;
    public float punchCooldown = 0.3f;
    private float nextPunchTime = 0f;

    [Header("Enchaînement (Combo 3 coups) 👊👊👊")]
    [Tooltip("Si tu ne recliques pas dans ce délai après un coup, le combo repart au 1er coup.")]
    public float comboResetDelay = 1.2f;
    [Tooltip("Le 3ème coup fait-il plus de dégâts que le 1er ? (progression +0/+5/+10)")]
    public bool comboDamageRamp = true;
    private int comboStep = 0;
    private float lastPunchTime = -10f;

    // --- AJOUT : Référence à l'Animator ---
    private Animator anim;
    private PlayerAim playerAim; // Pour resynchroniser la visée pile avant de tirer (fix décalage flingue/curseur)

    void Start()
    {
        // --- AJOUT : Récupération de l'Animator au lancement ---
        anim = GetComponentInChildren<Animator>();
        playerAim = GetComponent<PlayerAim>();

        if (muzzleFlashLight != null)
        {
            muzzleFlashLight.enabled = false;
        }
    }

    void Update()
    {
        // --- AJOUT : Vérification continue de l'arme équipée pour l'Animator ---
        if (anim != null && HotbarManager.Instance != null)
        {
            ItemData currentItem = HotbarManager.Instance.GetEquippedItem();
            bool hasWeapon = (currentItem != null && currentItem.isWeapon);
            anim.SetBool("WeaponEquipped", hasWeapon);
        }

        if (Input.GetMouseButtonDown(0) && !Cursor.visible)
        {
            ItemData equipped = (HotbarManager.Instance != null) ? HotbarManager.Instance.GetEquippedItem() : null;
            if (equipped != null && equipped.isWeapon)
            {
                AttemptShoot();
            }
            else
            {
                AttemptPunch();
            }
        }

        if (Input.GetKeyDown(KeyCode.R) && !Cursor.visible)
        {
            Reload();

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ItemData weapon = HotbarManager.Instance.GetEquippedItem();
                if (weapon != null && weapon.isWeapon && weapon.isIllegal)
                {
                    GameManager.Instance.ReportCrime(5);
                }
            }
        }
    }

    void AttemptShoot()
    {
        if (Time.time < nextFireTime) return;

        ItemData weapon = HotbarManager.Instance.GetEquippedItem();

        if (weapon != null && weapon.isWeapon)
        {
            if (currentAmmo <= 0)
            {
                UIManager.Instance.ShowNotification("Clic ! (Rechargez avec R)");
                return;
            }

            currentAmmo--;
            nextFireTime = Time.time + weapon.fireRate;

            // --- AJOUT : Déclenchement de l'animation de tir ---
            if (anim != null) anim.SetTrigger("Shoot");

            if (weapon.bulletPrefab != null && firePoint != null && playerAim != null)
            {
                // CORRECTIF VISÉE : on force PlayerAim à se resynchroniser sur la souris À CET INSTANT
                // (sinon on lisait la rotation calculée à la frame précédente, Update() s'exécutant
                // toujours avant LateUpdate()), puis on tire dans EXACTEMENT la même direction que
                // celle utilisée pour orienter le perso, au lieu de refaire un raycast séparé depuis
                // firePoint. Avant ce correctif, les deux directions partaient d'un point différent
                // (centre du perso vs bout du canon) et ne pointaient donc pas toujours pareil,
                // surtout sur les cibles proches — d'où le flingue visuellement pas aligné avec
                // l'endroit où la balle partait vraiment.
                playerAim.RotateTowardsMouse();
                Vector3 correctiveDirection = playerAim.AimDirection;

                GameObject newBullet = Instantiate(weapon.bulletPrefab, firePoint.position, Quaternion.LookRotation(correctiveDirection));

                Bullet bulletScript = newBullet.GetComponent<Bullet>();
                if (bulletScript != null)
                {
                    bulletScript.damage = weapon.damage;
                    bulletScript.shooter = this.gameObject; // <--- SÉCURITÉ DU JOUEUR ICI
                }

                if (GameManager.Instance != null) GameManager.Instance.ReportCrime(10);

                if (muzzleFlashLight != null)
                {
                    StartCoroutine(ShowMuzzleFlash());
                }
            }

            UIManager.Instance.UpdateAmmoDisplay(currentAmmo, weapon.maxAmmo, true);
        }
    }

    void AttemptPunch()
    {
        if (Time.time < nextPunchTime) return;
        nextPunchTime = Time.time + punchCooldown;

        Debug.Log($"[PlayerCombat] Punch Range utilisé en jeu : {punchRange}");

        // Si trop de temps s'est écoulé depuis le dernier coup, on repart du 1er coup du combo
        if (Time.time - lastPunchTime > comboResetDelay)
        {
            comboStep = 0;
        }
        lastPunchTime = Time.time;

        comboStep = (comboStep % 3) + 1; // Cycle 1 -> 2 -> 3 -> 1 -> ...

        if (anim != null)
        {
            anim.SetTrigger("Punch" + comboStep); // Déclenche Punch1, Punch2 ou Punch3 selon l'étape
        }

        int appliedDamage = punchDamage;
        if (comboDamageRamp) appliedDamage += (comboStep - 1) * 5; // 1er coup = base, 2e = +5, 3e = +10

        // Centré sur le joueur lui-même (pas sur firePoint, qui peut être positionné loin devant
        // pour l'arme à feu) + un cône devant lui, pour que "à mains nues" veuille vraiment dire
        // "à portée de bras", et pas toucher quelqu'un à distance.
        Vector3 checkCenter = transform.position + Vector3.up * 1f;
        Collider[] hits = Physics.OverlapSphere(checkCenter, punchRange);
        foreach (Collider hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue; // Pas se cogner soi-même

            Vector3 toTarget = hit.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                float facingDot = Vector3.Dot(transform.forward, toTarget.normalized);
                if (facingDot < 0.4f) continue; // Cible pas assez devant nous (cône ~115°), on ignore
            }

            TargetHealth target = hit.GetComponentInParent<TargetHealth>();
            if (target != null)
            {
                Vector3 hitPoint = hit.ClosestPoint(checkCenter);
                VFXHelper.SpawnBloodSplatter(hitPoint, transform.forward);

                target.TakeDamage(appliedDamage, this.gameObject, true); // true = coup à mains nues

                if (GameManager.Instance != null) GameManager.Instance.ReportMeleeCrime(3);

                break; // Un seul PNJ touché par coup de poing
            }
        }
    }

    public void Reload()
    {
        ItemData weapon = HotbarManager.Instance.GetEquippedItem();
        if (weapon != null && weapon.isWeapon)
        {
            // --- AJOUT : Déclenchement de l'animation de rechargement ---
            if (anim != null) anim.SetTrigger("Reload");

            currentAmmo = weapon.maxAmmo;
            UIManager.Instance.ShowNotification("Rechargement terminé !");
            UIManager.Instance.UpdateAmmoDisplay(currentAmmo, weapon.maxAmmo, true);
        }
    }

    private IEnumerator ShowMuzzleFlash()
    {
        muzzleFlashLight.enabled = true;
        yield return new WaitForSeconds(flashDuration);
        muzzleFlashLight.enabled = false;
    }
}