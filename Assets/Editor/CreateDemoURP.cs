#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using System.IO;

public class CreateDemoURP
{
    public static void Execute()
    {
        string folderPath = "Assets/Resources/Rendering";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string assetPath = folderPath + "/DemoURP.asset";
        string rendererPath = folderPath + "/DemoURP_Renderer.asset";

        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (asset != null) 
        {
            Debug.Log("Asset already exists.");
            return;
        }

        // Create Renderer Data
        // UniversalRendererData is the standard renderer for URP
        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        rendererData.name = "DemoURP_Renderer";
        AssetDatabase.CreateAsset(rendererData, rendererPath);

        // Create URP Asset
        // UniversalRenderPipelineAsset.Create(ScriptableRendererData defaultRendererData) is the standard way to create it via code if available
        // If not, we can use CreateInstance and assign the renderer data manually if the field is accessible, or via reflection
        
        asset = UniversalRenderPipelineAsset.Create(rendererData);
        if (asset == null)
        {
             Debug.LogError("Failed to create UniversalRenderPipelineAsset via Create method.");
             return;
        }
        
        AssetDatabase.CreateAsset(asset, assetPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created DemoURP asset.");
    }
}
#endif
