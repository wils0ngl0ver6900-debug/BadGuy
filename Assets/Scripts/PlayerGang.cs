using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class PlayerGang : MonoBehaviour
{
    public static PlayerGang Instance;

    [Header("Système de Gang")]
    public TerritoryManager.Faction playerFaction = TerritoryManager.Faction.Mafia;
    public float recruitRange = 25f;
    public int maxRecruits = 3;

    [HideInInspector]
    public List<NPCBrain> currentRecruits = new List<NPCBrain>();

    private NPCBrain currentTarget = null;
    private NPCBrain previousTarget = null;
    private GameObject selectionMarker;

    private void Awake()
    {
        // 💉 LE VACCIN ANTI-FANTÔMES : 
        // Si ce script se trouve n'importe où ailleurs que sur le Joueur, il s'autodétruit immédiatement !
        if (!gameObject.CompareTag("Player"))
        {
            Debug.LogWarning("⚠️ Un PlayerGang en double a été détecté et éliminé sur : " + gameObject.name);
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        CreateSelectionMarker();
    }

    private void CreateSelectionMarker()
    {
        selectionMarker = new GameObject("SelectionMarker_Gang");
        LineRenderer lr = selectionMarker.AddComponent<LineRenderer>();

        lr.startWidth = 0.15f;
        lr.endWidth = 0.15f;
        lr.positionCount = 37;
        lr.useWorldSpace = false;

        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(0f, 1f, 0.2f, 0.8f);
        lr.endColor = new Color(0f, 1f, 0.2f, 0.8f);

        float radius = 1.2f;
        for (int i = 0; i <= 36; i++)
        {
            float angle = i * Mathf.PI * 2f / 36f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.15f, Mathf.Sin(angle) * radius));
        }
        selectionMarker.SetActive(false);
    }

    void Update()
    {
        int removedCount = currentRecruits.RemoveAll(npc =>
        {
            if (npc == null) return true;
            TargetHealth th = npc.GetComponent<TargetHealth>();
            if (th != null && th.isDead) return true;
            return false;
        });

        if (removedCount > 0 && GangApp.Instance != null && GangApp.Instance.appPanel.activeInHierarchy)
        {
            GangApp.Instance.RefreshUI();
        }

        FindTargetEasy(); // Le système de visée facile !

        if (currentTarget != null)
        {
            if (!selectionMarker.activeSelf)
            {
                selectionMarker.transform.position = currentTarget.transform.position;
                selectionMarker.SetActive(true);
            }
            else
            {
                selectionMarker.transform.position = Vector3.Lerp(selectionMarker.transform.position, currentTarget.transform.position, Time.deltaTime * 15f);
            }

            if (previousTarget != currentTarget && UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification("Appuyez sur [R] pour Recruter");
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                TryRecruit(currentTarget);
                currentTarget = null;
                selectionMarker.SetActive(false);
            }
        }
        else
        {
            selectionMarker.SetActive(false);

            if (previousTarget != null && UIManager.Instance != null && UIManager.Instance.textNotification != null)
            {
                if (UIManager.Instance.textNotification.text == "Appuyez sur [R] pour Recruter")
                {
                    UIManager.Instance.HideNotification();
                }
            }
        }

        previousTarget = currentTarget;
    }

    // --- VISÉE OPTIMISÉE POUR CAMÉRA DE HAUT ---
    private void FindTargetEasy()
    {
        NPCBrain bestNPC = null;
        float minDistance = Mathf.Infinity;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 mouseWorldPos = ray.GetPoint(enter);

            NPCBrain[] allNPCs = FindObjectsOfType<NPCBrain>();

            foreach (NPCBrain npc in allNPCs)
            {
                if (npc.role == NPCBrain.NPCRole.Gang && npc.faction == playerFaction && npc.leader == null)
                {
                    if (Vector3.Distance(transform.position, npc.transform.position) <= recruitRange)
                    {
                        float distToMouse = Vector3.Distance(mouseWorldPos, npc.transform.position);

                        if (currentTarget == npc) distToMouse -= 2.0f; // Aimant magnétique

                        // Rayon généreux de 6 mètres pour ne pas rater la cible
                        if (distToMouse <= 6.0f && distToMouse < minDistance)
                        {
                            minDistance = distToMouse;
                            bestNPC = npc;
                        }
                    }
                }
            }
        }
        currentTarget = bestNPC;
    }

    private void TryRecruit(NPCBrain npc)
    {
        if (currentRecruits.Contains(npc)) return;

        if (currentRecruits.Count >= maxRecruits)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>Groupe plein ! (Max {maxRecruits})</color>");
            return;
        }

        if (TerritoryManager.Instance != null && !TerritoryManager.Instance.IsCurrentDistrictFullyControlled())
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=orange>Contrôlez ce quartier à 100% pour recruter ici !</color>");
            return;
        }

        // On est 100% sûr que c'est TON joueur le boss !
        npc.leader = this.transform;
        npc.ChangeState(NPCBrain.AIState.GardeDuCorps);

        if (npc.TryGetComponent<NavMeshAgent>(out NavMeshAgent agent))
        {
            agent.velocity = Vector3.zero;
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.isStopped = false;
        }

        currentRecruits.Add(npc);

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=#00FF00>Nouveau membre recruté !</color>");

        if (GangApp.Instance != null && GangApp.Instance.appPanel.activeInHierarchy)
            GangApp.Instance.RefreshUI();
    }

    public void DisbandGang()
    {
        foreach (NPCBrain npc in currentRecruits)
        {
            ResetNPC(npc);
        }
        currentRecruits.Clear();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Groupe dispersé.");
        if (GangApp.Instance != null && GangApp.Instance.appPanel.activeInHierarchy) GangApp.Instance.RefreshUI();
    }

    public void DismissMember(NPCBrain npc)
    {
        if (currentRecruits.Contains(npc))
        {
            ResetNPC(npc);
            currentRecruits.Remove(npc);

            if (GangApp.Instance != null && GangApp.Instance.appPanel.activeInHierarchy)
            {
                GangApp.Instance.RefreshUI();
            }
        }
    }

    private void ResetNPC(NPCBrain npc)
    {
        if (npc == null) return;

        npc.leader = null;
        npc.ChangeState(NPCBrain.AIState.Patrouille);

        if (npc.TryGetComponent<NavMeshAgent>(out NavMeshAgent agent))
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = false;
                agent.velocity = Vector3.zero;
            }
        }
    }
}