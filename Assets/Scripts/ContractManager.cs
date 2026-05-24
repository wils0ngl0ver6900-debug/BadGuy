using UnityEngine;

public class ContractManager : MonoBehaviour
{
    public static ContractManager Instance;

    public enum ContractType { None, Hitman, Mule, GoFast }

    [Header("État Actuel")]
    public bool hasActiveContract = false;
    public ContractType currentContract = ContractType.None;

    [Header("Récompenses d'Argent Sale")]
    public int hitmanReward = 500;
    public int muleReward = 800;
    public int goFastReward = 1200;

    [Header("Configuration Go Fast 🚗")]
    public string[] availableCarModels = { "Berline" };

    [Header("Configuration Mule 🎒")]
    [Tooltip("Glisse ici l'objet 'Sac de Drogue' créé dans Unity")]
    public ItemData muleBagItem;

    [Header("Détails Générés (Ne pas toucher)")]
    public string targetVehicleModel = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void AssignContract()
    {
        if (hasActiveContract) return;

        int rand = Random.Range(1, 4);
        currentContract = (ContractType)rand;

        string message = "";

        switch (currentContract)
        {
            case ContractType.Hitman:
                hasActiveContract = true;
                message = "<color=red>CONTRAT HITMAN :</color> Éliminez la cible marquée en ville.";
                break;

            case ContractType.Mule:
                // 1. On essaie de forcer le sac dans l'inventaire du joueur
                if (muleBagItem != null && InventoryManager.Instance != null)
                {
                    bool isAdded = InventoryManager.Instance.AddItem(muleBagItem);
                    if (!isAdded)
                    {
                        // Si l'inventaire est plein, la mission est annulée direct
                        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Inventaire plein ! Fais de la place avant de prendre un contrat Mule.</color>");
                        currentContract = ContractType.None;
                        return;
                    }

                    // On met à jour l'affichage de l'inventaire
                    InventoryUI ui = FindObjectOfType<InventoryUI>();
                    if (ui != null) ui.RefreshUI();

                    hasActiveContract = true;
                    message = "<color=yellow>CONTRAT MULE :</color> Le sac est dans ton inventaire. Va le livrer au client !";
                }
                else
                {
                    Debug.LogWarning("⚠️ [ContractManager] Il manque l'ItemData 'muleBagItem' dans l'Inspector !");
                    currentContract = ContractType.None;
                    return;
                }
                break;

            case ContractType.GoFast:
                hasActiveContract = true;
                if (availableCarModels.Length > 0)
                {
                    targetVehicleModel = availableCarModels[Random.Range(0, availableCarModels.Length)];
                }
                message = $"<color=cyan>CONTRAT GO FAST :</color> Trouvez et livrez une <b>{targetVehicleModel.ToUpper()}</b>.";
                break;
        }

        if (UIManager.Instance != null && hasActiveContract)
            UIManager.Instance.ShowNotification(message);
    }

    public void CompleteContract(ContractType typeToComplete)
    {
        if (!hasActiveContract || currentContract != typeToComplete) return;

        int reward = 0;
        string successMsg = "";

        switch (currentContract)
        {
            case ContractType.Hitman: reward = hitmanReward; successMsg = "Cible éliminée !"; break;
            case ContractType.Mule: reward = muleReward; successMsg = "Marchandise livrée !"; break;
            case ContractType.GoFast: reward = goFastReward; successMsg = "Véhicule livré !"; break;
        }

        if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(reward);

        if (TerritoryManager.Instance != null && TerritoryManager.Instance.currentDistrictName != "Inconnu")
        {
            TerritoryManager.Instance.IncreasePlayerControl(TerritoryManager.Instance.currentDistrictName, 10);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"<color=#00FF22>SUCCÈS : {successMsg} (+{reward}$)</color>");
            UIManager.Instance.UpdateHUD();
        }

        hasActiveContract = false;
        currentContract = ContractType.None;
        targetVehicleModel = "";
    }
}