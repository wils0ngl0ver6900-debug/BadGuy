using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// Outil de placement de routes en masse, couplé à la génération automatique du réseau
// TrafficNode pour la circulation IA — pensé comme alternative légère à EasyRoads3D/RoadSystem
// (qui plantaient Unity chez toi). Volontairement SIMPLE : pas de génération procédurale de
// mesh (source probable des plantages des autres outils), juste de l'instanciation de tes
// prefabs de segments de route existants + calcul de position/rotation, et création de
// TrafficNode liés en séquence. Robuste, prévisible, facile à déboguer si besoin.
//
// Usage : Tools → Générateur de routes. Active "Mode placement", clique dans la Scene View
// (sur le sol) pour poser des points le long du tracé voulu, dans l'ordre, puis "Générer"
// pose les segments de route ET les TrafficNode d'un coup, déjà reliés entre eux.
public class RoadNodeTool : EditorWindow
{
    private List<Vector3> waypoints = new List<Vector3>();
    private bool placementMode = false;

    private GameObject roadSegmentPrefab;
    private float segmentLength = 10f;
    private float roadYOffset = 0f;
    private int prefabLengthAxis = 2; // 0=X, 2=Z (le plus courant pour une route)

    private bool generateNodes = true;
    private int nodesPerSegment = 1;
    private bool bidirectional = true;

    private LayerMask groundLayerMask = ~0;

    [MenuItem("Tools/Générateur de routes")]
    public static void ShowWindow()
    {
        GetWindow<RoadNodeTool>("Générateur de routes");
    }

