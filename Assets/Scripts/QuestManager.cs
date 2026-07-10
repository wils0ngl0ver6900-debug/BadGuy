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
    [HideInInspector] public int currentProgress = 0;

    private int targetGoal = 0;
    private string description = "";

    // --- Mémoire des récompenses ---
    private int currentRewardAmount = 0;
    private bool currentRewardIsDirty = true;

    // --- NOUVEAU : Mémoire de la réputation ---
    private int currentReputationReward = 0;
    private string currentDistrictReward = "";

    private void Awake() { if (Instance == null) Instance = this; }

    // --- MODIFICATION : On ajoute les paramètres de réputation ---
    public void StartDynamicQuest(QuestObjectiveType type, int goal, string desc, string targetName = "", int rewardAmount = 0, bool isDirtyReward = true, int repReward = 0, string targetDistrict = "")
    {
        currentQuestType = type;
        targetGoal = goal;
        currentProgress = 0;
        description = desc;
        targetObjectName = targetName.ToLower().Trim();
        hasActiveQuest = true;

        // On mémorise l'argent
        currentRewardAmount = rewardAmount;
        currentRewardIsDirty = isDirtyReward;

        // On mémorise la réputation
        currentReputationReward = repReward;
        currentDistrictReward = targetDistrict.Trim();

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

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=#00FF00>QUÊTE TERMINÉE !</color>");

        // --- 1. DISTRIBUTION DE L'ARGENT ---
        if (currentRewardAmount > 0 && GameManager.Instance != null)
        {
            if (currentRewardIsDirty)
            {
                GameManager.Instance.dirtyMoney += currentRewardAmount;
                GameManager.Instance.SyncDirtyMoneyItem();
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>+ {currentRewardAmount} $ (Argent Sale)</color>");
            }
            else
            {
                GameManager.Instance.cleanMoney += currentRewardAmount;
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=#00FF41>+ {currentRewardAmount} $ (Argent Propre)</color>");
            }
        }

        // --- 2. DISTRIBUTION DE LA RÉPUTATION (NOUVEAU) ---
        if (currentReputationReward > 0 && !string.IsNullOrEmpty(currentDistrictReward))
        {
            if (TerritoryManager.Instance != null)
            {
                // Appel direct au TerritoryManager pour monter le contrôle !
                TerritoryManager.Instance.IncreasePlayerControl(currentDistrictReward, currentReputationReward);

                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification($"<color=#B026FF>+ {currentReputationReward}% Respect ({currentDistrictReward})</color>");
            }
        }

        // On rafraîchit l'interface pour tout afficher correctement
        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
    }
}