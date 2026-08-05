using UnityEngine;
using System.Collections;

public class Interactable : MonoBehaviour
{
    // --- NOUVEAUTÉ ICI : Ajout de WeedLab à la fin ---
    // HeroinLab et CocaineLab ajoutés à la fin aussi, pour la même raison : insérer au
    // milieu décalerait les valeurs numériques de tout ce qui vient après (SellDrugs...)
    // et casserait le "type" déjà configuré sur tous tes Interactable existants.
    public enum ActionType { HackATM, Pickpocket, Laundromat, Safehouse, BlackMarket, ShopLegal, ShopIllegal, StashBox, WeedLab, SellDrugs, HeroinLab, CocaineLab }
    public ActionType type;

    [Header("Configuration des actions")]
    public int cashReward = 250;
    public int bribeCost = 100;

    [Header("Mini-Jeu QTE (Effraction - Paramétrable)")]
    public float qteTimeToReact = 1.5f;
    public int qteStepsRequired = 3;
    private KeyCode[] possibleQTEKeys = { KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.M };

    [Header("Spécifique Pickpocket / PNJ")]
    public ItemData[] possibleLoot;

    [Header("Outils & Effraction")]
    public bool requiresTool = false;
    public string requiredToolName = "";

    [Header("Magasin (Boutique)")]
    public ItemData[] itemsForSale;
    public string shopName = "Boutique";

    [Header("Vente de Drogue au PNJ 💊")]
    public ItemData desiredDrug;                  // Assigné à l'instanciation par DrugDealZone (dépend du district)
    public float saleDuration = 4f;                // Durée de la barre de progression
    [Range(0, 100)] public int saleFailChancePercent = 15;
    public int saleReward = 150;                   // Prix unitaire de secours SI desiredDrug.valueInBlackMarket == 0
    private bool isSelling = false;
    private bool hasBeenResolved = false;          // Empêche de revendre au même PNJ une fois la vente conclue (succès, refus, ou stock épuisé)
    private bool hasBeenServed = false;            // Une fois vrai, ce PNJ précis ne peut plus être resollicité (évite de vendre 2x pendant qu'il s'en va)

    public virtual void Interact()
    {
        if (requiresTool)
        {
            if (string.IsNullOrEmpty(requiredToolName)) return;

            bool playerHasTool = false;
            string targetTool = requiredToolName.Trim().ToLower();

            if (HotbarManager.Instance != null)
            {
                foreach (HotbarSlot slot in HotbarManager.Instance.hotbarSlots)
                {
                    if (slot.itemInSlot != null && slot.itemInSlot.itemName.Trim().ToLower() == targetTool)
                    {
                        playerHasTool = true;
                        break;
                    }
                }
            }

            if (!playerHasTool && InventoryManager.Instance != null)
            {
                foreach (InventorySlot slot in InventoryManager.Instance.slots)
                {
                    if (slot.item != null && slot.item.itemName.Trim().ToLower() == targetTool)
                    {
                        playerHasTool = true;
                        break;
                    }
                }
            }

            if (!playerHasTool)
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Outil requis : {requiredToolName}");
                return;
            }
        }

