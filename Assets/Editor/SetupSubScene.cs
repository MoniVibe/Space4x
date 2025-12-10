using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Scenes;
using Space4X.Rendering.Catalog;

public class SetupSubScene
{
    [MenuItem("Space4X/Setup/Fix SubScene")]
    public static void Fix()
    {
        var bootstrapScenePath = "Assets/Scenes/Space4X_Bootstrap.unity";
        var mainScenePath = "Assets/temp12.unity";
        
        // 1. Create Bootstrap Scene
        var bootstrapScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        
        // 2. Create Catalog GO
        var go = new GameObject("Space4XRenderCatalog");
        var authoring = go.AddComponent<RenderCatalogAuthoring>();
        var assetPath = "Assets/Data/Space4XRenderCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<Space4XRenderCatalogDefinition>(assetPath);
        authoring.CatalogDefinition = catalog;
        
        // 3. Save Bootstrap Scene
        EditorSceneManager.SaveScene(bootstrapScene, bootstrapScenePath);
        
        // 4. Open Main Scene
        var mainScene = EditorSceneManager.OpenScene(mainScenePath);
        
        // 5. Create SubScene GO
        var subSceneGO = new GameObject("Space4X_Bootstrap");
        var subScene = subSceneGO.AddComponent<SubScene>();
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrapScenePath);
        subScene.SceneAsset = sceneAsset;
        
        // 6. Ensure AutoLoad
        subScene.AutoLoadScene = true;
        
        EditorSceneManager.SaveScene(mainScene);
        Debug.Log("Fixed SubScene setup.");
    }
}
