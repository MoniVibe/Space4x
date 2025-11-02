using UnityEditor;

public static class BakeSceneRunner
{
    public static void BakeActiveScene()
    {
        EditorApplication.ExecuteMenuItem("Entities/Bake/Bake Scene");
    }

    public static void BakeAllScenes()
    {
        EditorApplication.ExecuteMenuItem("Entities/Bake/Bake All Scenes");
    }
}
