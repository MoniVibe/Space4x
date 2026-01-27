#if UNITY_EDITOR || UNITY_STANDALONE
using System.Collections.Generic;
using System.Linq;
using PureDOTS.Runtime.Construction;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Construction
{
    /// <summary>
    /// ScriptableObject that holds array of BuildingPatternSpecAsset references.
    /// Supports additive merging (base + mods) and can build BuildingPatternCatalog BlobAsset.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingPatternCatalog", menuName = "PureDOTS/Construction/Building Pattern Catalog", order = 101)]
    public class BuildingPatternCatalogAsset : ScriptableObject
    {
        [Header("Pattern Specs")]
        [Tooltip("Array of building pattern spec assets to include in this catalog.")]
        public BuildingPatternSpecAsset[] PatternSpecs = new BuildingPatternSpecAsset[0];

        /// <summary>
        /// Merges multiple catalog assets, with later specs overriding earlier ones by PatternId.
        /// </summary>
        public static BuildingPatternSpecAsset[] MergeCatalogs(params BuildingPatternCatalogAsset[] catalogs)
        {
            if (catalogs == null || catalogs.Length == 0)
                return new BuildingPatternSpecAsset[0];

            var specDict = new Dictionary<int, BuildingPatternSpecAsset>();

            // Process catalogs in order (later ones override earlier ones)
            foreach (var catalog in catalogs)
            {
                if (catalog == null || catalog.PatternSpecs == null)
                    continue;

                foreach (var specAsset in catalog.PatternSpecs)
                {
                    if (specAsset == null)
                        continue;

                    if (specAsset.PatternId != 0)
                    {
                        specDict[specAsset.PatternId] = specAsset; // Later specs override earlier ones
                    }
                }
            }

            return specDict.Values.ToArray();
        }

        /// <summary>
        /// Builds a BuildingPatternCatalog BlobAsset from this catalog and optionally merged catalogs.
        /// </summary>
        public BlobAssetReference<BuildingPatternCatalog> BuildBlobAsset(params BuildingPatternCatalogAsset[] additionalCatalogs)
        {
            var allCatalogs = new List<BuildingPatternCatalogAsset> { this };
            if (additionalCatalogs != null)
            {
                allCatalogs.AddRange(additionalCatalogs);
            }

            var mergedSpecs = MergeCatalogs(allCatalogs.ToArray());

            return BuildBlobAssetFromSpecs(mergedSpecs);
        }

        /// <summary>
        /// Builds a BuildingPatternCatalog BlobAsset from an array of spec assets.
        /// </summary>
        public static BlobAssetReference<BuildingPatternCatalog> BuildBlobAssetFromSpecs(BuildingPatternSpecAsset[] specAssets)
        {
            if (specAssets == null || specAssets.Length == 0)
            {
                // Return empty catalog
                var emptyBuilder = new BlobBuilder(Allocator.Temp);
                ref var emptyRoot = ref emptyBuilder.ConstructRoot<BuildingPatternCatalog>();
                emptyBuilder.Allocate(ref emptyRoot.Specs, 0);
                var blob = emptyBuilder.CreateBlobAssetReference<BuildingPatternCatalog>(Allocator.Persistent);
                emptyBuilder.Dispose();
                return blob;
            }

            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var catalogRoot = ref blobBuilder.ConstructRoot<BuildingPatternCatalog>();
            var specsArray = blobBuilder.Allocate(ref catalogRoot.Specs, specAssets.Length);

            for (int i = 0; i < specAssets.Length; i++)
            {
                var specAsset = specAssets[i];
                if (specAsset == null)
                    continue;

                specsArray[i] = specAsset.ToSpec();
            }

            var blobAsset = blobBuilder.CreateBlobAssetReference<BuildingPatternCatalog>(Allocator.Persistent);
            blobBuilder.Dispose();

            return blobAsset;
        }
    }
}
#endif
























