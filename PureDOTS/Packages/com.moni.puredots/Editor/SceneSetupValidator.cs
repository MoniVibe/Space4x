using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PureDOTS.Authoring;

namespace PureDOTS.Editor
{
    public static class SceneSetupValidator
    {
        private readonly struct SingletonAuthoringDefinition
        {
            public SingletonAuthoringDefinition(Type type, string label)
            {
                Type = type;
                Label = label;
            }

            public Type Type { get; }
            public string Label { get; }
        }

        private static readonly SingletonAuthoringDefinition[] SingletonAuthorings =
        {
            new(typeof(PureDotsConfigAuthoring), nameof(PureDotsConfigAuthoring)),
            new(typeof(SpatialPartitionAuthoring), nameof(SpatialPartitionAuthoring))
        };

        [MenuItem("PureDOTS/Validation/Run Scene Setup Validator", priority = 600)]
        public static void RunSceneSetupValidator()
        {
            var issues = new List<string>();
            try
            {
                ValidateScenes(issues);
                ValidatePrefabs(issues);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (issues.Count > 0)
            {
                foreach (var issue in issues)
                {
                    Debug.LogError($"[SceneSetupValidator] {issue}");
                }

                EditorUtility.DisplayDialog(
                    "Scene Setup Validator",
                    $"Validation failed with {issues.Count} issue(s). Check the console for details.",
                    "OK");
                throw new InvalidOperationException("Scene setup validation failed.");
            }

            Debug.Log("[SceneSetupValidator] All scenes and prefabs passed validation.");
            EditorUtility.DisplayDialog("Scene Setup Validator", "All scenes and prefabs passed validation.", "OK");
        }

        [MenuItem("PureDOTS/Validation/Remove Missing Scripts (Scenes & Prefabs)", priority = 610)]
        public static void RemoveMissingScripts()
        {
            var modifications = new List<string>();

            try
            {
                RemoveMissingScriptsFromScenes(modifications);
                RemoveMissingScriptsFromPrefabs(modifications);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (modifications.Count == 0)
            {
                Debug.Log("[SceneSetupValidator] No missing scripts were found.");
                EditorUtility.DisplayDialog("Remove Missing Scripts", "No missing scripts were found in validated scenes/prefabs.", "OK");
                return;
            }

            foreach (var entry in modifications)
            {
                Debug.Log(entry);
            }

            EditorUtility.DisplayDialog(
                "Remove Missing Scripts",
                $"Removed missing scripts from {modifications.Count} asset(s). Check Console for details.",
                "OK");
        }

        private static void ValidateScenes(List<string> issues)
        {
            var guids = AssetDatabase.FindAssets("t:Scene");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!ShouldValidatePath(path))
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar(
                    "Scene Setup Validator",
                    $"Scanning scenes ({i + 1}/{guids.Length})",
                    (i + 1f) / Math.Max(1, guids.Length));

                ValidateSceneAsset(path, issues);
            }
        }

        private static void ValidatePrefabs(List<string> issues)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!ShouldValidatePath(path))
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar(
                    "Scene Setup Validator",
                    $"Scanning prefabs ({i + 1}/{guids.Length})",
                    (i + 1f) / Math.Max(1, guids.Length));

