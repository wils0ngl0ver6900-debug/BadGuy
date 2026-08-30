using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using System.Collections.Generic;

// Outil d'éditeur pour placer rapidement plusieurs PNJ dans une zone — même principe que
// BuildingScatterTool, mais posé sur le NavMesh (pas juste un raycast au sol) et avec la
// possibilité de forcer leur rôle (Civil/Policier/Gang) directement à la génération. Pensé
// pour des scénarios comme "plein de gang dans un secteur de cité sensible" ou "plein de
// flics au commissariat" — définis la zone, force le rôle, génère.
//
// Accès : menu Unity → Tools → Générateur de PNJ.
public class NPCScatterTool : EditorWindow
{
    private List<GameObject> npcPrefabs = new List<GameObject>();

    private Vector3 areaCenter = Vector3.zero;
    private Vector3 areaSize = new Vector3(60, 0, 60);
    private float minSpacing = 3f;
    private int densityTarget = 10;

    private LayerMask avoidLayerMask = 0;
    private float avoidCheckRadius = 1.5f;

    private bool forceRole = true;
    private NPCBrain.NPCRole roleToForce = NPCBrain.NPCRole.Gang;

    private bool alignToGround = true;
    private LayerMask groundLayerMask = ~0;

    [MenuItem("Tools/Générateur de PNJ")]
    public static void ShowWindow()
    {
        GetWindow<NPCScatterTool>("Générateur de PNJ");
    }

