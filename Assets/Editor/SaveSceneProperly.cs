using UnityEditor;
using UnityEditor.SceneManagement;

public class SaveSceneProperly
{
    public static void Execute()
    {
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/Demo/Space4X_MiningDemo_SubScene.unity");
    }
}
