using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class EnableMiningDemo
{
    [MenuItem("Tools/Space4X/Enable Mining Demo")]
    public static void Execute()
    {
        var go = GameObject.Find("Space4X_MiningDemo");
        if (go != null)
        {
            if (!go.activeSelf)
            {
                Undo.RecordObject(go, "Enable Space4X_MiningDemo");
                go.SetActive(true);
                EditorSceneManager.SaveScene(go.scene);
                Debug.Log("✓ Enabled Space4X_MiningDemo");
            }
            else
            {
                Debug.Log("Space4X_MiningDemo is already enabled.");
            }
        }
        else
        {
            Debug.LogError("Space4X_MiningDemo not found!");
        }
    }
}
