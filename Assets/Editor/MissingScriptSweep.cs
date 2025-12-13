#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MissingScriptSweep
{
    [MenuItem("Tools/Diagnostics/Sweep Missing Scripts In Prefabs")]
    public static void SweepPrefabs()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab");
        int hitCount = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            // Load prefab contents safely (doesn't instantiate into current scenes)
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                hitCount += DumpMissingOnHierarchy(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log($"[MissingScriptSweep] Done. Missing-script hits: {hitCount}");
    }

    private static int DumpMissingOnHierarchy(GameObject root, string assetPath)
    {
        int hits = 0;
        var stack = new System.Collections.Generic.Stack<Transform>();
        stack.Push(root.transform);

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            var comps = t.GetComponents<Component>();

            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null) continue;

                hits++;
                Debug.LogError(
                    $"[MissingScriptSweep] Missing script: asset='{assetPath}' goPath='{GetPath(t)}' componentIndex={i}",
                    t.gameObject);
            }

            for (int c = 0; c < t.childCount; c++)
                stack.Push(t.GetChild(c));
        }

        return hits;
    }

    private static string GetPath(Transform t)
    {
        var sb = new StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }
}
#endif
