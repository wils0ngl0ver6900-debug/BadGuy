using UnityEngine;

[RequireComponent(typeof(CarController))]
public class CarAI : MonoBehaviour
{
    [Header("Navigation routière")]
    public TrafficNode currentNode;
    public float waypointThreshold = 5f;

    [Tooltip("Décalage latéral (perpendiculaire à la route) appliqué au point visé sur chaque noeud. 0 = comportement normal (inchangé). Utile pour éviter que plusieurs IA suivant le même circuit ne roulent en file indienne parfaite — donne à chacune une valeur différente (ex: -3, -1, +1, +3).")]
    public float lateralOffset = 0f;

    [Tooltip("0 = comportement normal (inchangé), vise uniquement le noeud actuel. Au-dessus de 0 (ex: 0.4), mélange progressivement le point visé vers le PROCHAIN noeud à l'approche du noeud actuel — anticipe le virage suivant au lieu de piler dessus avant de tourner. Pensé pour une course sur circuit, laisse à 0 pour la circulation normale.")]
    [Range(0f, 1f)] public float lookAheadBlend = 0f;

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
        if (chaseTarget == null && currentNode == null)
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

        Vector3 targetPos;

        if (chaseTarget != null)
        {
            targetPos = chaseTarget.position;
        }
        else
        {
            targetPos = currentNode.transform.position;

            // Anticipation du virage suivant (désactivé par défaut, lookAheadBlend=0) :
            // plus on se rapproche du noeud actuel, plus le point visé se mélange vers le
            // PROCHAIN noeud — le circuit se prend "au large" au lieu de piler pile sur
            // chaque point avant de tourner sec. N'affecte jamais la circulation normale.
            if (lookAheadBlend > 0f && currentNode.nextNodes != null && currentNode.nextNodes.Count > 0)
            {
                float distToNode = Vector3.Distance(transform.position, targetPos);
                float blendZone = waypointThreshold * 4f;
                float blendFactor = 1f - Mathf.Clamp01(distToNode / blendZone);
                if (blendFactor > 0f)
                {
                    Vector3 nextPos = currentNode.nextNodes[0].transform.position;
                    targetPos = Vector3.Lerp(targetPos, nextPos, blendFactor * lookAheadBlend);
                }
            }

            // Décalage perpendiculaire à la direction du noeud (0 = pas de changement) :
            // sans ça, plusieurs IA sur le même circuit visent EXACTEMENT le même point à
            // chaque virage et finissent en file indienne quasi parfaite.
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
                // En mode course (lookAheadBlend > 0), le choix reste TOUJOURS déterministe
                // (index 0) plutôt qu'aléatoire : sur un circuit qui devrait être un chemin
                // unique, un noeud mal configuré avec plusieurs "Next Node" (ou un
                // embranchement qui reboucle sur lui-même) pouvait faire tourner une IA en
                // rond indéfiniment ou lui faire perdre le fil du circuit. Le trafic normal
                // garde le choix aléatoire (comportement d'origine, utile pour varier les
                // trajets en ville).
                currentNode = lookAheadBlend > 0f
                    ? currentNode.nextNodes[0]
                    : currentNode.nextNodes[Random.Range(0, currentNode.nextNodes.Count)];
            }
        }

        // Calcul de la trajectoire idéale
        Vector3 localTarget = transform.InverseTransformPoint(targetPos);
        float angleToTarget = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float idealSteer = Mathf.Clamp(angleToTarget / 45f, -1f, 1f);

        // Intégration de l'évitement en douceur (steerBias)
        // Si un obstacle est sur le côté, l'IA décale son volant (idéal pour doubler)
        virtualSteeringWheel = Mathf.Clamp(idealSteer + steerBias, -1f, 1f);

        // Logique d'accélération et de freinage dans les virages
        float angleAbs = Mathf.Abs(angleToTarget);
        float currentSpeed = rb.linearVelocity.magnitude;

        if (steerBias != 0f && closestFrontDistance < 4f)
        {
            // On lâche l'accélérateur si on est en train de frôler un obstacle
            virtualGasPedal = 0.2f;
        }
        else if (angleAbs > 30f && currentSpeed > 10f)
        {
            virtualGasPedal = -0.6f; // Freinage avant un gros virage
        }
        else if (angleAbs > 15f)
        {
            virtualGasPedal = 0.4f; // Ralentissement
        }
        else
        {
            virtualGasPedal = 1f - (Mathf.Abs(virtualSteeringWheel) * 0.2f); // Pied au plancher
        }

        // Freinage d'urgence absolu devant un mur ou un piéton
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
            carController.isHandbraking = false;
        }
    }

    // --- LE SYSTÈME DE VISION HAUTE-FIDÉLITÉ ---
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