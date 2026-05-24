using UnityEngine;
using System.Collections; // INDISPENSABLE pour utiliser les Coroutines (les timers)

public class PlayerCombat : MonoBehaviour
{
    [Header("Configuration")]
    public Transform playerCamera;
    public Transform firePoint;

    [Header("Effets Visuels 💥")]
    public Light muzzleFlashLight; // La fameuse lumière du canon
    public float flashDuration = 0.05f; // Durée du flash (très court !)

    [Header("Munitions")]
    public int currentAmmo;
    private float nextFireTime = 0f;

    void Start()
    {
        // On s'assure que la lumière est éteinte au début du jeu
        if (muzzleFlashLight != null)
        {
            muzzleFlashLight.enabled = false;
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !Cursor.visible)
        {
            AttemptShoot();
        }

        if (Input.GetKeyDown(KeyCode.R) && !Cursor.visible)
        {
            Reload();
            
            if (Input.GetKeyDown(KeyCode.Alpha1)) // Ou ta touche de sélection
            {
                ItemData weapon = HotbarManager.Instance.GetEquippedItem();
                if (weapon != null && weapon.isWeapon && weapon.isIllegal)
                {
                    GameManager.Instance.IncreaseNotoriety(5); // Petit gain pour exhibition
                }
            }
        }
    }

    void AttemptShoot()
    {
        if (Time.time < nextFireTime) return; // Empêche de tirer trop vite

        ItemData weapon = HotbarManager.Instance.GetEquippedItem();

        if (weapon != null && weapon.isWeapon)
        {
            if (currentAmmo <= 0)
            {
                UIManager.Instance.ShowNotification("Clic ! (Rechargez avec R)");
                return;
            }

            // On consomme une balle
            currentAmmo--;
            nextFireTime = Time.time + weapon.fireRate;

            // On fait apparaître la balle physique
            if (weapon.bulletPrefab != null && firePoint != null)
            {
                // --- NOUVEAU : Calcul de la VISÉE CORRECTIVE 🎯 ---

                // 1. On trouve où la souris pointe EXACTEMENT sur le sol
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                // Crée un sol plat imaginaire à la hauteur du joueur pour la visée
                Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));

                if (groundPlane.Raycast(ray, out float distance))
                {
                    // C'est le point d'impact idéal que le joueur VOUDRAIT toucher
                    Vector3 targetPoint = ray.GetPoint(distance);

                    // 2. On calcule la direction corrective : (Target - Canon)
                    Vector3 correctiveDirection = (targetPoint - firePoint.position).normalized;

                    // On ne veut pas que la balle pique vers le sol, on garde Y plat (optionnel mais recommandé en top down)
                    correctiveDirection.y = 0;

                    // 3. On crée la balle avec cette rotation CORRECTIVE (et pas firePoint.rotation)
                    GameObject newBullet = Instantiate(weapon.bulletPrefab, firePoint.position, Quaternion.LookRotation(correctiveDirection));

                    // On transmet les dégâts de l'arme à la balle
                    Bullet bulletScript = newBullet.GetComponent<Bullet>();
                    if (bulletScript != null) bulletScript.damage = weapon.damage;

                    // ---> DÉCLENCHE LE FLASH DE LUMIÈRE <---
                    if (muzzleFlashLight != null)
                        GameManager.Instance.IncreaseNotoriety(10); // Tirer fait monter la notoriété
                    {
                        StartCoroutine(ShowMuzzleFlash());
                    }
                }
            }

            // On met à jour l'affichage HUD permanent (Solution Pro)
            UIManager.Instance.UpdateAmmoDisplay(currentAmmo, weapon.maxAmmo, true);
        }
    }

    public void Reload()
    {
        ItemData weapon = HotbarManager.Instance.GetEquippedItem();
        if (weapon != null && weapon.isWeapon)
        {
            currentAmmo = weapon.maxAmmo;
            UIManager.Instance.ShowNotification("Rechargement terminé !");

            // ✅ AJOUTE CETTE LIGNE :
            UIManager.Instance.UpdateAmmoDisplay(currentAmmo, weapon.maxAmmo, true);
        }
    }

    // --- LA FONCTION MAGIQUE POUR LE FLASH ---
    private IEnumerator ShowMuzzleFlash()
    {
        muzzleFlashLight.enabled = true; // Allume la lumière
        yield return new WaitForSeconds(flashDuration); // Attend 0.05 secondes
        muzzleFlashLight.enabled = false; // Éteint la lumière
    }
}