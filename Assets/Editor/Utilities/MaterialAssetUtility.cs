#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.EditorUtilities
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Shared helpers for creating SRP-batcher compatible fallback materials.
    /// </summary>
    public static class MaterialAssetUtility
    {
        private const string DefaultMaterialPath = "Assets/Materials/Space4X_DefaultLit.mat";

        /// <summary>
        /// Ensures a URP/Lit material exists on disk that can be used anywhere we previously
        /// reached for the builtin Standard material (which is not SRP batcher compatible).
        /// </summary>
        public static Material GetOrCreateDefaultLitMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                UnityDebug.LogWarning("[MaterialAssetUtility] Could not find URP/Lit shader. Falling back to Standard which may still log SRP warnings.");
                shader = Shader.Find("Standard");
            }

            var created = new Material(shader)
            {
                name = "Space4X_DefaultLit"
            };

            AssetDatabase.CreateAsset(created, DefaultMaterialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return created;
        }
    }
}
#endif
