#if SPACE4X_SANDBOX_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Scenes;
using Space4X.Rendering.Catalog;
using Space4X.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public class SetupRenderSandbox
{
    [MenuItem("Space4X/Setup/Create Render Sandbox")]
    public static void Setup()
    {
        var scenePath = "Assets/Scenes/Space4X_RenderSandbox.unity";
        var subScenePath = "Assets/Scenes/Space4X_RenderSandbox_SubScene.unity";
        
        // 1. Create Main Scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, scenePath);
        
        // 2. Create SubScene Asset
        var subScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SaveScene(subScene, subScenePath);
        
        // 3. Setup SubScene Content
        // Render Catalog
        var catalogGO = new GameObject("RenderCatalog");
        var authoring = catalogGO.AddComponent<RenderCatalogAuthoring>();
        
        var assetPath = "Assets/Data/Space4XRenderCatalog_Sandbox.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<Space4XRenderCatalogDefinition>(assetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<Space4XRenderCatalogDefinition>();
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var mat = Space4X.EditorUtilities.MaterialAssetUtility.GetOrCreateDefaultLitMaterial();
            
            catalog.Entries = new Space4XRenderCatalogDefinition.Entry[]
            {
                new Space4XRenderCatalogDefinition.Entry
                {
                    ArchetypeId = Space4XRenderKeys.Carrier,
                    Mesh = cube,
                    Material = mat,
                    BoundsCenter = Vector3.zero,
                    BoundsExtents = Vector3.one
                }
            };
            AssetDatabase.CreateAsset(catalog, assetPath);
        }
        authoring.CatalogDefinition = catalog;
        
        // Debug Spawner
        var spawnerGO = new GameObject("DebugSpawner");
        var spawner = spawnerGO.AddComponent<Space4XDebugSpawnerAuthoring>();
        spawner.ArchetypeId = Space4XRenderKeys.Carrier;
        spawnerGO.transform.position = new Vector3(0, 0, 20);
        
        EditorSceneManager.SaveScene(subScene, subScenePath);
        
        // 4. Link SubScene in Main Scene
        EditorSceneManager.OpenScene(scenePath);
        var subSceneGO = new GameObject("Space4X_RenderSandbox_SubScene");
        var subSceneComp = subSceneGO.AddComponent<SubScene>();
        subSceneComp.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
        subSceneComp.AutoLoadScene = true;
        
        // 5. Setup Camera
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 5, -10);
            cam.transform.LookAt(new Vector3(0, 0, 20));
        }
        
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("Render Sandbox Setup Complete.");
    }
}
#endif
