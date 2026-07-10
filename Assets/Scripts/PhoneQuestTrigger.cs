using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class PhoneQuestTrigger : MonoBehaviour
{
    [Header("Configuration du Déclencheur")]
    public bool triggerOnWalk = true;
    private bool hasBeenTriggered = false;

    [Header("📞 L'Appel Téléphonique")]
    public string callerName = "Le Boss";
    public Dialogue callDialogue;

    [Header("🎯 La Quête à donner")]
    public QuestManager.QuestObjectiveType questType;
    public int questGoal = 1;
    [TextArea(2, 4)] public string questDescription = "Va me régler ce problème.";
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

    private void Start()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnWalk && !hasBeenTriggered && other.CompareTag("Player"))
        {
            TriggerPhoneCall();
        }
    }

    public void TriggerPhoneCall()
    {
        if (hasBeenTriggered) return;
        hasBeenTriggered = true;

        if (callDialogue.onDialogueEnd == null)
        {
            callDialogue.onDialogueEnd = new UnityEvent();
        }
        callDialogue.onDialogueEnd.AddListener(GiveTheQuest);

        if (CallApp.Instance != null)
        {
            CallApp.Instance.ReceiveCall(callerName, callDialogue);
        }
    }

    private void GiveTheQuest()
    {
        if (QuestManager.Instance != null)
        {
            // On envoie TOUTES les infos, argent ET réputation !
            QuestManager.Instance.StartDynamicQuest(questType, questGoal, questDescription, targetName, rewardAmount, isDirtyMoneyReward, reputationReward, districtName);
        }
    }
}