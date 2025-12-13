#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Space4X.EditorUtilities
{
    /// <summary>
    /// Utility helpers for normalizing file system paths to Unity asset-relative paths.
    /// </summary>
    public static class AssetPathUtil
    {
        /// <summary>
        /// Converts an absolute or mixed-separator path to an Assets-relative path understood by AssetDatabase.
        /// </summary>
        public static string ToAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            path = path.Replace('\\', '/');

            // Already asset-relative
            if (path.StartsWith("Assets/", StringComparison.Ordinal) || path.Equals("Assets", StringComparison.Ordinal))
            {
                return path;
            }

            // Convert from absolute Application.dataPath to Assets-relative
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (path.StartsWith(dataPath, StringComparison.Ordinal))
            {
                return "Assets" + path.Substring(dataPath.Length);
            }

            return path; // Fallback path (still safer than throwing)
        }

        /// <summary>
        /// Alias for ToAssetPath to make intent explicit when normalizing before AssetDatabase calls.
        /// </summary>
        public static string ToAssetRelativePath(string path) => ToAssetPath(path);
    }
}
#endif
