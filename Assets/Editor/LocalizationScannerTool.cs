using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;

// Outil de localisation étendu : au-delà des Text/TextMeshProUGUI déjà remplis dans une
// scène, scanne aussi les champs "public string" de tous les MonoBehaviour (dialogues,
// messages de notification stockés en champ, textes de course...) ET les champs "public
// string" de tes ScriptableObject (ItemData et autres) — trois sources différentes, une
// seule liste, un seul export/import.
//
// Une catégorie reste HORS DE PORTÉE d'un scan/apply automatique et fiable : le texte écrit
// EN DUR dans le corps des méthodes C# (ex: ShowNotification("Cible éliminée !") tapé
// directement dans un script). Un outil "Trouver les textes codés en dur" liste ces
// emplacements (fichier + ligne) pour que tu ailles les corriger toi-même à la main — les
// modifier automatiquement demanderait de réécrire du code source, risque bien trop élevé
// pour le faire sans supervision (variables interpolées, guillemets, mise en forme...).
//
// Accès : Tools → Outil de localisation.
public class LocalizationScannerTool : EditorWindow
{
    [System.Serializable]
    public class TextEntry
    {
        public string key;
        public string originalText;
        public string translatedText;
        public string source; // "Scene Text", "Champ MonoBehaviour", "ScriptableObject"
    }

    private List<TextEntry> entries = new List<TextEntry>();
    private List<string> hardcodedFindings = new List<string>();
    private Vector2 scroll;
    private Vector2 hardcodedScroll;
    private string statusMessage = "";
    private string scriptableObjectTypeFilter = "ItemData";

    // Mots-clés utilisés pour ne PAS remonter des champs de configuration (noms de tag,
    // de layer, identifiants techniques...) qui ne sont pas du texte affiché au joueur.
    private static readonly string[] NameHints = { "text", "message", "line", "label", "prompt", "desc", "name", "title", "sentence", "notification", "hint" };

