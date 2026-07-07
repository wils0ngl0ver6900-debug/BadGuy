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
    public float qteTimeToReact = 1.5f;
    public int qteStepsRequired = 3;
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
        if (requiresTool)
        {
            if (string.IsNullOrEmpty(requiredToolName))
            {
                Debug.LogWarning($"⚠️ [Interactable] L'objet {gameObject.name} demande un outil, mais le champ 'Required Tool Name' est vide dans l'Inspector !");
                return;
            }

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
                foreach (ItemData item in InventoryManager.Instance.items)
                {
                    if (item != null && item.itemName.Trim().ToLower() == targetTool)
                    {
                        playerHasTool = true;
                        break;
                    }
                }
            }

            if (!playerHasTool)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification($"Outil requis : {requiredToolName}");
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
                if (ShopManager.Instance != null)
                    ShopManager.Instance.OpenSellShop("Receleur");
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

            if (UIManager.Instance != null)
                UIManager.Instance.ShowQTE(currentKey.ToString(), actionName);

            float timer = qteTimeToReact;
            bool stepSuccess = false;

            while (timer > 0)
            {
                timer -= Time.deltaTime;

                if (UIManager.Instance != null && UIManager.Instance.qteSlider != null)
                {
                    UIManager.Instance.qteSlider.value = timer / qteTimeToReact;
                }

                if (GameManager.Instance != null && GameManager.Instance.isBeingSeen)
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

            yield return new WaitForSeconds(0.1f);
        }

        if (UIManager.Instance != null) UIManager.Instance.HideQTE();
        if (pc != null) pc.isDoingQTE = false;

        if (!qteFailed)
        {
            ItemData itemToSteal = null;
            if (type == ActionType.Pickpocket && possibleLoot.Length > 0)
            {
                itemToSteal = possibleLoot[Random.Range(0, possibleLoot.Length)];
            }
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

    private void ExecuteTheftSuccess(string actionName, ItemData itemToSteal)
    {
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
            if (GameManager.Instance != null)
            {
                // NOUVEAU : Si l'inventaire est plein, la fonction renvoie False et bloque le vol !
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
            if (type == ActionType.HackATM)
                QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.BraquerATM, 1);
            else if (type == ActionType.Pickpocket)
                QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.Pickpocket, cashReward > 0 ? cashReward : 1);
        }
    }
}