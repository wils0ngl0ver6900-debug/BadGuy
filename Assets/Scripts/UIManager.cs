using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("HUD Quartiers / Zones 🏙️")]
    public TextMeshProUGUI textDistrictName;
    public TextMeshProUGUI textDistrictControl;

    [Header("Santé UI")]
    public Slider healthBar;

    [Header("Composants HUD")]
    public TextMeshProUGUI textDirtyMoney;
    public TextMeshProUGUI textCleanMoney;
    public TextMeshProUGUI textNotoriety;
    public TextMeshProUGUI textNotification;
    public TextMeshProUGUI textAmmo;

    public TextMeshProUGUI textVehicleName;

    [Header("Système de Tooltip (Panneau FIXE)")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipName;
    public TextMeshProUGUI tooltipDescription;
    public TextMeshProUGUI tooltipRarity;
    public TextMeshProUGUI tooltipValue;

    [Header("Système de QTE (Mini-Jeu Effraction) ⏱️")]
    public GameObject qtePanel;
    public TextMeshProUGUI qteActionNameText;
    public TextMeshProUGUI qteKeyText;
    public Slider qteSlider;

    private Coroutine notificationCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        UpdateHUD();
        HideNotification();
        HideTooltip();
        HideQTE();
        UpdateAmmoDisplay(0, 0, false);

        if (textVehicleName != null) textVehicleName.gameObject.SetActive(false);
        if (textDistrictName != null) textDistrictName.gameObject.SetActive(false);
        if (textDistrictControl != null) textDistrictControl.gameObject.SetActive(false);
    }

    public void UpdateHUD()
    {
        if (GameManager.Instance == null) return;

        if (textDirtyMoney != null) textDirtyMoney.text = $"Argent Sale: {GameManager.Instance.dirtyMoney}$";
        if (textCleanMoney != null) textCleanMoney.text = $"Argent Propre: {GameManager.Instance.cleanMoney}$";
        if (textNotoriety != null) textNotoriety.text = $"Recherche: {GameManager.Instance.notoriety}%";

        UpdateDistrictControlHUD();
    }

    public void ShowTooltip(ItemData item)
    {
        if (tooltipPanel != null && item != null)
        {
            tooltipPanel.SetActive(true);
            if (tooltipName != null) tooltipName.text = item.itemName;
            if (tooltipDescription != null) tooltipDescription.text = item.description;

            if (tooltipRarity != null)
            {
                tooltipRarity.text = item.rarity.ToString();
                switch (item.rarity)
                {
                    case ItemData.Rarity.Basique: tooltipRarity.color = Color.white; break;
                    case ItemData.Rarity.PeuCourant: tooltipRarity.color = Color.green; break;
                    case ItemData.Rarity.Rare: tooltipRarity.color = new Color(0, 0.5f, 1f); break;
                    case ItemData.Rarity.Legendaire: tooltipRarity.color = new Color(1f, 0.5f, 0f); break;
                }
            }
        }
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    public void ShowNotification(string message)
    {
        if (textNotification != null)
        {
            textNotification.text = message;
            textNotification.gameObject.SetActive(true);

            if (notificationCoroutine != null) StopCoroutine(notificationCoroutine);
            notificationCoroutine = StartCoroutine(HideNotificationAfterDelay(3f));
        }
    }

    public void HideNotification()
    {
        if (textNotification != null) textNotification.gameObject.SetActive(false);
    }

    private IEnumerator HideNotificationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideNotification();
        notificationCoroutine = null;
    }

    public void UpdateAmmoDisplay(int current, int max, bool show)
    {
        if (textAmmo != null)
        {
            if (show)
            {
                textAmmo.text = $"{current} / {max}";
                textAmmo.gameObject.SetActive(true);
            }
            else textAmmo.gameObject.SetActive(false);
        }
    }

    public void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
    }

    // --- FONCTIONS DU MINI-JEU QTE ---
    public void ShowQTE(string keyName, string actionName)
    {
        if (qtePanel != null)
        {
            qtePanel.SetActive(true);
            if (qteKeyText != null) qteKeyText.text = keyName;
            if (qteActionNameText != null) qteActionNameText.text = actionName;
            if (qteSlider != null) qteSlider.value = 1f;
        }
    }

    public void UpdateQTETimer(float fillAmount)
    {
        if (qteSlider != null) qteSlider.value = fillAmount;
    }

    public void HideQTE()
    {
        if (qtePanel != null) qtePanel.SetActive(false);
    }

    // --- FONCTIONS HUD VÉHICULE ---
    public void ShowVehicleHUD(string vehicleName)
    {
        if (textVehicleName != null)
        {
            textVehicleName.text = vehicleName;
            textVehicleName.gameObject.SetActive(true);
        }
    }

    public void HideVehicleHUD()
    {
        if (textVehicleName != null)
        {
            textVehicleName.gameObject.SetActive(false);
        }
    }

    // --- GESTION DE L'AFFICHAGE DES QUARTIERS ---
    public void UpdateDistrictControlHUD()
    {
        if (TerritoryManager.Instance != null && TerritoryManager.Instance.currentDistrictName != "Inconnu")
        {
            var d = TerritoryManager.Instance.cityDistricts.Find(x => x.districtName == TerritoryManager.Instance.currentDistrictName);

            if (d != null && textDistrictControl != null)
            {
                // FIX : On utilise "playerControlPercentage" qui est le vrai nom de ta variable !
                textDistrictControl.text = $"[{d.playerControlPercentage}%]";
                textDistrictControl.gameObject.SetActive(true);
            }
        }
        else
        {
            if (textDistrictControl != null) textDistrictControl.gameObject.SetActive(false);
        }
    }

    public void ShowDistrictHUD(string name)
    {
        if (textDistrictName != null)
        {
            textDistrictName.text = name.ToUpper();
            textDistrictName.gameObject.SetActive(true);
        }

        UpdateDistrictControlHUD();
    }

    public void HideDistrictHUD()
    {
        if (textDistrictName != null) textDistrictName.gameObject.SetActive(false);
        if (textDistrictControl != null) textDistrictControl.gameObject.SetActive(false);
    }
}