    [MenuItem("Tools/Outil de localisation")]
    public static void ShowWindow()
    {
        GetWindow<LocalizationScannerTool>("Outil de localisation");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("1. Scanner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Trois sources scannées d'un coup : les Text/TextMeshProUGUI déjà remplis, les champs \"public string\" de tous les scripts (MonoBehaviour) de la scène, et les champs \"public string\" de tes ScriptableObject (ItemData...). Ouvre chaque scène une par une et relance pour couvrir tout ton jeu.", MessageType.None);

        if (GUILayout.Button("Scanner la scène actuelle (Text + champs de script)", GUILayout.Height(28)))
        {
            ScanCurrentScene();
        }

        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        scriptableObjectTypeFilter = EditorGUILayout.TextField("Type de ScriptableObject", scriptableObjectTypeFilter);
        if (GUILayout.Button("Scanner ces assets", GUILayout.Width(140)))
        {
            ScanScriptableObjects(scriptableObjectTypeFilter);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Tape le nom exact d'un type de ScriptableObject de ton projet (ex: ItemData) — scanne TOUS les assets de ce type, où qu'ils soient rangés.", MessageType.None);

        GUILayout.Space(8);
        EditorGUILayout.LabelField($"{entries.Count} texte(s) en mémoire", EditorStyles.miniLabel);

        GUILayout.Space(12);
        EditorGUILayout.LabelField("2. Export / Import CSV", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Exporte en CSV (clé, source, texte original, traduction). Colle les colonnes dans Google Sheets/DeepL/ChatGPT, remplis la traduction, réexporte, réimporte ici.", MessageType.None);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Exporter en CSV")) ExportCSV();
        if (GUILayout.Button("Importer un CSV traduit")) ImportCSV();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(12);
        EditorGUILayout.LabelField("3. Appliquer les traductions", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Remplace directement la valeur des Text/TMP ET des champs de script (MonoBehaviour + ScriptableObject) déjà scannés — pas besoin de repasser par chaque écran un par un.", MessageType.None);

        if (GUILayout.Button("Appliquer les traductions", GUILayout.Height(28)))
        {
            ApplyTranslations();
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            GUILayout.Space(8);
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }

        GUILayout.Space(12);
        EditorGUILayout.LabelField("4. Textes codés en dur dans le code (repérage seulement)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Trouve les chaînes de texte françaises tapées DIRECTEMENT dans tes scripts (ex: ShowNotification(\"...\")). Liste juste fichier + ligne pour que tu les corriges à la main — les modifier automatiquement serait trop risqué (variables interpolées, casse du code).", MessageType.None);

        if (GUILayout.Button("Chercher dans Assets/Scripts", GUILayout.Height(24)))
        {
            FindHardcodedStrings();
        }

        if (hardcodedFindings.Count > 0)
        {
            hardcodedScroll = EditorGUILayout.BeginScrollView(hardcodedScroll, GUILayout.Height(150));
            foreach (string finding in hardcodedFindings)
            {
                EditorGUILayout.LabelField(finding, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Aperçu des textes scannés", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(220));
        foreach (TextEntry entry in entries)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"[{entry.source}] {entry.key}", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Original : " + Truncate(entry.originalText, 80));
            if (!string.IsNullOrEmpty(entry.translatedText))
                EditorGUILayout.LabelField("Traduit : " + Truncate(entry.translatedText, 80));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Vider la liste"))
        {
            entries.Clear();
            statusMessage = "";
        }
    }

    private string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s.Substring(0, max) + "...";
    }

    private string BuildKeyForObject(GameObject go, string suffix = "")
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return string.IsNullOrEmpty(suffix) ? path : path + "#" + suffix;
    }

    private bool LooksLikeDisplayText(string fieldName, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length < 2) return false;

        string lowerName = fieldName.ToLower();
        foreach (string hint in NameHints)
        {
            if (lowerName.Contains(hint)) return true;
        }

        // Repli : une valeur avec un espace et plusieurs mots ressemble à une phrase, pas à
        // un identifiant technique (nom de layer, de tag, etc.).
        return value.Contains(" ") && value.Length > 8;
    }

    // --- Scan Scene (Text/TMP + champs MonoBehaviour) ---
    private void ScanCurrentScene()
    {
        int added = 0;

        Text[] uguiTexts = FindObjectsOfType<Text>(true);
        foreach (Text t in uguiTexts)
        {
            if (string.IsNullOrWhiteSpace(t.text)) continue;
            AddOrUpdateEntry(BuildKeyForObject(t.gameObject), t.text, "Scene Text", t);
            added++;
        }

        TMPro.TextMeshProUGUI[] tmpTexts = FindObjectsOfType<TMPro.TextMeshProUGUI>(true);
        foreach (TMPro.TextMeshProUGUI t in tmpTexts)
        {
            if (string.IsNullOrWhiteSpace(t.text)) continue;
            AddOrUpdateEntry(BuildKeyForObject(t.gameObject), t.text, "Scene Text", t);
            added++;
        }

        MonoBehaviour[] allBehaviours = FindObjectsOfType<MonoBehaviour>(true);
        foreach (MonoBehaviour mb in allBehaviours)
        {
            if (mb == null) continue;
            added += ScanObjectStringFields(mb, mb, BuildKeyForObject(mb.gameObject) + "#" + mb.GetType().Name, "Champ MonoBehaviour");
        }

        statusMessage = $"{added} texte(s) trouvé(s) dans la scène \"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\". Total en mémoire : {entries.Count}.";
        Debug.Log("[LocalizationScannerTool] " + statusMessage);
    }

    // --- Scan ScriptableObject assets ---
    private void ScanScriptableObjects(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            Debug.LogWarning("[LocalizationScannerTool] Indique un nom de type d'abord.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets($"t:{typeName}");
        int added = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset == null) continue;

            added += ScanObjectStringFields(asset, asset, path, "ScriptableObject");
        }

        statusMessage = $"{added} texte(s) trouvé(s) dans {guids.Length} asset(s) de type \"{typeName}\". Total en mémoire : {entries.Count}.";
        Debug.Log("[LocalizationScannerTool] " + statusMessage);
    }

