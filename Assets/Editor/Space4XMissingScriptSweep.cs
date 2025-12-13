#if UNITY_EDITOR && false // Disabled: caused infinite sweep loop when editing scenes
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Space4X.EditorUtilities
{
    [InitializeOnLoad]
    public static class Space4XMissingScriptSweep
    {
        private const string MenuPath = "Tools/Space4X/Full Missing Script Sweep";
        private static bool s_pendingPostImportSweep;
        private static bool s_isRunning;

        static Space4XMissingScriptSweep()
        {
            SchedulePostImportSweep();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssetDatabase.importPackageCompleted += OnPackageImportFinished;
            AssetDatabase.importPackageCancelled += OnPackageImportFinished;
            AssetDatabase.importPackageFailed += OnPackageImportFailed;
        }

        [MenuItem(MenuPath, priority = 10)]
        public static void RunSweepMenu()
        {
            RunSweep("Menu");
        }

        private static void SchedulePostImportSweep()
        {
            if (s_pendingPostImportSweep)
                return;

            s_pendingPostImportSweep = true;
            EditorApplication.delayCall += TryRunScheduledSweep;
        }

        private static void OnPackageImportFinished(string _)
        {
            SchedulePostImportSweep();
        }

        private static void OnPackageImportFailed(string _, string __)
        {
            SchedulePostImportSweep();
        }

        private static void TryRunScheduledSweep()
        {
            if (!s_pendingPostImportSweep)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRunScheduledSweep;
                return;
            }

            s_pendingPostImportSweep = false;
            RunSweep("PostImport");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (s_isRunning)
                    return;

                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    SchedulePostImportSweep();
                    return;
                }

                RunSweep("Before Play Mode");
            }
        }

        private static void RunSweep(string reason)
        {
            if (s_isRunning)
                return;

            s_isRunning = true;
            try
            {
                int prefabRemoved = SweepPrefabs();
                int sceneRemoved = SweepScenes();

                if (prefabRemoved > 0 || sceneRemoved > 0)
                {
                    AssetDatabase.SaveAssets();
                }

                Debug.Log($"[MissingScriptSweep] Removed {prefabRemoved} missing scripts from prefabs and {sceneRemoved} from scenes ({reason}).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MissingScriptSweep] Sweep failed: {ex}");
            }
            finally
            {
                s_isRunning = false;
                EditorUtility.ClearProgressBar();
            }
        }

        private static int SweepPrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int removedTotal = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar(
                    "Missing Script Sweep",
                    $"Prefabs {i + 1}/{guids.Length}\n{path}",
                    guids.Length == 0 ? 0f : 0.5f * (i / (float)guids.Length));

                removedTotal += CleanPrefab(path);
            }

            return removedTotal;
        }

        private static int SweepScenes()
        {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            int removedTotal = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar(
                    "Missing Script Sweep",
                    $"Scenes {i + 1}/{guids.Length}\n{path}",
                    0.5f + (guids.Length == 0 ? 0f : 0.5f * (i / (float)guids.Length)));

                removedTotal += CleanScene(path);
            }

            return removedTotal;
        }

        private static int CleanPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return 0;

            int removed = CleanupGameObject(prefab);
            if (removed > 0)
            {
                EditorUtility.SetDirty(prefab);
                PrefabUtility.SavePrefabAsset(prefab);
            }

            return removed;
        }

        private static int CleanScene(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;

            var wasLoaded = TryGetOpenScene(path, out var scene);
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            int removed = CleanupScene(scene);

            if (!wasLoaded)
            {
                if (removed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                EditorSceneManager.CloseScene(scene, true);
            }
            else if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            return removed;
        }

        private static bool TryGetOpenScene(string path, out Scene scene)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var candidate = SceneManager.GetSceneAt(i);
                if (string.Equals(candidate.path, path, StringComparison.OrdinalIgnoreCase))
                {
                    scene = candidate;
                    return true;
                }
            }

            scene = default;
            return false;
        }

        private static int CleanupScene(Scene scene)
        {
            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                removed += CleanupGameObject(root);
            }

            return removed;
        }

        private static int CleanupGameObject(GameObject go)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            foreach (Transform child in go.transform)
            {
                removed += CleanupGameObject(child.gameObject);
            }

            return removed;
        }
    }
}
#endif
