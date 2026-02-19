#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Space4X.UI.Editor
{
    [InitializeOnLoad]
    public static class Space4XShipPresetCatalogBootstrap
    {
        private const string CatalogPath = "Assets/Resources/UI/Space4XShipPresetCatalog.asset";

        static Space4XShipPresetCatalogBootstrap()
        {
            EditorApplication.delayCall += EnsureCatalogAsset;
        }

        [MenuItem("Space4X/UI/Ensure Ship Preset Catalog")]
        public static void EnsureCatalogAsset()
        {
            if (Application.isBatchMode)
                return;

            var existing = AssetDatabase.LoadAssetAtPath<Space4XShipPresetCatalog>(CatalogPath);
            if (existing != null)
                return;

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/UI");

            var catalog = ScriptableObject.CreateInstance<Space4XShipPresetCatalog>();
            catalog.ApplyDefaultFleetCrawlSlice();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UnityEngine.Debug.Log($"[Space4XShipPresetCatalogBootstrap] Created default catalog at '{CatalogPath}'.");
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
