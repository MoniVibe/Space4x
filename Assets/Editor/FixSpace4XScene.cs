using UnityEngine;
using UnityEditor;
using Unity.Scenes;
using UnityEditor.SceneManagement;

public class FixSpace4XScene
{
    [MenuItem("Tools/Space4X/Fix: Space4X Scene SubScene")]
    public static void Execute()
    {
        var subSceneGO = GameObject.Find("Space4X_MiningDemo_SubScene");
        if (subSceneGO == null)
        {
            // Try to find it by other names or create it
            subSceneGO = GameObject.Find("Space4X SubScene"); // Check if this is the one intended, but user said "Space4X_MiningDemo_SubScene"
            
            if (subSceneGO != null)
            {
                Debug.Log($"Found 'Space4X SubScene', checking if it should be renamed or if we need a new one.");
                // The user specifically said "Select the GameObject named Space4X_MiningDemo_SubScene".
                // If it doesn't exist, we should probably create it or rename the existing one if it's the intended target.
                // But 'Space4X SubScene' points to 'Space4XConfig.unity', which is different.
            }
            
            subSceneGO = new GameObject("Space4X_MiningDemo_SubScene");
            Undo.RegisterCreatedObjectUndo(subSceneGO, "Create Space4X_MiningDemo_SubScene");
        }

        var subScene = subSceneGO.GetComponent<SubScene>();
        if (subScene == null)
        {
            subScene = Undo.AddComponent<SubScene>(subSceneGO);
        }

        var scenePath = "Assets/Scenes/Demo/Space4X_MiningDemo_SubScene.unity";
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        
        if (sceneAsset != null)
        {
            Undo.RecordObject(subScene, "Assign SubScene Asset");
            subScene.SceneAsset = sceneAsset;
            subScene.AutoLoadScene = true;
            EditorUtility.SetDirty(subScene);
            Debug.Log($"✓ Assigned {scenePath} to {subSceneGO.name}");
        }
        else
        {
            Debug.LogError($"Could not find scene asset at {scenePath}");
        }

        // Check for authoring objects in main scene
        var authoringGO = GameObject.Find("Space4X_MiningDemo");
        if (authoringGO != null && authoringGO.scene == EditorSceneManager.GetActiveScene())
        {
            Debug.LogWarning("Found Space4X_MiningDemo in main scene! It should be in the SubScene.");
            // We can't easily move it via script without opening the subscene, but we can warn.
            // Or we can try to move it if the subscene is loaded.
        }

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
}
