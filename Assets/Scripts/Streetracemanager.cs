using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Gère une course de rue de bout en bout : spawn des 4 adversaires + la voiture du joueur
// sur une grille de départ, guidage du joueur avec le pathfinder le long du circuit,
// classement via RaceParticipant, récompense en argent sale pour le top 2, TP retour à
// la fin. Déclenchée par CallApp quand le joueur répond "Oui" à l'appel de Black Knight.
public class StreetRaceManager : MonoBehaviour
{
    public static StreetRaceManager Instance;

    [Header("Circuit 🏁")]
    [Tooltip("Premier noeud du circuit — sert aussi de ligne de départ/arrivée. Les noeuds suivants doivent former une boucle FERMÉE avec un seul 'Next Node' chacun (pas de branchement vers le trafic normal de la ville), sinon CarAI choisira un chemin au hasard à chaque intersection.")]
    public TrafficNode startFinishNode;
    public int lapsToWin = 3;

    [Header("Grille de départ")]
    [Tooltip("Position/orientation où apparaissent les 5 voitures, décalées les unes des autres le long de son axe droit (X local).")]
    public Transform gridStartPoint;
    public float gridCarSpacing = 4f;

    [Header("Véhicules de course")]
    [Tooltip("Prefab utilisé pour les 5 voitures (identique pour tout le monde, course équitable). Doit avoir CarController + CarAI + CarInteraction comme n'importe quelle voiture drivable.")]
    public GameObject raceCarPrefab;
    public string[] opponentNames = { "Vipère", "Le Fantôme", "Diesel", "Rafale" };

    [Header("Récompenses (argent sale 💵)")]
    public int firstPlaceReward = 5000;
    public int secondPlaceReward = 2000;

    private List<RaceParticipant> participants = new List<RaceParticipant>();
    private List<RaceParticipant> finishOrder = new List<RaceParticipant>();
    private List<GameObject> spawnedCars = new List<GameObject>();
    private RaceParticipant playerParticipant;
    private Vector3 preRacePlayerPosition;
    private bool raceActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool IsRaceActive() => raceActive;

    // Appelée depuis CallApp quand le joueur répond "Oui" à Black Knight.
    public void StartRace()
    {
        if (raceActive)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Une course est déjà en cours !</color>");
            return;
        }

        if (startFinishNode == null || raceCarPrefab == null || gridStartPoint == null)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Erreur : course pas configurée (voir StreetRaceManager dans l'Inspector).</color>");
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        preRacePlayerPosition = playerObj.transform.position;

        raceActive = true;
        participants.Clear();
        finishOrder.Clear();
        spawnedCars.Clear();

        // --- Voiture du joueur (position 0 sur la grille) ---
        GameObject playerCar = Instantiate(raceCarPrefab, GridPosition(0), gridStartPoint.rotation);
        spawnedCars.Add(playerCar);

        // On coupe l'IA sur CETTE instance avant qu'elle ne parte (CarAI.Start() mettrait
        // isDrivenByAI à true sinon) : le joueur doit la conduire lui-même.
        CarAI playerCarAI = playerCar.GetComponent<CarAI>();
        if (playerCarAI != null) playerCarAI.enabled = false;

        CarController playerCarController = playerCar.GetComponent<CarController>();
        if (playerCarController != null)
        {
            playerCarController.isPlayerOwned = true;
            playerCarController.isDrivenByAI = false;
        }

        RaceParticipant playerRP = playerCar.AddComponent<RaceParticipant>();
        playerRP.Initialize(startFinishNode.transform, lapsToWin, this, "Toi");
        playerParticipant = playerRP;
        participants.Add(playerRP);

        // On force le joueur à monter dedans (comme s'il venait de presser [E] dessus).
        CarInteraction playerCarInteraction = playerCar.GetComponentInChildren<CarInteraction>();
        if (playerCarInteraction != null)
        {
            playerObj.transform.position = playerCar.transform.position;
            playerCarInteraction.EnterCar();
        }

        // --- 4 adversaires IA (positions 1 à 4 sur la grille) ---
        for (int i = 0; i < 4; i++)
        {
            GameObject aiCar = Instantiate(raceCarPrefab, GridPosition(i + 1), gridStartPoint.rotation);
            spawnedCars.Add(aiCar);

            CarAI aiDriver = aiCar.GetComponent<CarAI>();
            if (aiDriver == null) aiDriver = aiCar.AddComponent<CarAI>();
            aiDriver.enabled = true;
            aiDriver.currentNode = startFinishNode;

            string oppName = i < opponentNames.Length ? opponentNames[i] : $"Adversaire {i + 1}";
            RaceParticipant aiRP = aiCar.AddComponent<RaceParticipant>();
            aiRP.Initialize(startFinishNode.transform, lapsToWin, this, oppName);
            participants.Add(aiRP);
        }

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"<color=cyan>Course lancée ! {lapsToWin} tours, en piste !</color>");

