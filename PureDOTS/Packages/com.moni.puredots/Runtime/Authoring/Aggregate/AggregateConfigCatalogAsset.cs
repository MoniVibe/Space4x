#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Aggregate;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace PureDOTS.Authoring.Aggregate
{
    /// <summary>
    /// ScriptableObject for holding multiple AggregateTypeConfigAsset references.
    /// Supports additive merging (base + mods) and builds AggregateConfigCatalog BlobAsset.
    /// </summary>
    [CreateAssetMenu(fileName = "AggregateConfigCatalog", menuName = "PureDOTS/Aggregate/Aggregate Config Catalog", order = 2)]
    public class AggregateConfigCatalogAsset : ScriptableObject
    {
        [Header("Type Configs")]
        [Tooltip("Array of aggregate type config assets. Later configs with the same TypeId override earlier ones.")]
        public AggregateTypeConfigAsset[] TypeConfigs;

        /// <summary>
        /// Merges multiple AggregateConfigCatalogAssets into a single array of AggregateTypeConfigAsset.
        /// Later configs with the same TypeId override earlier ones.
        /// </summary>
        public static AggregateTypeConfigAsset[] MergeCatalogs(AggregateConfigCatalogAsset[] catalogs)
        {
            var mergedConfigs = new Dictionary<ushort, AggregateTypeConfigAsset>();

            foreach (var catalog in catalogs)
            {
                if (catalog == null || catalog.TypeConfigs == null) continue;

                foreach (var configAsset in catalog.TypeConfigs)
                {
                    if (configAsset == null) continue;
                    mergedConfigs[configAsset.TypeId] = configAsset;
                }
            }

            return mergedConfigs.Values.ToArray();
        }

        /// <summary>
        /// Builds a BlobAssetReference from merged catalog assets.
        /// </summary>
        public BlobAssetReference<AggregateConfigCatalog> BuildBlobAsset(params AggregateConfigCatalogAsset[] additionalCatalogs)
        {
            var allCatalogs = new List<AggregateConfigCatalogAsset> { this };
            if (additionalCatalogs != null)
            {
                allCatalogs.AddRange(additionalCatalogs);
            }

            var mergedConfigs = MergeCatalogs(allCatalogs.ToArray());
            return BuildBlobAssetFromConfigs(mergedConfigs);
        }

        /// <summary>
        /// Builds a BlobAssetReference from an array of AggregateTypeConfigAsset.
        /// Properly builds blob arrays for rules.
        /// </summary>
        public static BlobAssetReference<AggregateConfigCatalog> BuildBlobAssetFromConfigs(AggregateTypeConfigAsset[] configAssets)
        {
            if (configAssets == null || configAssets.Length == 0)
            {
                // Return empty catalog
                var emptyBuilder = new BlobBuilder(Allocator.Temp);
                ref var emptyRoot = ref emptyBuilder.ConstructRoot<AggregateConfigCatalog>();
                emptyBuilder.Allocate(ref emptyRoot.TypeConfigs, 0);
                var blob = emptyBuilder.CreateBlobAssetReference<AggregateConfigCatalog>(Allocator.Persistent);
                emptyBuilder.Dispose();
                return blob;
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<AggregateConfigCatalog>();
            var configArray = builder.Allocate(ref root.TypeConfigs, configAssets.Length);

            for (int i = 0; i < configAssets.Length; i++)
            {
                var asset = configAssets[i];
                if (asset == null) continue;

                var rules = asset.ToAggregationRules();
                var rulesArray = builder.Allocate(ref configArray[i].Rules, rules.Length);
                for (int j = 0; j < rules.Length; j++)
                {
                    rulesArray[j] = rules[j];
                }

                configArray[i].TypeId = asset.TypeId;
                configArray[i].CompositionChangeThreshold = asset.CompositionChangeThreshold;
            }

            var blobAsset = builder.CreateBlobAssetReference<AggregateConfigCatalog>(Allocator.Persistent);
            builder.Dispose();
            return blobAsset;
        }

        private void OnValidate()
        {
            // Ensure unique TypeIds in editor
            if (TypeConfigs == null) return;
            var distinctIds = new HashSet<ushort>();
            foreach (var config in TypeConfigs)
            {
                if (config == null) continue;
                if (distinctIds.Contains(config.TypeId))
                {
                    Debug.LogWarning($"Duplicate AggregateTypeConfig TypeId found: {config.TypeId} in {name}. Please ensure all TypeIds are unique.");
                }
                distinctIds.Add(config.TypeId);
            }
        }
    }

}
#endif

