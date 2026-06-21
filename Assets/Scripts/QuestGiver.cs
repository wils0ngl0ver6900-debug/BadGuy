using UnityEngine;

public class QuestGiver : MonoBehaviour
{
    [Header("Paramètres de la Quête")]
    public QuestManager.QuestType questType;
    public int questGoal; // Ex: 50 pour 50$, ou 3 pour tuer 3 Skulls
    public string questDescription; // Ex: "Récupérer de l'argent sale"

    // Cette fonction sera appelée par l'événement de fin de dialogue !
    public void GiveQuest()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StartDynamicQuest(questType, questGoal, questDescription);
        }
    }
}