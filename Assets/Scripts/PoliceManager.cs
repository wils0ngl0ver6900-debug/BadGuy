using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class PoliceManager : MonoBehaviour
{
    public static PoliceManager Instance;

    [Header("Ressources (Prefabs) 🚓")]
    public GameObject copCarPrefab;

    [Header("Paramètres de Fuite 🚔")]
    public float escapeTimer = 0f;
    public float baseTimeToEscape = 15f;
    public bool isPlayerSpotted = false;
    public Vector3 lastKnownPosition;

    [Header("Gestion des Renforts 🚨")]
    public int maxCopsAllowed = 0;
    public List<GameObject> activeCops = new List<GameObject>();
    public float spawnCooldown = 5f;
    private float nextSpawnTime = 0f;

    [Header("Apparition Organique (Hors-Champ) 🗺️")]
    public float minSpawnDist = 60f;
    public float maxSpawnDist = 150f;

    [Header("Renforts à Pied 🚶")]
    public GameObject copPedestrianPrefab;
    public List<GameObject> activeFootCops = new List<GameObject>();
    public int maxFootCopsAllowed = 0;
    public float footSpawnCooldown = 6f;
    private float nextFootSpawnTime = 0f;
    public float minFootSpawnDist = 20f;
    public float maxFootSpawnDist = 50f;
    private const int MAX_FOOT_SPAWN_ATTEMPTS = 10;

    private Transform player;
    private Camera mainCam;
    private TrafficNode[] allNodes;
    private int lastStars = 0;

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
            lastStars = 0;
            return;
        }

        if (currentStars > lastStars) escapeTimer = baseTimeToEscape * currentStars;
        lastStars = currentStars;

        ManageEscape();
        UpdateMaxCops(currentStars);
        ManageReinforcements();

        UpdateMaxFootCops(currentStars);
        ManageFootReinforcements();
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
    }

    private void ManageEscape()
    {
        // --- CORRECTIF : On utilise la logique stabilisée du GameManager ! ---
        if (GameManager.Instance != null && GameManager.Instance.isEvading)
        {
            escapeTimer -= Time.deltaTime;

            if (escapeTimer <= 0)
            {
                GameManager.Instance.LoseCops();
                lastStars = 0;
            }
        }
        // J'ai supprimé la ligne qui forçait isPlayerSpotted à "false" en boucle et qui cassait l'UI.
    }

    private void UpdateMaxCops(int stars)
    {
        switch (stars)
        {
            case 1: maxCopsAllowed = 1; break;
            case 2: maxCopsAllowed = 3; break;
            case 3: maxCopsAllowed = 6; break;
            case 4: maxCopsAllowed = 10; break;
            case 5: maxCopsAllowed = 15; break;
            default: maxCopsAllowed = stars > 5 ? 15 : 1; break;
        }
    }

    // Les flics à pied montent en puissance plus progressivement que les voitures :
    // à 1-2 étoiles, c'est surtout des voitures qui patrouillent ; à partir de 3, du
    // monde à pied commence à converger vers ta position.
    private void UpdateMaxFootCops(int stars)
    {
        switch (stars)
        {
            case 1: maxFootCopsAllowed = 0; break;
            case 2: maxFootCopsAllowed = 1; break;
            case 3: maxFootCopsAllowed = 2; break;
            case 4: maxFootCopsAllowed = 4; break;
            case 5: maxFootCopsAllowed = 6; break;
            default: maxFootCopsAllowed = stars > 5 ? 6 : 0; break;
        }
    }

    private void ManageReinforcements()
    {
        activeCops.RemoveAll(item => item == null);

        if (activeCops.Count < maxCopsAllowed && Time.time >= nextSpawnTime)
        {
            SpawnCopOrganically();
            nextSpawnTime = Time.time + spawnCooldown;
        }
    }

    private void ManageFootReinforcements()
    {
        activeFootCops.RemoveAll(item => item == null);

        if (activeFootCops.Count < maxFootCopsAllowed && copPedestrianPrefab != null && Time.time >= nextFootSpawnTime)
        {
            SpawnFootCopOrganically();
            nextFootSpawnTime = Time.time + footSpawnCooldown;
        }
    }

    private void SpawnCopOrganically()
    {
        if (allNodes.Length == 0 || copCarPrefab == null) return;

        List<TrafficNode> validNodes = new List<TrafficNode>();

        foreach (TrafficNode node in allNodes)
        {
            float dist = Vector3.Distance(player.position, node.transform.position);

            if (dist >= minSpawnDist && dist <= maxSpawnDist)
            {
                Vector3 viewPos = mainCam.WorldToViewportPoint(node.transform.position);
                bool isOffScreen = viewPos.x < -0.1f || viewPos.x > 1.1f || viewPos.y < -0.1f || viewPos.y > 1.1f || viewPos.z < 0;

                if (isOffScreen) validNodes.Add(node);
            }
        }

        if (validNodes.Count > 0)
        {
            TrafficNode spawnNode = validNodes[Random.Range(0, validNodes.Count)];
            GameObject cop = Instantiate(copCarPrefab, spawnNode.transform.position, spawnNode.transform.rotation);
            CarAI ai = cop.GetComponent<CarAI>();
            if (ai != null) ai.currentNode = GetClosestNodeToPosition(lastKnownPosition);
            activeCops.Add(cop);
        }
    }

    private TrafficNode GetClosestNodeToPosition(Vector3 pos)
    {
        TrafficNode bestNode = null;
        float minDist = Mathf.Infinity;

        foreach (TrafficNode node in allNodes)
        {
            float dist = Vector3.Distance(pos, node.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                bestNode = node;
            }
        }
        return bestNode;
    }

    // Contrairement aux voitures (qui suivent le réseau TrafficNode), les flics à pied
    // apparaissent sur un point du NavMesh, dans un anneau autour du joueur et hors champ
    // (même vérification WorldToViewportPoint que pour les voitures un peu plus haut).
    private void SpawnFootCopOrganically()
    {
        if (player == null || copPedestrianPrefab == null) return;
        if (!TryFindOffscreenFootSpawnPoint(out Vector3 spawnPos)) return;

        GameObject cop = Instantiate(copPedestrianPrefab, spawnPos, Quaternion.identity);
        activeFootCops.Add(cop);
    }

    private bool TryFindOffscreenFootSpawnPoint(out Vector3 result)
    {
        result = player.position;
        if (mainCam == null) mainCam = Camera.main;

        for (int attempt = 0; attempt < MAX_FOOT_SPAWN_ATTEMPTS; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(minFootSpawnDist, maxFootSpawnDist);
            Vector3 candidate = player.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                continue;

            if (mainCam != null)
            {
                Vector3 viewPos = mainCam.WorldToViewportPoint(hit.position);
                bool isOffScreen = viewPos.x < -0.1f || viewPos.x > 1.1f || viewPos.y < -0.1f || viewPos.y > 1.1f || viewPos.z < 0;
                if (!isOffScreen) continue;
            }

            result = hit.position;
            return true;
        }

        return false;
    }

    public void DespawnAllCops()
    {
        foreach (GameObject cop in activeCops)
        {
            if (cop != null) Destroy(cop);
        }
        activeCops.Clear();

        // Les renforts à pied sont des flics "convoqués" pour l'occasion, tout comme les
        // voitures — ils repartent avec le niveau de recherche. Les Policier PRÉSENTS dans
        // le monde avant (patrouille de base, non trackés ici) ne sont pas concernés.
        foreach (GameObject cop in activeFootCops)
        {
            if (cop != null) Destroy(cop);
        }
        activeFootCops.Clear();

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