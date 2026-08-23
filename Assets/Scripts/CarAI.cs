using UnityEngine;

[RequireComponent(typeof(CarController))]
public class CarAI : MonoBehaviour
{
    [Header("Navigation routière")]
    public TrafficNode currentNode;
    public float waypointThreshold = 5f;

    [Tooltip("Décalage latéral (perpendiculaire à la route) appliqué au point visé. 0 = comportement normal. Utile pour éviter que plusieurs IA suivant le même circuit ne roulent en file indienne parfaite — donne à chacune une valeur différente (ex: -3, -1, +1, +3).")]
    public float lateralOffset = 0f;

    [Header("Mode course — circuit explicite (remplace currentNode en mode course)")]
    [Tooltip("Si renseigné, l'IA ignore complètement le graphe TrafficNode et suit ce circuit par index, sans ambiguïté possible (voir RaceCircuit.cs).")]
    public RaceCircuit raceCircuit;
    [HideInInspector] public int raceWaypointIndex = 0;

    [Header("Mode course — planification anticipée (actif si Race Circuit est renseigné)")]
    [Tooltip("Nombre de points du circuit regardés à l'avance pour anticiper les virages.")]
    public int raceLookAheadNodes = 5;
    [Tooltip("Vitesse visée en ligne droite (m/s).")]
    public float raceStraightSpeed = 40f;
    [Tooltip("Vitesse visée dans une épingle très serrée (m/s).")]
    public float raceHairpinSpeed = 12f;
    [Tooltip("Décélération au freinage utilisée pour calculer QUAND commencer à ralentir (m/s²). Plus haut = freine plus tard/fort, plus bas = freine plus tôt/doux.")]
    public float raceBrakingDeceleration = 10f;

    [Tooltip("Si la vitesse reste sous 1 m/s pendant ce temps en essayant d'avancer, déclenche une marche arrière de dégagement — indépendant des capteurs, basé uniquement sur la vitesse réelle.")]
    public float raceStuckTimeout = 0.35f;
    [Tooltip("Durée de la marche arrière de dégagement.")]
    public float raceReverseDuration = 0.45f;

    private float raceStuckTimer = 0f;
    private bool raceReversing = false;
    private float raceReverseTimer = 0f;

    [Header("Détection d'obstacles (Matrice 360)")]
    public float frontSensorLength = 7f;
    public float rearSensorLength = 3f;
    public float sensorFrontOffset = 2.5f;
    public LayerMask obstacleMask;

    [Header("Simulation Humaine (Bras & Pieds) 🧠")]
    public float steeringReactionTime = 0.15f; // Inertie des bras sur le volant
    public float pedalSmoothing = 3f;

    [HideInInspector] public Transform chaseTarget = null;

    private CarController carController;
    private Rigidbody rb;

    // --- VOLANT VIRTUEL ---
    private float virtualSteeringWheel = 0f;
    private float steeringVelocity = 0f;
    private float virtualGasPedal = 0f;

    // --- MACHINE À ÉTATS ORGANIQUE ---
    private int maneuverState = 0; // 0=Drive, 1=Brake, 2=Reverse (K-Turn), 3=Shift Gear
    private float maneuverTimer = 0f;
    private float stuckTimer = 0f;
    private float lockedAvoidDirection = 1f;

    // --- CAPTEURS DYNAMIQUES ---
    private bool isHumanInDanger = false;
    private float closestFrontDistance = 10f;
    private float steerBias = 0f; // La volonté de dévier de sa trajectoire

    private float ramTimer = 0f;

    void Start()
    {
        carController = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();
        carController.isDrivenByAI = true;
    }

    void Update()
    {
        if (!carController.isDrivenByAI) return;

        // 1. ANALYSE DE L'ENVIRONNEMENT (Les Yeux)
        ScanEnvironment();

        // 2. GESTION DU BLOCAGE (Si coincé contre un trottoir bas non détecté)
        if (maneuverState == 0 && Mathf.Abs(virtualGasPedal) > 0.1f && rb.linearVelocity.magnitude < 0.5f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1.2f) StartHumanManeuver(steerBias != 0 ? Mathf.Sign(steerBias) : (Random.value > 0.5f ? 1f : -1f));
        }
        else { stuckTimer = 0f; }

