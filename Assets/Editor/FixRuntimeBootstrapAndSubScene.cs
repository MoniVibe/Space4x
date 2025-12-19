using UnityEngine;
using UnityEditor;
using PureDOTS.Rendering;
using Unity.Scenes;
using UnityEditor.SceneManagement;

public class FixRuntimeBootstrapAndSubScene
{
    [MenuItem("Tools/Space4X/Fix: Runtime Bootstrap and SubScene")]
    public static void Execute()
    {
        AssetDatabase.Refresh();

        // 1. Fix RenderCatalogBootstrap
        var bootstrapGO = GameObject.Find("RenderCatalogBootstrap");
        if (bootstrapGO != null)
        {
            var bootstrap = bootstrapGO.GetComponent<RenderPresentationCatalogRuntimeBootstrap>();
            if (bootstrap != null)
            {
                string catalogPath = "Assets/Data/Space4XRenderCatalog.asset";
                var catalog = AssetDatabase.LoadAssetAtPath<RenderPresentationCatalogDefinition>(catalogPath);
                if (catalog != null)
                {
                    Undo.RecordObject(bootstrap, "Assign Real Catalog");
                    bootstrap.CatalogDefinition = catalog;
                    EditorUtility.SetDirty(bootstrap);
                    Debug.Log($"✓ Assigned {catalogPath} to RenderPresentationCatalogRuntimeBootstrap");
                }
                else
                {
                    Debug.LogError($"Could not load catalog at {catalogPath}");
                }
            }
            else
            {
                Debug.LogError("RenderPresentationCatalogRuntimeBootstrap component missing on RenderCatalogBootstrap");
            }
        }
        else
        {
            Debug.LogError("RenderCatalogBootstrap GameObject not found");
        }

        // 2. Fix and Reimport SubScene
        var subSceneGO = GameObject.Find("Space4X_MiningDemo_SubScene");
        if (subSceneGO != null)
        {
            var subScene = subSceneGO.GetComponent<SubScene>();
            if (subScene != null)
            {
                Undo.RecordObject(subScene, "Enable AutoLoad");
                subScene.AutoLoadScene = true;
                EditorUtility.SetDirty(subScene);
                Debug.Log("✓ Ensured AutoLoadScene is enabled for Space4X_MiningDemo_SubScene");

                if (subScene.SceneAsset != null)
                {
                    string scenePath = AssetDatabase.GetAssetPath(subScene.SceneAsset);
                    AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"✓ Reimported SubScene asset: {scenePath}");
                }
                else
                {
                    Debug.LogWarning("SubScene has no SceneAsset assigned!");
                }
            }
            else
            {
                Debug.LogError("SubScene component missing on Space4X_MiningDemo_SubScene");
            }
        }
        else
        {
            Debug.LogError("Space4X_MiningDemo_SubScene GameObject not found");
        }

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("✓ Saved open scenes");
    }
}
