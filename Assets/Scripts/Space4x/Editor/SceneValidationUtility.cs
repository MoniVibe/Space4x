using System.Collections.Generic;
using System.Linq;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Space4X.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Space4X.Editor
{
    /// <summary>
    /// Utility for validating Space4X scenes against bootstrap/tagging checklist.
    /// </summary>
    public static class SceneValidationUtility
    {
        /// <summary>
        /// Validates a scene and returns a list of issues found.
        /// </summary>
        public static List<ValidationIssue> ValidateScene(Scene scene)
        {
            var issues = new List<ValidationIssue>();

            if (!scene.IsValid())
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Scene is not valid",
                    GameObjectPath = ""
                });
                return issues;
            }

            var rootObjects = scene.GetRootGameObjects();
            var hasPureDotsConfig = false;
            var hasSpatialPartition = false;

            foreach (var root in rootObjects)
            {
                // Check for PureDotsConfigAuthoring
                if (root.GetComponent<PureDotsConfigAuthoring>() != null)
                {
                    hasPureDotsConfig = true;
                }

                // Check for SpatialPartitionAuthoring
                if (root.GetComponent<SpatialPartitionAuthoring>() != null)
                {
                    hasSpatialPartition = true;
                }

                // Check for service locator patterns
                CheckForServiceLocators(root, issues);
            }

            // Validate required components
            if (!hasPureDotsConfig)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Missing PureDotsConfigAuthoring component. Required for TimeState, RewindState, and GameplayFixedStep singletons.",
                    GameObjectPath = "Scene Root"
                });
            }

            if (!hasSpatialPartition)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = "Missing SpatialPartitionAuthoring component. Required for spatial grid and registry queries.",
                    GameObjectPath = "Scene Root"
                });
            }

            return issues;
        }

        /// <summary>
        /// Validates all scenes in the project.
        /// </summary>
        public static Dictionary<string, List<ValidationIssue>> ValidateAllScenes()
        {
            var results = new Dictionary<string, List<ValidationIssue>>();

            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            foreach (var guid in sceneGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var issues = ValidateScene(scene);
                if (issues.Count > 0)
                {
                    results[path] = issues;
                }
                EditorSceneManager.CloseScene(scene, false);
            }

            return results;
        }

        /// <summary>
        /// Checks for service locator patterns in a GameObject hierarchy.
        /// </summary>
        private static void CheckForServiceLocators(GameObject root, List<ValidationIssue> issues)
        {
            // Check all MonoBehaviours for service locator patterns
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null) continue;

                var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(behaviour));
                if (string.IsNullOrEmpty(scriptPath)) continue;

                // Read script content to check for patterns
                var scriptContent = System.IO.File.ReadAllText(scriptPath);
                
                // Check for FindObjectOfType (common service locator pattern)
                if (scriptContent.Contains("FindObjectOfType") && !scriptPath.Contains("Editor"))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Message = $"Potential service locator pattern detected: FindObjectOfType in {behaviour.GetType().Name}",
                        GameObjectPath = GetGameObjectPath(behaviour.gameObject)
                    });
                }

                // Check for singleton Instance patterns
                if (scriptContent.Contains(".Instance") && scriptContent.Contains("static") && !scriptPath.Contains("Editor"))
                {
                    // This is a heuristic - may have false positives
                    if (scriptContent.Contains("get {") || scriptContent.Contains("get =>"))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Warning,
                            Message = $"Potential singleton Instance pattern detected in {behaviour.GetType().Name}",
                            GameObjectPath = GetGameObjectPath(behaviour.gameObject)
                        });
                    }
                }
            }
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            var path = obj.name;
            var parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        [MenuItem("Tools/Space4X/Validate All Scenes")]
        public static void ValidateAllScenesMenu()
        {
            var results = ValidateAllScenes();

            if (results.Count == 0)
            {
                Debug.Log("[SceneValidation] All scenes passed validation!");
                return;
            }

            Debug.LogWarning($"[SceneValidation] Found issues in {results.Count} scene(s):");

            foreach (var kvp in results)
            {
                Debug.LogWarning($"\nScene: {kvp.Key}");
                foreach (var issue in kvp.Value)
                {
                    var logMethod = issue.Severity == ValidationSeverity.Error ? 
                        (System.Action<string>)Debug.LogError : 
                        Debug.LogWarning;
                    logMethod($"  [{issue.Severity}] {issue.Message} ({issue.GameObjectPath})");
                }
            }
        }

        public class ValidationIssue
        {
            public ValidationSeverity Severity;
            public string Message;
            public string GameObjectPath;
        }

        public enum ValidationSeverity
        {
            Warning,
            Error
        }
    }
}

