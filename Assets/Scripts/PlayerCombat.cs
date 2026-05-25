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

    void Start()
    {
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

            if (weapon.bulletPrefab != null && firePoint != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));

                if (groundPlane.Raycast(ray, out float distance))
                {
                    Vector3 targetPoint = ray.GetPoint(distance);
                    Vector3 correctiveDirection = (targetPoint - firePoint.position).normalized;
                    correctiveDirection.y = 0;

                    GameObject newBullet = Instantiate(weapon.bulletPrefab, firePoint.position, Quaternion.LookRotation(correctiveDirection));

                    Bullet bulletScript = newBullet.GetComponent<Bullet>();
                    if (bulletScript != null)
                    {
                        bulletScript.damage = weapon.damage;
                        bulletScript.shooter = this.gameObject; // <--- SÉCURITÉ DU JOUEUR ICI
                    }

                    if (muzzleFlashLight != null)
                        GameManager.Instance.ReportCrime(10);
                    {
                        StartCoroutine(ShowMuzzleFlash());
                    }
                }
            }

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