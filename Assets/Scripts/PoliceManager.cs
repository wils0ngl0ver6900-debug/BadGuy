using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// Système de police entièrement reconstruit. Le problème de fond de l'ancienne version :
// le NOMBRE de flics montait bien avec les étoiles, mais leur COMPORTEMENT ne changeait
// jamais — CarAI.chaseTarget (déjà prévu pour la poursuite) et NPCBrain.AlertToAttack()
// (déjà prévu pour l'alerte/poursuite à pied) n'étaient JAMAIS appelés. Les flics roulaient
// donc en trafic normal, jamais en vraie poursuite, peu importe le niveau de recherche.
//
// Paliers d'escalade configurables par étoile (Tiers), au lieu de switch() figés dans le
// code — plus lisible, plus facile à équilibrer sans toucher au script.
[System.Serializable]
public class PoliceTier
{
    [Tooltip("Nombre d'étoiles concerné par ce palier (1 à 5, dans l'ordre du tableau).")]
    public int stars = 1;
    public int maxCopCars = 1;
    public int maxFootCops = 0;
    public float carSpawnCooldown = 5f;
    public float footSpawnCooldown = 6f;
    [Tooltip("Multiplie vitesse/accélération/freinage des voitures de police à ce palier (1 = valeurs de base du prefab).")]
    public float copAggressiveness = 1f;
    [Tooltip("À partir de ce palier, des barrages apparaissent devant le joueur (nécessite Roadblock Prefab).")]
    public bool spawnRoadblocks = false;
    [Tooltip("À partir de ce palier, un hélicoptère survole le joueur (nécessite Helicopter Prefab).")]
    public bool spawnHelicopter = false;
}

public class PoliceManager : MonoBehaviour
{
    public static PoliceManager Instance;

    [Header("Prefabs")]
    public GameObject copCarPrefab;
    public GameObject copPedestrianPrefab;
    [Tooltip("Optionnel — laisse vide tant que tu n'as pas de prefab dédié, les barrages seront simplement ignorés.")]
    public GameObject roadblockPrefab;
    [Tooltip("Optionnel — laisse vide tant que tu n'as pas de prefab dédié, l'hélicoptère sera simplement ignoré.")]
    public GameObject helicopterPrefab;

    [Header("Paliers d'escalade (une entrée par étoile, dans l'ordre 1 à 5)")]
    public PoliceTier[] tiers = new PoliceTier[]
    {
        new PoliceTier { stars = 1, maxCopCars = 1, maxFootCops = 0, copAggressiveness = 1f },
        new PoliceTier { stars = 2, maxCopCars = 3, maxFootCops = 1, copAggressiveness = 1.1f },
        new PoliceTier { stars = 3, maxCopCars = 6, maxFootCops = 2, copAggressiveness = 1.25f, spawnRoadblocks = true },
        new PoliceTier { stars = 4, maxCopCars = 10, maxFootCops = 4, copAggressiveness = 1.4f, spawnRoadblocks = true, spawnHelicopter = true },
        new PoliceTier { stars = 5, maxCopCars = 15, maxFootCops = 6, copAggressiveness = 1.6f, spawnRoadblocks = true, spawnHelicopter = true },
    };

    [Header("Fuite")]
    public float baseTimeToEscape = 15f;
    private float escapeTimer = 0f;

    [Header("Apparition organique — voitures (hors champ, anneau autour du joueur)")]
    public float minSpawnDist = 60f;
    public float maxSpawnDist = 150f;

    [Header("Apparition organique — à pied")]
    public float minFootSpawnDist = 20f;
    public float maxFootSpawnDist = 50f;
    private const int MAX_SPAWN_ATTEMPTS = 10;

    [Header("Barrages")]
    [Tooltip("Distance devant le joueur (dans son sens de déplacement) où placer un barrage.")]
    public float roadblockAheadDistance = 45f;
    public float roadblockCooldown = 20f;
    public int maxRoadblocks = 2;
    public float roadblockDespawnDist = 100f;

