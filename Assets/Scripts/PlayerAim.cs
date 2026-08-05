using UnityEngine;

public class PlayerAim : MonoBehaviour
{
    private Camera mainCamera;
    public Vector3 AimDirection { get; private set; } = Vector3.forward;

    void Start()
    {
        mainCamera = Camera.main;

        // Le curseur est confin� � la fen�tre pour pouvoir tourner !
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    // --- MODIFICATION ICI : Update() devient LateUpdate() ---
    void LateUpdate()
    {
        // --- LE CORRECTIF EST ICI ---
        // Si le jeu est fig� (Map ouverte, Menu de pause...), on bloque la vis�e !
        if (Time.timeScale == 0f) return;
        // ----------------------------

        if (SafehouseManager.Instance != null && SafehouseManager.Instance.safehousePanel != null && SafehouseManager.Instance.safehousePanel.activeSelf) return;
        if (ShopManager.Instance != null && ShopManager.Instance.shopPanel != null && ShopManager.Instance.shopPanel.activeSelf) return;
        if (LaundromatManager.Instance != null && LaundromatManager.Instance.laundromatPanel != null && LaundromatManager.Instance.laundromatPanel.activeSelf) return;

        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null && pc.inventoryPanel != null && pc.inventoryPanel.activeSelf) return;

        if (PhoneManager.Instance != null && PhoneManager.Instance.isPhoneOpen) return;

        RotateTowardsMouse();
    }

    public void RotateTowardsMouse()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 lookPoint = ray.GetPoint(distance);
            Vector3 direction = (lookPoint - transform.position).normalized;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                AimDirection = direction;
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = targetRotation;
            }
        }
    }
}