    // Réflexion sur les champs "public string" d'un objet, + un niveau de récursion dans les
    // tableaux/listes de classes [System.Serializable] personnalisées (ex: CarBreakInMethod[]
    // sur CarBreakInConfig) pour aussi couvrir les champs imbriqués.
    private int ScanObjectStringFields(Object unityObject, object target, string keyPrefix, string sourceLabel)
    {
        int added = 0;
        if (target == null) return 0;

        System.Type type = target.GetType();
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (FieldInfo field in fields)
        {
            try
            {
                if (field.FieldType == typeof(string))
                {
                    string value = (string)field.GetValue(target);
                    if (!LooksLikeDisplayText(field.Name, value)) continue;
                    AddOrUpdateEntry($"{keyPrefix}.{field.Name}", value, sourceLabel, unityObject);
                    added++;
                }
                else if (field.FieldType.IsArray && field.FieldType.GetElementType() != null && field.FieldType.GetElementType().IsClass && field.FieldType.GetElementType() != typeof(string) && !typeof(Object).IsAssignableFrom(field.FieldType.GetElementType()))
                {
                    // Tableau d'une classe personnalisée (pas une référence Unity, pas une string) -> on descend d'un niveau.
                    System.Array array = (System.Array)field.GetValue(target);
                    if (array == null) continue;
                    for (int i = 0; i < array.Length; i++)
                    {
                        object element = array.GetValue(i);
                        added += ScanObjectStringFields(unityObject, element, $"{keyPrefix}.{field.Name}[{i}]", sourceLabel);
                    }
                }
            }
            catch
            {
                // Certains champs (types internes Unity, etc.) peuvent lever une exception à
                // la lecture — on les ignore silencieusement plutôt que de casser tout le scan.
            }
        }

        return added;
    }

    private void AddOrUpdateEntry(string key, string text, string source, Object sourceObject)
    {
        TextEntry existing = entries.FirstOrDefault(e => e.key == key);
        if (existing != null)
        {
            existing.originalText = text;
            existing.source = source;
        }
        else
        {
            entries.Add(new TextEntry { key = key, originalText = text, translatedText = "", source = source });
        }
    }

    // --- Export / Import ---
    private void ExportCSV()
    {
        if (entries.Count == 0)
        {
            Debug.LogWarning("[LocalizationScannerTool] Rien à exporter — scanne d'abord.");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Exporter les textes", Application.dataPath, "localization", "csv");
        if (string.IsNullOrEmpty(path)) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Clé;Source;Texte original;Traduction");
        foreach (TextEntry entry in entries)
        {
            sb.AppendLine($"{EscapeCsv(entry.key)};{EscapeCsv(entry.source)};{EscapeCsv(entry.originalText)};{EscapeCsv(entry.translatedText)}");
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        statusMessage = $"Exporté : {path}";
        Debug.Log("[LocalizationScannerTool] " + statusMessage);
    }

    private void ImportCSV()
    {
        string path = EditorUtility.OpenFilePanel("Importer un CSV traduit", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(path)) return;

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        int updated = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] cols = SplitCsvLine(lines[i]);
            if (cols.Length < 4) continue;

            string key = cols[0];
            string translated = cols[3];

            TextEntry existing = entries.FirstOrDefault(e => e.key == key);
            if (existing != null)
            {
                existing.translatedText = translated;
                updated++;
            }
        }

        statusMessage = $"{updated} traduction(s) importée(s) depuis {path}.";
        Debug.Log("[LocalizationScannerTool] " + statusMessage);
    }

    // --- Appliquer (Text/TMP scène + champs de script, via re-scan + réflexion en écriture) ---
    private void ApplyTranslations()
    {
        int applied = 0;

        Text[] uguiTexts = FindObjectsOfType<Text>(true);
        foreach (Text t in uguiTexts)
        {
            TextEntry entry = entries.FirstOrDefault(e => e.key == BuildKeyForObject(t.gameObject) && e.source == "Scene Text");
            if (entry != null && !string.IsNullOrEmpty(entry.translatedText))
            {
                Undo.RecordObject(t, "Appliquer traduction");
                t.text = entry.translatedText;
                applied++;
            }
        }

        TMPro.TextMeshProUGUI[] tmpTexts = FindObjectsOfType<TMPro.TextMeshProUGUI>(true);
        foreach (TMPro.TextMeshProUGUI t in tmpTexts)
        {
            TextEntry entry = entries.FirstOrDefault(e => e.key == BuildKeyForObject(t.gameObject) && e.source == "Scene Text");
            if (entry != null && !string.IsNullOrEmpty(entry.translatedText))
            {
                Undo.RecordObject(t, "Appliquer traduction");
                t.text = entry.translatedText;
                applied++;
            }
        }

        MonoBehaviour[] allBehaviours = FindObjectsOfType<MonoBehaviour>(true);
        foreach (MonoBehaviour mb in allBehaviours)
        {
            if (mb == null) continue;
            applied += ApplyObjectStringFields(mb, mb, BuildKeyForObject(mb.gameObject) + "#" + mb.GetType().Name, "Champ MonoBehaviour");
        }

        // ScriptableObject : réapplique sur tout asset déjà présent dans la liste (peu importe le type filtré au scan).
        foreach (TextEntry entry in entries.Where(e => e.source == "ScriptableObject" && !string.IsNullOrEmpty(e.translatedText)))
        {
            string assetPath = entry.key.Split('.')[0];
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset != null)
            {
                applied += ApplyObjectStringFields(asset, asset, assetPath, "ScriptableObject", entry);
            }
        }

