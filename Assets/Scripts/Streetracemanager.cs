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
    [Tooltip("Le circuit de course (voir RaceCircuit.cs) — une simple liste ordonnée de points, plus aucun lien avec le graphe TrafficNode du trafic normal. Le premier point de RaceCircuit sert de ligne de départ/arrivée.")]
    public RaceCircuit raceCircuit;
    public int lapsToWin = 3;

    [Header("Grille de départ")]
    [Tooltip("Position/orientation où apparaissent les 5 voitures, en quinconce (2 par rangée) le long de son axe droit/avant.")]
    public Transform gridStartPoint;
    [Tooltip("Écart latéral entre les 2 colonnes de la grille.")]
    public float gridCarSpacing = 4f;
    [Tooltip("Écart vers l'arrière entre chaque rangée de 2 voitures.")]
    public float gridRowSpacing = 6f;

    [Header("Véhicules de course")]
    [Tooltip("Prefab utilisé pour les 5 voitures (identique pour tout le monde, course équitable). Doit avoir CarController + CarAI + CarInteraction comme n'importe quelle voiture drivable.")]
    public GameObject raceCarPrefab;
    public string[] opponentNames = { "Vipère", "Le Fantôme", "Diesel", "Rafale" };

    [Tooltip("Décalage latéral donné à chaque adversaire IA (voir CarAI.lateralOffset) pour éviter qu'ils roulent tous en file indienne parfaite. Valeurs volontairement modestes : un décalage trop large pousse les IA hors piste dans les virages serrés (constaté avec -3/-1/1/3).")]
    public float[] opponentLateralOffsets = { -1.2f, -0.4f, 0.4f, 1.2f };

    [Header("Compte à rebours 🚦")]
    [Tooltip("Texte affichant 3, 2, 1, GO ! avant le départ. Tout le monde (joueur + IA) est bloqué pendant ce temps.")]
    public TMPro.TextMeshProUGUI countdownText;
    public float secondsPerCount = 1f;

    [Header("Compteur de tours")]
    [Tooltip("Texte affichant 'Tour X/Y' pendant la course.")]
    public TMPro.TextMeshProUGUI lapCounterText;

    [Header("Récompenses (argent sale 💵)")]
    public int firstPlaceReward = 5000;
    public int secondPlaceReward = 2000;

    [Header("Collisions entre voitures de course")]
    [Tooltip("Nom d'un Layer Unity dédié (Edit > Project Settings > Tags and Layers, crée un nouveau Layer et note son nom ici) assigné automatiquement aux 5 voitures pendant la course. Si renseigné, elles deviennent traversables entre elles (mais restent solides pour tout le reste : route, décor, autre circulation) — plus de carambolage en chaîne. Laisse vide pour garder les collisions normales entre elles.")]
    public string raceCarLayerName = "";

    private int raceCarLayer = -1;

    private List<RaceParticipant> participants = new List<RaceParticipant>();
    private List<RaceParticipant> finishOrder = new List<RaceParticipant>();
    private List<GameObject> spawnedCars = new List<GameObject>();
    private List<CarAI> aiDrivers = new List<CarAI>();
    private RaceParticipant playerParticipant;
    private CarController playerCarControllerRef;
    private Vector3 preRacePlayerPosition;
    private bool raceActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (lapCounterText != null) lapCounterText.gameObject.SetActive(false);
    }

    public bool IsRaceActive() => raceActive;

    private void Update()
    {
        if (raceActive && lapCounterText != null && playerParticipant != null)
        {
            int lapShown = Mathf.Min(playerParticipant.lapsCompleted + 1, lapsToWin);
            lapCounterText.text = $"Tour {lapShown}/{lapsToWin}";
        }
    }

    // Appelée depuis CallApp quand le joueur répond "Oui" à Black Knight.
    public void StartRace()
    {
        if (raceActive)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Une course est déjà en cours !</color>");
            return;
        }

        if (raceCircuit == null || raceCircuit.Count == 0 || raceCarPrefab == null || gridStartPoint == null)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Erreur : course pas configurée (voir StreetRaceManager dans l'Inspector).</color>");
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        StartCoroutine(StartRaceRoutine(playerObj));
    }

    private IEnumerator StartRaceRoutine(GameObject playerObj)
    {
        preRacePlayerPosition = playerObj.transform.position;

        raceActive = true;
        participants.Clear();
        finishOrder.Clear();
        spawnedCars.Clear();
        aiDrivers.Clear();

        // Comme pour les labos/le garage : pas d'appel qui sonne pendant la course.
        CallApp.RequestCallBlock();

        // Les 5 voitures deviennent traversables entre elles (pas avec le reste du monde)
        // si un Layer dédié est renseigné — évite les carambolages en chaîne.
        raceCarLayer = string.IsNullOrEmpty(raceCarLayerName) ? -1 : LayerMask.NameToLayer(raceCarLayerName);
        if (raceCarLayer >= 0)
        {
            Physics.IgnoreLayerCollision(raceCarLayer, raceCarLayer, true);
        }

        // --- Voiture du joueur (position 0 sur la grille) ---
        GameObject playerCar = Instantiate(raceCarPrefab, GridPosition(0), gridStartPoint.rotation);
        spawnedCars.Add(playerCar);
        if (raceCarLayer >= 0) SetLayerRecursively(playerCar, raceCarLayer);

        // On coupe l'IA sur CETTE instance avant qu'elle ne parte (CarAI.Start() mettrait
        // isDrivenByAI à true sinon) : le joueur doit la conduire lui-même.
        CarAI playerCarAI = playerCar.GetComponent<CarAI>();
        if (playerCarAI != null) playerCarAI.enabled = false;

        playerCarControllerRef = playerCar.GetComponent<CarController>();
        if (playerCarControllerRef != null)
        {
            playerCarControllerRef.isPlayerOwned = true;
            playerCarControllerRef.isDrivenByAI = false;
            // Bloqué dès maintenant : reste vrai pendant tout le compte à rebours, libéré
            // juste avant "GO !" plus bas.
            playerCarControllerRef.inputLocked = true;
        }

        RaceParticipant playerRP = playerCar.AddComponent<RaceParticipant>();
        playerRP.Initialize(raceCircuit.StartFinish, lapsToWin, this, "Toi");
        playerParticipant = playerRP;
        participants.Add(playerRP);

        // On attend UNE frame : Start() d'un objet fraîchement instancié (notamment
        // CarInteraction, qui y remplit player/playerRenderers/playerColliders) ne tourne
        // jamais de façon synchrone pendant Instantiate(). Appeler EnterCar() tout de suite
        // faisait planter CarInteraction sur un NullReferenceException (foreach sur
        // playerRenderers encore null) — ce qui coupait TOUT LE RESTE de cette méthode (les
        // 4 IA n'étaient donc jamais créées) et laissait le joueur avec ses collisions
        // toujours actives à chevaucher la voiture (d'où l'"envol" par résolution physique).
        yield return null;

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
            if (raceCarLayer >= 0) SetLayerRecursively(aiCar, raceCarLayer);

            CarAI aiDriver = aiCar.GetComponent<CarAI>();
            if (aiDriver == null) aiDriver = aiCar.AddComponent<CarAI>();
            // Désactivé pour l'instant : reste immobile pendant le compte à rebours, comme
            // le joueur (inputLocked). Réactivé juste avant "GO !" plus bas.
            aiDriver.enabled = false;
            aiDriver.raceCircuit = raceCircuit;
            aiDriver.raceWaypointIndex = 0;
            if (i < opponentLateralOffsets.Length) aiDriver.lateralOffset = opponentLateralOffsets[i];
            // Détection plus loin devant : à vitesse plus élevée, la distance par défaut
            // repérait un obstacle trop tard pour réagir à temps.
            aiDriver.frontSensorLength *= 1.6f;
            aiDrivers.Add(aiDriver);

            // Le joueur profite du lissage de direction (steeringSmoothing) pour un ressenti
            // plus doux, mais ça ralentit la réponse au volant — mauvais pour une IA qui doit
            // corriger vite dans un virage serré. On la laisse sur une réponse immédiate.
            CarController aiCarController = aiCar.GetComponent<CarController>();
            if (aiCarController != null)
            {
                aiCarController.steeringSmoothing = 0f;

                // Physique volontairement "trichée" par rapport au joueur : les IA n'ont ni
                // les mêmes réflexes ni la même précision, donc on compense en leur donnant
                // largement plus d'adhérence, de freins, de braquage et d'accélération que
                // la normale — course difficile, moins d'accidents, plus compétitives.
                aiCarController.gripLevel = 1f; // adhérence maximale
                aiCarController.driftGrip = 1f; // même en glisse/frein à main, ne décroche jamais vraiment
                aiCarController.brakingForce *= 2.2f;
                aiCarController.lowSpeedSteerAngle *= 1.6f;
                aiCarController.highSpeedSteerAngle *= 1.6f;
                aiCarController.accelerationForce *= 1.6f;
                aiCarController.maxSpeed *= 1.3f;
            }

            string oppName = i < opponentNames.Length ? opponentNames[i] : $"Adversaire {i + 1}";
            RaceParticipant aiRP = aiCar.AddComponent<RaceParticipant>();
            aiRP.Initialize(raceCircuit.StartFinish, lapsToWin, this, oppName);
            participants.Add(aiRP);
        }

        yield return StartCoroutine(CountdownRoutine());

        // Tout le monde est libéré en même temps, personne n'a d'avance.
        if (playerCarControllerRef != null) playerCarControllerRef.inputLocked = false;
        foreach (CarAI aiDriver in aiDrivers)
        {
            if (aiDriver != null) aiDriver.enabled = true;
        }

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"<color=cyan>Course lancée ! {lapsToWin} tours, en piste !</color>");

        StartCoroutine(GuidePlayerRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        if (countdownText == null) yield break;

        countdownText.gameObject.SetActive(true);

        string[] steps = { "3", "2", "1", "GO !" };
        foreach (string step in steps)
        {
            countdownText.text = step;
            yield return new WaitForSeconds(secondsPerCount);
        }

        countdownText.gameObject.SetActive(false);

        if (lapCounterText != null) lapCounterText.gameObject.SetActive(true);
    }

    // Grille en quinconce (2 voitures par rangée, décalées vers l'arrière à chaque
    // rangée) — bien moins large qu'un alignement des 5 voitures de front, qui pouvait les
    // faire se chevaucher/déborder sur un circuit étroit (constaté en vidéo : les IA se
    // touchaient et au moins une débordait du bord de la route au moment du GO).
    // Applique le layer à tout l'arbre de l'objet (le collider "achat" et celui de
    // DoorTrigger sont sur des enfants, pas la racine).
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private Vector3 GridPosition(int index)
    {
        int row = index / 2;
        int col = index % 2;

        float lateral = (col == 0 ? -1f : 1f) * (gridCarSpacing * 0.5f);
        float depth = -row * gridRowSpacing; // vers l'arrière du point de départ

        Vector3 pos = gridStartPoint.position
                    + gridStartPoint.right * lateral
                    + gridStartPoint.forward * depth;

        // Recalage au sol par raycast : sans ça, la hauteur Y venait uniquement de
        // gridStartPoint, qui peut être légèrement fausse selon l'endroit exact — les
        // voitures apparaissaient en légère "lévitation" au-dessus de la route.
        if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
        {
            pos.y = hit.point.y + 0.1f;
        }

        return pos;
    }

    // Guide le joueur avec les flèches du pathfinder tout au long du circuit, en changeant
    // de cible à chaque fois qu'il se rapproche du noeud suivant. Purement indicatif pour
    // le joueur — les adversaires IA suivent leur propre logique CarAI indépendamment.
    private IEnumerator GuidePlayerRoutine()
    {
        int index = 0;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        while (raceActive && playerParticipant != null && !playerParticipant.hasFinished)
        {
            Transform next = raceCircuit.waypoints[index % raceCircuit.Count];
            if (next == null) yield break;

            if (JobPathfinder.Instance != null) JobPathfinder.Instance.SetTargets(next);

            while (raceActive && playerObj != null && playerParticipant != null && !playerParticipant.hasFinished
                   && Vector3.Distance(playerObj.transform.position, next.position) > 10f)
            {
                // Sondé toutes les 0.1s (au lieu de 0.5s) : à haute vitesse, 0.5s laissait
                // le temps de franchir plusieurs points d'affilée avant la vérification
                // suivante, donnant l'impression que les flèches "sautent" d'un coup à un
                // point bien plus loin au lieu de progresser point par point.
                yield return new WaitForSeconds(0.1f);
            }

            index++;
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

        CallApp.ReleaseCallBlock();

        if (raceCarLayer >= 0)
        {
            Physics.IgnoreLayerCollision(raceCarLayer, raceCarLayer, false);
            raceCarLayer = -1;
        }

        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (lapCounterText != null) lapCounterText.gameObject.SetActive(false);
        if (JobPathfinder.Instance != null) JobPathfinder.Instance.HidePath();

        // Même principe que GarageManager.StoreVehicleRoutine(), la manœuvre la plus fiable
        // du projet pour ce genre de transition : fondu au noir d'abord (masque tout accroc
        // physique éventuel), puis le joueur est reposé avec sa Rigidbody en kinematic
        // pendant la téléportation (totalement insensible à toute collision/dépénétration
        // pendant qu'on le pose), et seulement relâchée une fois stabilisée.
        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            UIManager.Instance.transitionPanel.SetActive(true);
            yield return StartCoroutine(UIManager.Instance.FadeToBlack(0.5f));
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            // Coupe les collisions de TOUTES les voitures de course (l'écran est déjà noir,
            // peu importe où elles sont) avant de faire sortir/repositionner le joueur.
            foreach (GameObject car in spawnedCars)
            {
                if (car == null) continue;
                foreach (Collider col in car.GetComponentsInChildren<Collider>())
                {
                    if (col != null) col.enabled = false;
                }
            }

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
                if (ci != null) ci.ExitCar(); // sort normalement (écran déjà noir, sa position exacte importe peu ici)
            }

            yield return new WaitForFixedUpdate();
        }

        // On détruit les voitures PENDANT que l'écran est noir, pas après.
        foreach (GameObject car in spawnedCars)
        {
            if (car != null) Destroy(car);
        }

        if (playerObj != null)
        {
            Vector3 targetPos = preRacePlayerPosition;
            if (Physics.Raycast(targetPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
            {
                targetPos = hit.point + Vector3.up * 0.1f;
            }

            Rigidbody rb = playerObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // immunisé contre toute collision/dépénétration le temps de se poser
                rb.linearVelocity = Vector3.zero;
                rb.position = targetPos;
            }
            playerObj.transform.position = targetPos;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.WakeUp();
            }
        }

        spawnedCars.Clear();
        participants.Clear();
        finishOrder.Clear();
        playerParticipant = null;
        playerCarControllerRef = null;
        aiDrivers.Clear();

        if (UIManager.Instance != null && UIManager.Instance.transitionPanel != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeToClear(0.5f));
        }
    }
}