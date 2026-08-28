using UnityEngine;
using System.Collections.Generic;

// Système générique de déblocage d'apps téléphone — pensé pour être appelé depuis
// n'importe quelle UnityEvent (fin de mission/quête, GenericTriggerZone, QuestManager...),
// exactement comme CallApp.UnlockBlackKnightContact() pour le contact Black Knight, mais
// généralisé à N'IMPORTE QUELLE app plutôt que d'écrire un système sur mesure à chaque fois.
//
// Mise en place :
// 1. Pose ce script sur un objet (ex: _Managers).
// 2. Dans "Apps", ajoute une entrée par app à verrouiller : un identifiant de ton choix
//    (ex: "BankApp") + l'icône (le bouton) de cette app sur l'écran d'accueil du téléphone.
// 3. Pour débloquer une app depuis une quête/trigger : UnityEvent → cet objet →
//    AppUnlockManager → UnlockApp (string) → tape l'identifiant exact (ex: "BankApp").
[System.Serializable]
public class LockableApp
{
    [Tooltip("Identifiant unique de cette app — c'est CE texte qu'il faut donner à UnlockApp() pour la débloquer.")]
    public string appId;
    [Tooltip("L'icône (bouton) de cette app sur l'écran d'accueil du téléphone.")]
    public GameObject appIcon;
    [Tooltip("Coché (par défaut) : l'icône est cachée au lancement, tant que UnlockApp() n'a pas été appelée avec cet identifiant. Décoche pour une app toujours visible dès le début (elle reste dans la liste, juste jamais verrouillée).")]
    public bool lockedByDefault = true;
}

public class AppUnlockManager : MonoBehaviour
{
    public static AppUnlockManager Instance;

    [Tooltip("Une entrée par app déblocable. L'ordre n'a pas d'importance.")]
    public List<LockableApp> apps = new List<LockableApp>();

    private HashSet<string> unlockedApps = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        foreach (LockableApp app in apps)
        {
            if (app.appIcon == null) continue;
            app.appIcon.SetActive(!app.lockedByDefault);
        }
    }

    // À relier depuis une UnityEvent : fin de quête, GenericTriggerZone.onPlayerEnter,
    // ou n'importe quel autre script qui a un point de déclenchement. L'identifiant doit
    // correspondre exactement (casse comprise) à un "App Id" de la liste "Apps" ci-dessus.
    public void UnlockApp(string appId)
    {
        if (string.IsNullOrEmpty(appId) || unlockedApps.Contains(appId)) return;
        unlockedApps.Add(appId);

        LockableApp app = apps.Find(a => a.appId == appId);
        if (app == null)
        {
            Debug.LogWarning($"[AppUnlockManager] UnlockApp(\"{appId}\") appelé mais aucune app avec cet identifiant dans la liste \"Apps\".");
            return;
        }

        if (app.appIcon != null) app.appIcon.SetActive(true);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"<color=cyan>Nouvelle application débloquée : {appId}</color>");
    }

    // Utilisable par n'importe quel autre script pour vérifier si une app est débloquée
    // avant d'autoriser une action (sécurité en plus du simple masquage de l'icône).
    public bool IsAppUnlocked(string appId)
    {
        LockableApp app = apps.Find(a => a.appId == appId);
        if (app == null) return true; // app pas dans la liste = pas de restriction connue
        return !app.lockedByDefault || unlockedApps.Contains(appId);
    }
}