        switch (type)
        {
            case ActionType.HackATM:
            case ActionType.Pickpocket:
                StartCoroutine(QTERoutine());
                break;
            case ActionType.Laundromat:
                if (GameManager.Instance != null && GameManager.Instance.dirtyMoney > 0)
                {
                    if (LaundromatManager.Instance != null) LaundromatManager.Instance.OpenLaundromat();
                }
                else if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Vous n'avez pas d'argent sale à blanchir.");
                break;
            case ActionType.Safehouse:
            case ActionType.StashBox:
                if (SafehouseManager.Instance != null) SafehouseManager.Instance.OpenSafehouse();
                break;
            case ActionType.BlackMarket:
                if (ShopManager.Instance != null) ShopManager.Instance.OpenShop(itemsForSale, true, "Marché Noir");
                break;
            case ActionType.ShopLegal:
                if (ShopManager.Instance != null) ShopManager.Instance.OpenShop(itemsForSale, false, shopName);
                break;
            case ActionType.ShopIllegal:
                if (ShopManager.Instance != null) ShopManager.Instance.OpenSellShop("Receleur");
                break;
            // --- NOUVEAUTÉ ICI : Le déclencheur 3D de la plantation ---
            case ActionType.WeedLab:
                if (WeedLabManager.Instance != null) WeedLabManager.Instance.OpenLab();
                break;
            case ActionType.SellDrugs:
                TargetHealth th = GetComponent<TargetHealth>();
                if (th != null && th.isDead) break; // PNJ déjà mort, plus rien à vendre
                if (!isSelling && !hasBeenResolved) StartCoroutine(SellDrugRoutine());
                break;
            // --- AJOUT : Déclencheurs 3D des labos Héroïne / Cocaïne (même principe que WeedLab) ---
            case ActionType.HeroinLab:
                if (HeroinLabManager.Instance != null) HeroinLabManager.Instance.OpenLab();
                break;
            case ActionType.CocaineLab:
                if (CocaineLabManager.Instance != null) CocaineLabManager.Instance.OpenLab();
                break;
        }
    }

    private IEnumerator QTERoutine()
    {
        string actionName = (type == ActionType.HackATM) ? "Piratage ATM" : "Pickpocket";
        bool qteFailed = false;
        bool caughtInTheAct = false;

        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) pc.isDoingQTE = true;

        yield return new WaitForSeconds(0.2f);

        for (int step = 0; step < qteStepsRequired; step++)
        {
            KeyCode currentKey = possibleQTEKeys[Random.Range(0, possibleQTEKeys.Length)];
            if (UIManager.Instance != null) UIManager.Instance.ShowQTE(currentKey.ToString(), actionName);

            float timer = qteTimeToReact;
            bool stepSuccess = false;

            while (timer > 0)
            {
                timer -= Time.deltaTime;
                if (UIManager.Instance != null && UIManager.Instance.qteSlider != null) UIManager.Instance.qteSlider.value = timer / qteTimeToReact;
                if (GameManager.Instance != null && GameManager.Instance.isBeingSeen) caughtInTheAct = true;

                if (Input.anyKeyDown)
                {
                    if (Input.GetKeyDown(currentKey)) { stepSuccess = true; break; }
                    else if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2)) { stepSuccess = false; break; }
                }
                yield return null;
            }

            if (!stepSuccess) { qteFailed = true; break; }
            yield return new WaitForSeconds(0.1f);
        }

        if (UIManager.Instance != null) UIManager.Instance.HideQTE();
        if (pc != null) pc.isDoingQTE = false;

        if (!qteFailed)
        {
            ItemData itemToSteal = null;
            if (type == ActionType.Pickpocket && possibleLoot.Length > 0) itemToSteal = possibleLoot[Random.Range(0, possibleLoot.Length)];
            ExecuteTheftSuccess(actionName, itemToSteal);
        }
        else
        {
            NPCBrain civil = GetComponent<NPCBrain>();
            if (civil != null)
            {
                if (GameManager.Instance != null) GameManager.Instance.ReportCrime(10);
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("ÉCHEC ! Le civil donne l'alerte !");
                civil.ForcePanic();
            }
            else
            {
                if (GameManager.Instance != null) GameManager.Instance.ReportCrime(caughtInTheAct ? 30 : 15);
                string failMsg = caughtInTheAct ? "VU EN FLAGRANT DÉLIT !" : $"ÉCHEC ({actionName}) !";
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"{failMsg} L'alarme sonne !");
            }
        }
    }

    private IEnumerator SellDrugRoutine()
    {
        if (desiredDrug == null)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Ce client n'attend rien pour l'instant.");
            yield break;
        }

        // Le client peut être un DrugClientNPC (comportement d'attente/départ), mais Interactable
        // reste utilisable seul si jamais tu veux un point de vente fixe sans PNJ qui se déplace.
        DrugClientNPC client = GetComponent<DrugClientNPC>();

        int ownedAmount = (InventoryManager.Instance != null) ? InventoryManager.Instance.GetTotalItemAmount(desiredDrug) : 0;
        if (ownedAmount <= 0)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Vous n'avez pas de {desiredDrug.itemName} sur vous.");
            yield break;
        }

        // Le client négocie une quantité aléatoire (1 à 8), jamais plus que ce que tu as réellement sur toi.
        int requestedQty = Mathf.Min(Random.Range(1, 9), ownedAmount);

        isSelling = true;
        if (client != null) client.OnSaleStarted();

        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) pc.isDoingQTE = true; // Réutilise le même verrou de mouvement/tir que les autres mini-actions

        if (UIManager.Instance != null) UIManager.Instance.ShowActionProgress($"Vente de {requestedQty}x {desiredDrug.itemName}...");

        float elapsed = 0f;
        bool cancelled = false;
        while (elapsed < saleDuration)
        {
            // Annulation si le joueur s'éloigne (le trigger le déclenche via OnTriggerExit du PlayerController,
            // mais on vérifie aussi ici la distance en sécurité si le collider est grand)
            if (pc == null || Vector3.Distance(pc.transform.position, transform.position) > 6f)
            {
                cancelled = true;
                break;
            }
            elapsed += Time.deltaTime;
            if (UIManager.Instance != null) UIManager.Instance.UpdateActionProgress(elapsed / saleDuration);
            yield return null;
        }

        if (UIManager.Instance != null) UIManager.Instance.HideActionProgress();
        if (pc != null) pc.isDoingQTE = false;
        isSelling = false;

        if (cancelled)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Vente annulée.");
            if (client != null) client.OnSaleStarted(); // relance l'attente normale (annule l'état "en cours")
            yield break;
        }

        // --- CORRECTION DU BUG SIGNALÉ : on revérifie le stock RÉEL ici, juste avant de conclure.
        // Sans cette seconde vérification, négocier avec 2 clients en même temps (le temps que les 2
        // barres se remplissent en parallèle) permettait aux deux ventes de "réussir" alors qu'un seul
        // pochon existait réellement — chacune se basait sur le contrôle initial, jamais remis à jour.
        int actuallyOwned = (InventoryManager.Instance != null) ? InventoryManager.Instance.GetTotalItemAmount(desiredDrug) : 0;
        int qty = Mathf.Min(requestedQty, actuallyOwned);

        if (qty <= 0)
        {
            hasBeenResolved = true;
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Un autre client vous a pris vos dernières doses entre-temps !");
            if (client != null) client.OnSaleResolved(false);
            yield break;
        }

        bool clientRefuses = Random.Range(0, 100) < saleFailChancePercent;
        hasBeenResolved = true;

        if (clientRefuses)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Le client se méfie et appelle les flics !");
            if (GameManager.Instance != null) GameManager.Instance.ReportCrime(15);
            if (client != null) client.OnSaleResolved(false);
        }
        else
        {
            InventoryManager.Instance.RemoveItem(desiredDrug, qty);

            // Prix unitaire basé sur la valeur "marché noir" déjà définie sur l'objet (ItemData.valueInBlackMarket).
            // saleReward sert de secours si cette valeur n'a pas été configurée sur l'objet (= 0).
            int unitPrice = (desiredDrug.valueInBlackMarket > 0) ? desiredDrug.valueInBlackMarket : saleReward;
            int totalReward = unitPrice * qty;
            bool paid = (GameManager.Instance != null) && GameManager.Instance.AddDirtyMoney(totalReward);

            if (!paid && UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification("Vente faite, mais impossible d'encaisser (inventaire plein) !");
            }
            else if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification($"Vente réussie : {qty}x {desiredDrug.itemName} (+{totalReward}$ sale)");
            }

            if (client != null) client.OnSaleResolved(true);
        }
    }

    private void ExecuteTheftSuccess(string actionName, ItemData itemToSteal)
    {
        if (TerritoryManager.Instance != null && TerritoryManager.Instance.currentDistrictName != "Inconnu")
        {
            TerritoryManager.Instance.IncreasePlayerControl(TerritoryManager.Instance.currentDistrictName, 2);
        }

        if (itemToSteal != null)
        {
            int added = InventoryManager.Instance.AddItem(itemToSteal);
            if (added <= 0) return;
        }
        else
        {
            if (GameManager.Instance != null)
            {
                bool success = GameManager.Instance.AddDirtyMoney(cashReward);
                if (!success) return;
            }
        }

        if (UIManager.Instance != null)
        {
            string lootName = (itemToSteal != null) ? itemToSteal.itemName : $"{cashReward}$";
            UIManager.Instance.ShowNotification($"SUCCÈS ({actionName}) : +{lootName} !");
            UIManager.Instance.UpdateHUD();
        }

        if (QuestManager.Instance != null)
        {
            if (type == ActionType.HackATM) QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.BraquerATM, 1);
            else if (type == ActionType.Pickpocket) QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.Pickpocket, cashReward > 0 ? cashReward : 1);
        }
    }
}