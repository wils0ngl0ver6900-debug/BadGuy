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

    private bool useBentMesh = true;
    private float roadWidth = 8f;
    private float uvTileLength = 10f;
    private bool addMeshCollider = true;
    private int slicesPerMeter = 2; // densité de subdivision de la courbe (plus haut = plus lisse, plus de triangles)

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
        roadSegmentPrefab = (GameObject)EditorGUILayout.ObjectField(useBentMesh ? "Prefab (pour sa texture/matériau)" : "Prefab de segment (droit)", roadSegmentPrefab, typeof(GameObject), false);

        useBentMesh = EditorGUILayout.Toggle("Route courbée (mesh généré)", useBentMesh);

        if (useBentMesh)
        {
            roadWidth = EditorGUILayout.FloatField("Largeur de la route (m)", roadWidth);
            uvTileLength = EditorGUILayout.FloatField("Répétition de la texture tous les (m)", uvTileLength);
            slicesPerMeter = EditorGUILayout.IntSlider("Subdivisions par mètre (lissage)", slicesPerMeter, 1, 5);
            addMeshCollider = EditorGUILayout.Toggle("Ajouter un Mesh Collider", addMeshCollider);
            roadYOffset = EditorGUILayout.FloatField("Décalage vertical de la route", roadYOffset);
            EditorGUILayout.HelpBox("Génère un VRAI mesh de ruban qui suit la courbe sans aucun angle, peu importe la sévérité du virage — plus de tuiles rigides qui se chevauchent. Le matériau du prefab ci-dessus est réutilisé tel quel ; sa géométrie de détail (bordures, trottoirs...) n'est PAS reproduite, seulement une bande plate à la largeur indiquée.", MessageType.Info);
        }
        else
        {
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
            EditorGUILayout.HelpBox("Réutilise tes tuiles telles quelles, avec tous leurs détails (bordures, trottoirs...) — mais des angles restent visibles sur les virages serrés, la tuile étant rigide. Réservé aux tracés surtout droits ou aux virages très larges.", MessageType.None);
        }

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Circulation IA (TrafficNode)", EditorStyles.boldLabel);
        generateNodes = EditorGUILayout.Toggle("Générer les TrafficNode", generateNodes);
        using (new EditorGUI.DisabledScope(!generateNodes))
        {
            nodesPerSegment = EditorGUILayout.IntSlider("Noeuds par segment (subdivision)", nodesPerSegment, 1, 5);
            bidirectional = EditorGUILayout.Toggle("Route à double sens", bidirectional);
        }

        GUILayout.Space(16);
        using (new EditorGUI.DisabledScope(waypoints.Count < 2 || (!useBentMesh && roadSegmentPrefab == null)))
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
            // p0/p3 = voisins (avant/après), utilisés uniquement pour donner à la courbe une
            // tangente cohérente à l'approche — la courbe elle-même passe exactement par p1
            // et p2 (les deux points réellement posés pour ce tronçon), jamais par p0/p3.
            Vector3 p0 = waypoints[Mathf.Max(0, i - 1)];
            Vector3 p1 = waypoints[i];
            Vector3 p2 = waypoints[i + 1];
            Vector3 p3 = waypoints[Mathf.Min(waypoints.Count - 1, i + 2)];

            if (useBentMesh)
            {
                GenerateBentRoadMesh(p0, p1, p2, p3, roadRoot);
            }
            else
            {
                PlaceRoadSegmentsCurved(p0, p1, p2, p3, roadRoot);
            }

            if (generateNodes)
            {
                CreateNodesAlongCurve(p0, p1, p2, p3, i == 0, nodeRoot, createdNodes);
            }
        }

        if (generateNodes) LinkNodes(createdNodes);

        Debug.Log($"[RoadNodeTool] Tracé généré : {waypoints.Count - 1} tronçon(s), {createdNodes.Count} TrafficNode créé(s) et relié(s).");
    }

    // Interpolation Catmull-Rom : courbe lisse qui passe exactement par p1 et p2, en
    // s'appuyant sur p0/p3 pour orienter la tangente à l'approche/la sortie — technique
    // standard pour faire passer une courbe douce à travers une série de points, sans
    // toucher à la géométrie des prefabs (juste du positionnement, pas de risque de
    // plantage lié à une manipulation de mesh).
    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    // Génère un VRAI mesh de ruban qui suit la courbe — extrusion classique d'un profil
    // (ici : une simple bande plate à largeur fixe) le long d'une trajectoire, technique de
    // base bien maîtrisée, pas de génération procédurale complexe (pas d'intersections, pas
    // de LOD, pas de logique de jonction) — juste des positions calculées et une grille de
    // triangles, donc un risque de plantage très faible malgré le fait que ce soit un
    // "vrai" mesh généré.
    private void GenerateBentRoadMesh(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Transform root)
    {
        float approxLength = Vector3.Distance(p1, p2);
        int sliceCount = Mathf.Max(2, Mathf.RoundToInt(approxLength * slicesPerMeter));

        Vector3[] centers = new Vector3[sliceCount + 1];
        Vector3[] rights = new Vector3[sliceCount + 1];
        float[] cumulativeDist = new float[sliceCount + 1];

        for (int i = 0; i <= sliceCount; i++)
        {
            float t = (float)i / sliceCount;
            centers[i] = CatmullRom(p0, p1, p2, p3, t) + Vector3.up * roadYOffset;
        }

        for (int i = 0; i <= sliceCount; i++)
        {
            Vector3 tangent;
            if (i == 0) tangent = (centers[1] - centers[0]).normalized;
            else if (i == sliceCount) tangent = (centers[sliceCount] - centers[sliceCount - 1]).normalized;
            else tangent = (centers[i + 1] - centers[i - 1]).normalized;

            if (tangent == Vector3.zero) tangent = (p2 - p1).normalized; // repli si deux tranches quasi identiques

            rights[i] = Vector3.Cross(Vector3.up, tangent).normalized;
            cumulativeDist[i] = i == 0 ? 0f : cumulativeDist[i - 1] + Vector3.Distance(centers[i - 1], centers[i]);
        }

        Vector3[] verts = new Vector3[(sliceCount + 1) * 2];
        Vector2[] uvs = new Vector2[(sliceCount + 1) * 2];
        List<int> tris = new List<int>((sliceCount) * 6);

        for (int i = 0; i <= sliceCount; i++)
        {
            Vector3 halfWidth = rights[i] * (roadWidth / 2f);
            verts[i * 2] = centers[i] - halfWidth;
            verts[i * 2 + 1] = centers[i] + halfWidth;

            float v = cumulativeDist[i] / Mathf.Max(0.01f, uvTileLength);
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);

            if (i < sliceCount)
            {
                int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "RoadSegment_Bent";
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        GameObject go = new GameObject($"RoadSegment_Bent_{root.childCount}");
        go.transform.SetParent(root);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        Renderer sourceRenderer = roadSegmentPrefab != null ? roadSegmentPrefab.GetComponentInChildren<Renderer>() : null;
        if (sourceRenderer != null) mr.sharedMaterial = sourceRenderer.sharedMaterial;

        if (addMeshCollider)
        {
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }

        Undo.RegisterCreatedObjectUndo(go, "Générer route courbée");
    }

    private void PlaceRoadSegmentsCurved(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Transform root)
    {
        // Échantillonne finement la courbe pour connaître sa VRAIE longueur — une courbe
        // Catmull-Rom peut "bomber" entre p1 et p2, donc le vrai trajet est souvent plus
        // long que la distance à vol d'oiseau. Espacer les tuiles par paramètre t (comme
        // avant) plaçait les tuiles à des distances réelles inégales et laissait des trous
        // dans les zones où la courbe s'écarte le plus de la ligne droite.
        const int sampleCount = 40;
        Vector3[] samplePoints = new Vector3[sampleCount + 1];
        float[] cumulativeDist = new float[sampleCount + 1];

        for (int i = 0; i <= sampleCount; i++)
        {
            float tSample = (float)i / sampleCount;
            samplePoints[i] = CatmullRom(p0, p1, p2, p3, tSample);
            cumulativeDist[i] = i == 0 ? 0f : cumulativeDist[i - 1] + Vector3.Distance(samplePoints[i - 1], samplePoints[i]);
        }

        float totalCurveLength = cumulativeDist[sampleCount];
        int segmentCount = Mathf.Max(1, Mathf.RoundToInt(totalCurveLength / segmentLength));
        float actualSegLength = totalCurveLength / segmentCount;

        for (int s = 0; s < segmentCount; s++)
        {
            float distStart = actualSegLength * s;
            float distEnd = actualSegLength * (s + 1);
            float distMid = (distStart + distEnd) / 2f;

            Vector3 posStart = SampleByArcLength(samplePoints, cumulativeDist, distStart);
            Vector3 posEnd = SampleByArcLength(samplePoints, cumulativeDist, distEnd);
            Vector3 posMid = SampleByArcLength(samplePoints, cumulativeDist, distMid);

            Vector3 dir = (posEnd - posStart).normalized;
            if (dir == Vector3.zero) dir = (p2 - p1).normalized; // repli si deux points quasi identiques

            Quaternion rot = Quaternion.LookRotation(dir);
            if (prefabLengthAxis == 0) rot *= Quaternion.Euler(0f, 90f, 0f);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(roadSegmentPrefab, root);
            instance.transform.position = posMid + Vector3.up * roadYOffset;
            instance.transform.rotation = rot;
            Undo.RegisterCreatedObjectUndo(instance, "Générer route");
        }
    }

    // Trouve le point sur la courbe échantillonnée à une distance PARCOURUE donnée
    // (interpolation linéaire entre les deux échantillons qui l'encadrent) — c'est ce qui
    // garantit un espacement réel régulier entre les tuiles, pas juste régulier "sur le
    // papier" du paramètre de la courbe.
    private Vector3 SampleByArcLength(Vector3[] samplePoints, float[] cumulativeDist, float targetDist)
    {
        int count = samplePoints.Length;
        if (targetDist <= 0f) return samplePoints[0];
        if (targetDist >= cumulativeDist[count - 1]) return samplePoints[count - 1];

        for (int i = 1; i < count; i++)
        {
            if (cumulativeDist[i] >= targetDist)
            {
                float segStart = cumulativeDist[i - 1];
                float segEnd = cumulativeDist[i];
                float localT = (targetDist - segStart) / (segEnd - segStart);
                return Vector3.Lerp(samplePoints[i - 1], samplePoints[i], localT);
            }
        }
        return samplePoints[count - 1];
    }

    private void CreateNodesAlongCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, bool includeStart, Transform root, List<TrafficNode> createdNodes)
    {
        if (includeStart)
        {
            createdNodes.Add(CreateNode(p1, root, createdNodes.Count));
        }

        for (int i = 1; i <= nodesPerSegment; i++)
        {
            float t = (float)i / nodesPerSegment;
            Vector3 pos = CatmullRom(p0, p1, p2, p3, t);
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