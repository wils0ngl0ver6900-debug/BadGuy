using UnityEngine;

public class QuestGiver : MonoBehaviour
{
    [Header("Paramètres de la Quête")]
    public QuestManager.QuestObjectiveType questType;
    public int questGoal;
    public string questDescription;

    [Header("Cible Spécifique (Optionnel)")]
    public string targetName;

    [Header("💰 Récompense (Argent)")]
    public int rewardAmount = 500;
    [Tooltip("Coché = Argent Sale / Décoché = Argent Propre")]
    public bool isDirtyMoneyReward = true;

    // --- NOUVEAU : LA RÉPUTATION ! ---
    [Header("👑 Récompense (Réputation / Optionnel)")]
    [Tooltip("Le % de contrôle gagné. Mettre 0 si aucune réputation donnée.")]
    public int reputationReward = 0;
    [Tooltip("Le nom EXACT du quartier dans le TerritoryManager (ex: Downtown)")]
    public string districtName = "";

    public void GiveQuest()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StartDynamicQuest(questType, questGoal, questDescription, targetName, rewardAmount, isDirtyMoneyReward, reputationReward, districtName);
        }
    }
}