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

    [Header("Écran Noir (Transition) 🎬")]
    public GameObject transitionPanel;
    private CanvasGroup transitionCanvasGroup; // Le secret du fondu !

    private Coroutine notificationCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        SetupTransitionPanel();

        UpdateHUD();
        HideNotification();
        HideTooltip();
        HideQTE();
        UpdateAmmoDisplay(0, 0, false);

        if (textVehicleName != null) textVehicleName.gameObject.SetActive(false);
        if (textDistrictName != null) textDistrictName.gameObject.SetActive(false);
        if (textDistrictControl != null) textDistrictControl.gameObject.SetActive(false);
    }

    // --- CRÉATION DE L'ÉCRAN NOIR DANS LE CANVAS ---
    private void SetupTransitionPanel()
    {
        if (transitionPanel != null)
        {
            transitionCanvasGroup = transitionPanel.GetComponent<CanvasGroup>();
            if (transitionCanvasGroup == null) transitionCanvasGroup = transitionPanel.AddComponent<CanvasGroup>();
            return;
        }

        // CORRECTIF : On cherche le Canvas principal de ta scène !
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null) return;

        GameObject panelObj = new GameObject("DynamicTransitionPanel");
        // On place l'image DANS le Canvas, sinon elle est invisible !
        panelObj.transform.SetParent(mainCanvas.transform, false);

        Image bgImage = panelObj.AddComponent<Image>();
        bgImage.color = Color.black;
        bgImage.raycastTarget = false;

        RectTransform rect = panelObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        transitionCanvasGroup = panelObj.AddComponent<CanvasGroup>();
        transitionCanvasGroup.alpha = 0f; // Invisible au début
        transitionCanvasGroup.blocksRaycasts = false;
        transitionCanvasGroup.interactable = false;

        rect.SetAsLastSibling(); // Met l'écran noir par-dessus tout le reste

        transitionPanel = panelObj;
    }

    // --- LES FONCTIONS DE FONDU ---
    public IEnumerator FadeToBlack(float duration)
    {
        if (transitionCanvasGroup == null) yield break;

        transitionCanvasGroup.blocksRaycasts = true; // Bloque les clics de souris
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transitionCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        transitionCanvasGroup.alpha = 1f;
    }

    public IEnumerator FadeToClear(float duration)
    {
        if (transitionCanvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transitionCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        transitionCanvasGroup.alpha = 0f;
        transitionCanvasGroup.blocksRaycasts = false; // Libère la souris
    }

    // ... (Le reste de tes fonctions UIManager habituelles) ...

    public void UpdateHUD()
    {
        if (GameManager.Instance == null) return;
        if (textDirtyMoney != null) textDirtyMoney.text = $"Argent Sale: {GameManager.Instance.dirtyMoney}$";
        if (textCleanMoney != null) textCleanMoney.text = $"Argent Propre: {GameManager.Instance.cleanMoney}$";

        if (textNotoriety != null)
        {
            string stars = "";
            string activeColor = GameManager.Instance.isEvading ? "<color=#AAAAAA>" : "<color=red>";

            for (int i = 1; i <= 5; i++)
            {
                if (i <= GameManager.Instance.wantedLevel) stars += $"{activeColor}★</color> ";
                else stars += "<color=#444444>★</color> ";
            }
            textNotoriety.text = stars;
        }

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

    public void UpdateDistrictControlHUD()
    {
        if (TerritoryManager.Instance != null && TerritoryManager.Instance.currentDistrictName != "Inconnu")
        {
            var d = TerritoryManager.Instance.cityDistricts.Find(x => x.districtName == TerritoryManager.Instance.currentDistrictName);
            if (d != null && textDistrictControl != null)
            {
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