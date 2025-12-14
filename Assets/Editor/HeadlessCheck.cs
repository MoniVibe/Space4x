using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.IO;

public class HeadlessCheck
{
    public static void Check()
    {
        // 1. Fix PC_RPAsset
        string rpAssetPath = "Assets/Settings/PC_RPAsset.asset";
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rpAssetPath);
        if (asset != null)
        {
            // Force reimport
            AssetDatabase.ImportAsset(rpAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"Reimported {rpAssetPath}");
        }
        else
        {
            Debug.LogError($"Could not load {rpAssetPath}");
        }

        // 2. Clean HeadlessBootstrap
        string scenePath = "Assets/Scenes/HeadlessBootstrap.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        
        GameObject[] roots = scene.GetRootGameObjects();
        Debug.Log($"Scene {scene.name} has {roots.Length} root objects.");
        
        bool changed = false;
        foreach (var go in roots)
        {
            Debug.Log($"Root: {go.name}");
            if (go.name.Contains("SubScene"))
            {
                Debug.Log($"Found SubScene: {go.name}. Deleting...");
                GameObject.DestroyImmediate(go);
                changed = true;
            }
        }
        
        if (changed)
        {
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Scene saved with changes.");
        }
        else
        {
            Debug.Log("No SubScenes found to delete.");
        }
    }
}
