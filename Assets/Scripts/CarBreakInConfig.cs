using UnityEngine;

public enum BreakInMinigameType { Lockpick, Mash, QuickTime, Progress }

[System.Serializable]
public class CarBreakInMethod
{
    [Tooltip("Nom affiché sur la ligne du prompt (ex: \"Crochetage\", \"Pompe à vitre\", \"Casser la vitre\", \"Boîtier électronique\").")]
    public string methodName = "Crochetage";

    [Tooltip("Touche à presser pour lancer CETTE méthode directement (affichée dans le prompt).")]
    public KeyCode triggerKey = KeyCode.Alpha1;

    public BreakInMinigameType minigameType = BreakInMinigameType.Lockpick;

    [Header("Outil (laisse vide = aucun outil requis, toujours disponible)")]
    public ItemData requiredTool;
    [Tooltip("Coché : l'outil est détruit UNIQUEMENT si le mini-jeu est raté (jamais en cas de réussite).")]
    public bool consumeToolOnFailure = false;
    [Tooltip("Coché AU LIEU de la consommation : l'outil n'est jamais détruit, mais devient indisponible sur TOUTES les voitures pendant Cooldown Seconds après chaque usage (réussi ou raté) — pensé pour un boîtier électronique rechargeable plutôt qu'à usage unique.")]
    public bool useCooldownInstead = false;
    public float cooldownSeconds = 180f;

    [Header("Si Lockpick (façon Fallout, voir LockpickMinigame)")]
    public float lockpickTime = 12f;
    [Tooltip("Plus BAS = plus difficile (fenêtre de tolérance plus étroite).")]
    public float lockpickTolerance = 10f;

    [Header("Si Mash (marteler une touche pour remplir la barre)")]
    public KeyCode mashKey = KeyCode.E;
    public float mashDuration = 5f;
    public float mashFillPerPress = 0.09f;
    [Tooltip("Remplissage perdu par seconde si tu arrêtes de marteler — il faut un rythme soutenu, pas juste quelques pressions.")]
    public float mashDecayPerSecond = 0.18f;

    [Header("Si QuickTime (suite de touches rapide, façon casser une vitre)")]
    public int qteSteps = 2;
    public float qteTimeToReact = 0.9f;
    [Tooltip("Coché : déclenche l'alarme du véhicule à la réussite ET à l'échec (voir Header Alarme ci-dessous dans CarBreakInConfig) — pensé pour \"casser la vitre\", forcément bruyant peu importe l'issue.")]
    public bool alwaysTriggersAlarm = false;

    [Header("Si Progress (barre + codes périodiques, façon boîtier électronique)")]
    public float progressDuration = 12f;
    [Tooltip("Nombre de fois où la progression s'interrompt pour exiger un code (voir CodeEntryMinigame). 2 par défaut.")]
    public int codeInterruptions = 2;
    [Tooltip("Temps laissé pour taper chaque code avant échec total.")]
    public float codeTimeLimit = 4f;

    [Header("Risque")]
    [Tooltip("Risque (%) de déclencher l'alarme MÊME en cas de réussite (ignoré si Always Triggers Alarm est coché).")]
    [Range(0, 100)] public int alarmChancePercent = 35;
    [Tooltip("Risque (%) d'échec pur, indépendant du mini-jeu (ce modèle résiste à cet outil cette fois).")]
    [Range(0, 100)] public int failureChancePercent = 10;
}

// Configuration PARTAGÉE des méthodes d'effraction — définie UNE SEULE FOIS dans la scène
// (pose ce script sur _Managers), utilisée automatiquement par TOUTES les voitures via
// CarInteraction. Plus besoin d'ajouter un composant par voiture placée dans le monde.
public class CarBreakInConfig : MonoBehaviour
{
    public static CarBreakInConfig Instance;

    [Tooltip("Les méthodes disponibles, valables pour TOUTES les voitures éligibles du monde (voir CarInteraction pour les exemptions : voiture déjà possédée, à vendre, ou actuellement conduite par un PNJ).")]
    public CarBreakInMethod[] methods;

    [Header("Prompt flottant (instancié automatiquement au-dessus de chaque voiture éligible, à la demande)")]
    [Tooltip("Prefab d'un Canvas en World Space avec jusqu'à N lignes de texte TMP (une par méthode, dans l'ordre de \"Methods\" ci-dessus). Instancié une fois par voiture qui en a besoin, jamais configuré à la main sur les voitures elles-mêmes.")]
    public GameObject promptPrefab;
    [Tooltip("Décalage par rapport au point de sortie (Exit Point) de la voiture, là où le prompt apparaît.")]
    public Vector3 promptOffset = new Vector3(0f, 1.4f, 0f);
    [Tooltip("Rotation LOCALE fixe appliquée au prompt une fois instancié — pas de LookAt dynamique (peu fiable avec une caméra vue du dessus). Pour une caméra en plongée, (90, 0, 0) fait généralement 'regarder vers le haut' plutôt que vers l'horizon. Ajuste ces 3 valeurs en Play Mode jusqu'à ce que ce soit lisible avec TA caméra, puis laisse tel quel.")]
    public Vector3 promptRotation = new Vector3(90f, 0f, 0f);

    [Header("Alarme (déclenchée par une méthode avec risque, ex: casser la vitre)")]
    public float alarmDuration = 30f;
    [Tooltip("Rayon de détection autour de la voiture pendant l'alarme : si le joueur passe à portée d'un PNJ (civil ou policier) pendant que ça sonne, il gagne une étoile de recherche.")]
    public float alarmDetectionRadius = 15f;
    public int alarmWantedCrimePoints = 15;
    public float alarmCheckInterval = 0.5f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
}