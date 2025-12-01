#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RemoveMissingScripts
{
    [MenuItem("Tools/Cleanup/Remove Missing Scripts In Open Scenes")]
    public static void CleanOpenScenes()
    {
        int removed = 0;
        foreach (var go in Object.FindObjectsOfType<GameObject>(true))
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        Debug.Log($"[Cleanup] Removed {removed} missing script components from open scenes.");
    }
}
#endif
