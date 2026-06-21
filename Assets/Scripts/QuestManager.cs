using UnityEngine;
using TMPro;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    // La liste de toutes les missions possibles dans ton jeu !
    public enum QuestType { None, RecolterArgentSale, TuerVipers, TuerSkulls }

    [Header("UI Quête")]
    public GameObject questPanel;
    public TextMeshProUGUI questObjectiveText;

    [HideInInspector] public bool hasActiveQuest = false;
    [HideInInspector] public QuestType currentQuestType = QuestType.None;

    private int currentProgress = 0;
    private int targetGoal = 0;
    private string baseDescription = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (questPanel != null) questPanel.SetActive(false);
    }

    // Le PNJ appelle cette fonction pour lancer la quête
    public void StartDynamicQuest(QuestType type, int goal, string description)
    {
        hasActiveQuest = true;
        currentQuestType = type;
        targetGoal = goal;
        currentProgress = 0;
        baseDescription = description;

        UpdateQuestUI();

        if (questPanel != null) questPanel.SetActive(true);
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>NOUVELLE QUÊTE !</color>");
    }

    private void UpdateQuestUI()
    {
        if (questObjectiveText != null)
        {
            // Affiche "Objectif : Voler les passants (10/50)"
            questObjectiveText.text = $"Objectif : \n{baseDescription} ({currentProgress}/{targetGoal})";
        }
    }

    // --- LES DÉTECTEURS DE GAMEPLAY ---

    // 1. Appelée par ton GameManager quand tu gagnes de l'argent sale
    public void OnMoneyGained(int amount)
    {
        if (!hasActiveQuest || currentQuestType != QuestType.RecolterArgentSale) return;

        currentProgress += amount;
        if (currentProgress >= targetGoal)
        {
            currentProgress = targetGoal;
            CompleteCurrentQuest();
        }
        else UpdateQuestUI();
    }

    // 2. Appelée par le TargetHealth d'un ennemi quand il meurt
    public void OnEnemyKilled(TerritoryManager.Faction enemyFaction)
    {
        if (!hasActiveQuest) return;

        if ((currentQuestType == QuestType.TuerVipers && enemyFaction == TerritoryManager.Faction.Vipers) ||
            (currentQuestType == QuestType.TuerSkulls && enemyFaction == TerritoryManager.Faction.Skulls))
        {
            currentProgress++;
            if (currentProgress >= targetGoal)
            {
                currentProgress = targetGoal;
                CompleteCurrentQuest();
            }
            else UpdateQuestUI();
        }
    }

    private void CompleteCurrentQuest()
    {
        hasActiveQuest = false;
        currentQuestType = QuestType.None;

        if (questPanel != null) questPanel.SetActive(false);
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=#00FF00>QUÊTE ACCOMPLIE !</color>");

        // Bonus de réussite immédiat !
        if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(500);
    }
}