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

    [Tooltip("Décalage latéral donné à chaque adversaire IA (voir CarAI.lateralOffset), pensé à l'origine pour éviter la file indienne. Remis à 0 par défaut : sur une route étroite bordée de bâtiments, ce décalage pousse les voitures extérieures vers les bords — hors piste, dans le décor. Le système de Layer traversable entre voitures de course gère déjà la file indienne autrement, ce décalage n'est plus nécessaire pour ça. Ne le remonte que si ta route est large partout.")]
    public float[] opponentLateralOffsets = { 0f, 0f, 0f, 0f };

    [Tooltip("Couleur attribuée à chaque adversaire (le prefab est jaune de base) — via CarUpgrades.SetColor(), même système que la peinture au garage/tuning. Laisse une entrée vide/noire (0,0,0) pour garder la couleur d'origine du prefab sur cet adversaire.")]
    public Color[] opponentColors = {
        new Color(0.9f, 0.1f, 0.1f), // rouge
        new Color(0.1f, 0.4f, 0.9f), // bleu
        new Color(0.1f, 0.8f, 0.2f), // vert
        new Color(0.8f, 0.1f, 0.8f)  // violet
    };

    [Header("Compte à rebours 🚦")]
    [Tooltip("Texte affichant 3, 2, 1, GO ! avant le départ. Tout le monde (joueur + IA) est bloqué pendant ce temps.")]
    public TMPro.TextMeshProUGUI countdownText;
    public float secondsPerCount = 1f;

    [Header("Compteur de tours")]
    [Tooltip("Texte affichant 'Tour X/Y' pendant la course.")]
    public TMPro.TextMeshProUGUI lapCounterText;

    [Header("UI à masquer pendant la course")]
    [Tooltip("Objets d'UI désactivés pendant la course et réactivés à la fin (hotbar, minimap, étoiles de recherche...). Glisse ici les panels concernés de ta Hierarchy.")]
    public GameObject[] uiToHideDuringRace;

    [Header("Physique IA — valeurs ABSOLUES (pas des multiplicateurs du prefab)")]
    [Tooltip("Grip/freinage/braquage des IA sont désormais IDENTIQUES au prefab (donc à ta voiture) — plus aucune différence physique là-dessus. Seules Vitesse Max et Accélération gardent un léger avantage réglable ici, pour rester compétitives sans rien changer d'autre. Si ça vole encore avec des réglages identiques aux tiens, le souci n'est pas la vitesse/le grip IA.")]
    public float aiMaxSpeed = 55f;
    public float aiAccelerationForce = 100f;

    [Header("Récompenses (argent sale 💵)")]
    public int firstPlaceReward = 5000;
    public int secondPlaceReward = 2000;

    [Header("Collisions entre voitures de course")]
    [Tooltip("Nom d'un Layer Unity dédié (Edit > Project Settings > Tags and Layers, crée un nouveau Layer et note son nom ici) assigné automatiquement aux 5 voitures pendant la course. Si renseigné, elles deviennent traversables entre elles (mais restent solides pour tout le reste : route, décor, autre circulation) — plus de carambolage en chaîne. Laisse vide pour garder les collisions normales entre elles.")]
    public string raceCarLayerName = "";

    [Header("Sécurité hors-piste")]
    [Tooltip("Distance (m) à partir de laquelle joueur ET IA sont considérés hors piste et téléportés au dernier point du circuit passé. Mesurée par rapport au segment de circuit le plus proche, pas juste le point visé — fonctionne même en cas de grosse dérive.")]
    public float offTrackDistance = 15f;
    [Tooltip("Message affiché au joueur quand il est ramené sur la piste.")]
    public string offTrackMessage = "Hors piste — retour sur la piste !";

    private int raceCarLayer = -1;
    private int lastRaceDay = -1;

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

    // À appeler depuis CallApp avant de proposer la course — comme pour les jobs, une
    // seule course par jour.
    public bool HasRacedToday()
    {
        return TimeManager.Instance != null && lastRaceDay == TimeManager.Instance.currentDay;
    }

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

        // Sécurité (CallApp vérifie déjà HasRacedToday() avant de proposer la course, mais
        // au cas où StartRace() serait appelée d'ailleurs) : une seule course par jour.
        if (HasRacedToday())
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Tu as déjà couru aujourd'hui. Reviens demain !</color>");
            return;
        }

        if (raceCircuit == null || raceCircuit.Count == 0 || raceCarPrefab == null || gridStartPoint == null)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Erreur : course pas configurée (voir StreetRaceManager dans l'Inspector).</color>");
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        // Marqué dès le lancement effectif (pas seulement en cas de victoire) — comme un
        // job, la tentative "consomme" la course du jour, gagnée ou perdue.
        if (TimeManager.Instance != null) lastRaceDay = TimeManager.Instance.currentDay;

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

        foreach (GameObject uiElement in uiToHideDuringRace)
        {
            if (uiElement != null) uiElement.SetActive(false);
        }

        if (TimeManager.Instance != null) TimeManager.Instance.isPaused = true;

        // Les 5 voitures deviennent traversables entre elles (pas avec le reste du monde)
        // si un Layer dédié est renseigné — évite les carambolages en chaîne.
        raceCarLayer = string.IsNullOrEmpty(raceCarLayerName) ? -1 : LayerMask.NameToLayer(raceCarLayerName);
        if (raceCarLayer >= 0)
        {
            Physics.IgnoreLayerCollision(raceCarLayer, raceCarLayer, true);
        }

        // --- Voiture du joueur (position 0 sur la grille) ---
        GameObject playerCar = Instantiate(raceCarPrefab, GridPosition(0), gridStartPoint.rotation);
        SnapCarToGround(playerCar);
        spawnedCars.Add(playerCar);
        if (raceCarLayer >= 0) SetLayerRecursively(playerCar, raceCarLayer);

        // Kinematic POSÉ TOUT DE SUITE, avant même le yield return null plus bas — sinon ce
        // seul frame d'attente (nécessaire pour Start()) laissait la vraie physique tourner
        // au moins un pas (gravité, dépénétration...) et la voiture restait figée en l'air
        // pour tout le compte à rebours une fois le kinematic posé après coup.
        Rigidbody playerRb = playerCar.GetComponent<Rigidbody>();
        if (playerRb != null) playerRb.isKinematic = true;

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
            // La vitesse/adhérence boostées côté IA rendent les chocs bien plus violents
            // que la normale (énergie cinétique au carré de la vitesse) — sans ce plafond,
            // le moindre choc pouvait envoyer une voiture dans les airs.
            playerCarControllerRef.limitCollisionLaunch = true;
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
            SnapCarToGround(aiCar);
            spawnedCars.Add(aiCar);
            if (raceCarLayer >= 0) SetLayerRecursively(aiCar, raceCarLayer);

            Rigidbody aiRb = aiCar.GetComponent<Rigidbody>();
            if (aiRb != null) aiRb.isKinematic = true;

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
            // Synchronisé avec aiMaxSpeed (valeurs absolues ci-dessus) — sinon le
            // planificateur visait toujours ses valeurs par défaut (jusqu'à 65 m/s), au-delà
            // de ce que la voiture peut désormais physiquement atteindre.
            aiDriver.raceStraightSpeed = aiMaxSpeed;
            aiDriver.raceHairpinSpeed = aiMaxSpeed * 0.22f;
            // Réduit à 1m (5m par défaut) : protégé par l'avancement par projection
            // géométrique dans AdvanceRaceWaypoint() (une voiture qui dépasse un point sans
            // entrer pile dans le rayon avance quand même), donc pas de risque qu'elle
            // tourne indéfiniment autour d'un point devenu trop précis à atteindre.
            aiDriver.waypointThreshold = 1f;

            // TROUVÉ dans le prefab : obstacleMask (m_Bits: 0) ne détecte RIEN — tout le
            // système d'évitement (CheckFrontSensors/CheckRearSensors, steerBias, freinage
            // d'urgence) tourne dans le vide depuis le début, peu importe la précision de la
            // trajectoire. On force "Everything" ici plutôt que de dépendre du numéro exact
            // du Layer des bâtiments (potentiellement différent d'un projet à l'autre) —
            // sûr : le code exclut déjà le sol plat et les propres colliders de la voiture
            // (voir CheckFrontSensors), donc pas de faux positifs à craindre.
            aiDriver.obstacleMask = ~0;
            aiDrivers.Add(aiDriver);

            // Le joueur profite du lissage de direction (steeringSmoothing) pour un ressenti
            // plus doux, mais ça ralentit la réponse au volant — mauvais pour une IA qui doit
            // corriger vite dans un virage serré. On la laisse sur une réponse immédiate.
            CarController aiCarController = aiCar.GetComponent<CarController>();
            if (aiCarController != null)
            {
                aiCarController.steeringSmoothing = 0f;

                // Grip, driftGrip, freinage, braquage : plus AUCUN override, l'IA utilise
                // exactement les valeurs du prefab, identiques au joueur. Seules vitesse
                // max et accélération gardent un léger avantage (valeurs absolues, voir
                // Header ci-dessus) pour rester compétitives.
                aiCarController.accelerationForce = aiAccelerationForce;
                aiCarController.maxSpeed = aiMaxSpeed;

                // Même raison que côté joueur : même à vitesse rapprochée du joueur, deux
                // voitures qui se percutent de plein fouet restent un choc franc — sans ce
                // plafond, ça pouvait quand même envoyer une voiture dans les airs.
                aiCarController.limitCollisionLaunch = true;
            }

            // Couleur distincte par adversaire (le prefab est jaune de base), APRÈS le
            // boost physique ci-dessus : CarUpgrades capture les valeurs actuelles de
            // maxSpeed/accelerationForce/etc. comme "base" au premier appel — les capturer
            // avant le boost aurait annulé ce dernier au premier ApplyAll().
            if (i < opponentColors.Length && opponentColors[i] != Color.black)
            {
                CarUpgrades aiUpgrades = aiCar.GetComponent<CarUpgrades>();
                if (aiUpgrades == null) aiUpgrades = aiCar.AddComponent<CarUpgrades>();
                aiUpgrades.SetColor(opponentColors[i]);
            }

            string oppName = i < opponentNames.Length ? opponentNames[i] : $"Adversaire {i + 1}";
            RaceParticipant aiRP = aiCar.AddComponent<RaceParticipant>();
            aiRP.Initialize(raceCircuit.StartFinish, lapsToWin, this, oppName);
            participants.Add(aiRP);
        }

        // Les 5 Rigidbody sont déjà kinematic (posé immédiatement après chaque spawn,
        // voir plus haut) — on collecte juste les références pour les relâcher au "GO !".
        List<Rigidbody> raceRigidbodies = new List<Rigidbody>();
        foreach (GameObject car in spawnedCars)
        {
            if (car == null) continue;
            Rigidbody carRb = car.GetComponent<Rigidbody>();
            if (carRb != null) raceRigidbodies.Add(carRb);
        }

        // Recalage au sol supplémentaire une frame plus tard : les bounds d'un Renderer
        // fraîchement instancié (utilisées par SnapCarToGround) ne sont pas toujours
        // fiables DANS LA MÊME frame que l'Instantiate — d'où une hauteur encore légèrement
        // fausse malgré le kinematic posé immédiatement. Sans risque ici : tout est encore
        // kinematic, aucune physique ne peut interférer pendant ce recalage.
        //
        // C'est AUSSI le bon moment (et pas avant) pour corriger centerOfMass : Start() de
        // CarController (qui pose rb.centerOfMass = centerOfMassOffset, soit -0.7 sur ce
        // prefab) ne tourne jamais de façon synchrone pendant Instantiate() — une correction
        // posée AVANT ce yield aurait été silencieusement écrasée dès que Start() s'exécute
        // réellement. Après ce yield, Start() a garanti tourné pour les 5 voitures.
        yield return null;
        foreach (GameObject car in spawnedCars)
        {
            if (car == null) continue;

            SnapCarToGround(car);

            Rigidbody carRb = car.GetComponent<Rigidbody>();
            if (carRb != null)
            {
                // Ce prefab a centerOfMassOffset=-0.7 dans son CarController, ce qui place
                // le centre de masse à Y=-0.7 — sous le bas réel de la carrosserie (le
                // collider solide commence vers Y=0.01). Un centre de masse aussi loin sous
                // le châssis démultiplie le bras de levier de n'importe quel choc, donc le
                // couple de rotation qui en résulte : un impact modéré se traduit alors par
                // un tonneau/envol spectaculaire. On le recentre à une hauteur réaliste
                // (basse pour la stabilité, mais DANS la carrosserie, pas dessous).
                carRb.centerOfMass = new Vector3(0f, 0.4f, 0f);

                // linearDamping/angularDamping ne sont touchés nulle part dans
                // CarController — à 0/0.05 (valeurs du prefab), rien ne freine
                // naturellement une voiture une fois lancée par un choc, ni ne stoppe un
                // tournoiement : elle "vole"/tourne un long moment avant que la seule
                // gravité ne la ramène.
                carRb.linearDamping = 0.3f;
                carRb.angularDamping = 3f;
            }
        }

        yield return StartCoroutine(CountdownRoutine());

        foreach (Rigidbody carRb in raceRigidbodies)
        {
            if (carRb != null) carRb.isKinematic = false;
        }

        // Tout le monde est libéré en même temps, personne n'a d'avance.
        if (playerCarControllerRef != null) playerCarControllerRef.inputLocked = false;
        foreach (CarAI aiDriver in aiDrivers)
        {
            if (aiDriver != null) aiDriver.enabled = true;
        }

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"<color=cyan>Course lancée ! {lapsToWin} tours, en piste !</color>");

        StartCoroutine(OffTrackWatcherRoutine());
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

    // Recalage précis post-instanciation, basé sur le VRAI bas du/des collider(s) SOLIDES
    // (non-trigger) — c'est ce que la physique utilise réellement pour poser la voiture au
    // sol une fois relâchée du kinematic, donc la référence la plus fiable pour qu'il n'y
    // ait plus jamais d'écart entre la position calculée ici et celle où elle se
    // stabiliserait naturellement. Exclut explicitement les colliders "Is Trigger" (zones
    // d'interaction, souvent bien plus hautes/petites que la carrosserie — le prefab de
    // course en a justement une, 1x1x1, qui aurait faussé le calcul si attrapée par erreur).
    // Repli sur les renderers si aucun collider solide n'est trouvé (config inhabituelle).
    private void SnapCarToGround(GameObject car)
    {
        Collider[] cols = car.GetComponentsInChildren<Collider>();
        float lowestY = float.MaxValue;
        bool found = false;

        // Bounds calculées AVANT de désactiver quoi que ce soit (fiable pendant que les
        // colliders sont encore actifs).
        foreach (Collider c in cols)
        {
            if (c.isTrigger) continue;
            if (c.bounds.min.y < lowestY)
            {
                lowestY = c.bounds.min.y;
                found = true;
            }
        }

        if (!found)
        {
            Renderer[] rends = car.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            foreach (Renderer r in rends)
            {
                if (r.bounds.min.y < lowestY) lowestY = r.bounds.min.y;
            }
        }

        // LA VRAIE CAUSE DE LA LÉVITATION : le rayon part d'AU-DESSUS de la voiture et vise
        // le bas. Sans ceci, il traverse d'abord le corps de la voiture ELLE-MÊME (jusqu'à
        // ~1.86 unité de haut sur ce prefab) et touche son PROPRE TOIT avant même d'avoir pu
        // atteindre le vrai sol en dessous — peu importe la précision du calcul de hauteur
        // par ailleurs, hit.point.y était systématiquement le toit de la voiture, pas la
        // route. On désactive donc ses colliders le temps du rayon, puis on les réactive.
        bool[] wasEnabled = new bool[cols.Length];
        for (int i = 0; i < cols.Length; i++)
        {
            wasEnabled[i] = cols[i].enabled;
            cols[i].enabled = false;
        }

        bool raycastHit = Physics.Raycast(car.transform.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f);

        for (int i = 0; i < cols.Length; i++)
        {
            cols[i].enabled = wasEnabled[i];
        }

        if (raycastHit)
        {
            float clearance = car.transform.position.y - lowestY;
            Vector3 pos = car.transform.position;
            pos.y = hit.point.y + clearance + 0.05f;
            car.transform.position = pos;
        }
    }

    // Surveille joueur ET IA en continu pendant la course : si l'un d'eux s'éloigne de plus
    // de offTrackDistance du circuit (mesuré au segment le plus proche, pas juste le point
    // visé — fonctionne même en cas de grosse dérive), il est téléporté au dernier point du
    // circuit qu'il a dépassé. S'arrête toute seule quand raceActive redevient faux.
    private IEnumerator OffTrackWatcherRoutine()
    {
        while (raceActive)
        {
            yield return new WaitForSeconds(1f);
            if (!raceActive || raceCircuit == null || raceCircuit.Count == 0) yield break;

            if (playerCarControllerRef != null && playerParticipant != null && !playerParticipant.hasFinished)
            {
                CheckOffTrack(playerCarControllerRef.transform, playerCarControllerRef.GetComponent<Rigidbody>(), true, null);
            }

            foreach (CarAI aiDriver in aiDrivers)
            {
                if (aiDriver == null || !aiDriver.enabled) continue;
                CheckOffTrack(aiDriver.transform, aiDriver.GetComponent<Rigidbody>(), false, aiDriver);
            }
        }
    }

    private void CheckOffTrack(Transform carTransform, Rigidbody carRb, bool isPlayer, CarAI aiDriver)
    {
        if (carTransform == null) return;

        // Cherche le segment du circuit (entre deux points consécutifs) le plus proche de
        // la position actuelle, en testant TOUS les segments — pas seulement celui qu'on
        // est censé suivre. Plus robuste : fonctionne même si on a dérivé vers une autre
        // partie du circuit (ex: un virage en épingle où deux segments passent proches).
        float closestDist = float.MaxValue;
        int closestSegmentIndex = 0;

        for (int i = 0; i < raceCircuit.Count; i++)
        {
            Vector3 segStart = raceCircuit.GetPoint(i);
            Vector3 segEnd = raceCircuit.GetPoint(i + 1);
            Vector3 closestOnSeg = ClosestPointOnSegment(carTransform.position, segStart, segEnd);
            float dist = Vector3.Distance(carTransform.position, closestOnSeg);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestSegmentIndex = i;
            }
        }

        if (closestDist <= offTrackDistance) return;

        // Téléporte au DÉBUT du segment le plus proche — le dernier point du circuit le
        // plus plausible étant donné où le véhicule a dérivé.
        Vector3 tpPos = raceCircuit.GetPoint(closestSegmentIndex);
        if (Physics.Raycast(tpPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
        {
            tpPos = hit.point + Vector3.up * 0.5f;
        }

        if (carRb != null)
        {
            carRb.position = tpPos;
            carRb.linearVelocity = Vector3.zero;
            carRb.angularVelocity = Vector3.zero;
        }
        else
        {
            carTransform.position = tpPos;
        }

        // Réoriente vers le point suivant, pas l'angle qu'il avait en dérivant.
        Vector3 lookDir = raceCircuit.GetPoint(closestSegmentIndex + 1) - tpPos;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            if (carRb != null) carRb.rotation = Quaternion.LookRotation(lookDir.normalized);
            else carTransform.rotation = Quaternion.LookRotation(lookDir.normalized);
        }

        if (aiDriver != null) aiDriver.raceWaypointIndex = closestSegmentIndex;

        if (isPlayer && UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"<color=cyan>{offTrackMessage}</color>");
        }
    }

    private Vector3 ClosestPointOnSegment(Vector3 point, Vector3 segStart, Vector3 segEnd)
    {
        Vector3 segDir = segEnd - segStart;
        float segLenSq = segDir.sqrMagnitude;
        if (segLenSq < 0.0001f) return segStart;
        float t = Mathf.Clamp01(Vector3.Dot(point - segStart, segDir) / segLenSq);
        return segStart + segDir * t;
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

    // Appelée par chaque RaceParticipant (joueur ou IA) quand il termine son nombre de tours.
    public void NotifyParticipantFinished(RaceParticipant participant)
    {
        if (finishOrder.Contains(participant)) return;
        finishOrder.Add(participant);

        if (participant == playerParticipant)
        {
            EndRaceForPlayer();
        }
        else if (playerParticipant != null && !playerParticipant.hasFinished)
        {
            // Tous les adversaires ont fini AVANT le joueur : la course doit se terminer
            // quand même (défaite), sinon en restant à l'arrêt le joueur ne pouvait jamais
            // perdre — la course continuait indéfiniment.
            int aiFinishedCount = 0;
            foreach (RaceParticipant p in finishOrder)
            {
                if (p != playerParticipant) aiFinishedCount++;
            }
            if (aiFinishedCount >= participants.Count - 1)
            {
                finishOrder.Add(playerParticipant); // classé dernier
                EndRaceForPlayer();
            }
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

        foreach (GameObject uiElement in uiToHideDuringRace)
        {
            if (uiElement != null) uiElement.SetActive(true);
        }

        if (TimeManager.Instance != null) TimeManager.Instance.isPaused = false;

        if (raceCarLayer >= 0)
        {
            Physics.IgnoreLayerCollision(raceCarLayer, raceCarLayer, false);
            raceCarLayer = -1;
        }

        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (lapCounterText != null) lapCounterText.gameObject.SetActive(false);

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