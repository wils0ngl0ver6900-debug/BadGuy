using UnityEngine;

public class QuestGiver : MonoBehaviour
{
    [Header("Paramètres de la Quête")]
    public QuestManager.QuestObjectiveType questType;
    public int questGoal;
    public string questDescription;

    [Header("Cible Spécifique (Optionnel)")]
    public string targetName; // EX: Tape "Compactico" ici !

    public void GiveQuest()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StartDynamicQuest(questType, questGoal, questDescription, targetName);
        }
    }
}