using UnityEngine;
using UnityEngine.AI;

public class TargetHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Système de Butin (Loot) 💰")]
    public GameObject lootPrefab;
    public ItemData[] possibleDrops;
    [Range(0, 100)] public int dropChance = 50;

    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        DisableRagdoll();
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        NPCBrain npc = GetComponent<NPCBrain>();
        if (npc != null) npc.ForcePanic();

        currentHealth -= amount;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Touché ! -{amount} PV");

        if (GameManager.Instance != null) GameManager.Instance.ReportCrime(20);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Cible éliminée !");

        if (GameManager.Instance != null) GameManager.Instance.ReportCrime(40);

        GangObjective gangObj = GetComponent<GangObjective>();
        if (gangObj != null) gangObj.CompleteObjective();

        if (ContractManager.Instance != null) ContractManager.Instance.CompleteContract(ContractManager.ContractType.Hitman);

        SpawnLoot();

        EnableRagdoll();

        Destroy(gameObject, 15f);
    }

    private void DisableRagdoll()
    {
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs)
        {
            if (rb.gameObject == this.gameObject) continue;
            rb.isKinematic = true;
        }
    }

    private void EnableRagdoll()
    {
        // 1. Désactive les composants "vivants"
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        NPCBrain brain = GetComponent<NPCBrain>();
        if (brain != null)
        {
            // LE CORRECTIF EST ICI : On coupe brutalement le cerveau et l'action de tir !
            brain.StopAllCoroutines();
            brain.enabled = false;
        }

        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null) mainCollider.enabled = false;

        // 2. Active la physique des membres (Ragdoll)
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            rb.AddForce(-transform.forward * 5f + Vector3.up * 2f, ForceMode.Impulse);
        }
    }

    void SpawnLoot()
    {
        if (lootPrefab != null && possibleDrops.Length > 0)
        {
            if (Random.Range(0, 100) < dropChance)
            {
                ItemData droppedItem = possibleDrops[Random.Range(0, possibleDrops.Length)];
                GameObject loot = Instantiate(lootPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);

                LootItem lootScript = loot.GetComponent<LootItem>();
                if (lootScript != null) lootScript.itemToGive = droppedItem;
            }
        }
    }
}