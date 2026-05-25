using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance;

    [Header("Rythme des Événements ⏱️")]
    public float minTimeBetweenEvents = 30f;
    public float maxTimeBetweenEvents = 90f;

    [Header("Zone d'Apparition 🗺️")]
    public float minSpawnRadius = 25f;
    public float maxSpawnRadius = 60f;

    [Header("Ressources (Prefabs) 📦")]
    public GameObject copCarPrefab;
    public GameObject[] gangCarPrefabs;
    public GameObject[] gangPedestrianPrefabs;

    private Transform player;
    private TrafficNode[] allNodes;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        allNodes = FindObjectsOfType<TrafficNode>();
        StartCoroutine(DirectorLoop());
    }

    private IEnumerator DirectorLoop()
    {
        while (true)
        {
            float waitTime = Random.Range(minTimeBetweenEvents, maxTimeBetweenEvents);
            yield return new WaitForSeconds(waitTime);

            if (player != null) TriggerRandomEvent();
        }
    }

    private void TriggerRandomEvent()
    {
        int randomEvent = Random.Range(0, 3);
        switch (randomEvent)
        {
            case 0: SpawnPolicePatrol(); break;
            case 1: SpawnDriveBy(); break;
            case 2: SpawnStreetBrawl(); break;
        }
    }

    // CORRECTION ICI : Fait le lien direct avec ton TerritoryManager
    private TerritoryManager.Faction GetLocalDominantFaction()
    {
        if (TerritoryManager.Instance != null)
        {
            return TerritoryManager.Instance.GetDominantFactionInCurrentDistrict();
        }
        return TerritoryManager.Faction.None;
    }

    private void SpawnPolicePatrol()
    {
        if (copCarPrefab == null || allNodes.Length == 0) return;

        TrafficNode spawnNode = GetRandomNodeAroundPlayer();
        if (spawnNode != null)
        {
            TerritoryManager.Faction localFaction = GetLocalDominantFaction();
            if (localFaction != TerritoryManager.Faction.None && Random.value > 0.5f) return;

            GameObject copCar = Instantiate(copCarPrefab, spawnNode.transform.position, spawnNode.transform.rotation);
            CarAI ai = copCar.GetComponent<CarAI>();
            if (ai != null) ai.currentNode = spawnNode;
        }
    }

    private void SpawnDriveBy()
    {
        if (gangCarPrefabs.Length == 0 || allNodes.Length == 0) return;

        TrafficNode spawnNode = GetRandomNodeAroundPlayer();
        if (spawnNode != null)
        {
            GameObject carPrefab = gangCarPrefabs[Random.Range(0, gangCarPrefabs.Length)];
            GameObject gangCar = Instantiate(carPrefab, spawnNode.transform.position, spawnNode.transform.rotation);

            CarAI ai = gangCar.GetComponent<CarAI>();
            if (ai != null) ai.currentNode = spawnNode;

            CarController controller = gangCar.GetComponent<CarController>();
            if (controller != null) controller.maxSpeed += 10f;
        }
    }

    private void SpawnStreetBrawl()
    {
        if (gangPedestrianPrefabs.Length == 0) return;

        Vector3 randomDir = Random.insideUnitSphere * Random.Range(minSpawnRadius, maxSpawnRadius);
        randomDir += player.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDir, out hit, 10f, 1))
        {
            // Récupère la faction du quartier actuel !
            TerritoryManager.Faction localFaction = GetLocalDominantFaction();

            GameObject gangPrefab = gangPedestrianPrefabs[Random.Range(0, gangPedestrianPrefabs.Length)];
            NPCBrain prefabBrain = gangPrefab.GetComponent<NPCBrain>();

            int groupSize = 2;

            if (prefabBrain != null)
            {
                if (prefabBrain.faction == localFaction) groupSize = Random.Range(5, 9); // Surnombre chez eux !
                else groupSize = Random.Range(1, 4); // Minorité
            }

            for (int i = 0; i < groupSize; i++)
            {
                Vector3 spawnOffset = hit.position + (Random.insideUnitSphere * 3f);
                spawnOffset.y = hit.position.y;
                Instantiate(gangPrefab, spawnOffset, Quaternion.identity);
            }
        }
    }

    private TrafficNode GetRandomNodeAroundPlayer()
    {
        List<TrafficNode> validNodes = new List<TrafficNode>();
        foreach (TrafficNode node in allNodes)
        {
            float dist = Vector3.Distance(player.position, node.transform.position);
            if (dist >= minSpawnRadius && dist <= maxSpawnRadius)
            {
                validNodes.Add(node);
            }
        }

        if (validNodes.Count > 0) return validNodes[Random.Range(0, validNodes.Count)];
        return null;
    }
}