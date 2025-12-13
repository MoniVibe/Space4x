#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Authoring.Construction;
using PureDOTS.Runtime.Construction;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Construction
{
    /// <summary>
    /// Bootstrap system for Space4X-specific construction configuration.
    /// Creates ConstructionConfigState singleton with Space4X building pattern catalog.
    /// </summary>
    public static class Space4XConstructionBootstrap
    {
        /// <summary>
        /// Initializes construction system by creating ConstructionConfigState singleton with default config.
        /// Called from game-specific bootstrap code.
        /// </summary>
        public static void InitializeConstructionSystem(EntityManager entityManager, BuildingPatternCatalogAsset catalogAsset = null)
        {
            // Check if already initialized
            if (HasSingleton<ConstructionConfigState>(entityManager))
            {
                return; // Already initialized
            }

            BlobAssetReference<BuildingPatternCatalog> catalogBlob;

            if (catalogAsset != null && catalogAsset.PatternSpecs != null && catalogAsset.PatternSpecs.Length > 0)
            {
                // Build blob from provided catalog asset
                catalogBlob = BuildingPatternCatalogAsset.BuildBlobAssetFromSpecs(catalogAsset.PatternSpecs);
            }
            else
            {
                // Create empty catalog as fallback
                catalogBlob = BuildingPatternCatalogAsset.BuildBlobAssetFromSpecs(new BuildingPatternSpecAsset[0]);
            }

            // Create ConstructionConfigState singleton
            var configEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(configEntity, new ConstructionConfigState
            {
                Catalog = catalogBlob,
                AggregationCheckFrequency = 300u // Check every ~3.3 seconds at 90 TPS
            });
        }

        private static bool HasSingleton<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            return query.CalculateEntityCount() > 0;
        }
    }
}
#endif


















