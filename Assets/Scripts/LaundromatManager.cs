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

        // On affiche la souris dans le menu
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

        // CORRECTION : On cache la souris en jeu, mais on la confine pour que le joueur puisse tourner
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
            // --- NOUVEAU : Calcul de la taxe (30%) ---
            int tax = Mathf.RoundToInt(amount * 0.30f);
            int laundered = amount - tax; // Ce qui reste pour le joueur

            // On retire la totalité de l'argent sale sélectionné
            GameManager.Instance.dirtyMoney -= amount;
            // On ajoute seulement l'argent propre déduit de la taxe
            GameManager.Instance.cleanMoney += laundered;

            UIManager.Instance.UpdateHUD();

            // On affiche un joli message avec le détail
            UIManager.Instance.ShowNotification($"{laundered}$ blanchis (Taxe : {tax}$)");

            CloseLaundromat();
        }
        else
        {
            UIManager.Instance.ShowNotification("Montant invalide ou insuffisant !");
        }
    }
}