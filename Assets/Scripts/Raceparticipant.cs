using UnityEngine;

// Suit la progression d'un participant de course (tours complétés). Un tour ne compte que
// si le participant est repassé près du point de départ/arrivée APRÈS avoir réellement
// parcouru la majorité du circuit — pas seulement "s'être éloigné puis rapproché". Sans
// cette vérification, reculer puis avancer plusieurs fois près de la ligne d'arrivée
// suffisait à valider des tours sans jamais parcourir le circuit.
public class RaceParticipant : MonoBehaviour
{
    [HideInInspector] public int lapsCompleted = 0;
    [HideInInspector] public bool hasFinished = false;
    [HideInInspector] public string participantName = "Concurrent";

    private Transform startFinishPoint;
    private int lapsRequired;
    private bool hasLeftStartZone = false;
    private const float TRIGGER_RADIUS = 8f;

    private RaceCircuit raceCircuit;
    private int currentWaypointIndex = 1; // le prochain point du circuit qu'on attend
    private const float WAYPOINT_RADIUS = 6f;
    public int CurrentWaypointIndex => currentWaypointIndex;

    private StreetRaceManager raceManager;

    public void Initialize(Transform startFinish, int laps, StreetRaceManager manager, string name)
    {
        startFinishPoint = startFinish;
        lapsRequired = laps;
        raceManager = manager;
        participantName = name;
        lapsCompleted = 0;
        hasFinished = false;
        hasLeftStartZone = false; // doit s'éloigner du départ avant que le 1er passage compte
        raceCircuit = manager != null ? manager.raceCircuit : null;
        currentWaypointIndex = 1;
    }

    private void Update()
    {
        if (hasFinished || startFinishPoint == null) return;

        // Progression réelle le long du circuit : on avance vers le point suivant dans
        // l'ordre, au fur et à mesure qu'on s'en approche — exactement le même principe que
        // les IA (CarAI.AdvanceRaceWaypoint), appliqué ici au joueur aussi.
        if (raceCircuit != null && raceCircuit.Count > 0)
        {
            Vector3 nextPoint = raceCircuit.GetPoint(currentWaypointIndex);
            if (Vector3.Distance(transform.position, nextPoint) < WAYPOINT_RADIUS)
            {
                currentWaypointIndex++;
            }
        }

        float dist = Vector3.Distance(transform.position, startFinishPoint.position);

        if (!hasLeftStartZone)
        {
            if (dist > TRIGGER_RADIUS * 2f) hasLeftStartZone = true;
            return;
        }

        if (dist < TRIGGER_RADIUS)
        {
            // Un tour ne compte que si la majorité des points du circuit ont été validés
            // dans l'ordre — un simple recul/avance près de la ligne ne suffit plus.
            if (raceCircuit != null && raceCircuit.Count > 0)
            {
                int minWaypointsRequired = Mathf.CeilToInt(raceCircuit.Count * 0.75f);
                if (currentWaypointIndex < minWaypointsRequired) return;
            }

            lapsCompleted++;
            hasLeftStartZone = false; // il faut ressortir avant que le tour suivant compte
            currentWaypointIndex = 1; // repart à zéro pour le tour suivant (GetPoint boucle déjà tout seul)

            if (lapsCompleted >= lapsRequired)
            {
                hasFinished = true;
                if (raceManager != null) raceManager.NotifyParticipantFinished(this);
            }
        }
    }
}