    private Vector2 scroll;

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Tracé", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Active le mode placement, puis clique dans la Scene View (sur le sol) pour poser des points le long du tracé voulu, dans l'ordre. Rapproche les points dans les virages pour un tracé plus fidèle.", MessageType.None);

        bool newPlacementMode = EditorGUILayout.Toggle("Mode placement actif", placementMode);
        if (newPlacementMode != placementMode)
        {
            placementMode = newPlacementMode;
            SceneView.RepaintAll();
        }

        groundLayerMask = LayerMaskField("Layer(s) considéré(s) comme sol (clic)", groundLayerMask);

        EditorGUILayout.LabelField($"{waypoints.Count} point(s) posé(s)", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Annuler le dernier point"))
        {
            if (waypoints.Count > 0) waypoints.RemoveAt(waypoints.Count - 1);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Effacer le tracé"))
        {
            waypoints.Clear();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Route", EditorStyles.boldLabel);
        roadSegmentPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab de segment (droit)", roadSegmentPrefab, typeof(GameObject), false);

        EditorGUILayout.BeginHorizontal();
        segmentLength = EditorGUILayout.FloatField("Longueur réelle du prefab (m)", segmentLength);
        using (new EditorGUI.DisabledScope(roadSegmentPrefab == null))
        {
            if (GUILayout.Button("Mesurer auto", GUILayout.Width(100)))
            {
                segmentLength = MeasurePrefabLength(roadSegmentPrefab);
                Debug.Log($"[RoadNodeTool] Longueur mesurée : {segmentLength:F2}m.");
            }
        }
        EditorGUILayout.EndHorizontal();
        prefabLengthAxis = EditorGUILayout.Popup("Axe le long duquel le prefab est modélisé", prefabLengthAxis, new string[] { "X", "Y", "Z" });
        roadYOffset = EditorGUILayout.FloatField("Décalage vertical de la route", roadYOffset);
        EditorGUILayout.HelpBox("Le prefab est répété (\"carrelé\") entre chaque paire de points consécutifs plutôt qu'étiré — rendu propre peu importe la distance entre deux points. Indique la longueur RÉELLE de ton prefab pour un carrelage correct (mesure-le dans l'Inspector si besoin).", MessageType.None);

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Circulation IA (TrafficNode)", EditorStyles.boldLabel);
        generateNodes = EditorGUILayout.Toggle("Générer les TrafficNode", generateNodes);
        using (new EditorGUI.DisabledScope(!generateNodes))
        {
            nodesPerSegment = EditorGUILayout.IntSlider("Noeuds par segment (subdivision)", nodesPerSegment, 1, 5);
            bidirectional = EditorGUILayout.Toggle("Route à double sens", bidirectional);
        }

        GUILayout.Space(16);
        using (new EditorGUI.DisabledScope(waypoints.Count < 2 || roadSegmentPrefab == null))
        {
            if (GUILayout.Button("Générer", GUILayout.Height(32)))
            {
                Generate();
            }
        }

        if (GUILayout.Button("Supprimer les routes générées", GUILayout.Height(24)))
        {
            ClearGenerated();
        }

        EditorGUILayout.HelpBox("Le tracé (points cyan reliés par des lignes) se prévisualise dans la Scene View tant que cette fenêtre a le focus.", MessageType.Info);

        EditorGUILayout.EndScrollView();
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

    // Mesure la vraie longueur du prefab en l'instanciant temporairement à l'origine (lire
    // les bounds d'un prefab ASSET non-instancié n'est pas fiable) — élimine le risque de se
    // tromper en estimant à l'œil, cause la plus probable d'un chevauchement/scintillement
    // entre segments (Z-fighting) si la valeur entrée à la main est trop courte.
    private float MeasurePrefabLength(GameObject prefab)
    {
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;

        Renderer[] renderers = temp.GetComponentsInChildren<Renderer>();
        float length = segmentLength;

        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            foreach (Renderer r in renderers) combined.Encapsulate(r.bounds);
            length = prefabLengthAxis == 0 ? combined.size.x : (prefabLengthAxis == 1 ? combined.size.y : combined.size.z);
        }
        else
        {
            Debug.LogWarning("[RoadNodeTool] Aucun Renderer trouvé sur ce prefab, mesure impossible — valeur inchangée.");
        }

        DestroyImmediate(temp);
        return length;
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
        Handles.color = Color.cyan;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Handles.SphereHandleCap(0, waypoints[i], Quaternion.identity, 1.2f, EventType.Repaint);
            if (i > 0) Handles.DrawLine(waypoints[i - 1], waypoints[i]);
        }

        if (!placementMode) return;

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayerMask))
            {
                waypoints.Add(hit.point);
                Repaint();
                e.Use(); // empêche ce clic de AUSSI sélectionner un objet dans la scène
            }
        }
    }

    private void Generate()
    {
        Transform roadRoot = FindOrCreateRoot("_GeneratedRoads");
        Transform nodeRoot = FindOrCreateRoot("_GeneratedTrafficNodes");

        List<TrafficNode> createdNodes = new List<TrafficNode>();

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            Vector3 start = waypoints[i];
            Vector3 end = waypoints[i + 1];

            PlaceRoadSegments(start, end, roadRoot);

            if (generateNodes)
            {
                CreateNodesAlongSegment(start, end, i == 0, nodeRoot, createdNodes);
            }
        }

        if (generateNodes) LinkNodes(createdNodes);

        Debug.Log($"[RoadNodeTool] Tracé généré : {waypoints.Count - 1} tronçon(s), {createdNodes.Count} TrafficNode créé(s) et relié(s).");
    }

    private void PlaceRoadSegments(Vector3 start, Vector3 end, Transform root)
    {
        float totalDist = Vector3.Distance(start, end);
        Vector3 dir = (end - start).normalized;
        Quaternion rot = Quaternion.LookRotation(dir);
        // LookRotation aligne l'axe Z local sur la direction — si le prefab est modélisé le
        // long de X (choix fait plus haut dans l'UI), on compense pour que ce soit bien SON
        // axe long qui pointe dans la direction du tracé, pas Z par défaut.
        if (prefabLengthAxis == 0) rot *= Quaternion.Euler(0f, 90f, 0f);

        int segmentCount = Mathf.Max(1, Mathf.RoundToInt(totalDist / segmentLength));
        float actualSegLength = totalDist / segmentCount; // ajuste légèrement pour couvrir pile la distance entre les deux points

        for (int s = 0; s < segmentCount; s++)
        {
            Vector3 segStart = start + dir * (actualSegLength * s);
            Vector3 segCenter = segStart + dir * (actualSegLength / 2f) + Vector3.up * roadYOffset;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(roadSegmentPrefab, root);
            instance.transform.position = segCenter;
            instance.transform.rotation = rot;
            Undo.RegisterCreatedObjectUndo(instance, "Générer route");
        }
    }

    private void CreateNodesAlongSegment(Vector3 start, Vector3 end, bool includeStart, Transform root, List<TrafficNode> createdNodes)
    {
        if (includeStart)
        {
            createdNodes.Add(CreateNode(start, root, createdNodes.Count));
        }

        for (int i = 1; i <= nodesPerSegment; i++)
        {
            float t = (float)i / nodesPerSegment;
            Vector3 pos = Vector3.Lerp(start, end, t);
            createdNodes.Add(CreateNode(pos, root, createdNodes.Count));
        }
    }

    private TrafficNode CreateNode(Vector3 pos, Transform root, int index)
    {
        GameObject go = new GameObject($"Node_{index}");
        go.transform.position = pos;
        go.transform.SetParent(root);
        TrafficNode node = go.AddComponent<TrafficNode>();
        Undo.RegisterCreatedObjectUndo(go, "Générer TrafficNode");
        return node;
    }

    private void LinkNodes(List<TrafficNode> nodes)
    {
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            if (nodes[i].nextNodes == null) nodes[i].nextNodes = new List<TrafficNode>();
            nodes[i].nextNodes.Add(nodes[i + 1]);

            if (bidirectional)
            {
                if (nodes[i + 1].nextNodes == null) nodes[i + 1].nextNodes = new List<TrafficNode>();
                nodes[i + 1].nextNodes.Add(nodes[i]);
            }

            EditorUtility.SetDirty(nodes[i]);
            EditorUtility.SetDirty(nodes[i + 1]);
        }
    }

    private void ClearGenerated()
    {
        GameObject roads = GameObject.Find("_GeneratedRoads");
        GameObject nodes = GameObject.Find("_GeneratedTrafficNodes");

        if (roads != null) Undo.DestroyObjectImmediate(roads);
        if (nodes != null) Undo.DestroyObjectImmediate(nodes);

        if (roads == null && nodes == null) Debug.Log("[RoadNodeTool] Rien à supprimer.");
    }

    private Transform FindOrCreateRoot(string name)
    {
        GameObject root = GameObject.Find(name);
        if (root == null)
        {
            root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Créer racine " + name);
        }
        return root.transform;
    }
}