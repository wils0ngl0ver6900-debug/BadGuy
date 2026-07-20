using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LabSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    public enum PlantState
    {
        Vide, Terre_1_Mise, Graine_Mise, Arrosage_1_Fait, Terre_2_Mise, Arrosage_2_Fait, Arrosage_3_Fait, EnCroissance, Pret
    }
    public PlantState currentState = PlantState.Vide;

    [Header("UI & Visuels")]
    public Image slotImage;
    public Sprite imgPotVide;
    public Sprite imgPotTerre;
    public Sprite imgPotGraine;
    public Sprite imgPotCroissance;
    public Sprite imgPotPret;

    [Header("Récompense de Récolte 🌿")]
    public ItemData itemWeedDef;
    public int quantiteParRecolte = 5;

    [Header("Paramètres de Temps")]
    public float growTime = 120f;
    private float currentTimer = 0f;

    void Start() { UpdateVisuel(); }

    void Update()
    {
        if (currentState == PlantState.EnCroissance)
        {
            currentTimer -= Time.deltaTime;
            if (currentTimer <= 0)
            {
                currentState = PlantState.Pret;
                UpdateVisuel();
                if (WeedLabManager.Instance != null) WeedLabManager.Instance.ShowFeedback("La weed est prête à être récoltée !", Color.green);
            }
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        DraggableItem draggedItem = eventData.pointerDrag.GetComponent<DraggableItem>();
        if (draggedItem != null && draggedItem.itemData != null)
        {
            TraiterIngredient(draggedItem.itemData);
        }
    }

    private void TraiterIngredient(ItemData itemLache)
    {
        switch (currentState)
        {
            case PlantState.Vide:
                if (itemLache.itemName == "Terre") { currentState = PlantState.Terre_1_Mise; ConsommerItem(itemLache, true, "Terre placée au fond du pot !"); }
                else Refuser("Il faut d'abord mettre de la terre !");
                break;

            case PlantState.Terre_1_Mise:
                // --- MODIFICATION ICI : "Graine de Weed" ---
                if (itemLache.itemName == "Graine de Weed") { currentState = PlantState.Graine_Mise; ConsommerItem(itemLache, true, "Graine plantée avec succès !"); }
                else DetruirePlantation("Il fallait planter une graine");
                break;

            case PlantState.Graine_Mise:
                // --- MODIFICATION ICI : Texte simplifié ---
                if (itemLache.itemName == "Arrosoir") { currentState = PlantState.Arrosage_1_Fait; AccepterOutil("Terre humidifiée !"); }
                else DetruirePlantation("La graine avait besoin d'eau");
                break;

            case PlantState.Arrosage_1_Fait:
                if (itemLache.itemName == "Terre") { currentState = PlantState.Terre_2_Mise; ConsommerItem(itemLache, false, "Graine recouverte de terre !"); }
                else DetruirePlantation("Il fallait recouvrir la graine");
                break;

            case PlantState.Terre_2_Mise:
                // --- MODIFICATION ICI : Texte simplifié ---
                if (itemLache.itemName == "Arrosoir") { currentState = PlantState.Arrosage_2_Fait; AccepterOutil("Plante arrosée !"); }
                else DetruirePlantation("Mauvais ordre, il fallait arroser");
                break;

            case PlantState.Arrosage_2_Fait:
                // --- MODIFICATION ICI : Texte simplifié ---
                if (itemLache.itemName == "Arrosoir") { currentState = PlantState.Arrosage_3_Fait; AccepterOutil("Plante arrosée !"); }
                else DetruirePlantation("Mauvais ordre, il fallait encore arroser");
                break;

            case PlantState.Arrosage_3_Fait:
                // --- MODIFICATION ICI : Texte simplifié ---
                if (itemLache.itemName == "Arrosoir")
                {
                    currentState = PlantState.EnCroissance;
                    currentTimer = growTime;
                    AccepterOutil("Plante bien hydratée ! La croissance commence.", Color.green);
                }
                else DetruirePlantation("Mauvais ordre, il fallait arroser");
                break;

            case PlantState.EnCroissance:
                Refuser("Laissez la plante pousser tranquillement !");
                break;
            case PlantState.Pret:
                Refuser("La plante est déjà prête à être récoltée ! Cliquez pour ramasser.");
                break;
        }
        UpdateVisuel();
    }

    private void DetruirePlantation(string raison)
    {
        currentState = PlantState.Vide;
        UpdateVisuel();

        if (WeedLabManager.Instance != null)
        {
            WeedLabManager.Instance.ShowFeedback($"💥 ÉCHEC : {raison} ! Plantation détruite.", Color.red);
        }
    }

    private void ConsommerItem(ItemData item, bool detruireObjet, string message)
    {
        if (WeedLabManager.Instance != null)
        {
            WeedLabManager.Instance.ShowFeedback(message, Color.white);
            if (detruireObjet) WeedLabManager.Instance.ConsumeItem(item);
        }
    }

    private void AccepterOutil(string message, Color? customColor = null)
    {
        if (WeedLabManager.Instance != null)
        {
            WeedLabManager.Instance.ShowFeedback(message, customColor ?? new Color(0.2f, 0.6f, 1f)); // Bleu clair par défaut
        }
    }

    private void Refuser(string msg)
    {
        if (WeedLabManager.Instance != null) WeedLabManager.Instance.ShowFeedback(msg, new Color(1f, 0.5f, 0f)); // Orange
    }

    private void UpdateVisuel()
    {
        if (slotImage == null) return;

        switch (currentState)
        {
            case PlantState.Vide: slotImage.sprite = imgPotVide; break;
            case PlantState.Terre_1_Mise: slotImage.sprite = imgPotTerre; break;
            case PlantState.Graine_Mise: slotImage.sprite = imgPotGraine; break;
            case PlantState.Arrosage_1_Fait: slotImage.sprite = imgPotGraine; break;
            case PlantState.Terre_2_Mise: slotImage.sprite = imgPotCroissance; break;
            case PlantState.Arrosage_2_Fait: slotImage.sprite = imgPotCroissance; break;
            case PlantState.Arrosage_3_Fait: slotImage.sprite = imgPotCroissance; break;
            case PlantState.EnCroissance: slotImage.sprite = imgPotCroissance; break;
            case PlantState.Pret: slotImage.sprite = imgPotPret; break;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentState == PlantState.Pret)
        {
            if (InventoryManager.Instance != null && itemWeedDef != null)
            {
                int amountAdded = InventoryManager.Instance.AddItem(itemWeedDef, quantiteParRecolte, true);
                if (amountAdded > 0)
                {
                    if (WeedLabManager.Instance != null) WeedLabManager.Instance.ShowFeedback($"Récolte : +{amountAdded} Pochons de Weed !", Color.green);
                    currentState = PlantState.Vide;
                    UpdateVisuel();

                    InventoryUI invUI = FindObjectOfType<InventoryUI>();
                    if (invUI != null) invUI.RefreshUI();
                }
                else
                {
                    if (WeedLabManager.Instance != null) WeedLabManager.Instance.ShowFeedback("Sac à dos plein ! Impossible de récolter.", Color.red);
                }
            }
        }
    }
}