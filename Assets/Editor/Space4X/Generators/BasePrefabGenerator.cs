using System.IO;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.Generators
{
    /// <summary>
    /// Base class for prefab generators with common utilities.
    /// </summary>
    public abstract class BasePrefabGenerator : IPrefabGenerator
    {
        protected const string PrefabBasePath = "Assets/Prefabs/Space4X";

        public abstract bool Generate(PrefabMakerOptions options, PrefabMaker.GenerationResult result);
        public abstract void Validate(PrefabMaker.ValidationReport report);

        protected static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                var currentPath = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var nextPath = $"{currentPath}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = nextPath;
                }
            }
        }

        protected static GameObject LoadOrCreatePrefab(string prefabPath, string name, out bool isNew)
        {
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            isNew = existingPrefab == null;

            if (existingPrefab != null)
            {
                return PrefabUtility.LoadPrefabContents(prefabPath);
            }
            else
            {
                return new GameObject(name);
            }
        }

        protected static void SavePrefab(GameObject prefabObj, string prefabPath, bool isNew, PrefabMaker.GenerationResult result)
        {
            if (isNew)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabObj, prefabPath);
                Object.DestroyImmediate(prefabObj);
                result.CreatedCount++;
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(prefabObj, prefabPath);
                PrefabUtility.UnloadPrefabContents(prefabObj);
                result.UpdatedCount++;
            }
        }
    }
}

