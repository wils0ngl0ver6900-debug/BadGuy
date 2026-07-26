using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// Placer ce script sur un GameObject vide, positionné dans la petite zone de deal
// à l'intérieur d'un district (ex: un coin de rue dans le quartier "Downtown" qui
// réclame de la cocaïne, un autre dans "Southside" qui réclame de la weed, etc.)
//
// Volontairement PAS un singleton (contrairement à beaucoup de managers du projet,
// voir l'audit) : plusieurs zones existent en même temps sur la carte, chacune
// indépendante. Chaque zone gère uniquement ses propres clients.
public class DrugDealZone : MonoBehaviour
{
    [Header("Drogue(s) demandée(s) dans cette zone")]
    [Tooltip("Une drogue est tirée au sort par client parmi celles-ci (ex: seulement Cocaïne = tous les clients de cette zone en veulent).")]
    public ItemData[] possibleDrugs;

    [Header("PNJ Client")]
    [Tooltip("Prefab avec : DrugClientNPC + Interactable (type = SellDrugs) + NavMeshAgent + Collider (Is Trigger).")]
    public GameObject clientPrefab;
    public Transform exitPoint; // Optionnel : point de sortie commun (ex: vers une ruelle). Sinon, départ aléatoire.

    [Header("Zone")]
    public float zoneRadius = 10f;
    public int maxClients = 3;
    public float minSpawnDelay = 8f;
    public float maxSpawnDelay = 20f;

    [Header("Réglages de la vente (appliqués à chaque client)")]
    public float saleDuration = 4f;
    [Range(0, 100)] public int saleFailChancePercent = 15;
    [Tooltip("Prix unitaire de secours, utilisé seulement si l'ItemData de la drogue n'a pas de valeur dans 'Value In Black Market'.")]
    public int saleReward = 150;

    private readonly List<GameObject> activeClients = new List<GameObject>();

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (activeClients.Count < maxClients && clientPrefab != null && possibleDrugs.Length > 0)
            {
                SpawnClient();
            }
            yield return new WaitForSeconds(Random.Range(minSpawnDelay, maxSpawnDelay));
        }
    }

    private void SpawnClient()
    {
        Vector3 spawnPos = transform.position;
        Vector2 randomCircle = Random.insideUnitCircle * zoneRadius;
        Vector3 candidate = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, zoneRadius + 2f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject clientObj = Instantiate(clientPrefab, spawnPos, Quaternion.identity);
        activeClients.Add(clientObj);

        ItemData drug = possibleDrugs[Random.Range(0, possibleDrugs.Length)];

        Interactable interactable = clientObj.GetComponent<Interactable>();
        if (interactable != null)
        {
            interactable.type = Interactable.ActionType.SellDrugs;
            interactable.desiredDrug = drug;
            interactable.saleDuration = saleDuration;
            interactable.saleFailChancePercent = saleFailChancePercent;
            interactable.saleReward = saleReward;
        }

        DrugClientNPC client = clientObj.GetComponent<DrugClientNPC>();
        if (client != null)
        {
            client.Initialize(transform.position, zoneRadius, exitPoint, this);
        }
    }

    // Appelé par DrugClientNPC quand il a fini de partir (vendu ou non) et se détruit
    public void NotifyClientLeft(GameObject client)
    {
        activeClients.Remove(client);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.2f, 0.9f, 0.35f);
        Gizmos.DrawSphere(transform.position, zoneRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, zoneRadius);
    }
}