        StartCoroutine(GuidePlayerRoutine());
    }

    private Vector3 GridPosition(int index)
    {
        return gridStartPoint.position + gridStartPoint.right * (index * gridCarSpacing);
    }

    // Guide le joueur avec les flèches du pathfinder tout au long du circuit, en changeant
    // de cible à chaque fois qu'il se rapproche du noeud suivant. Purement indicatif pour
    // le joueur — les adversaires IA suivent leur propre logique CarAI indépendamment.
    private IEnumerator GuidePlayerRoutine()
    {
        TrafficNode current = startFinishNode;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        while (raceActive && playerParticipant != null && !playerParticipant.hasFinished)
        {
            if (current.nextNodes == null || current.nextNodes.Count == 0) yield break;

            TrafficNode next = current.nextNodes[0];
            if (JobPathfinder.Instance != null) JobPathfinder.Instance.SetTargets(next.transform);

            while (raceActive && playerObj != null && playerParticipant != null && !playerParticipant.hasFinished
                   && Vector3.Distance(playerObj.transform.position, next.transform.position) > 10f)
            {
                yield return new WaitForSeconds(0.5f);
            }

            current = next;
        }

        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();
    }

    // Appelée par chaque RaceParticipant (joueur ou IA) quand il termine son nombre de tours.
    public void NotifyParticipantFinished(RaceParticipant participant)
    {
        if (finishOrder.Contains(participant)) return;
        finishOrder.Add(participant);

        if (participant == playerParticipant)
        {
            EndRaceForPlayer();
        }
    }

    private void EndRaceForPlayer()
    {
        int placement = finishOrder.IndexOf(playerParticipant) + 1;

        int reward = 0;
        if (placement == 1) reward = firstPlaceReward;
        else if (placement == 2) reward = secondPlaceReward;

        if (reward > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.dirtyMoney += reward;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification($"<color=green>{placement}e place ! +{reward}€ (argent sale)</color>");
                UIManager.Instance.UpdateHUD();
            }
        }
        else
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification($"<color=yellow>{placement}e place. Pas de récompense cette fois.</color>");
        }

        StartCoroutine(CleanupRaceRoutine());
    }

    private IEnumerator CleanupRaceRoutine()
    {
        raceActive = false;

        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            CarController currentCar = null;
            foreach (GameObject car in spawnedCars)
            {
                if (car == null) continue;
                CarController cc = car.GetComponent<CarController>();
                if (cc != null && cc.isDrivenByPlayer) currentCar = cc;
            }

            if (currentCar != null)
            {
                CarInteraction ci = currentCar.GetComponentInChildren<CarInteraction>();
                if (ci != null) ci.ExitCarAt(preRacePlayerPosition);
                else playerObj.transform.position = preRacePlayerPosition;
            }
            else
            {
                playerObj.transform.position = preRacePlayerPosition;
            }
        }

        yield return new WaitForSeconds(1f);

        foreach (GameObject car in spawnedCars)
        {
            if (car != null) Destroy(car);
        }
        spawnedCars.Clear();
        participants.Clear();
        finishOrder.Clear();
        playerParticipant = null;
    }
}