                ValidatePrefabAsset(path, issues);
            }
        }

        private static void RemoveMissingScriptsFromScenes(List<string> modifications)
        {
            var guids = AssetDatabase.FindAssets("t:Scene");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!ShouldValidatePath(path))
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar(
                    "Remove Missing Scripts",
                    $"Scanning scenes ({i + 1}/{guids.Length})",
                    (i + 1f) / Math.Max(1, guids.Length));

                RemoveMissingScriptsFromScene(path, modifications);
            }
        }

        private static void RemoveMissingScriptsFromPrefabs(List<string> modifications)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!ShouldValidatePath(path))
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar(
                    "Remove Missing Scripts",
                    $"Scanning prefabs ({i + 1}/{guids.Length})",
                    (i + 1f) / Math.Max(1, guids.Length));

                RemoveMissingScriptsFromPrefab(path, modifications);
            }
        }

        private static void ValidateSceneAsset(string path, List<string> issues)
        {
            Scene openedScene = default;
            var wasLoaded = false;
            try
            {
                var activeScene = SceneManager.GetSceneByPath(path);
                if (activeScene.IsValid() && activeScene.isLoaded)
                {
                    wasLoaded = true;
                    openedScene = activeScene;
                }
                else
                {
                    openedScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                ValidateHierarchy(openedScene.GetRootGameObjects(), $"Scene '{path}'", issues);
            }
            catch (Exception ex)
            {
                issues.Add($"Scene '{path}' could not be validated: {ex.Message}");
            }
            finally
            {
                if (!wasLoaded && openedScene.IsValid())
                {
                    EditorSceneManager.CloseScene(openedScene, true);
                }
            }
        }

        private static void RemoveMissingScriptsFromScene(string path, List<string> modifications)
        {
            Scene openedScene = default;
            var wasLoaded = false;

            try
            {
                var activeScene = SceneManager.GetSceneByPath(path);
                if (activeScene.IsValid() && activeScene.isLoaded)
                {
                    wasLoaded = true;
                    openedScene = activeScene;
                }
                else
                {
                    openedScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                var removed = RemoveMissingScriptsFromHierarchy(openedScene.GetRootGameObjects());
                if (removed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(openedScene);
                    if (EditorSceneManager.SaveScene(openedScene))
                    {
                        modifications.Add($"[SceneSetupValidator] Scene '{path}' - removed {removed} missing script(s).");
                    }
                    else
                    {
                        modifications.Add($"[SceneSetupValidator] Scene '{path}' - removed {removed} missing script(s) but failed to save.");
                    }
                }
            }
            catch (Exception ex)
            {
                modifications.Add($"[SceneSetupValidator] Scene '{path}' removal failed: {ex.Message}");
            }
            finally
            {
                if (!wasLoaded && openedScene.IsValid())
                {
                    EditorSceneManager.CloseScene(openedScene, true);
                }
            }
        }

        private static void RemoveMissingScriptsFromPrefab(string path, List<string> modifications)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                var removed = RemoveMissingScriptsFromHierarchy(new[] { root });
                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    modifications.Add($"[SceneSetupValidator] Prefab '{path}' - removed {removed} missing script(s).");
                }
            }
            catch (Exception ex)
            {
                modifications.Add($"[SceneSetupValidator] Prefab '{path}' removal failed: {ex.Message}");
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ValidatePrefabAsset(string path, List<string> issues)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                ValidateHierarchy(new[] { root }, $"Prefab '{path}'", issues);
            }
            catch (Exception ex)
            {
                issues.Add($"Prefab '{path}' could not be validated: {ex.Message}");
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ValidateHierarchy(IReadOnlyList<GameObject> roots, string context, List<string> issues)
        {
            if (roots == null || roots.Count == 0)
            {
                return;
            }

            var singletonHits = new Dictionary<Type, List<Component>>(SingletonAuthorings.Length);
            foreach (var definition in SingletonAuthorings)
            {
                singletonHits[definition.Type] = new List<Component>();
            }

            var stack = new Stack<Transform>(roots.Count);
            foreach (var root in roots)
            {
                if (root != null)
                {
                    stack.Push(root.transform);
                }
            }

            while (stack.Count > 0)
            {
                var transform = stack.Pop();
                var go = transform.gameObject;

                var missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missingCount > 0)
                {
                    issues.Add($"{context}: GameObject '{GetHierarchyPath(go)}' has {missingCount} missing script reference(s).");
                }

                foreach (var definition in SingletonAuthorings)
                {
                    var components = go.GetComponents(definition.Type);
                    if (components == null || components.Length == 0)
                    {
                        continue;
                    }

                    foreach (var component in components)
                    {
                        if (component != null)
                        {
                            singletonHits[definition.Type].Add(component);
                        }
                    }
                }

                for (var i = 0; i < transform.childCount; i++)
                {
                    stack.Push(transform.GetChild(i));
                }
            }

            foreach (var definition in SingletonAuthorings)
            {
                var hits = singletonHits[definition.Type];
                if (hits.Count <= 1)
                {
                    continue;
                }

                var locations = new string[hits.Count];
                for (var i = 0; i < hits.Count; i++)
                {
                    locations[i] = GetHierarchyPath(hits[i].gameObject);
                }

                issues.Add($"{context}: Found {hits.Count} instances of {definition.Label}. Locations: {string.Join(", ", locations)}");
            }
        }

        private static int RemoveMissingScriptsFromHierarchy(IReadOnlyList<GameObject> roots)
        {
            if (roots == null || roots.Count == 0)
            {
                return 0;
            }

            var removed = 0;
            var stack = new Stack<Transform>(roots.Count);
            foreach (var root in roots)
            {
                if (root != null)
                {
                    stack.Push(root.transform);
                }
            }

            while (stack.Count > 0)
            {
                var transform = stack.Pop();
                var go = transform.gameObject;
                var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    removed += missing;
                    EditorUtility.SetDirty(go);
                }

                for (var i = 0; i < transform.childCount; i++)
                {
                    stack.Push(transform.GetChild(i));
                }
            }

            return removed;
        }

        private static bool ShouldValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (path.StartsWith("Packages/com.unity", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Library/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("ProjectSettings/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Packages/com.moni", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "<null>";
            }

            var path = gameObject.name;
            var current = gameObject.transform.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }
    }
}
