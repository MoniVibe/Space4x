using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SaveCurrentScene
{
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Saved scene: {scene.path}");
    }
}
