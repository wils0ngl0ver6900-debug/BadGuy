using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// Outil d'éditeur pour disperser rapidement des bâtiments sur une zone, plutôt que de tout
// placer à la main un par un. Évite tes routes existantes (via TrafficNode) et les
// chevauchements entre bâtiments. Génère sous un objet dédié "_GeneratedBuildings" pour
// pouvoir tout nettoyer et régénérer facilement en itérant sur les réglages.
//
// Accès : menu Unity → Tools → Générateur de bâtiments.
public class BuildingScatterTool : EditorWindow
{
    private List<GameObject> buildingPrefabs = new List<GameObject>();

    private Vector3 areaCenter = Vector3.zero;
    private Vector3 areaSize = new Vector3(150, 0, 150);
    private float minSpacing = 18f;
    private float roadAvoidRadius = 10f;
    private bool snapRotationTo90 = true;
    private bool alignToGround = true;
    private LayerMask groundLayerMask = ~0;
    private int densityTarget = 40;
    private LayerMask avoidLayerMask = 0;
    private float avoidCheckRadius = 3f;

    [MenuItem("Tools/Générateur de bâtiments")]
    public static void ShowWindow()
    {
        GetWindow<BuildingScatterTool>("Générateur de bâtiments");
    }

    private Vector2 scroll;

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Prefabs de bâtiments", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Glisse ici tous les prefabs de bâtiments que tu veux voir apparaître — un choisi au hasard à chaque emplacement.", MessageType.None);

        for (int i = 0; i < buildingPrefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            buildingPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(buildingPrefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                buildingPrefabs.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ Ajouter un prefab"))
        {
            buildingPrefabs.Add(null);
        }

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Zone de génération", EditorStyles.boldLabel);
        areaCenter = EditorGUILayout.Vector3Field("Centre (monde)", areaCenter);
        areaSize = EditorGUILayout.Vector3Field("Taille (X = largeur, Z = profondeur)", areaSize);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Espacement", EditorStyles.boldLabel);
        minSpacing = EditorGUILayout.FloatField("Distance minimum entre bâtiments", minSpacing);
        roadAvoidRadius = EditorGUILayout.FloatField("Distance minimum aux routes (TrafficNode)", roadAvoidRadius);
        densityTarget = EditorGUILayout.IntField("Nombre de bâtiments visé", densityTarget);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        snapRotationTo90 = EditorGUILayout.Toggle("Rotation alignée (0/90/180/270°)", snapRotationTo90);
        alignToGround = EditorGUILayout.Toggle("Poser au sol (rayon vers le bas)", alignToGround);
        groundLayerMask = LayerMaskField("Layer(s) considéré(s) comme sol", groundLayerMask);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Objets à éviter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Layer(s) des objets déjà présents à ne pas recouvrir (buissons, lampadaires, props divers...) — nécessite que ces objets aient un Collider.", MessageType.None);
        avoidLayerMask = LayerMaskField("Layer(s) à éviter", avoidLayerMask);
        avoidCheckRadius = EditorGUILayout.FloatField("Rayon de détection autour de chaque objet", avoidCheckRadius);

        GUILayout.Space(16);

        using (new EditorGUI.DisabledScope(buildingPrefabs.Count == 0 || AllPrefabsNull()))
        {
            if (GUILayout.Button("Générer", GUILayout.Height(32)))
            {
                Generate();
            }
        }

        if (GUILayout.Button("Supprimer les bâtiments générés", GUILayout.Height(24)))
        {
            ClearGenerated();
        }

        EditorGUILayout.HelpBox("La zone se prévisualise en jaune dans la Scene View (Gizmos activés). Les routes existantes (TrafficNode) sont évitées automatiquement.", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private bool AllPrefabsNull()
    {
        foreach (GameObject p in buildingPrefabs) if (p != null) return false;
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
        List<GameObject> validPrefabs = buildingPrefabs.FindAll(p => p != null);
        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning("[BuildingScatterTool] Aucun prefab valide assigné.");
            return;
        }

        TrafficNode[] roadNodes = FindObjectsOfType<TrafficNode>();
        List<Vector3> placedPositions = new List<Vector3>();

        Transform root = FindOrCreateRoot();

        int attempts = 0;
        int maxAttempts = densityTarget * 40; // large marge pour compenser les rejets (routes, chevauchements)
        int placed = 0;

        while (attempts < maxAttempts && placed < densityTarget)
        {
            attempts++;

            Vector3 candidate = areaCenter + new Vector3(
                Random.Range(-areaSize.x / 2f, areaSize.x / 2f),
                0f,
                Random.Range(-areaSize.z / 2f, areaSize.z / 2f)
            );

            bool tooCloseToRoad = false;
            foreach (TrafficNode node in roadNodes)
            {
                if (Vector3.Distance(candidate, node.transform.position) < roadAvoidRadius)
                {
                    tooCloseToRoad = true;
                    break;
                }
            }
            if (tooCloseToRoad) continue;

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
            {
                continue; // objet existant (buisson, lampadaire...) trop proche à cet endroit
            }

            if (alignToGround)
            {
                if (Physics.Raycast(candidate + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f, groundLayerMask))
                {
                    candidate = hit.point;
                }
                else
                {
                    continue; // pas de sol détecté ici, on ne place pas dans le vide
                }
            }

            GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
            float rotY = snapRotationTo90 ? Random.Range(0, 4) * 90f : Random.Range(0f, 360f);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            instance.transform.position = candidate;
            instance.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            Undo.RegisterCreatedObjectUndo(instance, "Générer bâtiment");

            placedPositions.Add(candidate);
            placed++;
        }

        EditorUtility.SetDirty(root.gameObject);
        Debug.Log($"[BuildingScatterTool] {placed} bâtiment(s) placé(s) en {attempts} tentative(s) ({(densityTarget - placed)} non placés faute d'espace valide — élargis la zone ou réduis l'espacement si besoin).");
    }

    private void ClearGenerated()
    {
        GameObject root = GameObject.Find("_GeneratedBuildings");
        if (root == null)
        {
            Debug.Log("[BuildingScatterTool] Rien à supprimer.");
            return;
        }
        Undo.DestroyObjectImmediate(root);
    }

    private Transform FindOrCreateRoot()
    {
        GameObject root = GameObject.Find("_GeneratedBuildings");
        if (root == null)
        {
            root = new GameObject("_GeneratedBuildings");
            Undo.RegisterCreatedObjectUndo(root, "Créer racine bâtiments générés");
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
        Handles.color = new Color(1f, 0.9f, 0.2f, 0.5f);
        Handles.DrawWireCube(areaCenter, new Vector3(areaSize.x, 1f, areaSize.z));
    }
}