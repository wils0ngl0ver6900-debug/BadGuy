using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// Outil d'éditeur pour changer le Layer de nombreux objets d'un coup, dispersés n'importe
// où dans la Hierarchy — soit en les ajoutant manuellement, soit en les trouvant par un
// filtre de nom (ex: "Building" trouve tout objet dont le nom contient ce texte, peu
// importe où il se trouve dans la scène).
//
// Accès : menu Unity → Tools → Changeur de Layer en masse.
public class LayerBatchTool : EditorWindow
{
    private List<GameObject> targets = new List<GameObject>();
    private string nameFilter = "";
    private int targetLayer = 0;
    private bool includeChildren = true;

    [MenuItem("Tools/Changeur de Layer en masse")]
    public static void ShowWindow()
    {
        GetWindow<LayerBatchTool>("Changeur de Layer en masse");
    }

    private Vector2 scroll;

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Trouver des objets par nom", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        nameFilter = EditorGUILayout.TextField("Contient le texte", nameFilter);
        if (GUILayout.Button("Chercher dans la scène", GUILayout.Width(150)))
        {
            FindByName();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Trouve TOUS les objets actifs de la scène dont le nom contient ce texte (insensible à la casse) et les ajoute à la liste ci-dessous, peu importe où ils se trouvent dans la Hierarchy.", MessageType.None);

        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Objets ciblés", EditorStyles.boldLabel);
        if (GUILayout.Button("+ Ajouter la sélection actuelle", GUILayout.Width(200)))
        {
            AddCurrentSelection();
        }
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < targets.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            targets[i] = (GameObject)EditorGUILayout.ObjectField(targets[i], typeof(GameObject), true);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                targets.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Vider la liste"))
        {
            targets.Clear();
        }

        GUILayout.Space(12);
        EditorGUILayout.LabelField($"{targets.Count(t => t != null)} objet(s) dans la liste", EditorStyles.miniLabel);

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Layer à appliquer", EditorStyles.boldLabel);
        targetLayer = EditorGUILayout.LayerField("Nouveau Layer", targetLayer);
        includeChildren = EditorGUILayout.Toggle("Inclure les enfants", includeChildren);

        GUILayout.Space(16);

        using (new EditorGUI.DisabledScope(targets.Count == 0 || AllTargetsNull()))
        {
            if (GUILayout.Button("Appliquer le Layer", GUILayout.Height(32)))
            {
                ApplyLayer();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private bool AllTargetsNull()
    {
        foreach (GameObject t in targets) if (t != null) return false;
        return true;
    }

    private void FindByName()
    {
        if (string.IsNullOrEmpty(nameFilter))
        {
            Debug.LogWarning("[LayerBatchTool] Tape un texte à rechercher avant de lancer la recherche.");
            return;
        }

        GameObject[] all = FindObjectsOfType<GameObject>();
        string filterLower = nameFilter.ToLower();
        int added = 0;

        foreach (GameObject go in all)
        {
            if (go.name.ToLower().Contains(filterLower) && !targets.Contains(go))
            {
                targets.Add(go);
                added++;
            }
        }

        Debug.Log($"[LayerBatchTool] {added} objet(s) trouvé(s) et ajouté(s) pour \"{nameFilter}\".");
    }

    private void AddCurrentSelection()
    {
        int added = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (!targets.Contains(go))
            {
                targets.Add(go);
                added++;
            }
        }
        Debug.Log($"[LayerBatchTool] {added} objet(s) de la sélection ajouté(s).");
    }

    private void ApplyLayer()
    {
        int changedCount = 0;

        foreach (GameObject go in targets)
        {
            if (go == null) continue;

            Undo.RecordObject(go, "Changer Layer");
            go.layer = targetLayer;
            changedCount++;

            if (includeChildren)
            {
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject == go) continue;
                    Undo.RecordObject(child.gameObject, "Changer Layer");
                    child.gameObject.layer = targetLayer;
                    changedCount++;
                }
            }

            EditorUtility.SetDirty(go);
        }

        Debug.Log($"[LayerBatchTool] Layer \"{LayerMask.LayerToName(targetLayer)}\" appliqué à {changedCount} objet(s) au total{(includeChildren ? " (enfants inclus)" : "")}.");
    }
}