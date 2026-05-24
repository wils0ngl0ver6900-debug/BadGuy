using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance;

    [Header("Rythme des Événements ⏱️")]
    public float minTimeBetweenEvents = 30f; // Minimum 30 sec de calme
    public float maxTimeBetweenEvents = 90f; // Maximum 1m30 sans événement

    [Header("Zone d'Apparition 🗺️")]
    public float minSpawnRadius = 25f; // Pas trop près pour qu'on ne les voie pas popper
    public float maxSpawnRadius = 60f; // Pas trop loin pour qu'on puisse interagir

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

        // On charge tous les noeuds de la ville en mémoire au démarrage
        allNodes = FindObjectsOfType<TrafficNode>();

        // Lance la boucle infinie du Réalisateur
        StartCoroutine(DirectorLoop());
    }

    private IEnumerator DirectorLoop()
    {
        while (true)
        {
            // On attend un temps aléatoire
            float waitTime = Random.Range(minTimeBetweenEvents, maxTimeBetweenEvents);
            yield return new WaitForSeconds(waitTime);

            // On lance un événement si le joueur existe
            if (player != null)
            {
                TriggerRandomEvent();
            }
        }
    }

    private void TriggerRandomEvent()
    {
        // Tire un chiffre au hasard entre 0, 1 et 2
        int randomEvent = Random.Range(0, 3);

        switch (randomEvent)
        {
            case 0:
                SpawnPolicePatrol();
                break;
            case 1:
                SpawnDriveBy();
                break;
            case 2:
                SpawnStreetBrawl();
                break;
        }
    }

    // ==========================================
    // ÉVÉNEMENT 1 : PATROUILLE DE POLICE
    // ==========================================
    private void SpawnPolicePatrol()
    {
        if (copCarPrefab == null || allNodes.Length == 0) return;

        TrafficNode spawnNode = GetRandomNodeAroundPlayer();
        if (spawnNode != null)
        {
            GameObject copCar = Instantiate(copCarPrefab, spawnNode.transform.position, spawnNode.transform.rotation);
            CarAI ai = copCar.GetComponent<CarAI>();
            if (ai != null) ai.currentNode = spawnNode;

            Debug.Log("<color=blue>[Director]</color> Patrouille de police déployée dans le secteur.");
        }
    }

    // ==========================================
    // ÉVÉNEMENT 2 : VOITURE DE GANG (DRIVE-BY)
    // ==========================================
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

            // On pourrait booster la vitesse de cette voiture spécifique pour simuler une fuite
            CarController controller = gangCar.GetComponent<CarController>();
            if (controller != null) controller.maxSpeed += 10f;

            Debug.Log("<color=red>[Director]</color> Véhicule suspect (Gang) en approche rapide.");
        }
    }

    // ==========================================
    // ÉVÉNEMENT 3 : RIXE DE RUE (PIÉTONS)
    // ==========================================
    private void SpawnStreetBrawl()
    {
        if (gangPedestrianPrefabs.Length == 0) return;

        // On cherche un point aléatoire sur le trottoir (NavMesh) autour du joueur
        Vector3 randomDir = Random.insideUnitSphere * Random.Range(minSpawnRadius, maxSpawnRadius);
        randomDir += player.position;

        NavMeshHit hit;
        // 1 = Walkable area
        if (NavMesh.SamplePosition(randomDir, out hit, 10f, 1))
        {
            // Fait apparaître 2 à 4 membres de gangs
            int groupSize = Random.Range(2, 5);
            for (int i = 0; i < groupSize; i++)
            {
                // Disperse un peu les membres du groupe
                Vector3 spawnOffset = hit.position + (Random.insideUnitSphere * 2f);
                spawnOffset.y = hit.position.y; // Garde les pieds sur terre

                GameObject gangPrefab = gangPedestrianPrefabs[Random.Range(0, gangPedestrianPrefabs.Length)];
                Instantiate(gangPrefab, spawnOffset, Quaternion.identity);
            }

            Debug.Log("<color=orange>[Director]</color> Regroupement de gang généré dans une ruelle.");
        }
    }

    // ==========================================
    // FONCTION UTILITAIRE : Trouver une route
    // ==========================================
    private TrafficNode GetRandomNodeAroundPlayer()
    {
        List<TrafficNode> validNodes = new List<TrafficNode>();

        foreach (TrafficNode node in allNodes)
        {
            float dist = Vector3.Distance(player.position, node.transform.position);

            // Le noeud doit être ni trop près (pour pas pop sous les yeux du joueur), ni trop loin
            if (dist >= minSpawnRadius && dist <= maxSpawnRadius)
            {
                validNodes.Add(node);
            }
        }

        if (validNodes.Count > 0)
        {
            return validNodes[Random.Range(0, validNodes.Count)];
        }

        return null; // Aucun noeud valide trouvé
    }
}