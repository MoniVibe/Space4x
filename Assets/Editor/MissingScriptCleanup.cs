using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Space4X.EditorUtilities
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Removes Missing Script components from every GameObject in the active scene
    /// so the Inspector stops throwing InvalidOperationException during serialization.
    /// </summary>
    public static class MissingScriptCleanup
    {
        [MenuItem("Tools/Space4X/Cleanup Missing Scripts In Scene")]
        private static void CleanupActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[MissingScriptCleanup] No active scene to clean.");
                return;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                CleanupRecursive(root);
            }

            Debug.Log($"[MissingScriptCleanup] Cleared missing scripts in scene '{scene.path}'.");
        }

        private static void CleanupRecursive(GameObject go)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            foreach (Transform child in go.transform)
            {
                CleanupRecursive(child.gameObject);
            }
        }
    }
}
