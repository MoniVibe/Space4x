using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UDebug = UnityEngine.Debug;
using SysEnvironment = System.Environment;

namespace Space4X.Editor.Diagnostics
{
    public static class Space4XMissingScriptScanner
    {
        private const string MenuPath = "Tools/Diagnostics/Space4X/Find Missing MonoScripts";

        private struct MissingRecord
        {
            public string AssetPath;
            public string ObjectPath;
            public int MissingCount;
            public string Scope;
        }

        [MenuItem(MenuPath)]
        public static void FindMissingMonoScripts()
        {
            RunScan(exitWhenBatch: false);
        }

        public static void RunHeadlessScan()
        {
            RunScan(exitWhenBatch: true);
        }

        [MenuItem("Tools/Diagnostics/Space4X/Remove Missing MonoScripts")]
        public static void RemoveMissingMonoScripts()
        {
            RunFix(exitWhenBatch: false);
        }

        public static void RunHeadlessFix()
        {
            RunFix(exitWhenBatch: true);
        }

        private static void RunScan(bool exitWhenBatch)
        {
            var missing = new List<MissingRecord>();
            var prefabPaths = CollectAssetPaths("t:GameObject", ".prefab");
            var scenePaths = CollectAssetPaths("t:Scene", ".unity");
            var assetPaths = CollectAssetPaths("t:ScriptableObject", ".asset");

            try
            {
                ScanPrefabs(prefabPaths, missing);
                ScanScenes(scenePaths, missing);
                ScanScriptableAssets(assetPaths, missing);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            LogResults(prefabPaths.Count, scenePaths.Count, assetPaths.Count, missing);
            WriteReportIfRequested(missing);

            if (exitWhenBatch && Application.isBatchMode)
            {
                EditorApplication.Exit(missing.Count == 0 ? 0 : 1);
            }
        }

        private static void RunFix(bool exitWhenBatch)
        {
            var prefabPaths = CollectAssetPaths("t:GameObject", ".prefab");
            var scenePaths = CollectAssetPaths("t:Scene", ".unity");
            var assetPaths = CollectAssetPaths("t:ScriptableObject", ".asset");
            var removedCount = 0;
            var touchedPrefabs = 0;
            var touchedScenes = 0;
            var touchedAssets = 0;
            var skippedMainAssets = 0;

            try
            {
                removedCount += FixPrefabs(prefabPaths, ref touchedPrefabs);
                removedCount += FixScenes(scenePaths, ref touchedScenes);
                removedCount += FixScriptableAssets(assetPaths, ref touchedAssets, ref skippedMainAssets);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (removedCount == 0)
            {
                UDebug.Log($"[Space4XMissingScriptScanner] No missing MonoScripts removed. Scanned {prefabPaths.Count} prefabs, {scenePaths.Count} scenes, {assetPaths.Count} scriptable assets.");
            }
            else
            {
                UDebug.LogWarning($"[Space4XMissingScriptScanner] Removed {removedCount} missing MonoScripts (prefabs touched={touchedPrefabs}, scenes touched={touchedScenes}, assets touched={touchedAssets}, main assets skipped={skippedMainAssets}).");
            }

            if (exitWhenBatch && Application.isBatchMode)
            {
                EditorApplication.Exit(removedCount == 0 ? 0 : 0);
            }
        }

        private static List<string> CollectAssetPaths(string filter, string extension)
        {
            var results = new List<string>();
            var guids = AssetDatabase.FindAssets(filter);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!path.EndsWith(extension))
                {
                    continue;
                }
                results.Add(path);
            }
            return results;
        }

        private static void ScanPrefabs(List<string> prefabPaths, List<MissingRecord> missing)
        {
            for (var i = 0; i < prefabPaths.Count; i++)
            {
                var path = prefabPaths[i];
                EditorUtility.DisplayProgressBar("Missing MonoScripts", $"Prefab {i + 1}/{prefabPaths.Count}", (float)(i + 1) / prefabPaths.Count);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                ScanHierarchy(prefab, prefab.transform, path, "Prefab", missing);
            }
        }

