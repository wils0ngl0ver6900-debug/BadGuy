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
        int diceRoll = Random.Range(1, 11);

        if (diceRoll <= 4) SpawnPolicePatrol();
        else if (diceRoll <= 7) SpawnStreetBrawl();
        else if (diceRoll <= 9) SpawnDriveBy();
        else
        {
            if (TerritoryManager.Instance != null && !TerritoryManager.Instance.isUnderAttack)
            {
                TerritoryManager.Instance.TriggerGangRetaliation();
            }
            else
            {
                SpawnStreetBrawl();
            }
        }
    }

    private TerritoryManager.Faction GetLocalDominantFaction()
    {
        if (TerritoryManager.Instance != null)
        {
            return TerritoryManager.Instance.GetDominantFactionInCurrentDistrict();
        }
        return TerritoryManager.Faction.None;
    }

    // --- VAGUE D'ASSAUT STRICTE ---
    public List<TargetHealth> SpawnTargetedAttackWave()
    {
        List<TargetHealth> spawnedEnemies = new List<TargetHealth>();
        if (gangPedestrianPrefabs.Length == 0 || player == null) return spawnedEnemies;

        int enemiesToSpawn = Random.Range(8, 13);

        TerritoryManager.Faction attackingFaction = (Random.value > 0.5f) ? TerritoryManager.Faction.Skulls : TerritoryManager.Faction.Vipers;

        List<GameObject> correctFactionPrefabs = new List<GameObject>();
        foreach (GameObject prefab in gangPedestrianPrefabs)
        {
            NPCBrain prefabBrain = prefab.GetComponent<NPCBrain>();
            if (prefabBrain != null && prefabBrain.faction == attackingFaction)
            {
                correctFactionPrefabs.Add(prefab);
            }
        }

        // LA NOUVELLE SÉCURITÉ : Si on ne trouve pas la bonne couleur, on affiche une erreur et on annule l'attaque
        if (correctFactionPrefabs.Count == 0)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification($"<color=red>ERREUR : Aucun Prefab avec la faction {attackingFaction} dans l'Inspecteur !</color>");
            }
            return spawnedEnemies; // Empêche l'escouade arc-en-ciel d'apparaître
        }

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            Vector3 randomDir = player.position + (Random.insideUnitSphere * Random.Range(15f, 30f));
            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 20f, 1))
            {
                GameObject gangPrefab = correctFactionPrefabs[Random.Range(0, correctFactionPrefabs.Count)];
                GameObject npcObj = Instantiate(gangPrefab, hit.position, Quaternion.identity);

                NPCBrain brain = npcObj.GetComponent<NPCBrain>();
                if (brain != null)
                {
                    brain.faction = attackingFaction;
                    brain.role = NPCBrain.NPCRole.Gang;
                    brain.ChangeState(NPCBrain.AIState.Combat);
                }

                TargetHealth th = npcObj.GetComponent<TargetHealth>();
                if (th != null) spawnedEnemies.Add(th);
            }
        }

        return spawnedEnemies;
    }

    // ----------------------------------------------------

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
            TerritoryManager.Faction localFaction = GetLocalDominantFaction();

            GameObject gangPrefab = gangPedestrianPrefabs[Random.Range(0, gangPedestrianPrefabs.Length)];
            NPCBrain prefabBrain = gangPrefab.GetComponent<NPCBrain>();

            int groupSize = 2;

            if (prefabBrain != null)
            {
                if (prefabBrain.faction == localFaction) groupSize = Random.Range(5, 9);
                else groupSize = Random.Range(1, 4);
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