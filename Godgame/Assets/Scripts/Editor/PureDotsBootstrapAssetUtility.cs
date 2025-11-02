#if UNITY_EDITOR
using System.Collections.Generic;
using Godgame.Authoring;
using PureDOTS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Godgame.Editor
{
    public static class PureDotsBootstrapAssetUtility
    {
        private const string ResourceCatalogPath = "Assets/Settings/PureDotsResourceTypes.asset";
        private const string RuntimeConfigPath = "Assets/Settings/PureDotsRuntimeConfig.asset";

        public static void EnsureBootstrapAssets()
        {
            var catalog = EnsureResourceCatalog();
            EnsureRuntimeConfig(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PureDotsBootstrapAssetUtility] Ensured PureDOTS runtime config and resource catalog assets.");
        }

        private static ResourceTypeCatalog EnsureResourceCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ResourceTypeCatalog>(ResourceCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ResourceTypeCatalog>();
                catalog.name = "PureDotsResourceTypes";
                AssetDatabase.CreateAsset(catalog, ResourceCatalogPath);
            }

            if (catalog.entries == null)
            {
                catalog.entries = new List<ResourceTypeDefinition>();
            }

            catalog.entries.Clear();
            catalog.entries.Add(new ResourceTypeDefinition
            {
                id = "wood",
                displayColor = new Color(0.74f, 0.51f, 0.25f, 1f)
            });
            catalog.entries.Add(new ResourceTypeDefinition
            {
                id = "stone",
                displayColor = new Color(0.52f, 0.54f, 0.56f, 1f)
            });

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void EnsureRuntimeConfig(ResourceTypeCatalog catalog)
        {
            var config = AssetDatabase.LoadAssetAtPath<PureDotsRuntimeConfig>(RuntimeConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PureDotsRuntimeConfig>();
                config.name = "PureDotsRuntimeConfig";
                AssetDatabase.CreateAsset(config, RuntimeConfigPath);
            }

            var serialized = new SerializedObject(config);
            serialized.FindProperty("_schemaVersion").intValue = PureDotsRuntimeConfig.LatestSchemaVersion;
            serialized.FindProperty("_resourceTypes").objectReferenceValue = catalog;

            var pooling = serialized.FindProperty("_pooling");
            if (pooling != null)
            {
                pooling.FindPropertyRelative("nativeListCapacity").intValue = 64;
                pooling.FindPropertyRelative("nativeQueueCapacity").intValue = 64;
                pooling.FindPropertyRelative("defaultEntityPrewarmCount").intValue = 0;
                pooling.FindPropertyRelative("entityPoolMaxReserve").intValue = 128;
                pooling.FindPropertyRelative("ecbPoolCapacity").intValue = 32;
                pooling.FindPropertyRelative("ecbWriterPoolCapacity").intValue = 32;
                pooling.FindPropertyRelative("resetOnRewind").boolValue = true;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }
    }
}
#endif
