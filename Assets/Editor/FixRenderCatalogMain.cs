using UnityEngine;
using UnityEditor;
using PureDOTS.Rendering;

public class FixRenderCatalogMain
{
    [MenuItem("Tools/Space4X/Fix: Render Catalog Main")]
    public static void Execute()
    {
        var go = GameObject.Find("RenderCatalogMain");
        if (go == null)
        {
            go = GameObject.Find("RenderCatalog");
        }

        if (go == null)
        {
            Debug.LogError("RenderCatalogMain or RenderCatalog GameObject not found in the scene.");
            return;
        }

        Debug.Log($"Found RenderCatalog object: {go.name}");

        // Rename to RenderCatalog if needed
        if (go.name != "RenderCatalog")
        {
            Undo.RecordObject(go, "Rename RenderCatalog");
            go.name = "RenderCatalog";
            Debug.Log("Renamed to RenderCatalog");
        }

        // Remove missing scripts
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        
        // Ensure RenderPresentationCatalogAuthoring exists
        var authoring = go.GetComponent<RenderPresentationCatalogAuthoring>();
        if (authoring == null)
        {
            authoring = Undo.AddComponent<RenderPresentationCatalogAuthoring>(go);
            Debug.Log("Added RenderPresentationCatalogAuthoring.");
        }

        // Assign the v2 catalog
        string assetPath = "Assets/Data/Space4XRenderCatalog_v2.asset";
        var catalogAsset = AssetDatabase.LoadAssetAtPath<RenderPresentationCatalogDefinition>(assetPath);
        if (catalogAsset != null)
        {
            Undo.RecordObject(authoring, "Assign CatalogDefinition");
            authoring.CatalogDefinition = catalogAsset;
            Debug.Log($"Assigned CatalogDefinition from {assetPath}");
            EditorUtility.SetDirty(go);
        }
        else
        {
            Debug.LogError($"Could not load {assetPath}");
        }
    }
}
