using UnityEngine;

// Suit la progression d'un participant de course (tours complétés) en surveillant sa
// distance au TrafficNode de départ/arrivée. Un "tour" est compté à chaque fois que le
// participant repasse suffisamment près de ce point APRÈS s'en être éloigné — sans cette
// condition, rester juste à côté du point de départ compterait plusieurs tours d'un coup.
public class RaceParticipant : MonoBehaviour
{
    [HideInInspector] public int lapsCompleted = 0;
    [HideInInspector] public bool hasFinished = false;
    [HideInInspector] public string participantName = "Concurrent";

    private Transform startFinishPoint;
    private int lapsRequired;
    private bool hasLeftStartZone = false;
    private const float TRIGGER_RADIUS = 8f;

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
    }

    private void Update()
    {
        if (hasFinished || startFinishPoint == null) return;

        float dist = Vector3.Distance(transform.position, startFinishPoint.position);

        if (!hasLeftStartZone)
        {
            if (dist > TRIGGER_RADIUS * 2f) hasLeftStartZone = true;
            return;
        }

        if (dist < TRIGGER_RADIUS)
        {
            lapsCompleted++;
            hasLeftStartZone = false; // il faut ressortir avant que le tour suivant compte

            if (lapsCompleted >= lapsRequired)
            {
                hasFinished = true;
                if (raceManager != null) raceManager.NotifyParticipantFinished(this);
            }
        }
    }
}