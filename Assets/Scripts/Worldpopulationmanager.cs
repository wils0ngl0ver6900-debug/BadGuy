using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// Génération dynamique de piétons civils et de voitures de trafic autour du joueur, façon
// GTA : apparaissent hors champ dans un anneau autour du joueur, disparaissent une fois
// trop loin derrière. Réutilise le même principe éprouvé que PoliceManager (apparition
// organique hors-champ) plutôt que d'inventer un système différent.
//
// Les tableaux de prefabs acceptent plusieurs entrées dès maintenant, même si tu n'en as
// qu'un seul pour l'instant — facile d'ajouter de la variété plus tard sans retoucher au
// script, juste en glissant d'autres prefabs dans Pedestrian Prefabs / Car Prefabs.
public class WorldPopulationManager : MonoBehaviour
{
    public static WorldPopulationManager Instance;

    [Header("Prefabs (plusieurs acceptés, un seul pour l'instant)")]
    public GameObject[] pedestrianPrefabs;
    public GameObject[] carPrefabs;

    [Header("Piétons")]
    public int maxPedestrians = 15;
    public float pedestrianMinSpawnDist = 25f;
    public float pedestrianMaxSpawnDist = 60f;
    [Tooltip("Au-delà de cette distance du joueur, un piéton généré est détruit (recyclage, performance).")]
    public float pedestrianDespawnDist = 90f;
    public float pedestrianSpawnCooldown = 2f;

    [Header("Voitures")]
    public int maxCars = 10;
    public float carMinSpawnDist = 40f;
    public float carMaxSpawnDist = 120f;
    public float carDespawnDist = 160f;
    public float carSpawnCooldown = 3f;

    [Header("Course en cours")]
    [Tooltip("Distance (m) autour de CHAQUE point du circuit de course considérée comme \"zone de course\" — aucune génération de piéton/voiture n'y a lieu tant qu'une course est active (StreetRaceManager.IsRaceActive()). Le reste de la ville continue de vivre normalement.")]
    public float raceZoneRadius = 25f;
    [Tooltip("Coché : au lancement d'une course, détruit aussi les piétons/voitures déjà générés qui se trouvent dans la zone de course (pas ceux placés à la main dans la scène — uniquement ceux issus de ce script).")]
    public bool clearZoneOnRaceStart = true;
    private bool wasRaceActiveLastFrame = false;

    private const int MAX_SPAWN_ATTEMPTS = 10;

    private List<GameObject> activePedestrians = new List<GameObject>();
    private List<GameObject> activeCars = new List<GameObject>();

    private Transform player;
    private Camera mainCam;
    private TrafficNode[] allNodes;
    private float nextPedSpawnTime = 0f;
    private float nextCarSpawnTime = 0f;

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
        if (player == null) return;

        bool raceActiveNow = StreetRaceManager.Instance != null && StreetRaceManager.Instance.IsRaceActive();
        if (raceActiveNow && !wasRaceActiveLastFrame && clearZoneOnRaceStart)
        {
            ClearGeneratedEntitiesInRaceZone();
        }
        wasRaceActiveLastFrame = raceActiveNow;

