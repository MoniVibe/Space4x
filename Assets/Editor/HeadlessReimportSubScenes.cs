using UnityEditor;
using UnityEngine;

public static class HeadlessReimportSubScenes
{
    public static void ReimportAllScenes()
    {
        var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        var options = ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport;
        var count = 0;

        foreach (var guid in sceneGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            AssetDatabase.ImportAsset(path, options);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[HeadlessReimport] Reimported {count} scene assets.");
    }
}
