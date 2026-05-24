using UnityEngine;
using System.Collections;

public class PoliceManager : MonoBehaviour
{
    public GameObject copPrefab; // Le Prefab du policier
    public int notorietyThreshold = 50; // À partir de combien d'étoiles/points ils arrivent ?
    public float spawnRadius = 15f; // À quelle distance du joueur ils apparaissent
    public float timeBetweenSpawns = 4f; // Temps entre chaque apparition

    private bool isSpawning = false;
    private Transform player;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        // Si on dépasse le seuil, l'alarme sonne !
        if (GameManager.Instance.notoriety >= notorietyThreshold)
        {
            if (!isSpawning) StartCoroutine(SpawnCopRoutine());
        }
        else
        {
            isSpawning = false; // On arrête d'en faire apparaître si la notoriété baisse
        }
    }

    IEnumerator SpawnCopRoutine()
    {
        isSpawning = true;
        while (GameManager.Instance.notoriety >= notorietyThreshold)
        {
            SpawnCop();
            yield return new WaitForSeconds(timeBetweenSpawns);
        }
        isSpawning = false;
    }

    void SpawnCop()
    {
        // Calcule une position aléatoire autour du joueur
        Vector2 randomCircle = Random.insideUnitCircle.normalized * spawnRadius;
        Vector3 spawnPos = player.position + new Vector3(randomCircle.x, 0, randomCircle.y);

        Instantiate(copPrefab, spawnPos, Quaternion.identity);
    }
}