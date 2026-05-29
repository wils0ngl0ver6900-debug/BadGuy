using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class TargetHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Système de Butin (Loot) 💰")]
    public GameObject lootPrefab;
    public ItemData[] possibleDrops;
    [Range(0, 100)] public int dropChance = 50;

    [HideInInspector] public bool isDead = false;

    private float spawnProtectionEndTime = 0f;

    void Start()
    {
        currentHealth = maxHealth;
        spawnProtectionEndTime = Time.time + 1.5f;
        DisableRagdoll();
    }

    public void TakeDamage(int amount, GameObject attacker = null)
    {
        if (isDead || Time.time < spawnProtectionEndTime) return;

        NPCBrain npc = GetComponent<NPCBrain>();
        if (npc != null) npc.ForcePanic();

        currentHealth -= amount;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Touché ! -{amount} PV");

        if (currentHealth <= 0)
        {
            Die(attacker);
        }
        else
        {
            // --- CORRECTIF : Notoriété uniquement quand le joueur blesse un civil/flic ---
            if (attacker != null && attacker.CompareTag("Player") && GameManager.Instance != null)
            {
                GameManager.Instance.ReportHitOrMurder();
            }
        }
    }

    void Die(GameObject attacker = null)
    {
        isDead = true;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Cible éliminée !");

        // --- CORRECTIF : On utilise la nouvelle limite max 4 étoiles ---
        if (attacker != null && attacker.CompareTag("Player") && GameManager.Instance != null)
        {
            GameManager.Instance.ReportHitOrMurder();
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

    public void TemporaryRagdoll(Vector3 pushForce)
    {
        if (isDead) return;
        StartCoroutine(TempRagdollRoutine(pushForce));
    }

    private IEnumerator TempRagdollRoutine(Vector3 pushForce)
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        NPCBrain brain = GetComponent<NPCBrain>();
        if (brain != null)
        {
            brain.StopAllCoroutines();
            brain.enabled = false;
        }

        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null) mainCollider.enabled = false;

        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        Transform hips = null;
        foreach (Rigidbody rb in rbs)
        {
            if (rb.gameObject == this.gameObject) continue;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddForce(pushForce, ForceMode.Impulse);
            if (hips == null) hips = rb.transform;
        }

        yield return new WaitForSeconds(3f);

        if (isDead) yield break;

        if (hips != null)
        {
            Vector3 newPos = hips.position;
            if (Physics.Raycast(hips.position + Vector3.up, Vector3.down, out RaycastHit hit, 3f))
            {
                newPos.y = hit.point.y;
            }
            transform.position = newPos;
        }

        DisableRagdoll();

        if (mainCollider != null) mainCollider.enabled = true;
        if (agent != null)
        {
            agent.Warp(transform.position);
            agent.enabled = true;
        }
        if (anim != null) anim.enabled = true;

        if (brain != null)
        {
            brain.enabled = true;
            brain.StartCoroutine("BrainTick");
        }
    }
}