        statusMessage = $"{applied} texte(s) remplacé(s). Ctrl+Z pour annuler (objets de scène) — pense à sauvegarder les ScriptableObject modifiés (Ctrl+S) si le résultat te convient.";
        Debug.Log("[LocalizationScannerTool] " + statusMessage);
    }

    private int ApplyObjectStringFields(Object unityObject, object target, string keyPrefix, string sourceLabel, TextEntry specificEntry = null)
    {
        int applied = 0;
        if (target == null) return 0;

        System.Type type = target.GetType();
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (FieldInfo field in fields)
        {
            try
            {
                if (field.FieldType == typeof(string))
                {
                    string key = $"{keyPrefix}.{field.Name}";
                    TextEntry entry = specificEntry != null && specificEntry.key == key ? specificEntry : entries.FirstOrDefault(e => e.key == key && e.source == sourceLabel);
                    if (entry != null && !string.IsNullOrEmpty(entry.translatedText))
                    {
                        if (unityObject != null) Undo.RecordObject(unityObject, "Appliquer traduction");
                        field.SetValue(target, entry.translatedText);
                        if (unityObject != null) EditorUtility.SetDirty(unityObject);
                        applied++;
                    }
                }
                else if (field.FieldType.IsArray && field.FieldType.GetElementType() != null && field.FieldType.GetElementType().IsClass && field.FieldType.GetElementType() != typeof(string) && !typeof(Object).IsAssignableFrom(field.FieldType.GetElementType()))
                {
                    System.Array array = (System.Array)field.GetValue(target);
                    if (array == null) continue;
                    for (int i = 0; i < array.Length; i++)
                    {
                        object element = array.GetValue(i);
                        applied += ApplyObjectStringFields(unityObject, element, $"{keyPrefix}.{field.Name}[{i}]", sourceLabel);
                    }
                }
            }
            catch
            {
            }
        }

        return applied;
    }

    // --- Repérage (pas d'application) des chaînes codées en dur dans les .cs ---
    private void FindHardcodedStrings()
    {
        hardcodedFindings.Clear();
        string scriptsPath = Path.Combine(Application.dataPath, "Scripts");
        if (!Directory.Exists(scriptsPath))
        {
            Debug.LogWarning("[LocalizationScannerTool] Dossier Assets/Scripts introuvable.");
            return;
        }

        string[] files = Directory.GetFiles(scriptsPath, "*.cs", SearchOption.AllDirectories);
        // Détecte une chaîne entre guillemets contenant au moins un caractère accentué
        // français courant — heuristique simple mais efficace pour repérer du texte FR
        // sans remonter des identifiants techniques en anglais.
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("\"([^\"]*[àâäéèêëïîôöùûüçÀÂÄÉÈÊËÏÎÔÖÙÛÜÇ][^\"]*)\"");

        int totalFound = 0;
        foreach (string file in files)
        {
            string[] lines = File.ReadAllLines(file);
            string fileName = Path.GetFileName(file);

            for (int i = 0; i < lines.Length; i++)
            {
                var matches = regex.Matches(lines[i]);
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    string text = m.Groups[1].Value;
                    if (text.Length < 4) continue; // ignore les toutes petites chaînes (souvent pas du texte affiché)
                    hardcodedFindings.Add($"{fileName}:{i + 1} — \"{Truncate(text, 70)}\"");
                    totalFound++;
                    if (totalFound >= 500) break; // garde-fou, évite une liste ingérable
                }
                if (totalFound >= 500) break;
            }
            if (totalFound >= 500) break;
        }

        statusMessage = $"{totalFound} chaîne(s) codée(s) en dur repérée(s) dans Assets/Scripts (liste ci-dessous) — à corriger toi-même dans le code, pas d'application automatique possible en sécurité.";
        Debug.Log("[LocalizationScannerTool] " + statusMessage);
    }

    private string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        if (s.Contains(";") || s.Contains("\""))
            return $"\"{s}\"";
        return s;
    }

    private string[] SplitCsvLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        StringBuilder current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ';' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}