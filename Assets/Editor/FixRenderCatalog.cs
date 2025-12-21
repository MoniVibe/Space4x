using UnityEngine;
using UnityEditor;
using PureDOTS.Rendering;
using System.Collections.Generic;

public class FixRenderCatalog
{
    public static void Execute()
    {
        var go = GameObject.Find("RenderCatalog");
        if (go == null)
        {
            Debug.LogError("RenderCatalog GameObject not found in the scene.");
            return;
        }

        Debug.Log($"Found RenderCatalog: {go.name}");

        // Remove missing scripts
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        Debug.Log("Removed missing scripts.");

        // Check if RenderPresentationCatalogAuthoring exists
        var authoring = go.GetComponent<RenderPresentationCatalogAuthoring>();
        if (authoring == null)
        {
            authoring = go.AddComponent<RenderPresentationCatalogAuthoring>();
            Debug.Log("Added RenderPresentationCatalogAuthoring.");
        }
        else
        {
            Debug.Log("RenderPresentationCatalogAuthoring already exists.");
        }

        // Try to find and assign the catalog asset
        if (authoring.CatalogDefinition == null)
        {
            string assetPath = "Assets/Data/Space4XRenderCatalog_v2.asset";
            var catalogAsset = AssetDatabase.LoadAssetAtPath<RenderPresentationCatalogDefinition>(assetPath);
            if (catalogAsset != null)
            {
                authoring.CatalogDefinition = catalogAsset;
                Debug.Log($"Assigned CatalogDefinition from {assetPath}");
                EditorUtility.SetDirty(go);
            }
            else
            {
                Debug.LogWarning($"Could not load {assetPath} as RenderPresentationCatalogDefinition. It might be the old type or missing.");
                
                // Try to find any RenderPresentationCatalogDefinition
                var guids = AssetDatabase.FindAssets("t:RenderPresentationCatalogDefinition");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    catalogAsset = AssetDatabase.LoadAssetAtPath<RenderPresentationCatalogDefinition>(path);
                    authoring.CatalogDefinition = catalogAsset;
                    Debug.Log($"Assigned alternative CatalogDefinition from {path}");
                    EditorUtility.SetDirty(go);
                }
            }
        }
    }
}