        DespawnFarEntities();
        ManagePedestrianSpawning();
        ManageCarSpawning();
    }

    // Ne détruit QUE les entités générées par ce script (activePedestrians/activeCars) —
    // jamais les PNJ/voitures placés à la main dans la scène, qui ne sont pas dans ces listes.
    private void ClearGeneratedEntitiesInRaceZone()
    {
        for (int i = activePedestrians.Count - 1; i >= 0; i--)
        {
            if (activePedestrians[i] != null && IsInsideActiveRaceZone(activePedestrians[i].transform.position))
            {
                Destroy(activePedestrians[i]);
                activePedestrians.RemoveAt(i);
            }
        }

        for (int i = activeCars.Count - 1; i >= 0; i--)
        {
            if (activeCars[i] != null && IsInsideActiveRaceZone(activeCars[i].transform.position))
            {
                Destroy(activeCars[i]);
                activeCars.RemoveAt(i);
            }
        }
    }

    // Vrai si une course est active ET que la position donnée est proche d'un point du
    // circuit — indépendant de raceActiveNow (cache déjà lu une fois par frame dans
    // Update, mais revérifié ici pour rester utilisable seul si appelé ailleurs).
    private bool IsInsideActiveRaceZone(Vector3 pos)
    {
        if (StreetRaceManager.Instance == null || !StreetRaceManager.Instance.IsRaceActive()) return false;

        RaceCircuit circuit = StreetRaceManager.Instance.raceCircuit;
        if (circuit == null || circuit.Count == 0) return false;

        for (int i = 0; i < circuit.Count; i++)
        {
            if (Vector3.Distance(pos, circuit.GetPoint(i)) < raceZoneRadius) return true;
        }
        return false;
    }

    private void DespawnFarEntities()
    {
        activePedestrians.RemoveAll(p => p == null);
        for (int i = activePedestrians.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(player.position, activePedestrians[i].transform.position) > pedestrianDespawnDist)
            {
                Destroy(activePedestrians[i]);
                activePedestrians.RemoveAt(i);
            }
        }

        activeCars.RemoveAll(c => c == null);
        for (int i = activeCars.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(player.position, activeCars[i].transform.position) > carDespawnDist)
            {
                Destroy(activeCars[i]);
                activeCars.RemoveAt(i);
            }
        }
    }

    private void ManagePedestrianSpawning()
    {
        if (pedestrianPrefabs == null || pedestrianPrefabs.Length == 0) return;
        if (activePedestrians.Count >= maxPedestrians || Time.time < nextPedSpawnTime) return;
        if (!TryFindOffscreenNavMeshPoint(pedestrianMinSpawnDist, pedestrianMaxSpawnDist, out Vector3 pos)) return;

        GameObject prefab = pedestrianPrefabs[Random.Range(0, pedestrianPrefabs.Length)];
        GameObject ped = Instantiate(prefab, pos, Quaternion.identity);
        activePedestrians.Add(ped);
        nextPedSpawnTime = Time.time + pedestrianSpawnCooldown;
    }

    private void ManageCarSpawning()
    {
        if (carPrefabs == null || carPrefabs.Length == 0 || allNodes.Length == 0) return;
        if (activeCars.Count >= maxCars || Time.time < nextCarSpawnTime) return;
        if (!TryFindOffscreenTrafficNode(carMinSpawnDist, carMaxSpawnDist, out TrafficNode node)) return;

        GameObject prefab = carPrefabs[Random.Range(0, carPrefabs.Length)];
        GameObject car = Instantiate(prefab, node.transform.position, node.transform.rotation);

        CarAI ai = car.GetComponent<CarAI>();
        if (ai != null) ai.currentNode = node;

        activeCars.Add(car);
        nextCarSpawnTime = Time.time + carSpawnCooldown;
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
            if (IsInsideActiveRaceZone(hit.position))
                continue;

            result = hit.position;
            return true;
        }
        return false;
    }

    private bool TryFindOffscreenTrafficNode(float minDist, float maxDist, out TrafficNode result)
    {
        result = null;
        List<TrafficNode> valid = new List<TrafficNode>();

        foreach (TrafficNode node in allNodes)
        {
            float dist = Vector3.Distance(player.position, node.transform.position);
            if (dist >= minDist && dist <= maxDist && !IsOnScreen(node.transform.position) && !IsInsideActiveRaceZone(node.transform.position))
                valid.Add(node);
        }

        if (valid.Count == 0) return false;
        result = valid[Random.Range(0, valid.Count)];
        return true;
    }

    private bool IsOnScreen(Vector3 worldPos)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return false;
        Vector3 viewPos = mainCam.WorldToViewportPoint(worldPos);
        return !(viewPos.x < -0.1f || viewPos.x > 1.1f || viewPos.y < -0.1f || viewPos.y > 1.1f || viewPos.z < 0);
    }
}