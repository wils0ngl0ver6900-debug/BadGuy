using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Bac de production du labo Cocaïne. Script séparé et dédié (ne touche pas à LabSlot,
// celui de la weed). Même principe : on dépose les ingrédients dans l'ordre, un mauvais
// ingrédient fait tout rater, la dernière étape lance le minuteur puis c'est prêt à
// récupérer. La recette (recipeSteps) se configure entièrement dans l'Inspector.
public class CocaineLabSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [System.Serializable]
    public class RecipeStep
    {
        public ItemData requiredItem;

        [Tooltip("Coché = l'objet est consommé (sac/coffre) quand il est déposé à cette étape. Décoche pour un outil réutilisable (type arrosoir/chalumeau).")]
        public bool consumeItem = true;

        public string successMessage = "Étape validée !";

        [Tooltip("Sprite du bac une fois cette étape validée. Laisse vide pour garder le sprite de l'étape précédente.")]
        public Sprite visualAfterStep;
    }

    [Header("Référence au Manager parent")]
    public CocaineLabManager manager;

    [Header("Recette (dans l'ordre) 🧪")]
    public RecipeStep[] recipeSteps;

    [Header("UI & Visuels")]
    public Image slotImage;
    public Sprite imgVide;
    public Sprite imgEnPreparation;
    public Sprite imgEnCours;
    public Sprite imgPret;

    [Header("Récompense de Production 💊")]
    public ItemData itemProduitFini;
    public int quantiteParProduction = 5;

    [Header("Paramètres de Temps")]
    public float tempsDeProduction = 180f;
    private float currentTimer = 0f;

    private int currentStepIndex = 0;
    private bool isProcessing = false; // Équivalent de "EnCroissance" chez la weed
    private bool isReady = false;

    void Start() { UpdateVisuel(); }

    void Update()
    {
        if (isProcessing)
        {
            currentTimer -= Time.deltaTime;
            if (currentTimer <= 0)
            {
                isProcessing = false;
                isReady = true;
                UpdateVisuel();

                string nom = itemProduitFini != null ? itemProduitFini.itemName : "Production";
                if (manager != null) manager.ShowFeedback($"{nom} prête à être récupérée !", Color.green);
            }
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (isProcessing) { Refuser("Laisse l'opération se terminer tranquillement !"); return; }
        if (isReady) { Refuser("La production est déjà prête ! Clique pour la récupérer."); return; }

        DraggableItem draggedItem = eventData.pointerDrag.GetComponent<DraggableItem>();
        if (draggedItem == null || draggedItem.itemData == null) return;

        TraiterIngredient(draggedItem.itemData);
    }

    private void TraiterIngredient(ItemData itemLache)
    {
        if (recipeSteps == null || recipeSteps.Length == 0) return;
        if (currentStepIndex < 0 || currentStepIndex >= recipeSteps.Length) return;

        RecipeStep step = recipeSteps[currentStepIndex];

        if (step.requiredItem != null && itemLache == step.requiredItem)
        {
            if (manager != null)
            {
                manager.ShowFeedback(step.successMessage, Color.white);
                if (step.consumeItem) manager.ConsumeItem(itemLache);
            }

            if (step.visualAfterStep != null && slotImage != null) slotImage.sprite = step.visualAfterStep;

            currentStepIndex++;

            if (currentStepIndex >= recipeSteps.Length)
            {
                isProcessing = true;
                currentTimer = tempsDeProduction;
                if (manager != null) manager.ShowFeedback("Ça presse... la production a démarré.", new Color(0.2f, 0.6f, 1f));
            }

            UpdateVisuel();
        }
        else
        {
            DetruireLot(itemLache);
        }
    }

    private void DetruireLot(ItemData itemRefuse)
    {
        currentStepIndex = 0;
        isProcessing = false;
        isReady = false;
        UpdateVisuel();

        if (manager != null)
            manager.ShowFeedback($"💥 RATÉ : mauvais ingrédient ({itemRefuse.itemName}). Le lot est perdu.", Color.red);
    }

    private void Refuser(string msg)
    {
        if (manager != null) manager.ShowFeedback(msg, new Color(1f, 0.5f, 0f));
    }

    private void UpdateVisuel()
    {
        if (slotImage == null) return;

        if (isReady) { slotImage.sprite = imgPret; return; }

        if (isProcessing)
        {
            // Si la dernière étape (celle qui a lancé la cuisson) a sa propre image,
            // on la garde affichée pendant le minuteur plutôt que de forcer imgEnCours.
            // C'est ce qui permet d'avoir une image dédiée pour CHAQUE étape (5 sur 5),
            // et pas juste 4 images génériques.
            if (recipeSteps != null && recipeSteps.Length > 0)
            {
                RecipeStep finalStep = recipeSteps[recipeSteps.Length - 1];
                if (finalStep.visualAfterStep != null) { slotImage.sprite = finalStep.visualAfterStep; return; }
            }
            slotImage.sprite = imgEnCours;
            return;
        }

        if (currentStepIndex <= 0 || recipeSteps == null || recipeSteps.Length == 0)
        {
            slotImage.sprite = imgVide;
            return;
        }

        RecipeStep lastStep = recipeSteps[currentStepIndex - 1];
        slotImage.sprite = lastStep.visualAfterStep != null ? lastStep.visualAfterStep : imgEnPreparation;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isReady) return;
        if (InventoryManager.Instance == null || itemProduitFini == null) return;

        int amountAdded = InventoryManager.Instance.AddItem(itemProduitFini, quantiteParProduction, true);
        if (amountAdded > 0)
        {
            if (manager != null) manager.ShowFeedback($"Récupéré : +{amountAdded} {itemProduitFini.itemName} !", Color.green);

            currentStepIndex = 0;
            isReady = false;
            UpdateVisuel();

            InventoryUI invUI = FindObjectOfType<InventoryUI>();
            if (invUI != null) invUI.RefreshUI();
        }
        else
        {
            if (manager != null) manager.ShowFeedback("Sac à dos plein ! Impossible de récupérer.", Color.red);
        }
    }
}