    private Vector2 scroll;

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Prefabs de PNJ", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Glisse ici le(s) prefab(s) de PNJ à placer — un choisi au hasard à chaque emplacement si tu en mets plusieurs.", MessageType.None);

        for (int i = 0; i < npcPrefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            npcPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(npcPrefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                npcPrefabs.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ Ajouter un prefab"))
        {
            npcPrefabs.Add(null);
        }

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Zone de génération", EditorStyles.boldLabel);
        areaCenter = EditorGUILayout.Vector3Field("Centre (monde)", areaCenter);
        areaSize = EditorGUILayout.Vector3Field("Taille (X = largeur, Z = profondeur)", areaSize);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Espacement", EditorStyles.boldLabel);
        minSpacing = EditorGUILayout.FloatField("Distance minimum entre PNJ", minSpacing);
        densityTarget = EditorGUILayout.IntField("Nombre de PNJ visé", densityTarget);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Objets à éviter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Layer(s) des objets déjà présents à ne pas superposer (nécessite un Collider dessus).", MessageType.None);
        avoidLayerMask = LayerMaskField("Layer(s) à éviter", avoidLayerMask);
        avoidCheckRadius = EditorGUILayout.FloatField("Rayon de détection", avoidCheckRadius);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Rôle (NPCBrain)", EditorStyles.boldLabel);
        forceRole = EditorGUILayout.Toggle("Forcer le rôle à la génération", forceRole);
        using (new EditorGUI.DisabledScope(!forceRole))
        {
            roleToForce = (NPCBrain.NPCRole)EditorGUILayout.EnumPopup("Rôle à assigner", roleToForce);
        }
        EditorGUILayout.HelpBox("Si le prefab a un composant NPCBrain, son champ Role sera réglé sur celui choisi ci-dessus pour chaque exemplaire généré. Décoche si tu préfères garder le rôle par défaut du prefab tel quel.", MessageType.None);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        alignToGround = EditorGUILayout.Toggle("Poser sur le NavMesh", alignToGround);
        if (!alignToGround)
        {
            groundLayerMask = LayerMaskField("Layer(s) considéré(s) comme sol (repli raycast)", groundLayerMask);
        }

        GUILayout.Space(16);

        using (new EditorGUI.DisabledScope(npcPrefabs.Count == 0 || AllPrefabsNull()))
        {
            if (GUILayout.Button("Générer", GUILayout.Height(32)))
            {
                Generate();
            }
        }

        if (GUILayout.Button("Supprimer les PNJ générés", GUILayout.Height(24)))
        {
            ClearGenerated();
        }

        EditorGUILayout.HelpBox("La zone se prévisualise en jaune dans la Scene View (Gizmos activés).", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private bool AllPrefabsNull()
    {
        foreach (GameObject p in npcPrefabs) if (p != null) return false;
        return true;
    }

    private LayerMask LayerMaskField(string label, LayerMask mask)
    {
        List<string> layers = new List<string>();
        for (int i = 0; i < 32; i++)
        {
            string name = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(name)) layers.Add(name);
        }
        return EditorGUILayout.MaskField(label, mask, layers.ToArray());
    }

    private void Generate()
    {
        List<GameObject> validPrefabs = npcPrefabs.FindAll(p => p != null);
        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning("[NPCScatterTool] Aucun prefab valide assigné.");
            return;
        }

        List<Vector3> placedPositions = new List<Vector3>();
        Transform root = FindOrCreateRoot();

        int attempts = 0;
        int maxAttempts = densityTarget * 40;
        int placed = 0;

        while (attempts < maxAttempts && placed < densityTarget)
        {
            attempts++;

            Vector3 rawCandidate = areaCenter + new Vector3(
                Random.Range(-areaSize.x / 2f, areaSize.x / 2f),
                0f,
                Random.Range(-areaSize.z / 2f, areaSize.z / 2f)
            );

            Vector3 candidate;

            if (alignToGround)
            {
                if (!NavMesh.SamplePosition(rawCandidate, out NavMeshHit navHit, 15f, NavMesh.AllAreas))
                    continue; // pas de NavMesh valide à cet endroit
                candidate = navHit.position;
            }
            else
            {
                if (!Physics.Raycast(rawCandidate + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f, groundLayerMask))
                    continue;
                candidate = hit.point;
            }

            bool tooCloseToOther = false;
            foreach (Vector3 p in placedPositions)
            {
                if (Vector3.Distance(candidate, p) < minSpacing)
                {
                    tooCloseToOther = true;
                    break;
                }
            }
            if (tooCloseToOther) continue;

            if (avoidLayerMask != 0 && Physics.CheckSphere(candidate, avoidCheckRadius, avoidLayerMask))
                continue;

            GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
            float rotY = Random.Range(0f, 360f);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            instance.transform.position = candidate;
            instance.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            Undo.RegisterCreatedObjectUndo(instance, "Générer PNJ");

            if (forceRole)
            {
                NPCBrain brain = instance.GetComponent<NPCBrain>();
                if (brain != null)
                {
                    brain.role = roleToForce;
                    EditorUtility.SetDirty(brain);
                }
            }

            placedPositions.Add(candidate);
            placed++;
        }

        EditorUtility.SetDirty(root.gameObject);
        Debug.Log($"[NPCScatterTool] {placed} PNJ placé(s) en {attempts} tentative(s) ({(densityTarget - placed)} non placés faute d'espace valide — élargis la zone ou réduis l'espacement si besoin).");
    }

    private void ClearGenerated()
    {
        GameObject root = GameObject.Find("_GeneratedNPCs");
        if (root == null)
        {
            Debug.Log("[NPCScatterTool] Rien à supprimer.");
            return;
        }
        Undo.DestroyObjectImmediate(root);
    }

    private Transform FindOrCreateRoot()
    {
        GameObject root = GameObject.Find("_GeneratedNPCs");
        if (root == null)
        {
            root = new GameObject("_GeneratedNPCs");
            Undo.RegisterCreatedObjectUndo(root, "Créer racine PNJ générés");
        }
        return root.transform;
    }

    private void OnFocus()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDestroy()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSceneGUI(SceneView view)
    {
        Handles.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        Handles.DrawWireCube(areaCenter, new Vector3(areaSize.x, 1f, areaSize.z));
    }
}