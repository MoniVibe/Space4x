using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class DiagnoseRenderCatalog
{
    [MenuItem("Tools/Space4X/Diagnose Render Catalog")]
    public static void Execute()
    {
        var go = GameObject.Find("RenderCatalog");
        if (go == null)
        {
            Debug.Log("No RenderCatalog found in active scene.");
            return;
        }

        Debug.Log($"Inspecting {go.name} in scene {go.scene.name}");
        
        var components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            var c = components[i];
            if (c == null)
            {
                Debug.LogError($"Component at index {i} is missing (null)!");
            }
            else
            {
                Debug.Log($"Component {i}: {c.GetType().FullName}");
            }
        }

        // Check if we are in the SubScene
        if (go.scene.path.Contains("Space4X_MiningDemo_SubScene"))
        {
            Debug.LogWarning("Found RenderCatalog inside the SubScene! This might be a duplicate.");
            // Optional: Delete it if it's just a leftover
            // Undo.DestroyObjectImmediate(go);
        }
    }
}
