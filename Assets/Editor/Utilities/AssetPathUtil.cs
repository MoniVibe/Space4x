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
            var dataPath = Application.dataPath.Replace('\\', '/');

            if (path.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + path.Substring(dataPath.Length);
            }

            return path;
        }

        /// <summary>
        /// Alias for ToAssetPath to make intent explicit when normalizing before AssetDatabase calls.
        /// </summary>
        public static string ToAssetRelativePath(string path) => ToAssetPath(path);
    }
}
