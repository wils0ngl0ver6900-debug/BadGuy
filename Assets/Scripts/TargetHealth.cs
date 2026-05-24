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
        // NOUVEAU : On utilise le Cerveau (NPCBrain) au lieu de WanderingNPC
        NPCBrain npc = GetComponent<NPCBrain>();
        if (npc != null) npc.ForcePanic(); // Si c'est un civil qui prend une balle, il panique direct !

        currentHealth -= amount;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Touché ! -{amount} PV");

        if (GameManager.Instance != null) GameManager.Instance.ReportCrime(20); // FIX : Nouveau système d'étoiles

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Cible éliminée !");

        if (GameManager.Instance != null)
            GameManager.Instance.ReportCrime(40); // FIX : Nouveau système d'étoiles

        // ---> VÉRIFICATION GUERRE DE GANG <---
        GangObjective gangObj = GetComponent<GangObjective>();
        if (gangObj != null)
        {
            gangObj.CompleteObjective();
        }

        // --- VÉRIFICATION DU CONTRAT ---
        if (ContractManager.Instance != null)
        {
            ContractManager.Instance.CompleteContract(ContractManager.ContractType.Hitman);
        }

        SpawnLoot();
        Destroy(gameObject);
    }

    void SpawnLoot()
    {
        if (lootPrefab != null && possibleDrops.Length > 0)
        {
            int random = Random.Range(0, 100);

            if (random < dropChance)
            {
                ItemData droppedItem = possibleDrops[Random.Range(0, possibleDrops.Length)];
                GameObject loot = Instantiate(lootPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);

                LootItem lootScript = loot.GetComponent<LootItem>();
                if (lootScript != null)
                {
                    lootScript.itemToGive = droppedItem;
                }
            }
        }
    }
}