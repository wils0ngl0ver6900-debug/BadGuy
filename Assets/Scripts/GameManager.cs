using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Économie & Criminologie")]
    public int dirtyMoney = 100;
    public int cleanMoney = 0;
    public int notoriety = 0;

    [Header("Infiltration 👁️")]
    public int spottersCount = 0; // Nombre de PNJ qui voient le joueur actuellement

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    public void AddDirtyMoney(int amount)
    {
        dirtyMoney += amount;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHUD();
        }
    }
    public void IncreaseNotoriety(int amount)
    {
        int oldNotoriety = notoriety;

        notoriety = Mathf.Clamp(notoriety + amount, 0, 100);
        UIManager.Instance.UpdateHUD();

        if (oldNotoriety < 50 && notoriety >= 50)
        {
            UIManager.Instance.ShowNotification("FUYEZ ! La police a été appelée !");
        }

        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.OnNotorietyIncreased(amount);
        }

        // C'EST TOUT ! On a retiré le lien avec le TerritoryManager ici.
    }
    public bool IsPlayerSpotted()
    {
        return spottersCount > 0;
    }

    // --- BLANCHIMENT ---
    public void LaunderAllMoney()
    {
        if (dirtyMoney <= 0)
        {
            UIManager.Instance.ShowNotification("Vous n'avez pas d'argent sale à blanchir.");
            return;
        }

        int tax = Mathf.RoundToInt(dirtyMoney * 0.30f);
        int laundered = dirtyMoney - tax;

        cleanMoney += laundered;
        UIManager.Instance.ShowNotification($"Blanchiment réussi ! Legitime : +{laundered}$ (Taxe: {tax}$)");
        dirtyMoney = 0;

        UIManager.Instance.UpdateHUD();
    }

    // --- CORRUPTION POLICE ---
    public void PayOffPolice(int cost)
    {
        if (cleanMoney >= cost && notoriety > 0)
        {
            cleanMoney -= cost;
            notoriety = Mathf.Clamp(notoriety - 30, 0, 100);
            UIManager.Instance.ShowNotification("Police corrompue. L'indice de recherche baisse !");
        }
        else
        {
            UIManager.Instance.ShowNotification("Pas assez d'argent propre ou aucun indice de recherche.");
        }
        UIManager.Instance.UpdateHUD();
    }

    // --- SYSTÈME DE POLICE : ARRESTATION ET CONFISCATION ---
    public void Busted()
    {
        // 1. Confiscation de l'argent sale
        dirtyMoney = 0;

        // 2. Confiscation des objets équipés dans les mains et dans la Hotbar
        if (HotbarManager.Instance != null)
        {
            HotbarManager.Instance.RemoveIllegalItems();
        }

        // 3. Confiscation des objets illégaux restants dans le sac à dos (via une liste temporaire sécurisée)
        System.Collections.Generic.List<ItemData> itemsToConfiscate = new System.Collections.Generic.List<ItemData>();

        foreach (var item in InventoryManager.Instance.items)
        {
            if (item != null && item.isIllegal)
            {
                itemsToConfiscate.Add(item);
            }
        }

        foreach (var item in itemsToConfiscate)
        {
            InventoryManager.Instance.RemoveItem(item);
        }

        // 4. Reset de la notoriété et mise à jour de l'UI
        notoriety = 0;
        UIManager.Instance.UpdateHUD();
        UIManager.Instance.ShowNotification("ARRÊTÉ ! Argent sale et objets illégaux confisqués.");

        // 5. Nettoyage des policiers présents autour du joueur
        CopAI[] cops = FindObjectsOfType<CopAI>();
        foreach (CopAI cop in cops) Destroy(cop.gameObject);
    }
}