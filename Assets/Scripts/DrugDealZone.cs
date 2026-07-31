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
    private Camera mainCam;
    private const int MAX_SPAWN_ATTEMPTS = 10;

    void Start()
    {
        mainCam = Camera.main;
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

    // Essaie plusieurs points dans la zone et ne retient que ceux hors du champ de la caméra
    // (même technique que PoliceManager pour les renforts : WorldToViewportPoint + marge).
    // Si aucun point hors champ n'est trouvé (le joueur couvre toute la zone du regard),
    // on ne spawn pas cette fois-ci — le SpawnLoop retentera au prochain intervalle.
    private bool TryFindOffscreenSpawnPoint(out Vector3 result)
    {
        result = transform.position;
        if (mainCam == null) mainCam = Camera.main;

        for (int attempt = 0; attempt < MAX_SPAWN_ATTEMPTS; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * zoneRadius;
            Vector3 candidate = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, zoneRadius + 2f, NavMesh.AllAreas))
                continue;

            if (mainCam != null)
            {
                Vector3 viewPos = mainCam.WorldToViewportPoint(hit.position);
                bool isOffScreen = viewPos.x < -0.1f || viewPos.x > 1.1f || viewPos.y < -0.1f || viewPos.y > 1.1f || viewPos.z < 0;
                if (!isOffScreen) continue; // Visible par le joueur : on retente un autre point
            }

            result = hit.position;
            return true;
        }

        return false;
    }

    private void SpawnClient()
    {
        if (!TryFindOffscreenSpawnPoint(out Vector3 spawnPos)) return;

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