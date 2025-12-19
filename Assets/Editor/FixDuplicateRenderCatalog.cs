using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class FixDuplicateRenderCatalog
{
    [MenuItem("Tools/Space4X/Fix: Remove Duplicate RenderCatalog")]
    public static void Execute()
    {
        var subScenePath = "Assets/Scenes/Demo/Space4X_MiningDemo_SubScene.unity";
        var scene = EditorSceneManager.OpenScene(subScenePath, OpenSceneMode.Additive);
        
        if (!scene.IsValid())
        {
            Debug.LogError($"Could not open scene {subScenePath}");
            return;
        }

        var roots = scene.GetRootGameObjects();
        GameObject duplicateCatalog = null;
        foreach (var go in roots)
        {
            if (go.name == "RenderCatalog")
            {
                duplicateCatalog = go;
                break;
            }
        }

        if (duplicateCatalog != null)
        {
            Undo.DestroyObjectImmediate(duplicateCatalog);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"✓ Removed duplicate RenderCatalog from {subScenePath}");
        }
        else
        {
            Debug.Log($"No RenderCatalog found in {subScenePath}");
        }

        EditorSceneManager.CloseScene(scene, true);
    }
}
