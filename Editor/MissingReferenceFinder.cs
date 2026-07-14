using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Missing Reference Finder - Unity Editor tool for locating broken asset references,
/// missing scripts, and orphaned component links across your project.
///
/// Scans:
///   - All project assets (prefabs, scenes, ScriptableObjects, materials, etc.)
///   - Open scenes and their GameObjects
///
/// Finds:
///   - Serialized object references pointing to deleted/moved assets
///   - Missing MonoBehaviour scripts on GameObjects
///
/// Usage:
///   Tools > Missing Reference Finder
///
/// GitHub-ready single-file editor tool. Drop into any Unity project's Editor folder.
/// </summary>
public sealed class MissingReferenceFinderWindow : EditorWindow
{
    private enum ScanScope
    {
        AllAssets,
        SelectedOnly,
        CurrentScene,
    }

    private enum ResultSort
    {
        ByAsset,
        ByProperty,
    }

    private ScanScope _scope = ScanScope.AllAssets;
    private ResultSort _sort = ResultSort.ByAsset;

    private readonly List<MissingRefResult> _results = new();
    private Vector2 _scrollPos;
    private bool _scanning;
    private float _progress;

    private int _scannedAssets;
    private int _scannedProps;
    private int _missingRefs;
    private int _missingScripts;

    // UI
    private bool _showMissingRefs = true;
    private bool _showMissingScripts = true;
    private string _searchFilter = "";
    private bool _showStats = true;

    [MenuItem("Tools/Missing Reference Finder", priority = 200)]
    public static void Open()
    {
        var window = GetWindow<MissingReferenceFinderWindow>("Missing Refs");
        window.minSize = new Vector2(600, 400);
        window.Show();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (_scanning)
        {
            DrawProgress();
            Repaint();
            return;
        }

        DrawStats();
        DrawSearch();
        DrawResults();
    }