    [Header("Hélicoptère")]
    public float helicopterHeight = 25f;
    public float helicopterOrbitRadius = 15f;
    public float helicopterOrbitSpeed = 30f; // degrés/seconde

    public bool isPlayerSpotted = false;
    public Vector3 lastKnownPosition;

    private List<GameObject> activeCops = new List<GameObject>();
    private List<GameObject> activeFootCops = new List<GameObject>();
    private List<GameObject> activeRoadblocks = new List<GameObject>();
    private GameObject activeHelicopter;
    private float helicopterOrbitAngle = 0f;

    private float nextCarSpawnTime = 0f;
    private float nextFootSpawnTime = 0f;
    private float nextRoadblockTime = 0f;

    private Transform player;
    private Camera mainCam;
    private TrafficNode[] allNodes;
    private int lastStars = 0;

    private PoliceTier CurrentTier => (lastStars >= 1 && lastStars <= tiers.Length) ? tiers[lastStars - 1] : null;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        mainCam = Camera.main;
        allNodes = FindObjectsOfType<TrafficNode>();
    }

    void Update()
    {
        if (GameManager.Instance == null || player == null) return;

        int currentStars = GameManager.Instance.wantedLevel;

        if (currentStars == 0)
        {
            if (lastStars != 0) OnWantedLevelCleared();
            lastStars = 0;
            return;
        }

        if (currentStars > lastStars)
        {
            escapeTimer = baseTimeToEscape * currentStars;
            AlertAllActiveCops(); // ré-alerte tout le monde à chaque nouvelle étoile, pas juste les nouveaux spawns
        }
        lastStars = currentStars;

        ManageEscape();
        ManageCarReinforcements();
        ManageFootReinforcements();
        ManageRoadblocks();
        ManageHelicopter();
    }

    public void ReportPlayerSight(Vector3 pos)
    {
        isPlayerSpotted = true;
        lastKnownPosition = pos;

        if (GameManager.Instance != null)
        {
            int currentStars = GameManager.Instance.wantedLevel;
            if (currentStars == 0) currentStars = 1;
            escapeTimer = baseTimeToEscape * currentStars;
        }

        AlertAllActiveCops();
    }

    // LE CŒUR DE LA REFONTE : assigne réellement chaseTarget (voitures) et déclenche
    // AlertToAttack (à pied) — c'est ce qui manquait entièrement avant. Appelé à chaque
    // nouvelle étoile ET à chaque nouveau signalement, pour que les flics déjà actifs
    // réagissent aussi, pas seulement les nouveaux qui apparaissent.
    //
    // Verrou anti-récursion INDISPENSABLE : NPCBrain.AlertToAttack() (pour un Policier)
    // rappelle PoliceManager.ReportPlayerSight() en toute fin de méthode ("appel radio" —
    // code déjà existant, légitime, pas touché ici). Sans ce verrou, ça formait exactement
    // ce cycle : AlertAllActiveCops -> AlertToAttack -> ReportPlayerSight ->
    // AlertAllActiveCops -> ... jusqu'au stack overflow (planté en jeu en tirant près d'un
    // policier). Le verrou empêche un second passage tant qu'un premier est en cours,
    // peu importe lequel des deux points d'entrée (Update() ou ReportPlayerSight) l'a lancé.
    private bool isAlertingCops = false;

    private void AlertAllActiveCops()
    {
        if (isAlertingCops) return;
        isAlertingCops = true;

        try
        {
            foreach (GameObject cop in activeCops)
            {
                if (cop == null) continue;
                CarAI ai = cop.GetComponent<CarAI>();
                if (ai != null) ai.chaseTarget = player;
            }

            foreach (GameObject cop in activeFootCops)
            {
                if (cop == null) continue;
                NPCBrain brain = cop.GetComponent<NPCBrain>();
                if (brain != null) brain.AlertToAttack(lastKnownPosition);
            }
        }
        finally
        {
            isAlertingCops = false;
        }
    }

    private void OnWantedLevelCleared()
    {
        // Les voitures arrêtent de poursuivre (repassent en trafic normal) plutôt que
        // d'être détruites brutalement — plus naturel que la disparition instantanée.
        foreach (GameObject cop in activeCops)
        {
            if (cop == null) continue;
            CarAI ai = cop.GetComponent<CarAI>();
            if (ai != null) ai.chaseTarget = null;
        }

        DespawnAllRoadblocks();
        DespawnHelicopter();
    }

    private void ManageEscape()
    {
        if (GameManager.Instance != null && GameManager.Instance.isEvading)
        {
            escapeTimer -= Time.deltaTime;

            if (escapeTimer <= 0)
            {
                GameManager.Instance.LoseCops();
                lastStars = 0;
                OnWantedLevelCleared();
            }
        }
    }

    private void ManageCarReinforcements()
    {
        activeCops.RemoveAll(item => item == null);
        PoliceTier tier = CurrentTier;
        if (tier == null) return;

        if (activeCops.Count < tier.maxCopCars && Time.time >= nextCarSpawnTime)
        {
            SpawnCopOrganically(tier);
            nextCarSpawnTime = Time.time + tier.carSpawnCooldown;
        }
    }

    private void ManageFootReinforcements()
    {
        activeFootCops.RemoveAll(item => item == null);
        PoliceTier tier = CurrentTier;
        if (tier == null) return;

        if (activeFootCops.Count < tier.maxFootCops && copPedestrianPrefab != null && Time.time >= nextFootSpawnTime)
        {
            SpawnFootCopOrganically();
            nextFootSpawnTime = Time.time + tier.footSpawnCooldown;
        }
    }

    private void SpawnCopOrganically(PoliceTier tier)
    {
        if (allNodes.Length == 0 || copCarPrefab == null) return;

        List<TrafficNode> validNodes = new List<TrafficNode>();
        foreach (TrafficNode node in allNodes)
        {
            float dist = Vector3.Distance(player.position, node.transform.position);
            if (dist >= minSpawnDist && dist <= maxSpawnDist && !IsOnScreen(node.transform.position))
                validNodes.Add(node);
        }

        if (validNodes.Count == 0) return;

        TrafficNode spawnNode = validNodes[Random.Range(0, validNodes.Count)];
        GameObject cop = Instantiate(copCarPrefab, spawnNode.transform.position, spawnNode.transform.rotation);

        CarAI ai = cop.GetComponent<CarAI>();
        if (ai != null)
        {
            ai.currentNode = spawnNode;
            ai.chaseTarget = player; // poursuite active dès l'apparition, pas juste du trafic normal
        }

        CarController cc = cop.GetComponent<CarController>();
        if (cc != null && tier.copAggressiveness != 1f)
        {
            cc.maxSpeed *= tier.copAggressiveness;
            cc.accelerationForce *= tier.copAggressiveness;
            cc.brakingForce *= tier.copAggressiveness;
        }

        activeCops.Add(cop);
    }

    private void SpawnFootCopOrganically()
    {
        if (player == null || copPedestrianPrefab == null) return;
        if (!TryFindOffscreenNavMeshPoint(minFootSpawnDist, maxFootSpawnDist, out Vector3 spawnPos)) return;

        GameObject cop = Instantiate(copPedestrianPrefab, spawnPos, Quaternion.identity);
        NPCBrain brain = cop.GetComponent<NPCBrain>();
        if (brain != null) brain.AlertToAttack(lastKnownPosition); // converge direct, pas de patrouille passive d'abord

        activeFootCops.Add(cop);
    }

    // --- Barrages ---
    private void ManageRoadblocks()
    {
        if (roadblockPrefab == null) return;
        PoliceTier tier = CurrentTier;
        if (tier == null || !tier.spawnRoadblocks) return;

        activeRoadblocks.RemoveAll(item => item == null);

        // Recycle les barrages trop loin derrière (le joueur a changé de direction ou les a dépassés).
        for (int i = activeRoadblocks.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(player.position, activeRoadblocks[i].transform.position) > roadblockDespawnDist)
            {
                Destroy(activeRoadblocks[i]);
                activeRoadblocks.RemoveAt(i);
            }
        }

        if (activeRoadblocks.Count >= maxRoadblocks || Time.time < nextRoadblockTime) return;

        // Placé devant le joueur dans son sens de déplacement (approximé par transform.forward,
        // valable à pied comme au volant) plutôt qu'une position aléatoire.
        Vector3 aheadPos = player.position + player.forward * roadblockAheadDistance;
        if (!NavMesh.SamplePosition(aheadPos, out NavMeshHit hit, 15f, NavMesh.AllAreas)) return;

        GameObject block = Instantiate(roadblockPrefab, hit.position, Quaternion.LookRotation(player.forward));
        activeRoadblocks.Add(block);
        nextRoadblockTime = Time.time + roadblockCooldown;
    }

    private void DespawnAllRoadblocks()
    {
        foreach (GameObject block in activeRoadblocks)
        {
            if (block != null) Destroy(block);
        }
        activeRoadblocks.Clear();
    }

    // --- Hélicoptère ---
    private void ManageHelicopter()
    {
        if (helicopterPrefab == null) return;
        PoliceTier tier = CurrentTier;

        if (tier == null || !tier.spawnHelicopter)
        {
            DespawnHelicopter();
            return;
        }

        if (activeHelicopter == null)
        {
            activeHelicopter = Instantiate(helicopterPrefab, player.position + Vector3.up * helicopterHeight, Quaternion.identity);
        }

        // Orbite lente au-dessus du joueur plutôt qu'immobile pile dessus — plus vivant,
        // et laisse un peu de marge de manœuvre visuelle si le joueur est sous un pont/toit.
        helicopterOrbitAngle += helicopterOrbitSpeed * Time.deltaTime;
        Vector3 offset = new Vector3(Mathf.Cos(helicopterOrbitAngle * Mathf.Deg2Rad), 0f, Mathf.Sin(helicopterOrbitAngle * Mathf.Deg2Rad)) * helicopterOrbitRadius;
        activeHelicopter.transform.position = player.position + Vector3.up * helicopterHeight + offset;
        activeHelicopter.transform.LookAt(player.position + Vector3.up * 1f);
    }

    private void DespawnHelicopter()
    {
        if (activeHelicopter != null)
        {
            Destroy(activeHelicopter);
            activeHelicopter = null;
        }
    }

    private bool TryFindOffscreenNavMeshPoint(float minDist, float maxDist, out Vector3 result)
    {
        result = player.position;
        if (mainCam == null) mainCam = Camera.main;

        for (int attempt = 0; attempt < MAX_SPAWN_ATTEMPTS; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(minDist, maxDist);
            Vector3 candidate = player.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                continue;
            if (IsOnScreen(hit.position))
                continue;

            result = hit.position;
            return true;
        }
        return false;
    }

    private bool IsOnScreen(Vector3 worldPos)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return false;
        Vector3 viewPos = mainCam.WorldToViewportPoint(worldPos);
        return !(viewPos.x < -0.1f || viewPos.x > 1.1f || viewPos.y < -0.1f || viewPos.y > 1.1f || viewPos.z < 0);
    }

    public void DespawnAllCops()
    {
        foreach (GameObject cop in activeCops)
        {
            if (cop != null) Destroy(cop);
        }
        activeCops.Clear();

        foreach (GameObject cop in activeFootCops)
        {
            if (cop != null) Destroy(cop);
        }
        activeFootCops.Clear();

        DespawnAllRoadblocks();
        DespawnHelicopter();

        NPCBrain[] allNPCs = FindObjectsOfType<NPCBrain>();
        foreach (NPCBrain npc in allNPCs)
        {
            if (npc != null && npc.role == NPCBrain.NPCRole.Policier)
            {
                TargetHealth health = npc.GetComponent<TargetHealth>();
                if (health != null && health.isDead)
                {
                    Destroy(npc.gameObject);
                }
            }
        }

        lastStars = 0;
    }
}