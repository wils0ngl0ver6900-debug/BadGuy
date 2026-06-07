using UnityEngine;
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

    void Start()
    {
        if (gangMenuPanel != null) gangMenuPanel.SetActive(false);
    }

    void Update()
    {
        // CORRECTIF 1 : Sécurité absolue anti-crash
        currentRecruits.RemoveAll(npc =>
        {
            if (npc == null) return true;
            TargetHealth th = npc.GetComponent<TargetHealth>();
            if (th != null && th.isDead) return true;
            return false; // On le garde s'il est vivant et valide
        });

        if (isMenuOpen) UpdateMenuText();

        if (Input.GetKeyDown(KeyCode.G))
        {
            ToggleMenu();
        }

        if (isMenuOpen) return;

        // 1. RECHERCHE DE LA CIBLE
        FindTargetWithMouse();

        // 2. AFFICHAGE DES NOTIFICATIONS ET RECRUTEMENT
        if (currentTarget != null)
        {
            if (previousTarget != currentTarget)
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Appuyez sur [R] pour Recruter");
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                TryRecruit(currentTarget);
                currentTarget = null;
            }
        }
        else if (previousTarget != null)
        {
            if (UIManager.Instance != null) UIManager.Instance.HideNotification();
        }

        previousTarget = currentTarget;
    }

    private void FindTargetWithMouse()
    {
        currentTarget = null;
        // CORRECTIF 2 : S'adapte à ta résolution QHD dynamiquement !
        float minScreenDistance = Screen.height * 0.15f;

        NPCBrain[] allNPCs = FindObjectsOfType<NPCBrain>();

        foreach (NPCBrain npc in allNPCs)
        {
            if (npc.role == NPCBrain.NPCRole.Gang && npc.faction == playerFaction && npc.currentState != NPCBrain.AIState.GardeDuCorps)
            {
                float distToPlayer = Vector3.Distance(transform.position, npc.transform.position);

                if (distToPlayer <= recruitRange)
                {
                    Vector3 screenPos = Camera.main.WorldToScreenPoint(npc.transform.position);

                    if (screenPos.z > 0)
                    {
                        float distToMouse = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), new Vector2(Input.mousePosition.x, Input.mousePosition.y));

                        if (distToMouse < minScreenDistance)
                        {
                            minScreenDistance = distToMouse;
                            currentTarget = npc;
                        }
                    }
                }
            }
        }
    }

    private void ToggleMenu()
    {
        if (gangMenuPanel == null) return;

        isMenuOpen = !isMenuOpen;
        gangMenuPanel.SetActive(isMenuOpen);

        if (isMenuOpen)
        {
            Cursor.visible = true;
            UpdateMenuText();
        }
        else
        {
            Cursor.visible = false;
        }
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
            }
        }
        currentRecruits.Clear();
        UpdateMenuText();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Groupe dispersé.");
    }
}