#if UNITY_EDITOR || UNITY_STANDALONE
using System.Collections.Generic;
using System.Linq;
using PureDOTS.Runtime.Guild;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Guild
{
    /// <summary>
    /// ScriptableObject that holds array of GuildTypeSpecAsset references.
    /// Supports additive merging (base + mods) and can build GuildTypeCatalog BlobAsset.
    /// </summary>
    [CreateAssetMenu(fileName = "GuildTypeCatalog", menuName = "PureDOTS/Guild/Guild Type Catalog", order = 101)]
    public class GuildTypeCatalogAsset : ScriptableObject
    {
        [Header("Type Specs")]
        [Tooltip("Array of guild type spec assets to include in this catalog.")]
        public GuildTypeSpecAsset[] TypeSpecs = new GuildTypeSpecAsset[0];

        /// <summary>
        /// Merges multiple catalog assets, with later specs overriding earlier ones by TypeId.
        /// </summary>
        public static GuildTypeSpecAsset[] MergeCatalogs(params GuildTypeCatalogAsset[] catalogs)
        {
            if (catalogs == null || catalogs.Length == 0)
                return new GuildTypeSpecAsset[0];

            var specDict = new Dictionary<ushort, GuildTypeSpecAsset>();

            // Process catalogs in order (later ones override earlier ones)
            foreach (var catalog in catalogs)
            {
                if (catalog == null || catalog.TypeSpecs == null)
                    continue;

                foreach (var specAsset in catalog.TypeSpecs)
                {
                    if (specAsset == null)
                        continue;

                    if (specAsset.TypeId > 0)
                    {
                        specDict[specAsset.TypeId] = specAsset; // Later specs override earlier ones
                    }
                }
            }

            return specDict.Values.ToArray();
        }

        /// <summary>
        /// Builds a GuildTypeCatalog BlobAsset from this catalog and optionally merged catalogs.
        /// </summary>
        public BlobAssetReference<GuildTypeCatalog> BuildBlobAsset(params GuildTypeCatalogAsset[] additionalCatalogs)
        {
            var allCatalogs = new List<GuildTypeCatalogAsset> { this };
            if (additionalCatalogs != null)
            {
                allCatalogs.AddRange(additionalCatalogs);
            }

            var mergedSpecs = MergeCatalogs(allCatalogs.ToArray());

            return BuildBlobAssetFromSpecs(mergedSpecs);
        }

        /// <summary>
        /// Builds a GuildTypeCatalog BlobAsset from an array of spec assets.
        /// </summary>
        public static BlobAssetReference<GuildTypeCatalog> BuildBlobAssetFromSpecs(GuildTypeSpecAsset[] specAssets)
        {
            if (specAssets == null || specAssets.Length == 0)
            {
                // Return empty catalog
                var emptyBuilder = new BlobBuilder(Allocator.Temp);
                ref var emptyRoot = ref emptyBuilder.ConstructRoot<GuildTypeCatalog>();
                emptyBuilder.Allocate(ref emptyRoot.TypeSpecs, 0);
                var blob = emptyBuilder.CreateBlobAssetReference<GuildTypeCatalog>(Allocator.Persistent);
                emptyBuilder.Dispose();
                return blob;
            }

            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var catalogRoot = ref blobBuilder.ConstructRoot<GuildTypeCatalog>();
            var specsArray = blobBuilder.Allocate(ref catalogRoot.TypeSpecs, specAssets.Length);

            for (int i = 0; i < specAssets.Length; i++)
            {
                var specAsset = specAssets[i];
                if (specAsset == null)
                    continue;

                ref var spec = ref specsArray[i];
                specAsset.ToSpec(blobBuilder, ref spec);
            }

            var blobAsset = blobBuilder.CreateBlobAssetReference<GuildTypeCatalog>(Allocator.Persistent);
            blobBuilder.Dispose();

            return blobAsset;
        }
    }
}
#endif
























