using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class BoneTransform
{
    public Transform bone;
    public Vector3 originalLocalPos;
    public Quaternion originalLocalRot;
}

public class TargetHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Système de Butin (Loot) 💰")]
    public GameObject lootPrefab;
    public ItemData[] possibleDrops;
    [Range(0, 100)] public int dropChance = 50;

    [HideInInspector] public bool isDead = false;
    [HideInInspector] public bool isKnockedOut = false;

    private float spawnProtectionEndTime = 0f;
    private List<BoneTransform> boneSnapshots = new List<BoneTransform>();

    void Start()
    {
        currentHealth = maxHealth;
        spawnProtectionEndTime = Time.time + 1.5f;

        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs)
        {
            if (rb.gameObject == this.gameObject) continue;

            boneSnapshots.Add(new BoneTransform
            {
                bone = rb.transform,
                originalLocalPos = rb.transform.localPosition,
                originalLocalRot = rb.transform.localRotation
            });
        }

        DisableRagdoll();
    }

    public void TakeDamage(int amount, GameObject attacker = null, bool isMelee = false)
    {
        if (isDead || Time.time < spawnProtectionEndTime) return;

        NPCBrain npc = GetComponent<NPCBrain>();
        Debug.Log($"[TargetHealth] TakeDamage sur '{gameObject.name}' — NPCBrain trouvé : {npc != null}{(npc != null ? $", rôle : {npc.role}" : "")}, attacker : {(attacker != null ? attacker.name : "null")}");
        if (npc != null)
        {
            if (attacker != null)
            {
                npc.AlertToAttack(attacker.transform.position);
            }
            else
            {
                npc.ForcePanic();
            }
        }

        currentHealth -= amount;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Touché ! -{amount} PV");

        if (currentHealth <= 0)
        {
            Die(attacker, isMelee);
        }
        else
        {
            if (attacker != null && attacker.CompareTag("Player") && GameManager.Instance != null)
            {
                GameManager.Instance.ReportHitOrMurder(isMelee);
            }
        }
    }

    void Die(GameObject attacker = null, bool isMelee = false)
    {
        isDead = true;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Cible éliminée !");

        if (isMelee && attacker != null && attacker.CompareTag("Player") && GameManager.Instance != null)
        {
            GameManager.Instance.RegisterUnarmedKill();
        }

        if (attacker != null && attacker.CompareTag("Player") && GameManager.Instance != null)
        {
            GameManager.Instance.ReportHitOrMurder(isMelee);
        }

        GangObjective gangObj = GetComponent<GangObjective>();
        if (gangObj != null) gangObj.CompleteObjective();

        if (ContractManager.Instance != null) ContractManager.Instance.CompleteContract(ContractManager.ContractType.Hitman);

        // ---> CORRECTIF POUR LA QUÊTE <---
        NPCBrain brain = GetComponent<NPCBrain>();
        if (brain != null && QuestManager.Instance != null)
        {
            // On valide l'action de tuer, et on envoie la faction du mort (Vipers, Skulls...) comme nom de cible !
            QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.TuerEnnemi, 1, brain.faction.ToString());
        }

        SpawnLoot();
        EnableRagdoll();

        StartCoroutine(SpawnBloodPoolDelayed());

        Destroy(gameObject, 15f);
    }

    private IEnumerator SpawnBloodPoolDelayed()
    {
        yield return new WaitForSeconds(1.5f); // Laisse le ragdoll retomber avant de placer la flaque

        // Le Transform racine ne suit PAS le ragdoll (seuls les os/Rigidbody enfants bougent
        // physiquement) — il reste à l'endroit où le PNJ marchait à sa mort. On calcule donc
        // la position réelle du corps en moyennant les os du ragdoll, sinon la flaque apparaît
        // loin du cadavre.
        Vector3 corpsePosition = transform.position;
        Rigidbody[] ragdollBones = GetComponentsInChildren<Rigidbody>();
        if (ragdollBones.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Rigidbody rb in ragdollBones)
            {
                if (rb.gameObject == this.gameObject) continue;
                sum += rb.position;
                count++;
            }
            if (count > 0) corpsePosition = sum / count;
        }

        VFXHelper.SpawnGrowingBloodPool(corpsePosition, transform);
    }

    private void DisableRagdoll()
    {
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs)
        {
            if (rb.gameObject == this.gameObject) continue;
            // Vitesse à zéro AVANT de passer en kinematic, pas après : une fois kinematic,
            // Unity refuse (avec un warning) qu'on lui fixe une vélocité — ce qui se
            // répétait des centaines de fois au chargement, un par PNJ de la scène.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
        if (isDead || isKnockedOut || Time.time < spawnProtectionEndTime) return;
        StartCoroutine(TempRagdollRoutine(pushForce));
    }

    private IEnumerator TempRagdollRoutine(Vector3 pushForce)
    {
        isKnockedOut = true;

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

        if (isDead)
        {
            isKnockedOut = false;
            yield break;
        }

        if (hips != null)
        {
            // On se replace d'abord horizontalement là où les hanches ont fini leur course
            // (X/Z), la hauteur exacte est corrigée juste en dessous une fois la pose
            // debout restaurée.
            Vector3 newPos = transform.position;
            newPos.x = hips.position.x;
            newPos.z = hips.position.z;
            transform.position = newPos;
        }

        DisableRagdoll();

        foreach (var snap in boneSnapshots)
        {
            snap.bone.localPosition = snap.originalLocalPos;
            snap.bone.localRotation = snap.originalLocalRot;
        }

        if (mainCollider != null) mainCollider.enabled = true;

        // Recalage au sol : fait ICI, une fois la pose debout restaurée (pas pendant que le
        // ragdoll est encore affalé), et basé sur le BAS du collider plutôt que sur les
        // hanches. Avant, transform.position.y était calé sur la hauteur de sol détectée
        // depuis les hanches — comme les hanches sont bien plus haut que les pieds, le
        // personnage se retrouvait planté avec le bassin au niveau du sol (à moitié enfoncé
        // dedans) au lieu des pieds.
        if (mainCollider != null && Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 10f))
        {
            float feetOffset = transform.position.y - mainCollider.bounds.min.y;
            Vector3 correctedPos = transform.position;
            correctedPos.y = groundHit.point.y + feetOffset;
            transform.position = correctedPos;
        }

        if (agent != null)
        {
            // Le recalage au sol ci-dessus se base sur la physique (raycast), pas sur le
            // NavMesh bâti — les deux peuvent différer de quelques centimètres selon le
            // terrain, suffisant pour qu'agent.Warp() échoue avec "not close enough to the
            // NavMesh". On cherche le point NavMesh valide le plus proche avant de warper.
            Vector3 warpTarget = transform.position;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            {
                warpTarget = navHit.position;
            }
            agent.Warp(warpTarget);
            agent.enabled = true;
        }

        if (anim != null)
        {
            anim.enabled = true;
            anim.Rebind();
            anim.Update(0f);
        }

        isKnockedOut = false;

        if (brain != null)
        {
            brain.enabled = true;
            brain.StartCoroutine("BrainTick");
        }
    }
}