using UnityEditor;
using UnityEngine;

public static class RemoveMissingScriptsInProject
{
    [MenuItem("Tools/Cleanup/Remove Missing Scripts In Project")]
    public static void CleanProject()
    {
        var guids = AssetDatabase.FindAssets("t:GameObject");
        int removed = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go) removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if ((i % 50) == 0) EditorUtility.DisplayProgressBar("Cleanup", path, (float)i/guids.Length);
        }
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cleanup] Removed {removed} missing script components from project prefabs.");
    }
}
