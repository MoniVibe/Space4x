using UnityEngine;
using UnityEditor;
using Unity.Scenes;

public class SetupSubScene
{
    public static void Execute()
    {
        var go = GameObject.Find("MiningDemoSubScene");
        if (go == null)
        {
            Debug.LogError("GameObject 'MiningDemoSubScene' not found.");
            return;
        }

        var subScene = go.GetComponent<SubScene>();
        if (subScene == null)
        {
            Debug.LogError("SubScene component not found.");
            return;
        }

        var scenePath = "Assets/Scenes/Demo/Space4X_MiningDemo_SubScene.unity";
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        
        if (sceneAsset == null)
        {
            Debug.LogError($"SceneAsset not found at {scenePath}");
            return;
        }

        subScene.SceneAsset = sceneAsset;
        Debug.Log("Successfully assigned SceneAsset to SubScene.");
    }
}
