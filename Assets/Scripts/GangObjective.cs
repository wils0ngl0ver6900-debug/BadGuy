using UnityEngine;

public class GangObjective : MonoBehaviour
{
    public enum ObjectiveType { Sabotage, Lieutenant, Boss }

    [Header("Configuration de la Cible")]
    public string districtName; // Dans quel quartier est cette cible ?
    public ObjectiveType type;

    private bool isCompleted = false;

    // Cette fonction sera appelée quand l'objet est détruit ou tué
    public void CompleteObjective()
    {
        if (isCompleted) return;
        isCompleted = true;

        if (TerritoryManager.Instance != null)
        {
            TerritoryManager.Instance.RegisterObjectiveCompletion(districtName, type);
        }
    }
}