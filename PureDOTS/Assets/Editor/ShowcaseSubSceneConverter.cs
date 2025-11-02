using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShowcaseSubSceneConverter
{
    public static void ConvertSceneToSubScene(string scenePath, string rootName, params string[] childNames)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            Debug.LogError("ConvertSceneToSubScene: scenePath is null or empty.");
            return;
        }

        if (childNames == null || childNames.Length == 0)
        {
            Debug.LogError("ConvertSceneToSubScene: No child names provided for conversion.");
            return;
        }

        // Ensure edit mode
        if (Application.isPlaying)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => ConvertSceneToSubScene(scenePath, rootName, childNames);
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"ConvertSceneToSubScene: Failed to open scene '{scenePath}'.");
            return;
        }

        var selection = new List<GameObject>();
        foreach (var name in childNames)
        {
            GameObject target = null;
            if (!string.IsNullOrEmpty(rootName))
            {
                var fullPath = $"/{rootName}/{name}";
                target = GameObject.Find(fullPath);
            }

            if (target == null)
            {
                target = GameObject.Find(name);
            }

            if (target == null)
            {
                Debug.LogWarning($"ConvertSceneToSubScene: Could not locate '{name}' in scene '{scenePath}'.");
                continue;
            }

            selection.Add(target);
        }

        if (selection.Count == 0)
        {
            Debug.LogWarning($"ConvertSceneToSubScene: No matching objects found to convert in scene '{scenePath}'.");
            return;
        }

        Selection.objects = selection.Cast<UnityEngine.Object>().ToArray();

        if (!EditorApplication.ExecuteMenuItem("Entities/Convert To Entity Scene"))
        {
            Debug.LogError("ConvertSceneToSubScene: Failed to execute 'Entities/Convert To Entity Scene'. Ensure Entities package menu is available.");
            return;
        }

        EditorSceneManager.SaveScene(scene);
    }

    public static void RunShowcaseConversion()
    {
        ConvertSceneToSubScene(
            "Assets/Showcase/Scenes/GodgameMining.unity",
            "GodgameLoop",
            "GodgameSystems",
            "ResourceNode",
            "Storehouse",
            "VillagerSpawner");

        ConvertSceneToSubScene(
            "Assets/Showcase/Scenes/Space4XMining.unity",
            "Space4XLoop",
            "Space4XSystems",
            "AsteroidNode",
            "OrbitalDepot",
            "MiningVesselSpawner");

        EditorSceneManager.OpenScene("Assets/Scenes/PureDotsTemplate.unity", OpenSceneMode.Single);
    }
}
