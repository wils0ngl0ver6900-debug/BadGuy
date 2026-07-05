using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LaundromatManager : MonoBehaviour
{
    public static LaundromatManager Instance;

    [Header("UI Blanchisserie")]
    public GameObject laundromatPanel;
    public Slider amountSlider;
    public TextMeshProUGUI amountText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        CloseLaundromat();
    }

    public void OpenLaundromat()
    {
        laundromatPanel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        amountSlider.minValue = 0;
        amountSlider.maxValue = GameManager.Instance.dirtyMoney;
        amountSlider.value = 0;

        UpdateSliderText();
    }

    public void CloseLaundromat()
    {
        laundromatPanel.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    public void UpdateSliderText()
    {
        amountText.text = $"Montant à blanchir : {amountSlider.value}$";
    }

    public void ConfirmLaunder()
    {
        int amount = (int)amountSlider.value;

        if (amount > 0 && GameManager.Instance.dirtyMoney >= amount)
        {
            int tax = Mathf.RoundToInt(amount * 0.30f);
            int laundered = amount - tax;

            GameManager.Instance.dirtyMoney -= amount;
            GameManager.Instance.cleanMoney += laundered;

            UIManager.Instance.UpdateHUD();
            UIManager.Instance.ShowNotification($"{laundered}$ blanchis (Taxe : {tax}$)");

            // ---> AJOUT POUR LA QUÊTE <---
            if (QuestManager.Instance != null)
                QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.BlanchirArgent, amount);

            CloseLaundromat();
        }
        else
        {
            UIManager.Instance.ShowNotification("Montant invalide ou insuffisant !");
        }
    }
}