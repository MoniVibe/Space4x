#if UNITY_EDITOR || UNITY_STANDALONE
using System.Collections.Generic;
using System.Linq;
using PureDOTS.Runtime.Motivation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Motivation
{
    /// <summary>
    /// ScriptableObject that holds array of MotivationSpecAsset references.
    /// Supports additive merging (base + mods) and can build MotivationCatalog BlobAsset.
    /// </summary>
    [CreateAssetMenu(fileName = "MotivationCatalog", menuName = "PureDOTS/Motivation/Motivation Catalog", order = 101)]
    public class MotivationCatalogAsset : ScriptableObject
    {
        [Header("Specs")]
        [Tooltip("Array of motivation spec assets to include in this catalog.")]
        public MotivationSpecAsset[] Specs = new MotivationSpecAsset[0];

        /// <summary>
        /// Merges multiple catalog assets, with later specs overriding earlier ones by SpecId.
        /// </summary>
        public static MotivationSpec[] MergeCatalogs(params MotivationCatalogAsset[] catalogs)
        {
            if (catalogs == null || catalogs.Length == 0)
                return new MotivationSpec[0];

            var specDict = new Dictionary<short, MotivationSpec>();

            // Process catalogs in order (later ones override earlier ones)
            foreach (var catalog in catalogs)
            {
                if (catalog == null || catalog.Specs == null)
                    continue;

                foreach (var specAsset in catalog.Specs)
                {
                    if (specAsset == null)
                        continue;

                    var spec = specAsset.ToSpec();
                    if (spec.SpecId >= 0)
                    {
                        specDict[spec.SpecId] = spec; // Later specs override earlier ones
                    }
                }
            }

            return specDict.Values.ToArray();
        }

        /// <summary>
        /// Builds a MotivationCatalog BlobAsset from this catalog and optionally merged catalogs.
        /// </summary>
        public BlobAssetReference<MotivationCatalog> BuildBlobAsset(params MotivationCatalogAsset[] additionalCatalogs)
        {
            var allCatalogs = new List<MotivationCatalogAsset> { this };
            if (additionalCatalogs != null)
            {
                allCatalogs.AddRange(additionalCatalogs);
            }

            var mergedSpecs = MergeCatalogs(allCatalogs.ToArray());

            return BuildBlobAssetFromSpecs(mergedSpecs);
        }

        /// <summary>
        /// Builds a MotivationCatalog BlobAsset from an array of specs.
        /// </summary>
        public static BlobAssetReference<MotivationCatalog> BuildBlobAssetFromSpecs(MotivationSpec[] specs)
        {
            if (specs == null || specs.Length == 0)
            {
                // Return empty catalog
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<MotivationCatalog>();
                builder.Allocate(ref root.Specs, 0);
                var blob = builder.CreateBlobAssetReference<MotivationCatalog>(Allocator.Persistent);
                builder.Dispose();
                return blob;
            }

            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var catalogRoot = ref blobBuilder.ConstructRoot<MotivationCatalog>();
            var specsArray = blobBuilder.Allocate(ref catalogRoot.Specs, specs.Length);

            for (int i = 0; i < specs.Length; i++)
            {
                specsArray[i] = specs[i];
            }

            var blobAsset = blobBuilder.CreateBlobAssetReference<MotivationCatalog>(Allocator.Persistent);
            blobBuilder.Dispose();

            return blobAsset;
        }
    }
}
#endif
























