using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class FixDuplicateConfig
{
    [MenuItem("Tools/Space4X/Fix: Remove Duplicate Config from Mining Demo")]
    public static void Execute()
    {
        var scenePath = "Assets/Scenes/Demo/Space4X_MiningDemo_SubScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        
        if (!scene.IsValid())
        {
            Debug.LogError($"Could not open scene {scenePath}");
            return;
        }

        // List of objects to remove that are provided by Space4XConfig.unity
        string[] duplicates = new string[]
        {
            "PureDotsConfigRoot",
            "SpatialPartitionRoot",
            "PhysicsStep" // Assuming this is also global, but let's be careful. TimeState comes from PureDotsConfig.
        };

        bool changed = false;
        foreach (var name in duplicates)
        {
            var go = FindRootObject(scene, name);
            if (go != null)
            {
                Undo.DestroyObjectImmediate(go);
                Debug.Log($"✓ Removed duplicate {name} from {scenePath}");
                changed = true;
            }
        }

        if (changed)
        {
            EditorSceneManager.SaveScene(scene);
        }
        else
        {
            Debug.Log("No duplicates found to remove.");
        }

        EditorSceneManager.CloseScene(scene, true);
    }

    private static GameObject FindRootObject(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == name) return go;
        }
        return null;
    }
}
