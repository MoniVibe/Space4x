using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SubSceneBatchConverter
{
    private static readonly Type SubSceneType = ResolveSubSceneType();

    public static void ConvertScene(string scenePath, string subSceneName, string parentPath, params string[] childNames)
    {
        EnsureNotPlaying();

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"SubSceneBatchConverter: Failed to open scene '{scenePath}'.");
            return;
        }

        if (SubSceneType == null)
        {
            Debug.LogError("SubSceneBatchConverter: Unity.Scenes.SubScene type could not be resolved. Ensure Entities packages are installed.");
            return;
        }

        GameObject parent = null;
        if (!string.IsNullOrEmpty(parentPath))
        {
            parent = GameObject.Find(parentPath);
            if (parent == null)
            {
                Debug.LogError($"SubSceneBatchConverter: Could not find parent '{parentPath}' in scene '{scenePath}'.");
                return;
            }
        }

        var targets = CollectTargets(scene, parent, childNames);
        if (targets.Count == 0)
        {
            Debug.LogWarning($"SubSceneBatchConverter: No valid objects found to convert in scene '{scenePath}'.");
            return;
        }

        var assetPath = BuildSubSceneAssetPath(scenePath, subSceneName);
        var additiveScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        additiveScene.name = subSceneName;

        foreach (var go in targets)
        {
            go.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(go, additiveScene);
        }

        EditorSceneManager.SaveScene(additiveScene, assetPath);
        EditorSceneManager.CloseScene(additiveScene, true);

        var containerName = GetUniqueChildName(parent, subSceneName + "_SubScene");
        var container = new GameObject(containerName);
        if (parent != null)
        {
            container.transform.SetParent(parent.transform, false);
        }

        var subSceneComponent = (Component)container.AddComponent(SubSceneType);
        var serialized = new SerializedObject(subSceneComponent);
        serialized.FindProperty("_SceneAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath);
        var autoLoadProperty = serialized.FindProperty("AutoLoadScene");
        if (autoLoadProperty != null)
        {
            autoLoadProperty.boolValue = true;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static List<GameObject> CollectTargets(Scene scene, GameObject parent, IEnumerable<string> childNames)
    {
        var result = new List<GameObject>();
        foreach (var name in childNames)
        {
            GameObject candidate = null;
            if (parent != null)
            {
                var childTransform = parent.transform.Find(name);
                if (childTransform != null)
                {
                    candidate = childTransform.gameObject;
                }
            }

            if (candidate == null)
            {
                candidate = GameObject.Find(name);
            }

            if (candidate == null)
            {
                Debug.LogWarning($"SubSceneBatchConverter: Could not find '{name}' in scene '{scene.path}'. Skipping.");
                continue;
            }

            if (candidate.scene != scene)
            {
                Debug.LogWarning($"SubSceneBatchConverter: '{name}' is not part of scene '{scene.path}'. Skipping.");
                continue;
            }

            if (candidate.GetComponent(SubSceneType) != null)
            {
                Debug.Log($"SubSceneBatchConverter: '{name}' already contains a SubScene component; skipping.");
                continue;
            }

            result.Add(candidate);
        }

        return result;
    }

    private static string BuildSubSceneAssetPath(string scenePath, string subSceneName)
    {
        var directory = Path.GetDirectoryName(scenePath);
        var sceneBase = Path.GetFileNameWithoutExtension(scenePath);
        var sanitized = string.IsNullOrWhiteSpace(subSceneName) ? "SubScene" : subSceneName;
        var fileName = $"{sceneBase}_{sanitized}.unity";
        var combined = Path.Combine(directory ?? string.Empty, fileName).Replace("\\", "/");
        return AssetDatabase.GenerateUniqueAssetPath(combined);
    }

    private static string GetUniqueChildName(GameObject parent, string baseName)
    {
        if (parent == null)
        {
            return baseName;
        }

        var existing = new HashSet<string>();
        foreach (Transform child in parent.transform)
        {
            existing.Add(child.name);
        }

        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        var index = 1;
        string candidate;
        do
        {
            candidate = baseName + " (" + index++ + ")";
        } while (existing.Contains(candidate));

        return candidate;
    }

    private static Type ResolveSubSceneType()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly =>
            {
                try
                {
                    return assembly.GetType("Unity.Scenes.SubScene");
                }
                catch
                {
                    return null;
                }
            })
            .FirstOrDefault(t => t != null);
    }

    private static void EnsureNotPlaying()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            throw new InvalidOperationException("Cannot convert SubScenes while in Play Mode. Conversion aborted; exit play mode and retry.");
        }
    }
}
