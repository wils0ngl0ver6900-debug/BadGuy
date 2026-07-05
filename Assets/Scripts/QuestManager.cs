using UnityEngine;
using TMPro;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public enum QuestObjectiveType
    {
        None, Pickpocket, BraquerATM, BlanchirArgent, VolerVoiture, LivrerVoiture,
        SemerFlics, AttirerFlics, ControlerTerritoire, Saboter, DetruireVoiture, RamenerObjet, TuerEnnemi, ArgentSale
    }

    [Header("UI Quête")]
    public GameObject questPanel;
    public TextMeshProUGUI questObjectiveText;

    [HideInInspector] public bool hasActiveQuest = false;
    [HideInInspector] public QuestObjectiveType currentQuestType;
    [HideInInspector] public string targetObjectName = "";

    // CORRECTION : On passe le currentProgress en public pour le DeliveryZone !
    [HideInInspector] public int currentProgress = 0;

    private int targetGoal = 0;
    private string description = "";

    private void Awake() { if (Instance == null) Instance = this; }

    public void StartDynamicQuest(QuestObjectiveType type, int goal, string desc, string targetName = "")
    {
        currentQuestType = type;
        targetGoal = goal;
        currentProgress = 0;
        description = desc;
        targetObjectName = targetName.ToLower().Trim();
        hasActiveQuest = true;

        if (questPanel != null) questPanel.SetActive(true);
        UpdateUI();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>NOUVELLE QUÊTE !</color>");
    }

    public void RegisterAction(QuestObjectiveType actionType, int amount = 1, string objName = "")
    {
        if (!hasActiveQuest || actionType != currentQuestType) return;

        if (!string.IsNullOrEmpty(targetObjectName))
        {
            if (string.IsNullOrEmpty(objName) || objName.ToLower().Trim() != targetObjectName)
            {
                return;
            }
        }

        currentProgress += amount;
        UpdateUI();

        if (currentProgress >= targetGoal) CompleteQuest();
    }

    private void UpdateUI()
    {
        if (questObjectiveText != null)
            questObjectiveText.text = $"Objectif : \n{description}\n({currentProgress} / {targetGoal})";
    }

    private void CompleteQuest()
    {
        hasActiveQuest = false;
        targetObjectName = "";
        if (questPanel != null) questPanel.SetActive(false);
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=#00FF00>MISSION ACCOMPLIE !</color>");

        if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(1500);
    }
}