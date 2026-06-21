using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using TMPro;

public class PlayerGang : MonoBehaviour
{
    [Header("Système de Gang")]
    public TerritoryManager.Faction playerFaction = TerritoryManager.Faction.Mafia;
    public float recruitRange = 25f;
    public int maxRecruits = 3;

    [Header("Interface de Gestion (Menu)")]
    public GameObject gangMenuPanel;
    public TextMeshProUGUI recruitListText;

    [HideInInspector]
    public List<NPCBrain> currentRecruits = new List<NPCBrain>();

    private bool isMenuOpen = false;
    private NPCBrain currentTarget = null;
    private NPCBrain previousTarget = null;

    // --- VISUEL PRO ---
    private GameObject selectionMarker;

    void Start()
    {
        if (gangMenuPanel != null) gangMenuPanel.SetActive(false);
        CreateSelectionMarker();
    }

    private void CreateSelectionMarker()
    {
        selectionMarker = new GameObject("SelectionMarker_Gang");
        LineRenderer lr = selectionMarker.AddComponent<LineRenderer>();

        lr.startWidth = 0.15f;
        lr.endWidth = 0.15f;
        lr.positionCount = 37; // 37 points pour boucler parfaitement le cercle
        lr.useWorldSpace = false;

        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(0f, 1f, 0.2f, 0.8f); // Vert fluo stylé
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
        currentRecruits.RemoveAll(npc =>
        {
            if (npc == null) return true;
            TargetHealth th = npc.GetComponent<TargetHealth>();
            if (th != null && th.isDead) return true;
            return false;
        });

        if (isMenuOpen) UpdateMenuText();
        if (Input.GetKeyDown(KeyCode.G)) ToggleMenu();

        if (isMenuOpen)
        {
            if (selectionMarker != null) selectionMarker.SetActive(false);
            return;
        }

        // 1. RECHERCHE DE LA CIBLE (LASER VOLUMÉTRIQUE + MAGNÉTISME)
        FindTargetWithVolumetricLaser();

        // 2. GESTION DU VISUEL ET RECRUTEMENT
        if (currentTarget != null)
        {
            // Glissement fluide de l'anneau de sélection
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

    // --- LA VISÉE INFAILLIBLE DES JEUX AAA ---
    private void FindTargetWithVolumetricLaser()
    {
        NPCBrain bestNPC = null;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        float searchRadius = 3.0f; // Épaisseur du "cylindre" laser
        float minDistance = Mathf.Infinity;

        NPCBrain[] allNPCs = FindObjectsOfType<NPCBrain>();

        foreach (NPCBrain npc in allNPCs)
        {
            if (npc.role == NPCBrain.NPCRole.Gang && npc.faction == playerFaction && npc.leader == null)
            {
                float distToPlayer = Vector3.Distance(transform.position, npc.transform.position);

                if (distToPlayer <= recruitRange)
                {
                    // On vise le torse (à 1 mètre du sol), pas les pieds !
                    Vector3 npcCenter = npc.transform.position + Vector3.up * 1.0f;

                    // Calcul mathématique de la distance entre le rayon de la caméra et le torse du PNJ
                    float distToLaser = Vector3.Cross(ray.direction, npcCenter - ray.origin).magnitude;

                    // LE MAGNÉTISME : Si c'est notre cible actuelle, on la considère artificiellement plus proche pour qu'elle "colle" !
                    if (currentTarget == npc)
                    {
                        distToLaser -= 1.5f; // Impossible de décrocher sans le faire exprès
                    }

                    if (distToLaser <= searchRadius && distToLaser < minDistance)
                    {
                        minDistance = distToLaser;
                        bestNPC = npc;
                    }
                }
            }
        }

        currentTarget = bestNPC;
    }

    private void ToggleMenu()
    {
        if (gangMenuPanel == null) return;

        isMenuOpen = !isMenuOpen;
        gangMenuPanel.SetActive(isMenuOpen);
        Cursor.visible = isMenuOpen;
        if (isMenuOpen) UpdateMenuText();
    }

    public void UpdateMenuText()
    {
        if (recruitListText == null) return;

        if (currentRecruits.Count == 0)
        {
            recruitListText.text = "Aucune recrue dans l'équipe.\nAllez dans un quartier contrôlé pour recruter.";
            return;
        }

        string list = $"ÉQUIPE ACTUELLE ({currentRecruits.Count}/{maxRecruits}) :\n\n";

        for (int i = 0; i < currentRecruits.Count; i++)
        {
            TargetHealth health = currentRecruits[i].GetComponent<TargetHealth>();
            string hp = health != null ? health.currentHealth.ToString() : "?";

            string colorTag = "<color=green>";
            if (health != null && health.currentHealth < 50) colorTag = "<color=orange>";
            if (health != null && health.currentHealth < 20) colorTag = "<color=red>";

            list += $"■ Garde du corps {i + 1} | PV : {colorTag}{hp}</color>\n";
        }

        recruitListText.text = list;
    }

    private void TryRecruit(NPCBrain npc)
    {
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

        npc.leader = this.transform;
        npc.ChangeState(NPCBrain.AIState.GardeDuCorps);
        currentRecruits.Add(npc);

        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=#00FF00>Nouveau membre recruté !</color>");
    }

    public void DisbandGang()
    {
        foreach (NPCBrain npc in currentRecruits)
        {
            if (npc != null)
            {
                npc.leader = null;
                npc.ChangeState(NPCBrain.AIState.Patrouille);

                if (npc.TryGetComponent<NavMeshAgent>(out NavMeshAgent agent))
                {
                    agent.isStopped = false;
                    if (agent.isOnNavMesh) agent.ResetPath();
                }
            }
        }
        currentRecruits.Clear();
        UpdateMenuText();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Groupe dispersé.");
    }
}