using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Space4X.EditorTools
{
    public static class MissingScriptStripper
    {
        private const string ReportFileName = "Space4X_MissingScripts_Report.txt";

        [MenuItem("Space4X/Tools/Missing Scripts/Scan and Strip Prefabs")]
        public static void ScanAndStripPrefabs()
        {
            var reportLines = new List<string>();
            var removedCount = 0;
            var prefabCount = 0;

            var guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    continue;
                }

                try
                {
                    var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
                    if (missing > 0)
                    {
                        prefabCount++;
                        reportLines.Add($"{path} (missing={missing})");
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        removedCount += missing;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var reportPath = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, ReportFileName);
            reportLines.Insert(0, $"Removed missing scripts: {removedCount} from {prefabCount} prefab(s)");
            File.WriteAllLines(reportPath, reportLines);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[MissingScriptStripper] Removed {removedCount} missing scripts. Report: {reportPath}");
        }
    }
}
