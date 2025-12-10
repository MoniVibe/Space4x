using UnityEngine;
using UnityEditor;
using Space4X.Rendering.Catalog;
using Unity.Scenes;
using Space4X.Rendering;

public class CreateRenderCatalog
{
    [MenuItem("Space4X/Setup/Create Render Catalog")]
    public static void Create()
    {
        // 1. Create Asset
        var assetPath = "Assets/Data/Space4XRenderCatalog.asset";
        System.IO.Directory.CreateDirectory("Assets/Data");
        
        var catalog = AssetDatabase.LoadAssetAtPath<Space4XRenderCatalogDefinition>(assetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<Space4XRenderCatalogDefinition>();
            
            // Find default mesh/material
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            
            catalog.Entries = new Space4XRenderCatalogDefinition.Entry[]
            {
                new Space4XRenderCatalogDefinition.Entry
                {
                    ArchetypeId = Space4XRenderKeys.Carrier, // 200
                    Mesh = cube,
                    Material = mat,
                    BoundsCenter = Vector3.zero,
                    BoundsExtents = Vector3.one // 2x2x2 cube
                }
            };
            
            AssetDatabase.CreateAsset(catalog, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created catalog asset at {assetPath}");
        }

        // 2. Create GameObject
        var goName = "Space4XRenderCatalog";
        var go = GameObject.Find(goName);
        if (go == null)
        {
            go = new GameObject(goName);
        }

        var authoring = go.GetComponent<RenderCatalogAuthoring>();
        if (authoring == null) authoring = go.AddComponent<RenderCatalogAuthoring>();
        
        authoring.CatalogDefinition = catalog;
        
        // 3. Create SubScene
        var subSceneName = "Space4X_Bootstrap";
        var subSceneGO = GameObject.Find(subSceneName);
        SubScene subScene = null;
        
        if (subSceneGO == null)
        {
            subSceneGO = new GameObject(subSceneName);
            subScene = subSceneGO.AddComponent<SubScene>();
            // We need to assign a scene asset to the SubScene. 
            // Creating a scene asset via script and assigning it is a bit complex.
            // For now, let's just leave the GO in the main scene and let the user know 
            // they might need to move it to a SubScene manually if baking doesn't happen.
            // However, we can try to create a scene file.
            
            var scenePath = $"Assets/Scenes/{subSceneName}.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            
            // We can't easily create a scene and assign it to SubScene without EditorSceneManager
            // and it might close the current scene.
            // Let's just create the GO and log instructions.
        }
        else
        {
            subScene = subSceneGO.GetComponent<SubScene>();
        }

        if (subScene != null)
        {
            go.transform.SetParent(subScene.transform);
        }
        
        Selection.activeGameObject = go;
        Debug.Log("Created RenderCatalogAuthoring GameObject. Please ensure it is in a SubScene for baking.");
    }
}
