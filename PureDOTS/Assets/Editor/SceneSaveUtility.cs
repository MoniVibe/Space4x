using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class SceneSaveUtility
{
    public static void SaveActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.SaveScene(scene);
        }
    }
}
