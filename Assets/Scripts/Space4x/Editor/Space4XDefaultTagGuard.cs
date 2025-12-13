#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Prevents automation scripts from re-registering Unity's built-in tags (MainCamera, Player, etc).
    /// Runs once per domain reload, strips any duplicate defaults from TagManager, and keeps future edits idempotent.
    /// </summary>
    [InitializeOnLoad]
    static class Space4XDefaultTagGuard
    {
        private static readonly string[] s_unityDefaultTags =
        {
            "Untagged",
            "Respawn",
            "Finish",
            "EditorOnly",
            "MainCamera",
            "Player",
            "GameController"
        };

        private static bool s_registered;
        private static bool s_checked;

        static Space4XDefaultTagGuard()
        {
            if (s_registered)
                return;

            s_registered = true;
            EditorApplication.delayCall += RemoveDuplicateDefaultTags;
        }

        private static void RemoveDuplicateDefaultTags()
        {
            if (s_checked)
                return;

            s_checked = true;

            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
                return;

            var serializedTagManager = new SerializedObject(tagManagerAssets[0]);
            var tagsProp = serializedTagManager.FindProperty("tags");
            if (tagsProp == null)
                return;

            bool modified = false;
            for (int i = tagsProp.arraySize - 1; i >= 0; i--)
            {
                var tagProp = tagsProp.GetArrayElementAtIndex(i);
                if (IsUnityDefaultTag(tagProp.stringValue))
                {
                    tagsProp.DeleteArrayElementAtIndex(i);
                    modified = true;
                }
            }

            if (!modified)
                return;

            serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[Space4XDefaultTagGuard] Removed duplicate Unity default tags from TagManager asset.");
        }

        private static bool IsUnityDefaultTag(string tagValue)
        {
            return Array.IndexOf(s_unityDefaultTags, tagValue) >= 0;
        }
    }
}
#endif
