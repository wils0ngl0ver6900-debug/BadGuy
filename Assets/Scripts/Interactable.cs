using UnityEngine;
using System.Collections;

public class Interactable : MonoBehaviour
{
    public enum ActionType { HackATM, Pickpocket, Laundromat, Safehouse, BlackMarket, ShopLegal, ShopIllegal, StashBox }
    public ActionType type;

    [Header("Configuration des actions")]
    public int cashReward = 250;
    public int bribeCost = 100;

    [Header("Mini-Jeu QTE (Effraction - Paramétrable) ⏱️")]
    public float qteTimeToReact = 1.5f; // Temps max par touche
    public int qteStepsRequired = 3; // Nombre de réussites à la suite
    private KeyCode[] possibleQTEKeys = { KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.M };

    [Header("Spécifique Pickpocket / PNJ")]
    public ItemData[] possibleLoot;

    [Header("Outils & Effraction 🛠️")]
    public bool requiresTool = false;
    public string requiredToolName = "";

    [Header("Magasin (Boutique) 🛒")]
    public ItemData[] itemsForSale;
    public string shopName = "Boutique";

    public virtual void Interact()
    {
        // 1. VÉRIFICATION DE L'OUTIL (Fouille de l'inventaire + Hotbar)
        if (requiresTool)
        {
            // Sécurité : Vérifie si tu n'as pas oublié d'écrire le nom de l'outil dans l'Inspector d'Unity
            if (string.IsNullOrEmpty(requiredToolName))
            {
                Debug.LogWarning($"⚠️ [Interactable] L'objet {gameObject.name} demande un outil, mais le champ 'Required Tool Name' est vide dans l'Inspector !");
                return;
            }

            bool playerHasTool = false;
            string targetTool = requiredToolName.Trim().ToLower(); // On ignore les majuscules et espaces en trop

            // A. On vérifie d'abord dans la ceinture (Hotbar)
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

            // B. Si pas trouvé dans la ceinture, on cherche dans le sac à dos (Inventory)
            if (!playerHasTool && InventoryManager.Instance != null)
            {
                foreach (ItemData item in InventoryManager.Instance.items)
                {
                    if (item != null && item.itemName.Trim().ToLower() == targetTool)
                    {
                        playerHasTool = true;
                        break;
                    }
                }
            }

            // C. Si la carte est introuvable NULLE PART, on bloque l'action
            if (!playerHasTool)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification($"Outil requis : {requiredToolName}");
                return; // On arrête l'interaction ici !
            }
        }

        // 2. LOGIQUE SELON LE TYPE D'ACTION
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
                else if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowNotification("Vous n'avez pas d'argent sale à blanchir.");
                }
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
                // APPEL CORRIGÉ : On lance la fonction pour le RECELEUR (Vente)
                if (ShopManager.Instance != null)
                    ShopManager.Instance.OpenSellShop("Receleur");
                break;
        }
    }

    // --- LE MINI-JEU DE QTE ---
    private IEnumerator QTERoutine()
    {
        string actionName = (type == ActionType.HackATM) ? "Piratage ATM" : "Pickpocket";
        bool qteFailed = false;
        bool caughtInTheAct = false;

        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) pc.isDoingQTE = true; // Bloque les mouvements à pied

        // FIX "GHOST INPUT" : On attend 0.2 seconde le temps de lâcher la touche d'interaction
        yield return new WaitForSeconds(0.2f);

        for (int step = 0; step < qteStepsRequired; step++)
        {
            KeyCode currentKey = possibleQTEKeys[Random.Range(0, possibleQTEKeys.Length)];

            if (UIManager.Instance != null)
                UIManager.Instance.ShowQTE(currentKey.ToString(), actionName);

            float timer = qteTimeToReact;
            bool stepSuccess = false;

            while (timer > 0)
            {
                timer -= Time.deltaTime;

                // Mise à jour de la jauge UI
                if (UIManager.Instance != null && UIManager.Instance.qteSlider != null)
                {
                    UIManager.Instance.qteSlider.value = timer / qteTimeToReact;
                }

                // Vérification en temps réel des témoins (Infiltration)
                if (GameManager.Instance != null && GameManager.Instance.spottersCount > 0)
                {
                    caughtInTheAct = true;
                }

                if (Input.anyKeyDown)
                {
                    if (Input.GetKeyDown(currentKey))
                    {
                        stepSuccess = true;
                        break;
                    }
                    // Évite de faire rater le QTE si le joueur clique avec la souris
                    else if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                    {
                        stepSuccess = false;
                        break;
                    }
                }
                yield return null;
            }

            if (!stepSuccess)
            {
                qteFailed = true;
                break;
            }

            // Petit délai de confort visuel entre deux touches
            yield return new WaitForSeconds(0.1f);
        }

        // Fin du mini-jeu : Nettoyage de l'UI et libération du joueur
        if (UIManager.Instance != null) UIManager.Instance.HideQTE();
        if (pc != null) pc.isDoingQTE = false;

        // TRAITEMENT DU RÉSULTAT
        if (!qteFailed)
        {
            ItemData itemToSteal = null;
            if (type == ActionType.Pickpocket && possibleLoot.Length > 0)
            {
                itemToSteal = possibleLoot[Random.Range(0, possibleLoot.Length)];
            }
            // Si on gagne, il ne s'enfuit pas et on a la récompense :
            ExecuteTheftSuccess(actionName, itemToSteal);
        }
        else
        {
            // Si on perd, la panique se lance selon la cible :
            WanderingNPC civil = GetComponent<WanderingNPC>();

            if (civil != null)
            {
                // FIX NOTORIÉTÉ : C'est un PNJ (Pickpocket), donc +10 points de recherche
                if (GameManager.Instance != null) GameManager.Instance.IncreaseNotoriety(10);
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("ÉCHEC ! Le civil donne l'alerte !");

                civil.TriggerPanic(true); // Le PNJ s'enfuit en hurlant
            }
            else
            {
                // C'est un objet (Piratage ATM), la police est prévenue automatiquement
                if (GameManager.Instance != null) GameManager.Instance.IncreaseNotoriety(caughtInTheAct ? 30 : 15);
                string failMsg = caughtInTheAct ? "VU EN FLAGRANT DÉLIT !" : $"ÉCHEC ({actionName}) !";
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"{failMsg} L'alarme sonne !");
            }
        }
    }

    private void ExecuteTheftSuccess(string actionName, ItemData itemToSteal)
    {
        // SYSTÈME DE TERRITOIRE : Augmente le contrôle du quartier actuel (+2%)
        if (TerritoryManager.Instance != null && TerritoryManager.Instance.currentDistrictName != "Inconnu")
        {
            TerritoryManager.Instance.IncreasePlayerControl(TerritoryManager.Instance.currentDistrictName, 2);
        }

        if (itemToSteal != null)
        {
            bool hasSpace = InventoryManager.Instance.AddItem(itemToSteal);
            if (!hasSpace) return;
        }
        else
        {
            if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(cashReward);
        }

        if (UIManager.Instance != null)
        {
            string lootName = (itemToSteal != null) ? itemToSteal.itemName : $"{cashReward}$";
            UIManager.Instance.ShowNotification($"SUCCÈS ({actionName}) : +{lootName} !");
            UIManager.Instance.UpdateHUD();
        }
    }
}