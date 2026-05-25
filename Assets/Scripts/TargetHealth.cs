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

    [HideInInspector] public bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        DisableRagdoll();
    }

    // MODIFICATION : On accepte un paramètre optionnel 'attacker'
    public void TakeDamage(int amount, GameObject attacker = null)
    {
        if (isDead) return;

        NPCBrain npc = GetComponent<NPCBrain>();
        if (npc != null) npc.ForcePanic();

        currentHealth -= amount;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Touché ! -{amount} PV");

        // CORRECTIF CRUCIAL : On ne rapporte le crime que si c'est le JOUEUR qui a infligé les dégâts !
        if (attacker != null && attacker.CompareTag("Player") && GameManager.Instance != null)
        {
            GameManager.Instance.ReportCrime(20);
        }

        if (currentHealth <= 0)
        {
            Die(attacker);
        }
    }

    void Die(GameObject attacker = null)
    {
        isDead = true;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Cible éliminée !");

        // CORRECTIF CRUCIAL : On ne rapporte le crime de meurtre que si c'est le JOUEUR le tueur !
        if (attacker != null && attacker.CompareTag("Player") && GameManager.Instance != null)
        {
            GameManager.Instance.ReportCrime(40);
        }

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
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        NPCBrain brain = GetComponent<NPCBrain>();
        if (brain != null)
        {
            brain.StopAllCoroutines();
            if (brain.muzzleFlashLight != null) brain.muzzleFlashLight.enabled = false;
            brain.enabled = false;
        }

        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null) mainCollider.enabled = false;

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