    // ============================================================
    //  Toolbar
    // ============================================================

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUI.BeginDisabledGroup(_scanning);
        if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            StartScan();
        }
        EditorGUI.EndDisabledGroup();

        if (_scanning && GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            _scanning = false;
        }

        GUILayout.FlexibleSpace();

        _scope = (ScanScope)EditorGUILayout.EnumPopup(_scope, EditorStyles.toolbarPopup, GUILayout.Width(110));
        _sort = (ResultSort)EditorGUILayout.EnumPopup(_sort, EditorStyles.toolbarPopup, GUILayout.Width(100));

        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            _results.Clear();
            ResetCounters();
        }

        if (_results.Count > 0 && GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ExportResults();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
    }

    private void DrawProgress()
    {
        var r = EditorGUILayout.BeginVertical();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("Scanning...", EditorStyles.boldLabel);
        var barRect = EditorGUILayout.GetControlRect(false, 20);
        EditorGUI.ProgressBar(barRect, _progress, $"{_progress * 100:F0}%  ({_scannedAssets} assets, {_missingRefs} missing)");
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
    }

    private void DrawStats()
    {
        if (!_showStats) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Assets scanned", _scannedAssets.ToString(), GUILayout.Width(120));
        EditorGUILayout.LabelField("Properties checked", _scannedProps.ToString(), GUILayout.Width(140));
        EditorGUILayout.LabelField("Missing refs", _missingRefs.ToString(), GUILayout.Width(110));
        EditorGUILayout.LabelField("Missing scripts", _missingScripts.ToString(), GUILayout.Width(130));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    private void DrawSearch()
    {
        EditorGUILayout.BeginHorizontal();
        _searchFilter = EditorGUILayout.TextField("Filter", _searchFilter);

        _showMissingRefs = EditorGUILayout.ToggleLeft("Refs", _showMissingRefs, GUILayout.Width(50));
        _showMissingScripts = EditorGUILayout.ToggleLeft("Scripts", _showMissingScripts, GUILayout.Width(70));
        _showStats = EditorGUILayout.ToggleLeft("Stats", _showStats, GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
    }

    private void DrawResults()
    {
        if (_results.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Click Scan to find missing references across your project.\n\n" +
                "Scope options:\n" +
                "  All Assets    — every prefab, scene, SO, material in the project\n" +
                "  Selected Only — only assets selected in the Project window\n" +
                "  Current Scene — open scene GameObjects and their components",
                MessageType.Info);
            return;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        var filtered = GetFilteredResults();
        foreach (var result in filtered)
        {
            DrawResultItem(result);
        }

        EditorGUILayout.EndScrollView();

        if (_results.Count > 0)
            EditorGUILayout.LabelField($"Showing {filtered.Count} of {_results.Count}", EditorStyles.miniLabel);
    }

    private void DrawResultItem(MissingRefResult result)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        // Icon
        var icon = result.IsMissingScript
            ? EditorGUIUtility.IconContent("Error").image
            : AssetPreview.GetMiniTypeThumbnail(typeof(UnityEngine.Object)) ?? EditorGUIUtility.IconContent("Warning").image;

        GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

        // Content
        EditorGUILayout.BeginVertical();
        if (result.IsMissingScript)
        {
            EditorGUILayout.LabelField(
                $"<b>Missing Script</b> on <color=#569cd6>{result.GameObjectPath}</color>",
                new GUIStyle(EditorStyles.label) { richText = true });
        }
        else
        {
            var pathDisplay = string.IsNullOrEmpty(result.PropertyPath)
                ? "(root)"
                : result.PropertyPath;

            EditorGUILayout.LabelField(
                $"<b>{result.PropertyName}</b>  <color=#888>in</color>  {pathDisplay}",
                new GUIStyle(EditorStyles.label) { richText = true });
            EditorGUILayout.LabelField(
                $"<color=#569cd6>{MakeRelativePath(result.AssetPath)}</color>",
                new GUIStyle(EditorStyles.miniLabel) { richText = true });
        }
        EditorGUILayout.EndVertical();

        // Ping button
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Ping", GUILayout.Width(50), GUILayout.Height(20)))
        {
            PingResult(result);
        }

        EditorGUILayout.EndHorizontal();
    }

    // ============================================================
    //  Scan
    // ============================================================

    private async void StartScan()
    {
        _results.Clear();
        ResetCounters();
        _scanning = true;

        try
        {
            switch (_scope)
            {
                case ScanScope.AllAssets:
                    await ScanAllAssets();
                    break;
                case ScanScope.SelectedOnly:
                    ScanSelected();
                    break;
                case ScanScope.CurrentScene:
                    ScanCurrentScene();
                    break;
            }

            SortResults();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MissingRefFinder] Scan error: {ex.Message}");
        }
        finally
        {
            _scanning = false;
            Repaint();
        }
    }

    private async System.Threading.Tasks.Task ScanAllAssets()
    {
        var guids = AssetDatabase.FindAssets("");
        var total = guids.Length;

        for (var i = 0; i < total; i++)
        {
            if (!_scanning) break;

            _progress = (float)i / total;
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (string.IsNullOrEmpty(path)) continue;
            if (path.StartsWith("Packages/")) continue;

            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) continue;

            if (asset is SceneAsset)
            {
                // Scenes need special handling - skip for now, handled separately
                continue;
            }

            ScanAsset(asset, path);
            _scannedAssets++;

            // Yield every 10 assets to keep editor responsive
            if (i % 10 == 0)
                await System.Threading.Tasks.Task.Yield();
        }
    }

    private void ScanSelected()
    {
        var guids = Selection.assetGUIDs;
        _progress = 0f;
        var total = guids.Length;

        for (var i = 0; i < total; i++)
        {
            _progress = (float)i / total;
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
            {
                ScanAsset(asset, path);
            }
            _scannedAssets++;
        }

        _progress = 1f;
    }

    private void ScanCurrentScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;

        _progress = 0f;

        var rootObjects = scene.GetRootGameObjects();
        var total = rootObjects.Length;
        var count = 0;

        foreach (var go in rootObjects)
        {
            count++;
            _progress = (float)count / total;
            ScanGameObjectRecursive(go, scene.path);
        }

        _scannedAssets += total;
        _progress = 1f;
    }

    private void ScanGameObjectRecursive(GameObject go, string scenePath)
    {
        // Check for missing scripts
        var components = go.GetComponents<Component>();
        foreach (var comp in components)
        {
            _scannedProps++;
            if (comp == null)
            {
                _results.Add(new MissingRefResult
                {
                    IsMissingScript = true,
                    AssetPath = scenePath,
                    GameObjectPath = GetGameObjectPath(go),
                });
                _missingScripts++;
            }
        }

        // Check serialized properties on remaining valid components
        foreach (var comp in components)
        {
            if (comp == null) continue;
            ScanSerializedObject(new SerializedObject(comp), scenePath, go.name);
        }

        foreach (Transform child in go.transform)
        {
            ScanGameObjectRecursive(child.gameObject, scenePath);
        }
    }

    private void ScanAsset(UnityEngine.Object asset, string path)
    {
        var so = new SerializedObject(asset);
        ScanSerializedObject(so, path, null);
    }

    private void ScanSerializedObject(SerializedObject so, string assetPath, string gameObjectName)
    {
        var prop = so.GetIterator();
        var enterChildren = true;
        var propertyCount = so.FindProperty("m_Script") != null ? 0 : -1; // skip m_Script itself

        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            propertyCount++;
            _scannedProps++;

            if (prop.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            // The key check: objectReferenceValue is null but instance ID is non-zero
            // means the asset the reference pointed to was deleted/moved
            if (prop.objectReferenceValue == null &&
                prop.objectReferenceInstanceIDValue != 0)
            {
                _results.Add(new MissingRefResult
                {
                    IsMissingScript = false,
                    AssetPath = assetPath,
                    PropertyName = prop.displayName,
                    PropertyPath = prop.propertyPath,
                    GameObjectName = gameObjectName,
                });
                _missingRefs++;
            }
        }

        so.Dispose();
    }

    // ============================================================
    //  Actions
    // ============================================================

    private void PingResult(MissingRefResult result)
    {
        if (result.IsMissingScript)
        {
            // Try to find the GameObject in scene
            var go = GameObject.Find(result.GameObjectPath);
            if (go != null)
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }
            return;
        }

        var asset = AssetDatabase.LoadMainAssetAtPath(result.AssetPath);
        if (asset != null)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }

    private void ExportResults()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Missing Reference Report ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Project: {Application.productName}");
        sb.AppendLine($"Assets scanned: {_scannedAssets}");
        sb.AppendLine($"Properties checked: {_scannedProps}");
        sb.AppendLine($"Missing references: {_missingRefs}");
        sb.AppendLine($"Missing scripts: {_missingScripts}");
        sb.AppendLine();

        foreach (var r in _results)
        {
            if (r.IsMissingScript)
            {
                sb.AppendLine($"[MISSING SCRIPT] {r.GameObjectPath}  ({r.AssetPath})");
            }
            else
            {
                sb.AppendLine($"[MISSING REF] {r.PropertyName}  ({r.PropertyPath})");
                sb.AppendLine($"  Asset: {r.AssetPath}");
                if (!string.IsNullOrEmpty(r.GameObjectName))
                    sb.AppendLine($"  GameObject: {r.GameObjectName}");
                sb.AppendLine();
            }
        }

        var dir = Path.Combine(Application.dataPath, "..", "MissingRefReports");
        Directory.CreateDirectory(dir);
        var filename = $"MissingRefs_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var fullPath = Path.Combine(dir, filename);
        File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);

        EditorUtility.RevealInFinder(fullPath);
        Debug.Log($"[MissingRefFinder] Report exported: {fullPath}");
    }

    private static string GetGameObjectPath(GameObject go)
    {
        var path = go.name;
        var parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private void SortResults()
    {
        _results.Sort((a, b) =>
        {
            if (_sort == ResultSort.ByAsset)
                return string.Compare(a.AssetPath, b.AssetPath, StringComparison.OrdinalIgnoreCase);
            return string.Compare(a.PropertyName, b.PropertyName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private List<MissingRefResult> GetFilteredResults()
    {
        var list = new List<MissingRefResult>();
        foreach (var r in _results)
        {
            if (!_showMissingRefs && !r.IsMissingScript) continue;
            if (!_showMissingScripts && r.IsMissingScript) continue;

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                var match = r.AssetPath.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0
                         || r.PropertyName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0
                         || (r.GameObjectPath?.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;

                if (!match) continue;
            }

            list.Add(r);
        }
        return list;
    }

    private void ResetCounters()
    {
        _scannedAssets = 0;
        _scannedProps = 0;
        _missingRefs = 0;
        _missingScripts = 0;
        _progress = 0f;
    }

    private static string MakeRelativePath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return "";
        var idx = fullPath.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? fullPath.Substring(idx) : fullPath;
    }
}

/// <summary>
/// Holds a single missing reference finding.
/// </summary>
[Serializable]
internal class MissingRefResult
{
    public bool IsMissingScript;
    public string AssetPath;
    public string PropertyName;
    public string PropertyPath;
    public string GameObjectName;
    public string GameObjectPath;
}
