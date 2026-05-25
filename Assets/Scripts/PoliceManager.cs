using UnityEngine;
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
        if (!isPlayerSpotted)
        {
            escapeTimer -= Time.deltaTime;

            if (escapeTimer <= 0)
            {
                GameManager.Instance.LoseCops();
                lastStars = 0;
            }
        }

        isPlayerSpotted = false;
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

    private void ManageReinforcements()
    {
        activeCops.RemoveAll(item => item == null);

        if (activeCops.Count < maxCopsAllowed && Time.time >= nextSpawnTime)
        {
            SpawnCopOrganically();
            nextSpawnTime = Time.time + spawnCooldown;
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

    // --- LE NETTOYEUR INVISIBLE ---
    public void DespawnAllCops()
    {
        foreach (GameObject cop in activeCops)
        {
            if (cop != null) Destroy(cop);
        }
        activeCops.Clear();

        NPCBrain[] allNPCs = FindObjectsOfType<NPCBrain>();
        foreach (NPCBrain npc in allNPCs)
        {
            if (npc.role == NPCBrain.NPCRole.Policier)
            {
                Destroy(npc.gameObject);
            }
        }

        lastStars = 0;
    }
}