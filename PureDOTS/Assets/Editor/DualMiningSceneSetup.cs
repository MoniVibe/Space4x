using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DualMiningSceneSetup
{
    private const string RootName = "ShowcaseRoot";
    private const string GodgameLoopName = "GodgameLoop";
    private const string Space4xLoopName = "Space4XLoop";

    private const string ResourcePrefabPath = "Assets/PureDOTS/Prefabs/ResourceNode.prefab";
    private const string StorehousePrefabPath = "Assets/PureDOTS/Prefabs/Storehouse.prefab";
    private const string VillagerSpawnerPrefabPath = "Assets/PureDOTS/Prefabs/VillagerSpawner.prefab";

    private static readonly Vector3 GodgameOffset = new Vector3(-200f, 0f, 0f);
    private static readonly Vector3 Space4xOffset = new Vector3(200f, 0f, 0f);

    [MenuItem("PureDOTS/Showcase/Reset Dual Mining Loops")]
    public static void ResetDualMiningLoops()
    {
        if (!EnsureActiveScene())
        {
            return;
        }

        var root = EnsureRoot(RootName);
        var godgameLoop = EnsureChild(root, GodgameLoopName);
        var space4xLoop = EnsureChild(root, Space4xLoopName);

        ClearChildren(godgameLoop.transform);
        ClearChildren(space4xLoop.transform);

        PopulateLoop(godgameLoop.transform, GodgameOffset, LoopPreset.Godgame);
        PopulateLoop(space4xLoop.transform, Space4xOffset, LoopPreset.Space4X);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private enum LoopPreset
    {
        Godgame,
        Space4X
    }

    private static bool EnsureActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded)
        {
            Debug.LogError("DualMiningSceneSetup: No scene is currently loaded. Open a scene before running this command.");
            return false;
        }

        return true;
    }

    private static GameObject EnsureRoot(string name)
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == name)
            {
                return go;
            }
        }

        var root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, "Create Showcase Root");
        return root;
    }

    private static GameObject EnsureChild(GameObject parent, string childName)
    {
        var child = parent.transform.Find(childName);
        if (child != null)
        {
            return child.gameObject;
        }

        var go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, "Create Showcase Loop");
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static void ClearChildren(Transform parent)
    {
        var toRemove = new List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
        {
            toRemove.Add(parent.GetChild(i).gameObject);
        }

        foreach (var child in toRemove)
        {
            Undo.DestroyObjectImmediate(child);
        }
    }

    private static void PopulateLoop(Transform parent, Vector3 origin, LoopPreset preset)
    {
        parent.position = origin;

        var storehouse = InstantiatePrefab(StorehousePrefabPath, parent, origin + new Vector3(0f, 0f, 0f));
        storehouse.name = preset == LoopPreset.Godgame ? "GodgameStorehouse" : "Space4XStorehouse";

        var spawner = InstantiatePrefab(VillagerSpawnerPrefabPath, parent, origin + new Vector3(6f, 0f, 0f));
        spawner.name = preset == LoopPreset.Godgame ? "GodgameVillagerSpawner" : "Space4XVillagerSpawner";

        var resourcePositions = new[]
        {
            origin + new Vector3(-8f, 0f, 6f),
            origin + new Vector3(-8f, 0f, -6f),
            origin + new Vector3(-14f, 0f, 0f)
        };

        for (int i = 0; i < resourcePositions.Length; i++)
        {
            var node = InstantiatePrefab(ResourcePrefabPath, parent, resourcePositions[i]);
            node.name = preset == LoopPreset.Godgame
                ? $"GodgameResourceNode_{i + 1}"
                : $"Space4XResourceNode_{i + 1}";
        }
    }

    private static GameObject InstantiatePrefab(string assetPath, Transform parent, Vector3 worldPosition)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogError($"DualMiningSceneSetup: Unable to load prefab at '{assetPath}'.");
            return new GameObject("MissingPrefab");
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
        {
            Debug.LogError($"DualMiningSceneSetup: Failed to instantiate prefab '{prefab.name}'.");
            return new GameObject("FailedPrefab");
        }

        Undo.RegisterCreatedObjectUndo(instance, "Instantiate Mining Prefab");
        instance.transform.position = worldPosition;
        return instance;
    }
}