        private static int FixPrefabs(List<string> prefabPaths, ref int touchedPrefabs)
        {
            var removed = 0;
            for (var i = 0; i < prefabPaths.Count; i++)
            {
                var path = prefabPaths[i];
                EditorUtility.DisplayProgressBar("Remove Missing MonoScripts", $"Prefab {i + 1}/{prefabPaths.Count}", (float)(i + 1) / prefabPaths.Count);

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    continue;
                }

                var removedInPrefab = RemoveMissingScriptsInHierarchy(root);
                if (removedInPrefab > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    touchedPrefabs++;
                }

                removed += removedInPrefab;
                PrefabUtility.UnloadPrefabContents(root);
            }

            return removed;
        }

        private static void ScanScenes(List<string> scenePaths, List<MissingRecord> missing)
        {
            var openScenes = new HashSet<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(scene.path))
                {
                    openScenes.Add(scene.path);
                }
            }

            for (var i = 0; i < scenePaths.Count; i++)
            {
                var path = scenePaths[i];
                EditorUtility.DisplayProgressBar("Missing MonoScripts", $"Scene {i + 1}/{scenePaths.Count}", (float)(i + 1) / scenePaths.Count);

                var alreadyOpen = openScenes.Contains(path);
                Scene scene;
                if (alreadyOpen)
                {
                    scene = SceneManager.GetSceneByPath(path);
                }
                else
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                if (scene.IsValid())
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        ScanHierarchy(root, root.transform, path, "Scene", missing);
                    }
                }

                if (!alreadyOpen && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ScanScriptableAssets(List<string> assetPaths, List<MissingRecord> missing)
        {
            for (var i = 0; i < assetPaths.Count; i++)
            {
                var path = assetPaths[i];
                EditorUtility.DisplayProgressBar("Missing MonoScripts", $"Scriptable Asset {i + 1}/{assetPaths.Count}", (float)(i + 1) / assetPaths.Count);

                var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (volumeProfile != null)
                {
                    var volumeMissing = FindMissingVolumeProfileComponents(volumeProfile, missing, path);
                    if (volumeMissing > 0)
                    {
                        continue;
                    }
                }

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null || assets.Length == 0)
                {
                    continue;
                }

                foreach (var asset in assets)
                {
                    if (asset == null)
                    {
                        missing.Add(new MissingRecord
                        {
                            AssetPath = path,
                            ObjectPath = "<null asset>",
                            MissingCount = 1,
                            Scope = "ScriptableObject"
                        });
                        continue;
                    }

                    if (!HasMissingScript(asset))
                    {
                        continue;
                    }

                    missing.Add(new MissingRecord
                    {
                        AssetPath = path,
                        ObjectPath = asset.name,
                        MissingCount = 1,
                        Scope = "ScriptableObject"
                    });
                }
            }
        }

        private static int FixScenes(List<string> scenePaths, ref int touchedScenes)
        {
            var removed = 0;
            var openScenes = new HashSet<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(scene.path))
                {
                    openScenes.Add(scene.path);
                }
            }

            for (var i = 0; i < scenePaths.Count; i++)
            {
                var path = scenePaths[i];
                EditorUtility.DisplayProgressBar("Remove Missing MonoScripts", $"Scene {i + 1}/{scenePaths.Count}", (float)(i + 1) / scenePaths.Count);

                var alreadyOpen = openScenes.Contains(path);
                Scene scene;
                if (alreadyOpen)
                {
                    scene = SceneManager.GetSceneByPath(path);
                }
                else
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                var removedInScene = 0;
                if (scene.IsValid())
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        removedInScene += RemoveMissingScriptsInHierarchy(root);
                    }
                }

                if (removedInScene > 0)
                {
                    if (scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    touchedScenes++;
                }

                removed += removedInScene;

                if (!alreadyOpen && scene.IsValid())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return removed;
        }

        private static int FixScriptableAssets(List<string> assetPaths, ref int touchedAssets, ref int skippedMainAssets)
        {
            var removed = 0;

            for (var i = 0; i < assetPaths.Count; i++)
            {
                var path = assetPaths[i];
                EditorUtility.DisplayProgressBar("Remove Missing MonoScripts", $"Scriptable Asset {i + 1}/{assetPaths.Count}", (float)(i + 1) / assetPaths.Count);

                var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (volumeProfile != null)
                {
                    var removedFromVolume = FixMissingVolumeProfileComponents(volumeProfile);
                    if (removedFromVolume > 0)
                    {
                        touchedAssets++;
                        AssetDatabase.ImportAsset(path);
                    }
                    removed += removedFromVolume;
                }

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null || assets.Length == 0)
                {
                    continue;
                }

                var removedInAsset = 0;
                var nullAssets = 0;
                foreach (var asset in assets)
                {
                    if (asset == null)
                    {
                        nullAssets++;
                        continue;
                    }

                    if (!HasMissingScript(asset))
                    {
                        continue;
                    }

                    if (AssetDatabase.IsMainAsset(asset))
                    {
                        skippedMainAssets++;
                        continue;
                    }

                    removedInAsset++;
                    UnityEngine.Object.DestroyImmediate(asset, true);
                }

                if (nullAssets > 0)
                {
                    removedInAsset += StripMissingScriptBlocks(path);
                }

                if (removedInAsset > 0)
                {
                    touchedAssets++;
                    AssetDatabase.ImportAsset(path);
                }

                removed += removedInAsset;
            }

            return removed;
        }

        private static void ScanHierarchy(GameObject root, Transform current, string assetPath, string scope, List<MissingRecord> missing)
        {
            var missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current.gameObject);
            if (missingCount > 0)
            {
                var path = AnimationUtility.CalculateTransformPath(current, root.transform);
                var objectPath = string.IsNullOrEmpty(path) ? root.name : $"{root.name}/{path}";
                missing.Add(new MissingRecord
                {
                    AssetPath = assetPath,
                    ObjectPath = objectPath,
                    MissingCount = missingCount,
                    Scope = scope
                });
            }

            for (var i = 0; i < current.childCount; i++)
            {
                ScanHierarchy(root, current.GetChild(i), assetPath, scope, missing);
            }
        }

        private static int RemoveMissingScriptsInHierarchy(GameObject root)
        {
            var removed = 0;
            var stack = new Stack<Transform>();
            stack.Push(root.transform);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(current.gameObject);

                for (var i = 0; i < current.childCount; i++)
                {
                    stack.Push(current.GetChild(i));
                }
            }

            return removed;
        }

        private static bool HasMissingScript(UnityEngine.Object asset)
        {
            var serializedObject = new SerializedObject(asset);
            var scriptProperty = serializedObject.FindProperty("m_Script");
            if (scriptProperty == null)
            {
                return false;
            }
            return scriptProperty.objectReferenceValue == null;
        }

        private static int StripMissingScriptBlocks(string path)
        {
            if (!File.Exists(path))
            {
                return 0;
            }

            var lines = File.ReadAllLines(path);
            if (lines.Length == 0)
            {
                return 0;
            }

            var output = new List<string>(lines.Length);
            var current = new List<string>();
            var removedIds = new HashSet<string>();
            var removedBlocks = 0;
            var blockHasMissingScript = false;
            string blockFileId = null;

            void FlushBlock()
            {
                if (current.Count == 0)
                {
                    return;
                }

                if (blockHasMissingScript)
                {
                    removedBlocks++;
                    if (!string.IsNullOrEmpty(blockFileId))
                    {
                        removedIds.Add(blockFileId);
                    }
                }
                else
                {
                    output.AddRange(current);
                }

                current.Clear();
                blockHasMissingScript = false;
                blockFileId = null;
            }

            foreach (var line in lines)
            {
                if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    FlushBlock();
                    current.Add(line);
                    blockFileId = ExtractFileIdFromHeader(line);
                    continue;
                }

                current.Add(line);
                if (TryDetectMissingScript(line))
                {
                    blockHasMissingScript = true;
                }
            }

            FlushBlock();

            if (removedBlocks == 0 || removedIds.Count == 0)
            {
                if (removedBlocks > 0)
                {
                    File.WriteAllLines(path, output);
                }
                return removedBlocks;
            }

            var cleaned = new List<string>(output.Count);
            foreach (var line in output)
            {
                if (TryExtractFileIdFromListEntry(line, out var fileId) && removedIds.Contains(fileId))
                {
                    continue;
                }
                cleaned.Add(line);
            }

            File.WriteAllLines(path, cleaned);
            return removedBlocks;
        }

        private static string ExtractFileIdFromHeader(string line)
        {
            var ampIndex = line.IndexOf('&');
            if (ampIndex < 0 || ampIndex == line.Length - 1)
            {
                return null;
            }

            return line.Substring(ampIndex + 1).Trim();
        }

        private static bool TryExtractFileIdFromListEntry(string line, out string fileId)
        {
            fileId = null;
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("- {fileID:", StringComparison.Ordinal))
            {
                return false;
            }

            var start = trimmed.IndexOf(':');
            if (start < 0)
            {
                return false;
            }

            start++;
            while (start < trimmed.Length && trimmed[start] == ' ')
            {
                start++;
            }

            var end = start;
            while (end < trimmed.Length && trimmed[end] != ',' && trimmed[end] != '}')
            {
                end++;
            }

            if (end <= start)
            {
                return false;
            }

            fileId = trimmed.Substring(start, end - start).Trim();
            return !string.IsNullOrEmpty(fileId);
        }

        private static bool TryDetectMissingScript(string line)
        {
            if (!line.Contains("m_Script:", StringComparison.Ordinal))
            {
                return false;
            }

            if (line.Contains("fileID: 0", StringComparison.Ordinal))
            {
                return true;
            }

            if (!TryExtractGuid(line, out var guid))
            {
                return false;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path);
        }

        private static bool TryExtractGuid(string line, out string guid)
        {
            guid = null;
            var guidIndex = line.IndexOf("guid:", StringComparison.Ordinal);
            if (guidIndex < 0)
            {
                return false;
            }

            guidIndex += "guid:".Length;
            while (guidIndex < line.Length && line[guidIndex] == ' ')
            {
                guidIndex++;
            }

            var end = guidIndex;
            while (end < line.Length && line[end] != ',' && line[end] != '}')
            {
                end++;
            }

            if (end <= guidIndex)
            {
                return false;
            }

            guid = line.Substring(guidIndex, end - guidIndex).Trim();
            return !string.IsNullOrEmpty(guid);
        }

        private static int FindMissingVolumeProfileComponents(VolumeProfile profile, List<MissingRecord> missing, string assetPath)
        {
            var serializedObject = new SerializedObject(profile);
            var componentsProperty = serializedObject.FindProperty("components");
            if (componentsProperty == null || !componentsProperty.isArray)
            {
                return 0;
            }

            var missingCount = 0;
            for (var i = 0; i < componentsProperty.arraySize; i++)
            {
                var element = componentsProperty.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != null)
                {
                    continue;
                }

                missingCount++;
                missing.Add(new MissingRecord
                {
                    AssetPath = assetPath,
                    ObjectPath = $"{profile.name}/components[{i}]",
                    MissingCount = 1,
                    Scope = "VolumeProfile"
                });
            }

            return missingCount;
        }

        private static int FixMissingVolumeProfileComponents(VolumeProfile profile)
        {
            var serializedObject = new SerializedObject(profile);
            var componentsProperty = serializedObject.FindProperty("components");
            if (componentsProperty == null || !componentsProperty.isArray)
            {
                return 0;
            }

            var removed = 0;
            for (var i = componentsProperty.arraySize - 1; i >= 0; i--)
            {
                var element = componentsProperty.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != null)
                {
                    continue;
                }

                componentsProperty.DeleteArrayElementAtIndex(i);
                removed++;
            }

            if (removed > 0)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            return removed;
        }

        private static void LogResults(int prefabCount, int sceneCount, int assetCount, List<MissingRecord> missing)
        {
            if (missing.Count == 0)
            {
                UDebug.Log($"[Space4XMissingScriptScanner] No missing MonoScripts found. Scanned {prefabCount} prefabs, {sceneCount} scenes, {assetCount} scriptable assets.");
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"[Space4XMissingScriptScanner] Missing MonoScripts found: {missing.Count} entries.");
            foreach (var record in missing)
            {
                builder.AppendLine($"- {record.Scope}: {record.AssetPath} :: {record.ObjectPath} (missing {record.MissingCount})");
            }
            UDebug.LogWarning(builder.ToString());
        }

        private static void WriteReportIfRequested(List<MissingRecord> missing)
        {
            var reportPath = SysEnvironment.GetEnvironmentVariable("SPACE4X_MISSING_SCRIPT_REPORT");
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                return;
            }

            try
            {
                var builder = new StringBuilder();
                foreach (var record in missing)
                {
                    builder.AppendLine($"{record.Scope}|{record.AssetPath}|{record.ObjectPath}|{record.MissingCount}");
                }
                File.WriteAllText(reportPath, builder.ToString());
                UDebug.Log($"[Space4XMissingScriptScanner] Wrote report to '{reportPath}'.");
            }
            catch (Exception ex)
            {
                UDebug.LogError($"[Space4XMissingScriptScanner] Failed to write report: {ex}");
            }
        }
    }
}
