using UnityEngine;

public class TargetHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Système de Butin (Loot) 💰")]
    public GameObject lootPrefab; // Le modèle 3D du sac/boîte par terre
    public ItemData[] possibleDrops; // Liste des objets qu'il peut faire tomber
    [Range(0, 100)] public int dropChance = 50; // % de chance qu'il fasse tomber un truc

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        // NOUVEAU : Si c'est un civil qui appelle la police, on lui coupe son appel !
        WanderingNPC npc = GetComponent<WanderingNPC>();
        if (npc != null) npc.CancelCall();

        currentHealth -= amount;
        UIManager.Instance.ShowNotification($"Touché ! -{amount} PV");
        GameManager.Instance.ReportCrime(20);
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        UIManager.Instance.ShowNotification("Cible éliminée !");

        if (GameManager.Instance != null)
            GameManager.Instance.ReportCrime(40);

        // ---> NOUVEAU : VÉRIFICATION GUERRE DE GANG <---
        GangObjective gangObj = GetComponent<GangObjective>();
        if (gangObj != null)
        {
            gangObj.CompleteObjective();
        }

        // --- VÉRIFICATION DU CONTRAT (Déjà présent) ---
        if (ContractManager.Instance != null)
        {
            ContractManager.Instance.CompleteContract(ContractManager.ContractType.Hitman);
        }

        SpawnLoot();
        Destroy(gameObject);
    }

    void SpawnLoot()
    {
        // On vérifie si on a bien configuré un Prefab et des objets possibles
        if (lootPrefab != null && possibleDrops.Length > 0)
        {
            // On lance les dés (0 à 100)
            int random = Random.Range(0, 100);

            if (random < dropChance)
            {
                // 1. On choisit un objet au hasard dans la liste
                ItemData droppedItem = possibleDrops[Random.Range(0, possibleDrops.Length)];

                // 2. On fait apparaître le modèle 3D par terre (légèrement surélevé pour ne pas être sous le sol)
                GameObject loot = Instantiate(lootPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);

                // 3. On glisse l'objet choisi dans le script du butin
                LootItem lootScript = loot.GetComponent<LootItem>();
                if (lootScript != null)
                {
                    lootScript.itemToGive = droppedItem;
                }
            }
        }
    }
}