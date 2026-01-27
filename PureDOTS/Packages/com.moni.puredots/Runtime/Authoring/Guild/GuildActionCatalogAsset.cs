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
    /// ScriptableObject that holds array of GuildActionSpecAsset references.
    /// Supports additive merging (base + mods) and can build GuildActionCatalog BlobAsset.
    /// </summary>
    [CreateAssetMenu(fileName = "GuildActionCatalog", menuName = "PureDOTS/Guild/Guild Action Catalog", order = 102)]
    public class GuildActionCatalogAsset : ScriptableObject
    {
        [Header("Action Specs")]
        [Tooltip("Array of guild action spec assets to include in this catalog.")]
        public GuildActionSpecAsset[] ActionSpecs = new GuildActionSpecAsset[0];

        /// <summary>
        /// Merges multiple catalog assets, with later specs overriding earlier ones by ActionId.
        /// </summary>
        public static GuildActionSpecAsset[] MergeCatalogs(params GuildActionCatalogAsset[] catalogs)
        {
            if (catalogs == null || catalogs.Length == 0)
                return new GuildActionSpecAsset[0];

            var specDict = new Dictionary<ushort, GuildActionSpecAsset>();

            // Process catalogs in order (later ones override earlier ones)
            foreach (var catalog in catalogs)
            {
                if (catalog == null || catalog.ActionSpecs == null)
                    continue;

                foreach (var specAsset in catalog.ActionSpecs)
                {
                    if (specAsset == null)
                        continue;

                    if (specAsset.ActionId > 0)
                    {
                        specDict[specAsset.ActionId] = specAsset; // Later specs override earlier ones
                    }
                }
            }

            return specDict.Values.ToArray();
        }

        /// <summary>
        /// Builds a GuildActionCatalog BlobAsset from this catalog and optionally merged catalogs.
        /// </summary>
        public BlobAssetReference<GuildActionCatalog> BuildBlobAsset(params GuildActionCatalogAsset[] additionalCatalogs)
        {
            var allCatalogs = new List<GuildActionCatalogAsset> { this };
            if (additionalCatalogs != null)
            {
                allCatalogs.AddRange(additionalCatalogs);
            }

            var mergedSpecs = MergeCatalogs(allCatalogs.ToArray());

            return BuildBlobAssetFromSpecs(mergedSpecs);
        }

        /// <summary>
        /// Builds a GuildActionCatalog BlobAsset from an array of spec assets.
        /// </summary>
        public static BlobAssetReference<GuildActionCatalog> BuildBlobAssetFromSpecs(GuildActionSpecAsset[] specAssets)
        {
            if (specAssets == null || specAssets.Length == 0)
            {
                // Return empty catalog
                var emptyBuilder = new BlobBuilder(Allocator.Temp);
                ref var emptyRoot = ref emptyBuilder.ConstructRoot<GuildActionCatalog>();
                emptyBuilder.Allocate(ref emptyRoot.ActionSpecs, 0);
                var blob = emptyBuilder.CreateBlobAssetReference<GuildActionCatalog>(Allocator.Persistent);
                emptyBuilder.Dispose();
                return blob;
            }

            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var catalogRoot = ref blobBuilder.ConstructRoot<GuildActionCatalog>();
            var specsArray = blobBuilder.Allocate(ref catalogRoot.ActionSpecs, specAssets.Length);

            for (int i = 0; i < specAssets.Length; i++)
            {
                var specAsset = specAssets[i];
                if (specAsset == null)
                    continue;

                ref var spec = ref specsArray[i];
                specAsset.ToSpec(blobBuilder, ref spec);
            }

            var blobAsset = blobBuilder.CreateBlobAssetReference<GuildActionCatalog>(Allocator.Persistent);
            blobBuilder.Dispose();

            return blobAsset;
        }
    }
}
#endif
























