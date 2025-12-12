#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RemoveMissingScripts
{
    [MenuItem("Tools/Cleanup/Remove Missing Scripts In Open Scenes")]
    public static void CleanOpenScenes()
    {
        int removed = 0;
        var objects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (var go in objects)
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        Debug.Log($"[Cleanup] Removed {removed} missing script components from open scenes.");
    }
}
#endif