        // 3. EXÉCUTION DU CRÉNEAU (Si bloqué)
        if (maneuverState > 0)
        {
            ExecuteHumanManeuver();
        }
        else
        {
            // 4. CONDUITE NORMALE (Si la voie est libre)
            Drive();
        }

        // --- APPLICATION PHYSIQUE (Simulation des muscles) ---
        ApplyOrganicInputs();
    }

    private void ApplyOrganicInputs()
    {
        // Les pédales sont appuyées avec douceur (sauf freinage d'urgence)
        carController.moveInput = Mathf.Lerp(carController.moveInput, virtualGasPedal, Time.deltaTime * pedalSmoothing);

        // Le volant tourne de manière fluide, impossible de passer de -1 à 1 instantanément
        carController.turnInput = Mathf.SmoothDamp(carController.turnInput, virtualSteeringWheel, ref steeringVelocity, steeringReactionTime);
    }

    // --- LA MANŒUVRE D'ÉVITEMENT 100% ORGANIQUE ---
    private void StartHumanManeuver(float direction)
    {
        maneuverState = 1;
        maneuverTimer = 0f;
        // On décide de quel côté on va braquer pour le créneau
        lockedAvoidDirection = direction;
    }

    private void ExecuteHumanManeuver()
    {
        maneuverTimer += Time.deltaTime;

        if (maneuverState == 1) // Phase 1 : Piler sur les freins (Surprise !)
        {
            virtualGasPedal = 0f;
            carController.isHandbraking = true;
            virtualSteeringWheel = 0f;

            if (rb.linearVelocity.magnitude < 0.2f || maneuverTimer > 0.8f)
            {
                maneuverState = 2; // On passe la marche arrière
                maneuverTimer = 0f;
            }
        }
        else if (maneuverState == 2) // Phase 2 : Le Créneau Dynamique (On recule en regardant derrière)
        {
            carController.isHandbraking = false;
            virtualGasPedal = -0.5f; // Pédale de recul à moitié
            virtualSteeringWheel = -lockedAvoidDirection; // On contre-braque à fond

            bool isRearBlocked = CheckRearSensors(rearSensorLength);
            bool isFrontClear = !CheckFrontSensors(frontSensorLength * 0.5f); // L'avant est-il assez dégagé pour repartir ?

            // L'HUMAIN DÉCIDE DE S'ARRÊTER DE RECULER SI :
            // 1. Il a assez de place devant (et a reculé au moins 0.8s pour l'élan)
            // 2. Il touche un mur derrière lui
            // 3. Ça fait trop longtemps qu'il recule (Sécurité anti-glitch, 4 secondes max)
            if ((isFrontClear && maneuverTimer > 0.8f) || isRearBlocked || maneuverTimer > 4.0f)
            {
                // LE SECRET : Si on s'est arrêté de reculer car le cul touche un mur, MAIS que l'avant est toujours coincé...
                if (isRearBlocked && !isFrontClear)
                {
                    // L'IA apprend ! Elle se dit : "Bon, braquer à gauche n'a pas marché, je vais braquer de l'autre côté à la prochaine tentative."
                    lockedAvoidDirection *= -1f;
                }

                maneuverState = 3; // On passe la marche avant
                maneuverTimer = 0f;
            }
        }
        else if (maneuverState == 3) // Phase 3 : Le passage de vitesse (La pause de la boîte de vitesse)
        {
            carController.isHandbraking = false;

            if (maneuverTimer < 0.35f) // Pause de 0.35 seconde (temps humain pour changer la vitesse)
            {
                virtualGasPedal = 0f;
            }
            else
            {
                virtualGasPedal = 0.5f; // On repart !
            }

            virtualSteeringWheel = lockedAvoidDirection; // On braque pour contourner l'obstacle

            if (maneuverTimer > 1.8f)
            {
                maneuverState = 0; // Fin de la manœuvre, retour à la conduite normale
            }
        }
    }

    void Drive()
    {
        bool raceMode = raceCircuit != null && raceCircuit.Count > 0;

        if (chaseTarget == null && currentNode == null && !raceMode)
        {
            virtualGasPedal = 0f;
            carController.isHandbraking = true;
            return;
        }

        // Action de police agressive (Bélier)
        if (ramTimer > 0f)
        {
            ramTimer -= Time.deltaTime;
            virtualGasPedal = -1f;
            virtualSteeringWheel = 0f;
            carController.isHandbraking = false;
            return;
        }

        if (raceMode)
        {
            // Mode course : circuit explicite (RaceCircuit), totalement indépendant du
            // graphe TrafficNode ci-dessous — élimine tout risque d'embranchement mal
            // configuré ou de boucle accidentelle qui pouvait coincer une IA au même
            // endroit indéfiniment.
            //
            // Dégagement après choc INDÉPENDANT des capteurs (Obstacle Mask) : si le Layer
            // d'un nouvel obstacle (immeuble...) n'est pas inclus dedans, l'IA le percute
            // sans jamais le "voir", et ni le freinage d'urgence ni la récupération normale
            // (tous deux basés sur les capteurs) ne se déclenchent — elle restait plantée
            // contre le mur indéfiniment. Ici, seule la vitesse réelle du Rigidbody compte,
            // aucune dépendance aux capteurs.
            if (raceReversing)
            {
                raceReverseTimer += Time.deltaTime;
                virtualGasPedal = -1f; // marche arrière franche, pas à moitié — dégagement rapide
                carController.isHandbraking = false; // au cas où resté actif d'un virage précédent

                Vector3 toTarget = raceCircuit.GetPoint(raceWaypointIndex) - transform.position;
                Vector3 local = transform.InverseTransformPoint(transform.position + toTarget);
                virtualSteeringWheel = local.x > 0f ? -1f : 1f; // s'écarte de l'obstacle en reculant

                if (raceReverseTimer > raceReverseDuration)
                {
                    raceReversing = false;
                    raceStuckTimer = 0f;
                }
            }
            else
            {
                if (rb.linearVelocity.magnitude < 1f)
                {
                    raceStuckTimer += Time.deltaTime;
                    if (raceStuckTimer > raceStuckTimeout)
                    {
                        raceReversing = true;
                        raceReverseTimer = 0f;
                    }
                }
                else
                {
                    raceStuckTimer = 0f;
                }

                if (!raceReversing)
                {
                    AdvanceRaceWaypoint();
                    ComputeRaceDriving();
                }
            }
        }
        else
        {
            Vector3 targetPos;

            if (chaseTarget != null)
            {
                targetPos = chaseTarget.position;
            }
            else
            {
                targetPos = currentNode.transform.position;

                // Décalage perpendiculaire à la direction du noeud (0 = pas de changement) :
                // sans ça, plusieurs IA suivant le même chemin visent EXACTEMENT le même
                // point et finissent en file indienne quasi parfaite.
                if (!Mathf.Approximately(lateralOffset, 0f))
                {
                    Vector3 dirToNode = (targetPos - transform.position);
                    dirToNode.y = 0f;
                    if (dirToNode.sqrMagnitude > 0.01f)
                    {
                        Vector3 perpendicular = Vector3.Cross(Vector3.up, dirToNode.normalized);
                        targetPos += perpendicular * lateralOffset;
                    }
                }

                if (Vector3.Distance(transform.position, currentNode.transform.position) < waypointThreshold && currentNode.nextNodes.Count > 0)
                {
                    currentNode = currentNode.nextNodes[Random.Range(0, currentNode.nextNodes.Count)];
                }
            }

            // Calcul de la trajectoire idéale (comportement d'origine : trafic normal en
            // ville, ou poursuite d'une cible chaseTarget).
            Vector3 localTarget = transform.InverseTransformPoint(targetPos);
            float angleToTarget = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
            float idealSteer = Mathf.Clamp(angleToTarget / 45f, -1f, 1f);

            virtualSteeringWheel = Mathf.Clamp(idealSteer + steerBias, -1f, 1f);

            float angleAbs = Mathf.Abs(angleToTarget);
            float currentSpeed = rb.linearVelocity.magnitude;

            if (steerBias != 0f && closestFrontDistance < 4f)
            {
                virtualGasPedal = 0.2f;
            }
            else if (angleAbs <= 8f)
            {
                virtualGasPedal = 1f - (Mathf.Abs(virtualSteeringWheel) * 0.2f);
            }
            else
            {
                float turnSeverity = Mathf.Clamp01((angleAbs - 8f) / 70f);
                float targetGasPedal = Mathf.Lerp(1f, -0.9f, turnSeverity);
                if (currentSpeed < 8f) targetGasPedal = Mathf.Max(targetGasPedal, 0.2f);
                virtualGasPedal = targetGasPedal;
            }
        }

        // Freinage d'urgence absolu devant un mur ou un piéton (s'applique dans tous les cas)
        if (closestFrontDistance < 2.5f || (isHumanInDanger && closestFrontDistance < 5f))
        {
            virtualGasPedal = -1f;
            carController.isHandbraking = (closestFrontDistance < 1.5f); // Frein à main si vraiment trop proche

            // Si le mur est VRAIMENT trop près et qu'on ne fait pas déjà un créneau, on le lance
            if (closestFrontDistance < 1.5f && !isHumanInDanger)
            {
                StartHumanManeuver(steerBias != 0 ? Mathf.Sign(steerBias) : (Random.value > 0.5f ? 1f : -1f));
            }
        }
        else
        {
            // En mode course, ComputeRaceDriving() a déjà décidé du frein à main (rallye en
            // virage serré) — ne pas l'écraser ici juste parce que ce n'est pas une urgence.
            if (!raceMode) carController.isHandbraking = false;
        }
    }

    // Avance au point suivant du RaceCircuit. Deux conditions, l'une OU l'autre suffit :
    // - proximité classique (comme avant) ;
    // - OU la voiture a géométriquement DÉPASSÉ le point (projetée sur la direction du
    //   segment courant->suivant), même sans être entrée dans le rayon exact.
    // Cette 2e condition est le vrai correctif : sur un virage trop serré pour le rayon de
    // braquage de la voiture à sa vitesse du moment, elle pouvait tourner autour du point
    // sans jamais y entrer précisément — un simple "je suis proche" ne suffisait pas, il
    // fallait détecter "je suis passée devant" même à distance.
    private void AdvanceRaceWaypoint()
    {
        Vector3 currentTarget = raceCircuit.GetPoint(raceWaypointIndex);
        Vector3 nextTarget = raceCircuit.GetPoint(raceWaypointIndex + 1);

        float distToCurrent = Vector3.Distance(transform.position, currentTarget);
        bool closeEnough = distToCurrent < waypointThreshold;

        bool passedByProjection = false;
        Vector3 segmentDir = nextTarget - currentTarget;
        if (segmentDir.sqrMagnitude > 0.01f)
        {
            Vector3 toCar = transform.position - currentTarget;
            float projection = Vector3.Dot(toCar, segmentDir.normalized);
            // "Dépassé" seulement si raisonnablement proche (3x le seuil) — évite de
            // sauter un point à cause d'un simple raccourci géométrique lointain.
            passedByProjection = projection > 0f && distToCurrent < waypointThreshold * 3f;
        }

        if (closeEnough || passedByProjection)
        {
            raceWaypointIndex = (raceWaypointIndex + 1) % raceCircuit.Count;
        }
    }

    // --- MODE COURSE : planification anticipée façon IA de compétition ---
    // Regarde plusieurs noeuds à l'avance, calcule une vitesse de sécurité pour chaque
    // virage à venir selon sa sévérité, puis remonte le calcul jusqu'à MAINTENANT pour
    // savoir s'il faut déjà lever le pied — au lieu de réagir seulement une fois sur le
    // point du virage (ce qui donnait : freinage sec, arrêt, dépassement, redémarrage lent).
    private void ComputeRaceDriving()
    {
        Vector3[] upcoming = new Vector3[raceLookAheadNodes];
        int count = Mathf.Min(raceLookAheadNodes, raceCircuit.Count);
        for (int i = 0; i < count; i++)
        {
            upcoming[i] = raceCircuit.GetPoint(raceWaypointIndex + i);
        }

        if (count == 0)
        {
            virtualGasPedal = 0f;
            return;
        }

        // Sévérité de chaque virage à venir : angle entre la direction d'arrivée et la
        // direction de sortie à ce point (0 = tout droit, 1 = quasi demi-tour).
        float[] severity = new float[count];
        Vector3 incomingDir = upcoming[0] - transform.position;
        for (int i = 0; i < count; i++)
        {
            Vector3 outgoingDir = (i + 1 < count) ? (upcoming[i + 1] - upcoming[i]) : incomingDir;
            severity[i] = (incomingDir.sqrMagnitude > 0.01f && outgoingDir.sqrMagnitude > 0.01f)
                ? Mathf.Clamp01(Vector3.Angle(incomingDir, outgoingDir) / 90f)
                : 0f;
            incomingDir = outgoingDir;
        }

        // Vitesse qu'il faudrait avoir MAINTENANT pour pouvoir freiner à temps jusqu'à la
        // vitesse sûre de chaque virage à venir (cinématique : v0 = racine(v² + 2*a*d)).
        // Le minimum sur toute la fenêtre d'anticipation fait loi : un virage serré loin
        // devant impose déjà de lever le pied bien avant d'y être, comme un vrai pilote.
        float targetSpeed = raceStraightSpeed;
        Vector3 fromPoint = transform.position;
        for (int i = 0; i < count; i++)
        {
            float dist = Vector3.Distance(fromPoint, upcoming[i]);
            float safeSpeedHere = Mathf.Lerp(raceStraightSpeed, raceHairpinSpeed, severity[i]);
            float requiredSpeedNow = Mathf.Sqrt(safeSpeedHere * safeSpeedHere + 2f * raceBrakingDeceleration * dist);
            targetSpeed = Mathf.Min(targetSpeed, requiredSpeedNow);
            fromPoint = upcoming[i];
        }

        float currentSpeed = rb.linearVelocity.magnitude;
        virtualGasPedal = Mathf.Clamp((targetSpeed - currentSpeed) * 0.4f, -1f, 1f);

        // Direction façon "pure pursuit" : vise un point interpolé le long du chemin à une
        // distance qui grandit avec la vitesse (regarde plus loin quand on va plus vite),
        // plutôt qu'un point fixe sur le noeud — la voiture amorce le virage en douceur au
        // lieu de pivoter sec dessus.
        float lookAheadDist = Mathf.Clamp(currentSpeed * 0.5f, 4f, 14f);
        Vector3 steerTarget = upcoming[count - 1];
        Vector3 from2 = transform.position;
        float remaining = lookAheadDist;
        for (int i = 0; i < count; i++)
        {
            float segDist = Vector3.Distance(from2, upcoming[i]);
            if (segDist >= remaining)
            {
                steerTarget = Vector3.Lerp(from2, upcoming[i], remaining / Mathf.Max(segDist, 0.01f));
                break;
            }
            remaining -= segDist;
            from2 = upcoming[i];
        }

        if (!Mathf.Approximately(lateralOffset, 0f))
        {
            Vector3 dirToTarget = steerTarget - transform.position;
            dirToTarget.y = 0f;
            if (dirToTarget.sqrMagnitude > 0.01f)
            {
                Vector3 perpendicular = Vector3.Cross(Vector3.up, dirToTarget.normalized);
                steerTarget += perpendicular * lateralOffset;
            }
        }

        Vector3 localTarget = transform.InverseTransformPoint(steerTarget);
        float angleToTarget = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        // steerBias amplifié en course (x1.6) : à vitesse plus élevée, un évitement mou
        // arrive trop tard pour être efficace contre un obstacle (immeuble...) détecté.
        virtualSteeringWheel = Mathf.Clamp((angleToTarget / 45f) + steerBias * 1.6f, -1f, 1f);

        // Le planificateur de vitesse ci-dessus peut commander une accélération franche si
        // la vitesse actuelle est sous la cible — correct en ligne droite, mais dangereux
        // EN PLEIN VIRAGE très serré (volant presque à fond) : accélérer fort en tournant
        // fait perdre l'adhérence. On plafonne seulement dans ce cas extrême, pas sur un
        // simple virage modéré (les voitures IA ont un grip renforcé, voir StreetRaceManager).
        float steerMagnitude = Mathf.Abs(virtualSteeringWheel);
        if (steerMagnitude > 0.75f && virtualGasPedal > 0.5f)
        {
            virtualGasPedal = 0.5f;
        }

        // Frein à main façon rallye : volant presque à fond ET encore nettement trop de
        // vitesse par rapport à la cible du virage — un coup de frein à main aide à faire
        // pivoter la voiture court plutôt que de juste glisser tout droit vers l'extérieur.
        carController.isHandbraking = steerMagnitude > 0.8f && currentSpeed > targetSpeed + 3f;

        if (steerBias != 0f && closestFrontDistance < 4f)
        {
            virtualGasPedal = Mathf.Min(virtualGasPedal, 0.2f);
        }
    }


    private void ScanEnvironment()
    {
        closestFrontDistance = frontSensorLength;
        steerBias = 0f;
        isHumanInDanger = false;

        CheckFrontSensors(frontSensorLength);
    }

    private bool CheckFrontSensors(float distanceToCheck)
    {
        Vector3 startPos = transform.position + (transform.forward * sensorFrontOffset) + (Vector3.up * 0.5f);

        // 5 Rayons pour analyser la forme de l'obstacle devant
        Vector3[] dirs = {
            transform.forward,
            Quaternion.Euler(0, -15, 0) * transform.forward, // Intérieur Gauche
            Quaternion.Euler(0, 15, 0) * transform.forward,  // Intérieur Droit
            Quaternion.Euler(0, -35, 0) * transform.forward, // Extérieur Gauche
            Quaternion.Euler(0, 35, 0) * transform.forward   // Extérieur Droit
        };

        bool isBlocked = false;

        for (int i = 0; i < dirs.Length; i++)
        {
            float length = (i == 0) ? distanceToCheck : (i < 3 ? distanceToCheck * 0.9f : distanceToCheck * 0.7f);

            if (Physics.Raycast(startPos, dirs[i], out RaycastHit hit, length, obstacleMask))
            {
                if (hit.collider.transform.root == transform.root) continue;
                if (hit.normal.y > 0.8f && !hit.collider.CompareTag("Player")) continue; // Ignore le sol sauf pour le joueur

                // Est-ce un humain ?
                bool hitHuman = hit.collider.GetComponentInParent<NPCBrain>() != null || hit.collider.GetComponentInParent<PlayerController>() != null;
                if (hitHuman) isHumanInDanger = true;

                // Enregistre la distance la plus critique
                if (hit.distance < closestFrontDistance) closestFrontDistance = hit.distance;

                // Calcul du "Poids" d'évitement (Le volant tourne proportionnellement au danger)
                float biasWeight = (1f - (hit.distance / length));

                if (i == 1) steerBias += biasWeight * 0.8f; // Obstacle léger gauche -> Braque fort à droite
                else if (i == 2) steerBias -= biasWeight * 0.8f; // Obstacle léger droit -> Braque fort à gauche
                else if (i == 3) steerBias += biasWeight * 0.4f; // Obstacle ext gauche -> Braque un peu à droite
                else if (i == 4) steerBias -= biasWeight * 0.4f; // Obstacle ext droit -> Braque un peu à gauche
                else if (i == 0) steerBias += (Random.value > 0.5f ? 1f : -1f) * biasWeight; // Face au mur -> Choisit un côté

                isBlocked = true;
            }
        }
        return isBlocked;
    }

    private bool CheckRearSensors(float distanceToCheck)
    {
        // Origine des lasers arrière (au niveau du coffre)
        Vector3 startPos = transform.position - (transform.forward * sensorFrontOffset) + (Vector3.up * 0.5f);

        Vector3[] dirs = {
            -transform.forward, // Pile derrière
            Quaternion.Euler(0, -25, 0) * -transform.forward, // Arrière Gauche
            Quaternion.Euler(0, 25, 0) * -transform.forward   // Arrière Droite
        };

        foreach (Vector3 dir in dirs)
        {
            if (Physics.Raycast(startPos, dir, out RaycastHit hit, distanceToCheck, obstacleMask))
            {
                if (hit.collider.transform.root == transform.root) continue;
                if (hit.normal.y > 0.8f) continue;

                return true; // Un seul impact à l'arrière suffit pour stopper la marche arrière
            }
        }
        return false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (carController == null || !carController.isDrivenByAI) return;

        if (chaseTarget != null)
        {
            CarController targetCar = collision.collider.GetComponentInParent<CarController>();
            if (targetCar != null && targetCar.isDrivenByPlayer && collision.relativeVelocity.magnitude > 3f)
            {
                ramTimer = 1.5f; // IA bélier
            }
        }
    }
}