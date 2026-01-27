#if UNITY_EDITOR || UNITY_STANDALONE
using System.Collections.Generic;
using System.Linq;
using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Guild;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Guild
{
    /// <summary>
    /// ScriptableObject that holds array of GuildGovernanceSpecAsset references.
    /// Supports additive merging (base + mods) and can build GuildGovernanceCatalog BlobAsset.
    /// </summary>
    [CreateAssetMenu(fileName = "GuildGovernanceCatalog", menuName = "PureDOTS/Guild/Guild Governance Catalog", order = 103)]
    public class GuildGovernanceCatalogAsset : ScriptableObject
    {
        [Header("Governance Specs")]
        [Tooltip("Array of governance spec assets to include in this catalog.")]
        public GuildGovernanceSpecAsset[] GovernanceSpecs = new GuildGovernanceSpecAsset[0];

        /// <summary>
        /// Merges multiple catalog assets, with later specs overriding earlier ones by Type.
        /// </summary>
        public static GuildGovernanceSpecAsset[] MergeCatalogs(params GuildGovernanceCatalogAsset[] catalogs)
        {
            if (catalogs == null || catalogs.Length == 0)
                return new GuildGovernanceSpecAsset[0];

            var specDict = new Dictionary<PureDOTS.Runtime.Aggregates.GuildLeadership.GovernanceType, GuildGovernanceSpecAsset>();

            // Process catalogs in order (later ones override earlier ones)
            foreach (var catalog in catalogs)
            {
                if (catalog == null || catalog.GovernanceSpecs == null)
                    continue;

                foreach (var specAsset in catalog.GovernanceSpecs)
                {
                    if (specAsset == null)
                        continue;

                    specDict[specAsset.Type] = specAsset; // Later specs override earlier ones
                }
            }

            return specDict.Values.ToArray();
        }

        /// <summary>
        /// Builds a GuildGovernanceCatalog BlobAsset from this catalog and optionally merged catalogs.
        /// </summary>
        public BlobAssetReference<GuildGovernanceCatalog> BuildBlobAsset(params GuildGovernanceCatalogAsset[] additionalCatalogs)
        {
            var allCatalogs = new List<GuildGovernanceCatalogAsset> { this };
            if (additionalCatalogs != null)
            {
                allCatalogs.AddRange(additionalCatalogs);
            }

            var mergedSpecs = MergeCatalogs(allCatalogs.ToArray());

            return BuildBlobAssetFromSpecs(mergedSpecs);
        }

        /// <summary>
        /// Builds a GuildGovernanceCatalog BlobAsset from an array of spec assets.
        /// </summary>
        public static BlobAssetReference<GuildGovernanceCatalog> BuildBlobAssetFromSpecs(GuildGovernanceSpecAsset[] specAssets)
        {
            if (specAssets == null || specAssets.Length == 0)
            {
                // Return empty catalog
                var emptyBuilder = new BlobBuilder(Allocator.Temp);
                ref var emptyRoot = ref emptyBuilder.ConstructRoot<GuildGovernanceCatalog>();
                emptyBuilder.Allocate(ref emptyRoot.GovernanceSpecs, 0);
                var blob = emptyBuilder.CreateBlobAssetReference<GuildGovernanceCatalog>(Allocator.Persistent);
                emptyBuilder.Dispose();
                return blob;
            }

            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var catalogRoot = ref blobBuilder.ConstructRoot<GuildGovernanceCatalog>();
            var specsArray = blobBuilder.Allocate(ref catalogRoot.GovernanceSpecs, specAssets.Length);

            for (int i = 0; i < specAssets.Length; i++)
            {
                var specAsset = specAssets[i];
                if (specAsset == null)
                    continue;

                specsArray[i] = specAsset.ToSpec();
            }

            var blobAsset = blobBuilder.CreateBlobAssetReference<GuildGovernanceCatalog>(Allocator.Persistent);
            blobBuilder.Dispose();

            return blobAsset;
        }
    }
}
#endif

